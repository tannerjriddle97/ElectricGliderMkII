using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using ElectricGliderMk2;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

internal static class Program
{
    public static int Main(string[] args)
    {
        string game = Path.GetFullPath(args[0]), references = Path.GetFullPath(args[1]);
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            foreach (string root in new[] { game, Path.Combine(game, "Mods"), Path.Combine(game, "Lib"), references, args.Length > 2 ? args[2] : references })
            {
                string file = Path.Combine(root, name.Name + ".dll");
                if (File.Exists(file)) return AssemblyLoadContext.Default.LoadFromAssemblyPath(file);
            }
            return null;
        };
        try { return PhysicsChecks.Run(references, args.Length > 2 ? args[2] : null, game); }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}

internal static class PhysicsChecks
{
    public static int Run(string references, string? renderReferences, string game)
    {
        foreach (string dll in Directory.GetFiles(references, "*.dll")) Assembly.LoadFrom(dll);
        Type epConfig = AccessTools.TypeByName("ElectricalProgressive.ElectricalProgressive")!;
        AccessTools.Field(epConfig, "maxGliderSpeed").SetValue(null, 1f);
        AccessTools.Field(epConfig, "gliderDurabilityLossAmount").SetValue(null, 15);
        int checks = 0;
        void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
        var system = new Mk2System();
        Type epType = AccessTools.TypeByName("ElectricalProgressive.Net.EGliderFlightPacketHandler")!;
        object ep = Activator.CreateInstance(epType)!;
        bool modSystemAvailable = true;
        var loader = Proxy.Make<IModLoader>((m, a) => m.Name == "GetModSystem"
            ? (m.IsGenericMethod && m.GetGenericArguments()[0] == typeof(Mk2System) ? (modSystemAvailable ? system : null) : ep)
            : Proxy.Default(m.ReturnType));
        ICoreAPI? api = null;
        IWorldAccessor? world = null;
        var bags = new CountingInventory("offline-flight");
        var item = new ItemMk2Glider { Code = new AssetLocation("electricglidermk2:glider-mk2") };
        var stack = new ItemStack(item); stack.Attributes.SetInt("durability", 4000); bags[0].Itemstack = stack;
        var entity = new EntityPlayer { EntityId = -100 };
        entity.WatchedAttributes.SetString("playerUID", "offline-flight");
        bool changingInventory = false;
        int inventoryReads = 0;
        var inventory = Proxy.Make<IPlayerInventoryManager>((m, a) => m.Name == "GetOwnInventory"
            ? (changingInventory && ++inventoryReads > 1 ? null : bags) : Proxy.Default(m.ReturnType));
        var data = Proxy.Make<IWorldPlayerData>((m, a) => Proxy.Default(m.ReturnType));
        var player = Proxy.Make<IServerPlayer>((m, a) => m.Name switch {
            "get_Entity" => entity, "get_PlayerUID" => "offline-flight", "get_InventoryManager" => inventory,
            "get_WorldData" => data, _ => Proxy.Default(m.ReturnType) });
        var calendar = Proxy.Make<IGameCalendar>((m, a) => m.Name == "get_TotalHours" ? 100.0 : Proxy.Default(m.ReturnType));
        world = Proxy.Make<IWorldAccessor>((m, a) => m.Name switch {
            "get_Api" => api, "PlayerByUid" => player, "get_Calendar" => calendar, _ => Proxy.Default(m.ReturnType) });
        api = Proxy.Make<ICoreAPI>((m, a) => m.Name switch {
            "get_ModLoader" => loader, "get_World" => world, "get_Side" => EnumAppSide.Server,
            _ => Proxy.Default(m.ReturnType) });
        entity.Api = api; entity.World = world;
        AccessTools.PropertySetter(typeof(Entity), "Properties").Invoke(entity, new object[] {
            new EntityProperties { Habitat = EnumHabitat.Land, Client = new EntityClientProperties([], new()),
                Server = new EntityServerProperties([], new()) } });
        system.Start(api);
        RenderChecks.Run(entity, bags, Check, renderReferences);
        VisualChecks.Run(entity, bags, Check, game, renderReferences != null);
        var native = new PModulePlayerInAir(); native.Initialize(null, entity);
        var epAir = (PModuleInAir)Activator.CreateInstance(AccessTools.TypeByName("Vintagestory.API.Common.Entities.EGlideringPlayerInAir")!)!;
        epAir.Initialize(null, entity);
        var gravity = new PModuleGravity(); gravity.Initialize(null, entity);
        var drag = new PModuleMotionDrag(); drag.Initialize(null, entity);
        var physics = new TestPhysics(entity);
        physics.Add(native); physics.Add(epAir); physics.Add(gravity); physics.Add(drag);
        // The render-frame hook must not re-read a changing backpack and then
        // dereference an absent slot, nor assume its mod system is available.
        entity.Controls.Gliding = true; entity.Controls.IsFlying = true; entity.Controls.Sneak = false;
        entity.Pos.Motion.Set(0, -0.2, 0);
        changingInventory = true; inventoryReads = 0;
        Mk2System.FlightStepPrefix(physics, entity.Pos, entity.Controls, 1f / 60, out var snapshotState);
        Check(snapshotState.Active && inventoryReads == 1, "flight prefix uses one stable inventory snapshot");
        changingInventory = false;
        modSystemAvailable = false;
        var motionBeforeMissing = entity.Pos.Motion.Clone();
        Mk2System.FlightStepPrefix(physics, entity.Pos, entity.Controls, 1f / 60, out var missingSystemState);
        Check(!missingSystemState.Active, "missing mod system safely leaves native flight active");
        Check(entity.Pos.Motion.DistanceTo(motionBeforeMissing) == 0, "unavailable flight context does not freeze or replace motion");
        modSystemAvailable = true;
        bags[0].Itemstack = null;
        Mk2System.FlightStepPrefix(physics, entity.Pos, entity.Controls, 1f / 60, out var emptyState);
        Check(!emptyState.Active, "empty equipped slot safely bypasses the flight addon");
        bags[0].Itemstack = stack;
        entity.Api = null!;
        Mk2System.FlightStepPrefix(physics, entity.Pos, entity.Controls, 1f / 60, out var missingApiState);
        Check(!missingApiState.Active, "missing entity API safely bypasses the flight addon");
        entity.Api = api;
        Mk2System.FlightStepPrefix(physics, entity.Pos, entity.Controls, 1f / 60, out var recoveredState);
        Check(recoveredState.Active && recoveredState.Elapsed == 0, "flight resumes with a fresh session when context returns");
        foreach (float dt in new[] { 1f / 20, 1f / 60, 1f / 144 })
        foreach (bool boost in new[] { false, true })
        foreach (double pitch in new[] { -Math.PI / 2, 0, Math.PI / 3 })
        {
            entity.Controls.Gliding = false; physics.MotionAndCollision(entity.Pos, entity.Controls, 0);
            entity.Controls.Gliding = true; entity.Controls.IsFlying = true; entity.Controls.Sneak = boost;
            entity.Pos.Pitch = (float)pitch; entity.Pos.Yaw = 0;
            entity.Pos.Motion.Set(0, -0.8, 0); entity.Controls.GlideSpeed = 0;
            double distance = 0, launchHeight = 0;
            for (int i = 0; i < (int)Math.Round(5 / dt); i++)
            {
                double previousY = entity.Pos.Motion.Y;
                physics.MotionAndCollision(entity.Pos, entity.Controls, dt);
                if (i == 0 && pitch == 0) Check(Math.Abs(entity.Pos.Motion.Z) > 0.001,
                    "deployment retains native forward launch impulse even at high frame rates");
                distance += Math.Abs(entity.Pos.Motion.Z) * dt * 60;
                if ((i + 1) * dt <= 2 && !boost)
                {
                    launchHeight += entity.Pos.Motion.Y * dt * 60;
                    Check(launchHeight <= 1e-8, "passive Space deployment cannot produce an upward launch");
                }
                Check(entity.Pos.Motion.Length() <= (boost ? system.BoostSpeedCap : system.SpeedCap) + 1e-8, "pipeline total speed cap");
                if (i == 0 && pitch <= 0) Check(entity.Pos.Motion.Y < 0, "deployment no longer arrests an existing fall");
            }
            if (pitch >= 0) Check(distance > 3, "native pipeline produces forward travel from rest");
            if (boost && pitch > 0) Check(entity.Pos.Motion.Y > 0 && entity.Pos.Motion.Z < 0,
                "upward-facing boost produces upward and forward travel through real EP and gravity");
            if (pitch < 0) Check(entity.Pos.Motion.Y * 60 < -8.1, $"dive protection expires through real modules: dt={dt}, boost={boost}, velocity={entity.Pos.Motion}, gliding={entity.Controls.Gliding}, eligible={Mk2System.IsMk2Gliding(entity,entity.Controls)}");
        }
        foreach (float dt in new[] { 1f / 20, 1f / 60, 1f / 144 })
        foreach (bool boost in new[] { false, true })
        {
            entity.Controls.Gliding = false; physics.MotionAndCollision(entity.Pos, entity.Controls, 0);
            entity.Controls.Gliding = true; entity.Controls.IsFlying = true; entity.Controls.Sneak = boost;
            entity.Controls.GlideSpeed = 0; entity.Pos.Pitch = (float)(-Math.PI / 2);
            entity.Pos.Motion.Set(0, 0, 0);
            physics.MotionAndCollision(entity.Pos, entity.Controls, dt);
            Check(entity.Pos.Motion.Y < -Vintagestory.API.Config.GlobalConstants.GravityPerSecond * dt,
                "fresh deployment allows native dive acceleration without the former gravity guard");
        }
        // Exercise transitions through the real modules, not only the limiter.
        foreach (float dt in new[] { 1f / 20, 1f / 60, 1f / 144 })
        {
            entity.Controls.Gliding = false; physics.MotionAndCollision(entity.Pos, entity.Controls, 0);
            entity.Controls.Gliding = true; entity.Controls.IsFlying = true;
            entity.Controls.GlideSpeed = 0; entity.Pos.Motion.Set(0, 0, 0);
            double Fly(double seconds, bool boost, double pitch)
            {
                entity.Controls.Sneak = boost; entity.Pos.Pitch = (float)pitch;
                for (int i = 0; i < Math.Round(seconds / dt); i++)
                {
                    physics.MotionAndCollision(entity.Pos, entity.Controls, dt);
                    Check(entity.Pos.Motion.Length() * 60 <= (boost ? 45 : 35) + 1e-6, "speed cycle respects active cap");
                }
                return entity.Pos.Motion.Length() * 60;
            }
            double powered = Fly(6, true, 0);
            Check(powered > 44, $"continuous level boost reaches forty-five: {powered}");
            double earlyCoast = Fly(1, false, 0);
            Check(earlyCoast > 30 && earlyCoast < 35, $"release preserves some momentum while shedding speed: {earlyCoast}");
            double coast = Fly(10, false, 0);
            Check(coast > 11 && coast <= 12.01, $"level coast slows to twelve: {coast}");
            double regained = Fly(4, true, 0);
            Check(regained > 44, $"boost visibly regains speed from coasting: {regained}");
            Fly(10, false, 0);
            double dive = Fly(4, false, -Math.PI / 2);
            Check(dive > 34, $"unpowered dive regains speed: {dive}");
            double pullup = Fly(12, false, Math.PI / 3);
            Check(pullup < 9, $"unpowered pull-up loses speed: {pullup}");
            stack.Attributes.SetInt("durability", 0);
            entity.Controls.Sneak = true; entity.Pos.Pitch = 0;
            for (int i = 0; i < Math.Round(6 / dt); i++) physics.MotionAndCollision(entity.Pos, entity.Controls, dt);
            Check(entity.Pos.Motion.Length() * 60 <= 12.01, "empty battery cannot sustain boost speed");
            stack.Attributes.SetInt("durability", 4000);
            AccessTools.Field(epConfig, "maxGliderSpeed").SetValue(null, 0.15f);
            double lower = Fly(6, true, 0);
            Check(lower <= 9.001, "lower EP speed limit applies even with continuous boost");
            AccessTools.Field(epConfig, "maxGliderSpeed").SetValue(null, 1f);
        }
        // Release after a brief boost or climb must not spend a stale high
        // airspeed setting as an extra forward impulse.
        foreach (float dt in new[] { 1f / 20, 1f / 60, 1f / 144 })
        foreach (double held in new[] { 0.2, 3.0 })
        foreach (double pitch in new[] { 0.0, Math.PI / 3 })
        {
            entity.Controls.Gliding = false; physics.MotionAndCollision(entity.Pos, entity.Controls, 0);
            entity.Controls.Gliding = true; entity.Controls.IsFlying = true; entity.Controls.Sneak = true;
            entity.Pos.Pitch = (float)pitch; entity.Pos.Motion.Set(0, 0, -0.2);
            for (int i = 0; i < Math.Round(held / dt); i++) physics.MotionAndCollision(entity.Pos, entity.Controls, dt);
            entity.Controls.Sneak = false; entity.Pos.Pitch = 0;
            double horizontalBefore = Math.Sqrt(entity.Pos.Motion.X * entity.Pos.Motion.X + entity.Pos.Motion.Z * entity.Pos.Motion.Z);
            for (int i = 0; i < Math.Round(0.6 / dt); i++)
            {
                physics.MotionAndCollision(entity.Pos, entity.Controls, dt);
                double horizontalAfter = Math.Sqrt(entity.Pos.Motion.X * entity.Pos.Motion.X + entity.Pos.Motion.Z * entity.Pos.Motion.Z);
                Check(horizontalAfter <= Math.Max(horizontalBefore, Math.Min(12.0 / 60, horizontalBefore + 6 / 60.0 * dt)) + 1e-7,
                    $"boost release does not add horizontal momentum: rate={1 / dt}, held={held}, pitch={pitch}, before={horizontalBefore * 60}, after={horizontalAfter * 60}");
                horizontalBefore = horizontalAfter;
            }
        }
        foreach (float dt in new[] { 1f / 20, 1f / 60, 1f / 144 })
        {
            entity.Controls.Gliding = false; physics.MotionAndCollision(entity.Pos, entity.Controls, 0);
            entity.Controls.Gliding = true; entity.Controls.IsFlying = true; entity.Controls.Sneak = false;
            entity.Controls.Backward = false; entity.Pos.Pitch = 0; entity.Pos.Yaw = 0;
            entity.Pos.Motion.Set(0, 0, -0.2);
            for (int i = 0; i < Math.Round(3 / dt); i++) physics.MotionAndCollision(entity.Pos, entity.Controls, dt);
            double beforeBrake = entity.Pos.Motion.Length() * 60;
            entity.Controls.Backward = true;
            for (int i = 0; i < Math.Round(1 / dt); i++) physics.MotionAndCollision(entity.Pos, entity.Controls, dt);
            double braked = entity.Pos.Motion.Length() * 60;
            Check(beforeBrake > 11 && braked < 6.1 && braked > 4,
                $"holding S gently slows real native flight below cruise: {beforeBrake} -> {braked}");
            entity.Controls.Backward = false;
            for (int i = 0; i < Math.Round(2 / dt); i++)
            {
                double before = entity.Pos.Motion.Length();
                physics.MotionAndCollision(entity.Pos, entity.Controls, dt);
                Check(entity.Pos.Motion.Length() <= before + 6 / 60.0 * dt + 1e-7,
                    "releasing S resumes cruise gradually without a speed surge");
            }
            Check(entity.Pos.Motion.Length() * 60 > 11, "cruise recovers after S release");
        }
        // The original glider must retain EP's higher configured speed cap.
        var stockItem = (Item)Activator.CreateInstance(epType.Assembly.GetTypes().Single(t => t.Name == "ItemEGlider"))!;
        stockItem.Code = new AssetLocation("electricalprogressiveequipment:static-glider");
        var stock = new ItemStack(stockItem); stock.Attributes.SetInt("durability", 1000);
        bags[0].Itemstack = stock;
        entity.Controls.Gliding = true; entity.Controls.IsFlying = true; entity.Controls.Sneak = true;
        entity.Controls.GlideSpeed = 0.3; entity.Pos.Pitch = 0; entity.Pos.Motion.Set(0, 0, 0);
        for (int i = 0; i < 100; i++) physics.MotionAndCollision(entity.Pos, entity.Controls, 1f / 60);
        Check(entity.Pos.Motion.Length() > system.SpeedCap + 0.01, "original glider retains its own flight and higher speed cap");
        Check(entity.ShouldReceiveDamage(new DamageSource { Type = EnumDamageType.Gravity }, 20), "original glider gets no added fall immunity");
        bags[0].Itemstack = stack;
        // Drive V through real physics plus the 20 Hz server energy meter and
        // the actual client charge handler. No game, socket or save is opened.
        entity.Controls.Gliding = false; entity.Controls.IsFlying = false; entity.Controls.Sneak = false;
        entity.Pos.Motion.Set(0, 0, 0); stack.Attributes.SetInt("durability", 4000);
        long clock = 0;
        var serverWorld = Proxy.Make<IServerWorldAccessor>((m, a) => m.Name switch {
            "get_ElapsedMilliseconds" => clock, "get_AllOnlinePlayers" => new IPlayer[] { player },
            "PlayerByUid" => player, "get_Api" => api, _ => Proxy.Default(m.ReturnType) });
        var server = Proxy.Make<ICoreServerAPI>((m, a) => m.Name switch {
            "get_World" => serverWorld, "get_ModLoader" => loader, "get_Side" => EnumAppSide.Server,
            _ => Proxy.Default(m.ReturnType) });
        AccessTools.Field(typeof(Mk2System), "server").SetValue(system, server);
        var mirror = new CountingInventory("offline-client");
        var clientInventory = Proxy.Make<IPlayerInventoryManager>((m, a) => m.Name == "GetOwnInventory" ? mirror : Proxy.Default(m.ReturnType));
        var clientPlayer = Proxy.Make<IClientPlayer>((m, a) => m.Name switch {
            "get_Entity" => entity, "get_InventoryManager" => clientInventory, _ => Proxy.Default(m.ReturnType) });
        var clientWorld = Proxy.Make<IClientWorldAccessor>((m, a) => m.Name == "get_Player" ? clientPlayer : Proxy.Default(m.ReturnType));
        var client = Proxy.Make<ICoreClientAPI>((m, a) => m.Name == "get_World" ? clientWorld : Proxy.Default(m.ReturnType));
        var clientSystem = new Mk2System();
        AccessTools.Field(typeof(Mk2System), "client").SetValue(clientSystem, client);
        MethodInfo receive = AccessTools.Method(typeof(Mk2System), "OnChargeUpdate");
        var messages = new List<ChargeUpdate>();
        var channel = Proxy.Make<IServerNetworkChannel>((m, a) => {
            if (m.Name == "SendPacket" && a?[0] is ChargeUpdate update)
            {
                using var encoded = new MemoryStream();
                ProtoBuf.Serializer.Serialize(encoded, update);
                encoded.Position = 0;
                update = ProtoBuf.Serializer.Deserialize<ChargeUpdate>(encoded);
                messages.Add(update);
                // Model the single native inventory update that establishes
                // identity; all later ticks use only the lightweight handler.
                if (mirror[0].Empty) mirror[0].Itemstack = stack.Clone();
                receive.Invoke(clientSystem, new object[] { update });
            }
            return Proxy.Default(m.ReturnType);
        });
        AccessTools.Field(typeof(Mk2System), "serverChannel").SetValue(system, channel);
        MethodInfo request = AccessTools.Method(typeof(Mk2System), "OnLiftRequest");
        MethodInfo debit = AccessTools.Method(typeof(Mk2System), "ServerTick");
        bags.DirtyMarks = 0; bags.HeavyChanges = 0;
        double previousLiftY = 0;
        entity.Controls.Forward = true; entity.Controls.Right = true;
        for (int i = 0; i < 300; i++)
        {
            clock = (long)(i * 1000.0 / 60);
            if (i % 12 == 0) request.Invoke(system, new object[] { player, new LiftRequest { Held = true } });
            physics.MotionAndCollision(entity.Pos, entity.Controls, 1f / 60);
            Check(entity.Pos.Motion.Y >= previousLiftY - 1e-10, "V lift has no velocity drops across heartbeat/charge ticks");
            Check(entity.Pos.Motion.Y * 60 <= 4 + 1e-8, "V lift keeps four blocks/second cap");
            previousLiftY = entity.Pos.Motion.Y;
            if (i % 3 == 0) debit.Invoke(system, new object[] { 0.05f });
        }
        Check(Math.Abs(entity.Pos.Motion.Y * 60 - 4) < 1e-6, "V lift reaches smooth steady ascent through real modules");
        Check(Math.Abs(Math.Sqrt(entity.Pos.Motion.X * entity.Pos.Motion.X + entity.Pos.Motion.Z * entity.Pos.Motion.Z) * 60 - 4) < 1e-6,
            "V diagonal positioning reaches four blocks/second through real modules without diagonal speed bonus");
        entity.Controls.Forward = false; entity.Controls.Right = false;
        Check(stack.Attributes.GetInt("durability") == 3775, "V server billing remains 45 units/second");
        Check(messages.Count == 100 && bags.DirtyMarks == 1, "one initial inventory sync replaces 100 repeated slot updates");
        Check(bags.HeavyChanges == 0 && mirror.HeavyChanges == 0 && mirror.DirtyMarks == 0, "charge-only sync triggers no server/client inventory refresh callbacks");
        Check(mirror[0].Itemstack!.Attributes.GetInt("durability") == stack.Attributes.GetInt("durability"), "client charge matches server after sustained lift");
        double currentFraction = mirror[0].Itemstack!.Attributes.GetDouble("mk2FractionalUse");
        receive.Invoke(clientSystem, new object[] { messages[^2] });
        Check(mirror[0].Itemstack!.Attributes.GetInt("durability") == 3775
            && mirror[0].Itemstack!.Attributes.GetDouble("mk2FractionalUse") == currentFraction, "late charge packets cannot roll the meter back");
        var savedMirror = mirror[0].Itemstack;
        mirror[0].Itemstack = new ItemStack(item); mirror[0].Itemstack!.Attributes.SetInt("durability", 1234);
        receive.Invoke(clientSystem, new object[] { messages[^1] });
        Check(mirror[0].Itemstack!.Attributes.GetInt("durability") == 1234, "charge packet cannot update a replacement stack in the same slot");
        mirror[0].Itemstack = savedMirror;
        stack.Attributes.SetInt("durability", 1); debit.Invoke(system, new object[] { 0.05f });
        Check(stack.Attributes.GetInt("durability") == 0 && mirror[0].Itemstack!.Attributes.GetInt("durability") == 0,
            "empty battery is synced immediately");
        Check(!Mk2System.Powered(entity, entity.Controls, out _, out _), "depleted V lift stops without a stale charged client meter");
        entity.Controls.Gliding = false; stack.Attributes.SetInt("durability", 0);
        Check(!entity.ShouldReceiveDamage(new DamageSource { Type = EnumDamageType.Gravity }, 20), "equipped empty folded glider blocks fall damage");
        Check(entity.ShouldReceiveDamage(new DamageSource { Type = EnumDamageType.Fire }, 1), "other damage remains enabled");
        bags[0].Itemstack = null;
        Check(entity.ShouldReceiveDamage(new DamageSource { Type = EnumDamageType.Gravity }, 20), "unequipping removes fall immunity");
        AccessTools.Field(typeof(Mk2System), "server").SetValue(system, null);
        AccessTools.Field(typeof(Mk2System), "serverChannel").SetValue(system, null);
        system.Dispose();
        Console.WriteLine($"PASS: {checks} offline checks through the Harmony-patched native/EP physics pipeline. No game/server or save loaded.");
        return 0;
    }
}

