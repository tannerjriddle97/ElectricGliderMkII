using System.Reflection;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using HarmonyLib;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace ElectricGliderMk2;

[ProtoContract]
public class LiftRequest
{
    [ProtoMember(1)] public bool Held { get; set; }
    [ProtoMember(2)] public float ViewPitch { get; set; }
    [ProtoMember(3)] public float ViewYaw { get; set; }
}

[ProtoContract]
public class ChargeUpdate
{
    [ProtoMember(1)] public int SlotId { get; set; }
    [ProtoMember(2)] public string StackId { get; set; } = "";
    [ProtoMember(3)] public long Sequence { get; set; }
    [ProtoMember(4)] public int Charge { get; set; }
    [ProtoMember(5)] public double Fraction { get; set; }
}

public class Mk2System : ModSystem
{
    public const string LiftHotkey = "electricglidermk2-lift";
    private const string Channel = "electricglidermk2";
    private const string PatchId = "obbra.electricglidermk2";
    private ICoreClientAPI? client;
    private ICoreServerAPI? server;
    private IClientNetworkChannel? clientChannel;
    private IServerNetworkChannel? serverChannel;
    private const string ChargeSyncId = "mk2ChargeSyncId";
    private const string ChargeSyncSequence = "mk2ChargeSyncSequence";
    private readonly ConcurrentDictionary<string, long> liftLeases = new();
    private readonly ConcurrentDictionary<string, FlightAim> flightAims = new();
    private sealed record FlightAim(double Pitch, double Yaw, long Expires);
    private volatile bool liftHeld;
    private bool lastSent;
    private long nextHeartbeat;
    private long clientTick;
    private long serverTick;
    private Harmony? harmony;
    private FieldInfo? speedCapField;
    private FieldInfo? drainField;
    private ILoadedSound? engineSound;
    private static readonly ConditionalWeakTable<EntityControls, GlideSession> glideSessions = new();
    private sealed class GlideSession
    {
        public bool Active, WasBoosting, CoastingAfterBoost;
        public double Elapsed, SpeedLimit;
    }

    public override void Start(ICoreAPI api)
    {
        api.RegisterItemClass("Mk2Glider", typeof(ItemMk2Glider));
        Type epConfig = AccessTools.TypeByName("ElectricalProgressive.ElectricalProgressive")
            ?? throw new InvalidOperationException("Electrical Progressive Core was not loaded.");
        speedCapField = AccessTools.Field(epConfig, "maxGliderSpeed");
        drainField = AccessTools.Field(epConfig, "gliderDurabilityLossAmount");
        // In singleplayer Start runs for both sides in the same process.
        if (!Harmony.HasAnyPatches(PatchId))
        {
            harmony = new Harmony(PatchId);
            FlightVisibility.Install(harmony);
            harmony.Patch(AccessTools.PropertyGetter(typeof(EntityPlayer), nameof(EntityPlayer.LightHsv)),
                postfix: new HarmonyMethod(typeof(ThrustLight), nameof(ThrustLight.LightPostfix)));
            harmony.Patch(AccessTools.Method(typeof(EntityAgent), nameof(EntityAgent.OnGameTick)),
                prefix: new HarmonyMethod(typeof(Mk2System), nameof(LiftAnimationPrefix)));
            harmony.Patch(AccessTools.Method(typeof(PModuleInAir), nameof(PModuleInAir.Applicable)),
                postfix: new HarmonyMethod(typeof(Mk2System), nameof(AirApplicablePostfix)));
            harmony.Patch(AccessTools.Method(typeof(PModuleInAir), nameof(PModuleInAir.DoApply)),
                prefix: new HarmonyMethod(typeof(Mk2System), nameof(AirApplyPrefix)));
            harmony.Patch(AccessTools.Method(typeof(EntityBehaviorControlledPhysics), nameof(EntityBehaviorControlledPhysics.MotionAndCollision)),
                prefix: new HarmonyMethod(typeof(Mk2System), nameof(FlightStepPrefix)),
                postfix: new HarmonyMethod(typeof(Mk2System), nameof(FlightStepPostfix)));
            harmony.Patch(AccessTools.Method(typeof(PModuleGravity), nameof(PModuleGravity.Applicable)),
                postfix: new HarmonyMethod(typeof(Mk2System), nameof(GravityPostfix)));
            harmony.Patch(AccessTools.Method(typeof(PModuleMotionDrag), nameof(PModuleMotionDrag.DoApply)),
                prefix: new HarmonyMethod(typeof(Mk2System), nameof(DragPrefix)));
            harmony.Patch(AccessTools.Method(typeof(EntityPlayer), nameof(EntityPlayer.ShouldReceiveDamage)),
                prefix: new HarmonyMethod(typeof(Mk2System), nameof(FallDamagePrefix)));
            Type epFlight = AccessTools.TypeByName("ElectricalProgressive.Net.EGliderFlightPacketHandler")
                ?? throw new InvalidOperationException("Electrical Progressive Equipment was not loaded.");
            harmony.Patch(AccessTools.Method(epFlight, "TryFindGlider"),
                prefix: new HarmonyMethod(typeof(Mk2System), nameof(EpFindPrefix)));
            harmony.Patch(AccessTools.Method(epFlight, "OnDurabilityTick"),
                prefix: new HarmonyMethod(typeof(Mk2System), nameof(EpDrainPrefix)));
        }
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        client = api;
        FlightVisibility.InstallOptionalRenderer(new Harmony(PatchId), api.Logger);
        clientChannel = api.Network.RegisterChannel(Channel).RegisterMessageType<LiftRequest>()
            .RegisterMessageType<ChargeUpdate>().SetMessageHandler<ChargeUpdate>(OnChargeUpdate);
        api.Input.RegisterHotKeyFirst(LiftHotkey, "Mk II glider: vertical lift (hold)", GlKeys.Space,
            HotkeyType.CharacterControls, shiftPressed: true);
        api.Input.SetHotKeyHandler(LiftHotkey, _ => FindGlider(api.World.Player?.Entity) != null);
        clientTick = api.Event.RegisterGameTickListener(ClientTick, 20);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        server = api;
        serverChannel = api.Network.RegisterChannel(Channel).RegisterMessageType<LiftRequest>()
            .RegisterMessageType<ChargeUpdate>().SetMessageHandler<LiftRequest>(OnLiftRequest);
        serverTick = api.Event.RegisterGameTickListener(ServerTick, 50);
        api.Event.PlayerLeave += OnPlayerLeave;
    }

    public double SpeedCap => Math.Min(FlightModel.MaxGlideBlocksPerSecond / FlightModel.MotionTicksPerSecond,
        Math.Clamp(Convert.ToDouble(speedCapField?.GetValue(null) ?? 1f), 0.005, 1));
    public double BoostSpeedCap => Math.Min(FlightModel.MaxBoostBlocksPerSecond / FlightModel.MotionTicksPerSecond,
        Math.Clamp(Convert.ToDouble(speedCapField?.GetValue(null) ?? 1f), 0.005, 1));
    public double BaseDrain => Math.Clamp(Convert.ToDouble(drainField?.GetValue(null) ?? 15), 0, 200);

    public (double Pitch, double Yaw) FlightAngles(Entity entity, EntityPos fallback)
    {
        // Camera aim is used only for upright V positioning. Gliding uses the
        // entity's normal pitch/yaw convention on both client and server.
        if (client?.World.Player is IClientPlayer local && local.Entity?.EntityId == entity.EntityId)
            return (FlightModel.SignedPitch(local.CameraPitch), FlightModel.SignedPitch(client.Input.MouseYaw));
        if (server != null && entity is EntityPlayer player && player.Player != null
            && flightAims.TryGetValue(player.Player.PlayerUID, out FlightAim? aim)
            && aim.Expires >= server.World.ElapsedMilliseconds)
            return (aim.Pitch, aim.Yaw);
        return (FlightModel.SignedPitch(fallback.Pitch), FlightModel.SignedPitch(fallback.Yaw));
    }

    public static ItemSlot? FindGlider(Entity? entity)
    {
        if (entity is not EntityPlayer player || player.World == null) return null;
        IPlayerInventoryManager? inventory = player.Player?.InventoryManager;
        IInventory? bags = inventory?.GetOwnInventory("backpack");
        if (bags == null) return null;
        foreach (ItemSlot slot in bags)
            if (slot is ItemSlotBackpack && slot.Itemstack?.Collectible is ItemMk2Glider) return slot;
        return null;
    }