internal sealed class TestPhysics(Entity entity) : EntityBehaviorControlledPhysics(entity)
{
    public void Add(PModule module) => physicsModules.Add(module);
}

internal sealed class CountingInventory(string id) : InventoryGeneric(2, "backpack", id, null, (_, inv) => new ItemSlotBackpack(inv))
{
    public int HeavyChanges, DirtyMarks;
    public override void OnItemSlotModified(ItemSlot slot) { HeavyChanges++; base.OnItemSlotModified(slot); }
    public override void MarkSlotDirty(int slotId) { DirtyMarks++; base.MarkSlotDirty(slotId); }
}

public class Proxy : DispatchProxy
{
    public System.Func<MethodInfo, object?[]?, object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args);
    public static T Make<T>(System.Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        // The game exposes an internal IPlayer member. Match its access for
        // this test-only proxy, without modifying any game assembly.
        Type generator = typeof(DispatchProxy).Assembly.GetType("System.Reflection.DispatchProxyGenerator")!;
        object map = generator.GetField("s_alcProxyAssemblyMap", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        object?[] lookup = { AssemblyLoadContext.GetLoadContext(typeof(Proxy).Assembly), null };
        if ((bool)map.GetType().GetMethod("TryGetValue")!.Invoke(map, lookup)!)
        {
            var builder = (AssemblyBuilder)lookup[1]!.GetType().GetField("_ab", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(lookup[1])!;
            if (!builder.GetCustomAttributesData().Any(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute"))
                builder.SetCustomAttribute(new CustomAttributeBuilder(typeof(System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute).GetConstructor(new[] { typeof(string) })!, new object[] { "VintagestoryAPI" }));
        }
        T proxy = Create<T, Proxy>(); ((Proxy)(object)proxy).Handler = handler; return proxy;
    }
    public static object? Default(Type type) => type == typeof(void) ? null : type.IsValueType ? Activator.CreateInstance(type) : null;
}