    private static bool Eligible(EntityPlayer player)
        => player.Alive && !player.FeetInLiquid && !player.Swimming && player.MountedOn == null
            && !player.Controls.IsClimbing && player.Player?.WorldData?.FreeMove != true;

    public bool LiftActive(EntityPlayer player)
    {
        if (client != null) return player.EntityId == client.World.Player?.Entity?.EntityId && liftHeld;
        return player.Player != null && server != null && liftLeases.TryGetValue(player.Player.PlayerUID, out long until)
            && until >= server.World.ElapsedMilliseconds;
    }

    public static bool Powered(Entity entity, EntityControls controls, out Mk2System? system, out bool lift)
    {
        system = null; lift = false;
        if (entity is not EntityPlayer player || !Eligible(player)) return false;
        ItemStack? stack = FindGlider(entity)?.Itemstack;
        if (stack?.Collectible is not ItemMk2Glider || (stack.Attributes?.GetInt("durability", 0) ?? 0) <= 0) return false;
        system = entity.Api?.ModLoader?.GetModSystem<Mk2System>();
        if (system == null) return false;
        lift = system.LiftActive(player);
        return lift;
    }

    private void ClientTick(float dt)
    {
        if (client?.World.Player?.Entity is not EntityPlayer player) return;
        ResetInactiveGlide(player);
        bool key = client.Input.MouseGrabbed && KeyHeld(client);
        bool wasLiftHeld = liftHeld;
        liftHeld = key && Eligible(player) && FindGlider(player)?.Itemstack?.Attributes.GetInt("durability", 0) > 0;
        if (liftHeld)
        {
            player.Controls.Gliding = false;
            player.Controls.IsFlying = false;
            player.Controls.GlideSpeed = 0;
            KeepLiftUpright(player);
        }
        else if (wasLiftHeld)
        {
            // Space is the only action that deploys the glider. Preserve a Space
            // press received after lift was released but before this input tick.
            player.Controls.GlideSpeed = 0;
            player.Controls.IsFlying = player.Controls.Gliding || player.Player?.WorldData?.FreeMove == true;
        }
        long now = client.World.ElapsedMilliseconds;
        if (liftHeld != lastSent || now >= nextHeartbeat)
        {
            var aim = FlightAngles(player, player.Pos);
            clientChannel?.SendPacket(new LiftRequest { Held = liftHeld, ViewPitch = (float)aim.Pitch, ViewYaw = (float)aim.Yaw });
            lastSent = liftHeld;
            nextHeartbeat = now + 200;
        }
        UpdateSound(player);
    }

    private static bool KeyHeld(ICoreClientAPI api)
    {
        KeyCombination? mapping = api.Input.GetHotKeyByCode(LiftHotkey)?.CurrentMapping;
        if (mapping == null) return false;
        bool[] keys = api.Input.KeyboardKeyStateRaw;
        bool Down(int code) => code > 0 && code < keys.Length && keys[code];
        return Down(mapping.KeyCode) && (!mapping.SecondKeyCode.HasValue || Down(mapping.SecondKeyCode.Value))
            && (!mapping.Ctrl || Down((int)GlKeys.ControlLeft) || Down((int)GlKeys.ControlRight))
            && (!mapping.Shift || Down((int)GlKeys.ShiftLeft) || Down((int)GlKeys.ShiftRight))
            && (!mapping.Alt || Down((int)GlKeys.AltLeft) || Down((int)GlKeys.AltRight));
    }

    private void OnLiftRequest(IServerPlayer player, LiftRequest request)
    {
        if (!float.IsFinite(request.ViewPitch) || !float.IsFinite(request.ViewYaw))
        {
            liftLeases.TryRemove(player.PlayerUID, out _);
            flightAims.TryRemove(player.PlayerUID, out _);
            return;
        }
        if (player.Entity != null && Eligible(player.Entity) && FindGlider(player.Entity) != null)
            flightAims[player.PlayerUID] = new FlightAim(FlightModel.SignedPitch(request.ViewPitch),
                FlightModel.SignedPitch(request.ViewYaw), server!.World.ElapsedMilliseconds + 1000);
        else flightAims.TryRemove(player.PlayerUID, out _);
        if (request.Held && player.Entity != null && Eligible(player.Entity)
            && FindGlider(player.Entity)?.Itemstack?.Attributes.GetInt("durability", 0) > 0)
        {
            liftLeases[player.PlayerUID] = server!.World.ElapsedMilliseconds + 650;
            player.Entity.Controls.Gliding = false;
            player.Entity.Controls.IsFlying = false;
            player.Entity.Controls.GlideSpeed = 0;
            SetLiftPoseFlag(player.Entity, true);
        }
        else
        {
            liftLeases.TryRemove(player.PlayerUID, out _);
            if (player.Entity != null) SetLiftPoseFlag(player.Entity, false);
        }
    }

    private void OnPlayerLeave(IServerPlayer player)
    {
        liftLeases.TryRemove(player.PlayerUID, out _);
        flightAims.TryRemove(player.PlayerUID, out _);
    }

    private void ServerTick(float dt)
    {
        if (server == null) return;
        foreach (IPlayer player in server.World.AllOnlinePlayers)
        {
            EntityPlayer entity = player.Entity;
            if (entity == null) continue;
            ResetInactiveGlide(entity);
            if (!Eligible(entity)) { SetLiftPoseFlag(entity, false); continue; }
            ItemSlot? slot = FindGlider(entity);
            if (slot?.Itemstack == null) { SetLiftPoseFlag(entity, false); continue; }
            bool lift = LiftActive(entity);
            SetLiftPoseFlag(entity, lift && slot.Itemstack.Attributes.GetInt("durability", 0) > 0);
            if (!lift && (!entity.Controls.Gliding || !entity.Controls.Sneak)) continue;
            ItemStack stack = slot.Itemstack;
            int charge = stack.Attributes.GetInt("durability", 0);
            if (charge <= 0) { liftLeases.TryRemove(player.PlayerUID, out _); continue; }
            double rate = FlightModel.DrainPerSecond(lift, entity.Pos.Pitch, BaseDrain);
            var debit = FlightModel.Debit(charge, stack.Attributes.GetDouble("mk2FractionalUse", 0), rate, dt);
            stack.Attributes.SetInt("durability", debit.Charge);
            stack.Attributes.SetDouble("mk2FractionalUse", debit.Fraction);
            if (charge != debit.Charge) SyncCharge(slot, player);
            if (debit.Charge == 0) liftLeases.TryRemove(player.PlayerUID, out _);
        }
    }

    public static void MarkChargeDirty(ItemSlot slot)
    {
        InventoryBase? inventory = slot.Inventory;
        if (inventory == null) return;
        int slotId = inventory.GetSlotId(slot);
        // The item and its contents did not change. ItemSlot.MarkDirty invokes
        // InventoryPlayerBackpacks.OnItemSlotModified, which reloads all bags
        // and BroadcastPlayerData() on every charge tick. Queue just the slot
        // for ordinary inventory synchronization instead of refreshing the player.
        if (slotId >= 0) inventory.MarkSlotDirty(slotId);
    }

    public void SyncCharge(ItemSlot slot, IPlayer player)
    {
        if (serverChannel == null || player is not IServerPlayer recipient
            || slot.Inventory is not InventoryBase inventory || slot.Itemstack == null)
        {
            MarkChargeDirty(slot);
            return;
        }
        int slotId = inventory.GetSlotId(slot);
        if (slotId < 0) return;
        var attributes = slot.Itemstack.Attributes;
        string id = attributes.GetString(ChargeSyncId);
        if (string.IsNullOrEmpty(id))
        {
            id = Guid.NewGuid().ToString("N");
            attributes.SetString(ChargeSyncId, id);
            // One ordinary update establishes the stack identity. Subsequent
            // charge ticks bypass inventory packets and client bag reloads.
            MarkChargeDirty(slot);
        }
        long sequence = attributes.GetLong(ChargeSyncSequence) + 1;
        attributes.SetLong(ChargeSyncSequence, sequence);
        serverChannel.SendPacket(new ChargeUpdate {
            SlotId = slotId, StackId = id, Sequence = sequence,
            Charge = attributes.GetInt("durability", 0), Fraction = attributes.GetDouble("mk2FractionalUse", 0)
        }, recipient);
    }

    private void OnChargeUpdate(ChargeUpdate update)
    {
        IInventory? bags = client?.World.Player?.InventoryManager.GetOwnInventory("backpack");
        if (bags == null || update.SlotId < 0 || update.SlotId >= bags.Count) return;
        ApplyChargeUpdate(bags[update.SlotId], update);
    }

    public static bool ApplyChargeUpdate(ItemSlot? slot, ChargeUpdate update)
    {
        if (slot?.Itemstack?.Collectible is not ItemMk2Glider || string.IsNullOrEmpty(update.StackId)
            || update.Charge < 0 || !double.IsFinite(update.Fraction) || update.Fraction < 0 || update.Fraction >= 1)
            return false;
        var attributes = slot.Itemstack.Attributes;
        if (attributes.GetString(ChargeSyncId) != update.StackId
            || update.Sequence <= attributes.GetLong(ChargeSyncSequence)) return false;
        attributes.SetInt("durability", update.Charge);
        attributes.SetDouble("mk2FractionalUse", update.Fraction);
        attributes.SetLong(ChargeSyncSequence, update.Sequence);
        // Do not mark/modify the client inventory slot: native inventory update
        // packets call DidModifyItemSlot and reload all bag contents.
        return true;
    }

    private static void SetLiftPoseFlag(EntityPlayer player, bool active)
    {
        if (player.WatchedAttributes.GetBool("mk2VerticalLift") != active)
            player.WatchedAttributes.SetBool("mk2VerticalLift", active);
    }

    public static bool UsesLiftPose(EntityPlayer player)
    {
        Mk2System? system = player.Api?.ModLoader.GetModSystem<Mk2System>();
        if (system?.client?.World.Player?.Entity?.EntityId == player.EntityId)
            return system.liftHeld;
        return player.Alive && player.WatchedAttributes.GetBool("mk2VerticalLift");
    }

    public static bool HideFirstPersonFlightBody(EntityPlayer player, bool isSelf, RenderMode mode, bool isShadowPass)
        => isSelf && !isShadowPass
            && (mode == RenderMode.FirstPerson || mode == RenderMode.ImmersiveFirstPerson)
            && player.Alive && FindGlider(player) != null
            && (player.Controls.Gliding || UsesLiftPose(player));

    public static void KeepLiftUpright(EntityPlayer player)
    {
        if (!UsesLiftPose(player)) return;
        player.WalkPitch = 0;
        player.Pos.Roll = 0;
        // Animation selection uses ServerControls, which can retain the previous
        // glide even though local physics Controls have already left glide mode.
        player.ServerControls.Gliding = false;
        player.ServerControls.IsFlying = false;
        player.CurrentControls &= ~(EnumEntityActivity.Glide | EnumEntityActivity.Fly);
        foreach (string animation in new[] { "glide", "creativefly" })
        {
            player.TpAnimManager?.StopSelfAnimation(animation);
            player.SelfFpAnimManager?.StopSelfAnimation(animation);
        }
    }

    public static void LiftAnimationPrefix(EntityAgent __instance)
    {
        if (__instance is EntityPlayer player && player.Api?.Side == EnumAppSide.Client)
            KeepLiftUpright(player);
    }

    private void UpdateSound(EntityPlayer player)
    {
        bool powered = Powered(player, player.Controls, out _, out _);
        if (!powered) { engineSound?.Stop(); return; }
        if (engineSound == null)
            engineSound = client!.World.LoadSound(new SoundParams {
                Location = new AssetLocation("electricalprogressiveequipment:sounds/gliderboost.ogg"),
                ShouldLoop = true, RelativePosition = true, DisposeOnFinish = false, Volume = 0.22f
            });
        if (engineSound?.IsPlaying == false) engineSound.Start();
    }

    public static void ApplyLift(float dt, Entity entity, EntityPos pos, EntityControls controls)
    {
        if (!Powered(entity, controls, out Mk2System? system, out _)) return;
        var aim = system!.FlightAngles(entity, pos);
        var motion = FlightModel.StepLift(pos.Motion.X, pos.Motion.Y, pos.Motion.Z,
            aim.Yaw, (controls.Right ? 1 : 0) - (controls.Left ? 1 : 0),
            (controls.Forward ? 1 : 0) - (controls.Backward ? 1 : 0), system.SpeedCap, dt);
        pos.Motion.Set(motion.X, motion.Y, motion.Z);
        controls.GlideSpeed = 0;
    }

    public static void AirApplicablePostfix(PModuleInAir __instance, Entity entity, EntityControls controls, ref bool __result)
    {
        // Let the normal player air module provide lift even while grounded,
        // without pretending the player has deployed a glider or creative flight.
        if (__instance is PModulePlayerInAir && Powered(entity, controls, out _, out bool lift) && lift)
            __result = true;
    }

    public static bool AirApplyPrefix(PModuleInAir __instance, float dt, Entity entity, EntityPos pos, EntityControls controls)
    {
        // EP adds a second air module. Do not apply a second free-fall/walk
        // impulse to the Mk II while its independent lift mode is active.
        if (__instance.GetType().FullName == "Vintagestory.API.Common.Entities.EGlideringPlayerInAir"
            && Powered(entity, controls, out _, out _))
            return false;
        if (__instance is not PModulePlayerInAir || !Powered(entity, controls, out _, out bool lift) || !lift) return true;
        ApplyLift(dt, entity, pos, controls);
        return false;
    }

    public static void GravityPostfix(Entity entity, EntityControls controls, ref bool __result)
    {
        if (__result && Powered(entity, controls, out _, out _)) __result = false;
    }

    public static bool DragPrefix(Entity entity, EntityControls controls)
        => !Powered(entity, controls, out _, out _);

    public static bool IsMk2Gliding(Entity entity, EntityControls controls)
        => entity is EntityPlayer player && Eligible(player) && controls.Gliding && FindGlider(player) != null;

    private static void ResetInactiveGlide(EntityPlayer player)
    {
        if (IsMk2Gliding(player, player.Controls)) return;
        if (glideSessions.TryGetValue(player.Controls, out GlideSession? session))
        {
            session.Active = false;
            session.Elapsed = 0;
        }
    }

    public static bool FallDamagePrefix(EntityPlayer __instance, DamageSource damageSource, ref bool __result)
    {
        if (damageSource?.Type != EnumDamageType.Gravity || FindGlider(__instance) == null) return true;
        __result = false;
        return false;
    }

    public readonly record struct FlightStepState(bool Active, double PreviousY, double Elapsed, double Cap,
        bool Boosting, double HorizontalCap, bool Braking);

    // Keep both real flight modules and all their state changes. Apply limits
    // once, after the entire force pipeline and before terrain collision.
    public static void FlightStepPrefix(EntityBehaviorControlledPhysics __instance, EntityPos pos,
        EntityControls controls, float dt, out FlightStepState __state)
    {
        __state = default;
        if (controls == null) return;
        GlideSession session = glideSessions.GetValue(controls, _ => new GlideSession());
        void ResetSession()
        {
            session.Active = false;
            session.Elapsed = 0;
            session.WasBoosting = session.CoastingAfterBoost = false;
        }
        if (__instance?.Entity is not EntityPlayer player || !__instance.EnableModulesApplication
            || pos?.Motion == null || player.World == null || !controls.Gliding || !Eligible(player))
        {
            ResetSession();
            return;
        }
        Mk2System? system = player.Api?.ModLoader?.GetModSystem<Mk2System>();
        // Inventory updates can replace/empty a backpack between render-frame
        // lookups. Capture the item once, validate it, and use that snapshot.
        ItemStack? stack = FindGlider(player)?.Itemstack;
        if (system == null || stack?.Collectible is not ItemMk2Glider || stack.Attributes == null)
        {
            ResetSession();
            return;
        }
        int charge = stack.Attributes.GetInt("durability", 0);
        if (charge > 0 && system.LiftActive(player))
        {
            ResetSession();
            return;
        }
        bool deploying = !session.Active;
        bool boosting = controls.Sneak && charge > system.BaseDrain;
        if (deploying)
        {
            session.SpeedLimit = Math.Max(6 / FlightModel.MotionTicksPerSecond, pos.Motion.Length());
            session.WasBoosting = session.CoastingAfterBoost = false;
        }
        if (session.WasBoosting && !boosting)
        {
            // A climb can leave the speed setting well above actual momentum.
            // Do not spend that unused setting as thrust when boost is released.
            session.SpeedLimit = Math.Max(0.005, Math.Min(session.SpeedLimit, pos.Motion.Length()));
            session.CoastingAfterBoost = true;
        }
        if (boosting || FlightModel.SignedPitch(pos.Pitch) < -0.05) session.CoastingAfterBoost = false;
        double horizontalCap = double.PositiveInfinity;
        if (session.CoastingAfterBoost)
        {
            double horizontal = Math.Sqrt(pos.Motion.X * pos.Motion.X + pos.Motion.Z * pos.Motion.Z);
            // Preserve natural slow-speed recovery, but do not convert climb
            // momentum into an abrupt horizontal kick after releasing Shift.
            horizontalCap = Math.Max(horizontal, Math.Min(FlightModel.CoastBlocksPerSecond / 60,
                horizontal + 6 / 60.0 * Math.Max(0, dt)));
        }
        session.WasBoosting = boosting;
        session.SpeedLimit = FlightModel.StepSpeedLimit(session.SpeedLimit, pos.Pitch, boosting, system.BoostSpeedCap, dt,
            controls.Backward);
        // Drive native thrust with the same time-based airspeed progression.
        // EP's fixed increment per physics call otherwise makes sustained boost
        // much weaker at 20 Hz than at 60 Hz. Native forces/steering still run.
        controls.GlideSpeed = session.SpeedLimit;
        __state = new(true, pos.Motion.Y, session.Elapsed, session.SpeedLimit, boosting, horizontalCap, controls.Backward);
        session.Active = true;
    }

    public static void FlightStepPostfix(EntityBehaviorControlledPhysics __instance, EntityPos pos,
        EntityControls controls, float dt, FlightStepState __state)
    {
        if (!__state.Active) return;
        var motion = FlightModel.LimitFlightStep(pos.Motion.X, pos.Motion.Y, pos.Motion.Z,
            __state.PreviousY, pos.Pitch, pos.Yaw, __state.Boosting, __state.Cap, __state.Elapsed, dt, __state.Braking);
        double horizontal = Math.Sqrt(motion.X * motion.X + motion.Z * motion.Z);
        if (horizontal > __state.HorizontalCap)
        {
            motion.X *= __state.HorizontalCap / horizontal;
            motion.Z *= __state.HorizontalCap / horizontal;
        }
        pos.Motion.Set(motion.X, motion.Y, motion.Z);
        controls.GlideSpeed = Math.Clamp(controls.GlideSpeed, 0.005, __state.Cap);
        glideSessions.GetValue(controls, _ => new GlideSession()).Elapsed += Math.Max(0, dt);
    }

    public static bool EpFindPrefix(Entity entity, ref ItemSlot slot, ref bool __result)
    {
        ItemSlot? mk2 = FindGlider(entity);
        if (mk2 == null) return true;
        slot = mk2;
        __result = true;
        return false;
    }

    public static void EpDrainPrefix(Dictionary<string, bool> ___activeAfterburners, ICoreServerAPI? ___sapi)
    {
        if (___sapi == null) return;
        foreach (IPlayer player in ___sapi.World.AllOnlinePlayers)
            if (FindGlider(player.Entity) != null) ___activeAfterburners.Remove(player.PlayerUID);
    }

    public override void Dispose()
    {
        if (client != null) client.Event.UnregisterGameTickListener(clientTick);
        if (server != null)
        {
            server.Event.UnregisterGameTickListener(serverTick);
            server.Event.PlayerLeave -= OnPlayerLeave;
        }
        engineSound?.Dispose();
        harmony?.UnpatchAll(PatchId);
        liftLeases.Clear();
        flightAims.Clear();
        base.Dispose();
    }
}
