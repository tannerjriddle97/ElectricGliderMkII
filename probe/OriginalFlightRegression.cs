using System.Reflection;
using System.Runtime.CompilerServices;
using ElectricGliderMk2;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

public partial class Probe
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void OriginalEpFlight(object handler,float dt,Entity entity,EntityPos pos,EntityControls controls)
        => throw new NotSupportedException("Reverse patch was not initialized");

    private void TestPhysics(ICoreServerAPI realApi,Item mk2,Item original)
    {
        var bags=new CountingBackpack(realApi);
        var upgraded=new ItemStack(mk2); upgraded.Attributes.SetInt("durability",4000);
        var stock=new ItemStack(original); stock.Attributes.SetInt("durability",1000);
        bags[0].Itemstack=upgraded;
        EntityPlayer entity=new(){Api=realApi,EntityId=-9981};
        entity.WatchedAttributes.SetString("playerUID","mk2-probe");
        AccessTools.PropertySetter(typeof(Entity),"Properties").Invoke(entity,new object[]{new EntityProperties{Habitat=EnumHabitat.Land,Client=new EntityClientProperties([],new())}});
        var inventory=TestProxy.Make<IPlayerInventoryManager>((m,args)=>m.Name=="GetOwnInventory"?bags:null);
        var data=TestProxy.Make<IWorldPlayerData>((m,args)=>TestProxy.Default(m.ReturnType));
        IServerPlayer player=TestProxy.Make<IServerPlayer>((m,args)=>m.Name switch{
            "get_Entity"=>entity,"get_PlayerUID"=>"mk2-probe","get_InventoryManager"=>inventory,"get_WorldData"=>data,_=>TestProxy.Default(m.ReturnType)});
        IServerWorldAccessor world=TestProxy.Make<IServerWorldAccessor>((m,args)=>m.Name switch{
            "PlayerByUid"=>player,"get_AllOnlinePlayers"=>new IPlayer[]{player},_=>m.Invoke(realApi.World,args)});
        entity.World=world;
        TestFirstPersonFlightRendering(entity,bags,stock);
        entity.TpAnimManager.Init(realApi,entity);entity.SelfFpAnimManager.Init(realApi,entity);
        ICoreServerAPI wrapped=TestProxy.Make<ICoreServerAPI>((m,args)=>m.Name=="get_World"?world:m.Invoke(realApi,args));
        var system=realApi.ModLoader.GetModSystem<Mk2System>();
        FieldInfo serverField=AccessTools.Field(typeof(Mk2System),"server");
        object? oldServer=serverField.GetValue(system);serverField.SetValue(system,wrapped);
        FieldInfo channelField=AccessTools.Field(typeof(Mk2System),"serverChannel");
        object? oldChannel=channelField.GetValue(system);channelField.SetValue(system,null);
        Type epType=AccessTools.TypeByName("ElectricalProgressive.Net.EGliderFlightPacketHandler");
        object handler=realApi.ModLoader.GetModSystem(epType.FullName!);
        MethodInfo flight=AccessTools.Method(epType,"ApplyFlyingPhysics");
        var reverse=new Harmony("mk2.probe.original");
        reverse.CreateReversePatcher(flight,new HarmonyMethod(typeof(Probe),nameof(OriginalEpFlight))).Patch(HarmonyReversePatchType.Original);
        MethodInfo tick=AccessTools.Method(typeof(Mk2System),"ServerTick");
        MethodInfo request=AccessTools.Method(typeof(Mk2System),"OnLiftRequest");
        var air=new PModulePlayerInAir();air.Initialize(null,entity);
        var epAir=(PModuleInAir)Activator.CreateInstance(AccessTools.TypeByName("Vintagestory.API.Common.Entities.EGlideringPlayerInAir"))!;epAir.Initialize(null,entity);
        var gravity=new PModuleGravity();gravity.Initialize(null,entity);
        var drag=new PModuleMotionDrag();drag.Initialize(null,entity);
        var physics=new ProbePhysicsPipeline(entity);
        foreach(var module in new PModule[]{air,epAir,gravity,drag})physics.Add(module);
        void Reset(bool boost,double pitch,double yaw)
        {
            entity.Controls.Gliding=true;entity.Controls.IsFlying=true;entity.Controls.Sneak=boost;
            entity.Controls.GlideSpeed=0.1;entity.Pos.Pitch=(float)pitch;entity.Pos.Yaw=(float)yaw;
            entity.Pos.Motion.Set(0.05,-0.01,0.02);
            AccessTools.Field(epType,"wasAfterburnerActive").SetValue(handler,false);
            AccessTools.Field(epType,"isAfterburnerCooldown").SetValue(handler,false);
            AccessTools.Field(epType,"cooldownTimer").SetValue(handler,0f);
        }
        try
        {
            Check(Harmony.GetPatchInfo(AccessTools.Method(typeof(EntityBehaviorControlledPhysics),"MotionAndCollision"))?.Owners.Contains("obbra.electricglidermk2")==true,"Mk II limits wrap the complete native physics pipeline");
            foreach(bool boost in new[]{false,true})
            foreach(double pitch in new[]{-0.5,0.0,0.5})
            foreach(double yaw in new[]{0.0,1.7,3.14})
            {
                bags[0].Itemstack=stock;
                Reset(boost,pitch,yaw);OriginalEpFlight(handler,0.05f,entity,entity.Pos,entity.Controls);
                var baseline=entity.Pos.Motion.Clone();double baseSpeed=entity.Controls.GlideSpeed;
                Reset(boost,pitch,yaw);flight.Invoke(handler,new object[]{0.05f,entity,entity.Pos,entity.Controls});
                Check(entity.Pos.Motion.DistanceTo(baseline)<1e-9 && Math.Abs(entity.Controls.GlideSpeed-baseSpeed)<1e-12,"original glider unchanged versus original EP method: "+boost+","+pitch+","+yaw);
                bags[0].Itemstack=upgraded;
                Reset(boost,pitch,yaw);
                OriginalEpFlight(handler,0.05f,entity,entity.Pos,entity.Controls);
                baseline=entity.Pos.Motion.Clone();baseSpeed=entity.Controls.GlideSpeed;
                Reset(boost,pitch,yaw);
                flight.Invoke(handler,new object[]{0.05f,entity,entity.Pos,entity.Controls});
                Check(entity.Pos.Motion.DistanceTo(baseline)<1e-9&&Math.Abs(entity.Controls.GlideSpeed-baseSpeed)<1e-12,
                    "Mk II retains EP native flight method without erasing its motion");
            }
            bags[0].Itemstack=upgraded;bags[1].Itemstack=stock;
            var findArgs=new object?[]{entity,null};
            Check((bool)AccessTools.Method(epType,"TryFindGlider").Invoke(null,findArgs)! && ReferenceEquals(findArgs[1],bags[0]),"EP detector selects Mk II even alongside original");
            Reset(true,0.4,0.5);
            for(int i=0;i<200;i++)
            {
                physics.MotionAndCollision(entity.Pos,entity.Controls,0.05f);
                Check(entity.Pos.Motion.Length()<=system.BoostSpeedCap+1e-6,"Mk II boosted speed remains capped across all physics modules");
            }
            entity.Controls.Gliding=false;
            foreach(int charge in new[]{0,4000})
            {
                upgraded.Attributes.SetInt("durability",charge);
                Check(!entity.ShouldReceiveDamage(new DamageSource{Type=EnumDamageType.Gravity},20),"equipped folded Mk II prevents fall damage at charge "+charge);
                Check(entity.ShouldReceiveDamage(new DamageSource{Type=EnumDamageType.Fire},1),"Mk II does not grant fire immunity");
            }
            bags[0].Itemstack=stock;
            Check(entity.ShouldReceiveDamage(new DamageSource{Type=EnumDamageType.Gravity},20),"original glider has no added fall immunity");
            bags[0].Itemstack=upgraded;
            Reset(true,0.4,0.5);
            // The separate energy meter must not be stacked with EP's original one.
            var active=(Dictionary<string,bool>)AccessTools.Field(epType,"activeAfterburners").GetValue(handler)!;
            var epServerField=AccessTools.Field(epType,"sapi");object? previous=epServerField.GetValue(handler);epServerField.SetValue(handler,wrapped);
            try
            {
                active[player.PlayerUID]=true;int before=upgraded.Attributes.GetInt("durability");
                AccessTools.Method(epType,"OnDurabilityTick").Invoke(handler,new object[]{1f});
                Check(upgraded.Attributes.GetInt("durability")==before,"EP original meter does not double-charge Mk II");
                bags.HeavyChanges=0;bags.DirtyMarks=0;
                entity.Pos.Pitch=0;tick.Invoke(system,new object[]{2f});
                Check(upgraded.Attributes.GetInt("durability")==before-45,"Mk II powered flight billed at 1.5x original cost");
                Check(bags.HeavyChanges==0 && bags.DirtyMarks>0,"charge is queued for inventory synchronization without backpack reload/player-data callback");
            }
            finally{epServerField.SetValue(handler,previous);}
            entity.Controls.Sneak=false;entity.OnGround=true;entity.Pos.Motion.Set(0,0,0);
            request.Invoke(system,new object[]{player,new LiftRequest{Held=true}});
            entity.CollidedVertically=true;
            Check(air.Applicable(entity,entity.Pos,entity.Controls),"V lift works from ground without glider deployment");entity.CollidedVertically=false;
            int liftBefore=upgraded.Attributes.GetInt("durability");
            for(int i=0;i<100;i++)
            {
                air.DoApply(0.05f,entity,entity.Pos,entity.Controls);epAir.DoApply(0.05f,entity,entity.Pos,entity.Controls);
                if(gravity.Applicable(entity,entity.Pos,entity.Controls))gravity.DoApply(0.05f,entity,entity.Pos,entity.Controls);
                drag.DoApply(0.05f,entity,entity.Pos,entity.Controls);tick.Invoke(system,new object[]{0.05f});
            }
            Check(Math.Abs(entity.Pos.Motion.Y*60-4)<0.001,"separate lift maintains four blocks per second");
            Check(upgraded.Attributes.GetInt("durability")==liftBefore-225,"lift billed at 3x original cost");
            Check(bags.HeavyChanges==0 && bags.DirtyMarks>=100,"continuous lift never triggers full backpack/player-data refresh for charge deductions");
            request.Invoke(system,new object[]{player,new LiftRequest{Held=false}});
            Check(!entity.Controls.Gliding && !entity.Controls.IsFlying && entity.Controls.GlideSpeed==0,"V release leaves glider folded and airspeed zero");
            TestLiftInput(realApi,system,entity,inventory,data,air);
        }
        finally{serverField.SetValue(system,oldServer);channelField.SetValue(system,oldChannel);}
    }

    private void TestLiftInput(ICoreServerAPI realApi,Mk2System system,EntityPlayer entity,IPlayerInventoryManager inventory,IWorldPlayerData data,PModulePlayerInAir air)
    {
        var keys=new bool[300];float yaw=0;bool grabbed=true;
        var mapping=new KeyCombination{KeyCode=(int)GlKeys.V};var hotkey=new HotKey{CurrentMapping=mapping};
        var input=TestProxy.Make<IInputAPI>((m,args)=>m.Name switch{"get_KeyboardKeyStateRaw"=>keys,"get_MouseGrabbed"=>grabbed,"GetHotKeyByCode"=>hotkey,"get_MouseYaw"=>yaw,_=>TestProxy.Default(m.ReturnType)});
        var player=TestProxy.Make<IClientPlayer>((m,args)=>m.Name switch{"get_Entity"=>entity,"get_InventoryManager"=>inventory,"get_WorldData"=>data,"get_PlayerUID"=>"mk2-probe","get_CameraPitch"=>0f,_=>TestProxy.Default(m.ReturnType)});
        var world=TestProxy.Make<IClientWorldAccessor>((m,args)=>m.Name switch{"get_Player"=>player,"PlayerByUid"=>player,"LoadSound"=>null,_=>m.Invoke(realApi.World,args)});
        var client=TestProxy.Make<ICoreClientAPI>((m,args)=>m.Name switch{"get_World"=>world,"get_Input"=>input,_=>TestProxy.Default(m.ReturnType)});
        var channel=TestProxy.Make<IClientNetworkChannel>((m,args)=>TestProxy.Default(m.ReturnType));
        var clientField=AccessTools.Field(typeof(Mk2System),"client");var channelField=AccessTools.Field(typeof(Mk2System),"clientChannel");
        clientField.SetValue(system,client);channelField.SetValue(system,channel);
        MethodInfo tick=AccessTools.Method(typeof(Mk2System),"ClientTick");
        try
        {
            keys[(int)GlKeys.V]=true;tick.Invoke(system,new object[]{0.02f});
            Check(system.LiftActive(entity)&&!entity.Controls.Gliding,"V remains independent of gliding");
            Check(Mk2System.HideFirstPersonFlightBody(entity,true,RenderMode.FirstPerson,false),
                "first-person arms hidden during separate V lift");
            entity.ServerControls.Gliding=true;entity.ServerControls.IsFlying=true;
            entity.CurrentControls=EnumEntityActivity.Glide;entity.WalkPitch=0.8f;entity.Pos.Roll=0.4f;
            entity.TpAnimManager.ActiveAnimationsByAnimCode["glide"]=new AnimationMetaData{Code="glide",Animation="glide"};
            entity.SelfFpAnimManager.ActiveAnimationsByAnimCode["glide-fp"]=new AnimationMetaData{Code="glide-fp",Animation="glide-fp"};
            Mk2System.KeepLiftUpright(entity);
            Check(!entity.ServerControls.Gliding&&!entity.ServerControls.IsFlying&&entity.WalkPitch==0&&entity.Pos.Roll==0,
                "V lift clears stale visual flight flags, body pitch and roll");
            Check(!entity.TpAnimManager.ActiveAnimationsByAnimCode.ContainsKey("glide")&&!entity.SelfFpAnimManager.ActiveAnimationsByAnimCode.ContainsKey("glide-fp"),
                "V lift suppresses third-person and first-person glider animations");
            foreach(float heading in new[]{0f,0.8f,1.7f,3.14f,4.9f})
            foreach(int direction in new[]{0,1,2,3})
            {
                yaw=heading;entity.Controls.Forward=direction==0;entity.Controls.Backward=direction==1;
                entity.Controls.Right=direction==2;entity.Controls.Left=direction==3;
                entity.Controls.MovespeedMultiplier=1;entity.Controls.CalcMovementVectors(new EntityPos{Pitch=0,Yaw=heading},0.05f);
                entity.Pos.Motion.Set(0,0,0);air.DoApply(0.05f,entity,entity.Pos,entity.Controls);
                double dot=entity.Pos.Motion.X*entity.Controls.WalkVector.X+entity.Pos.Motion.Z*entity.Controls.WalkVector.Z;
                Check(dot>0,"V positioning matches normal walking direction "+direction+" at yaw "+heading);
            }
            entity.Controls.Forward=entity.Controls.Backward=entity.Controls.Left=entity.Controls.Right=false;
            keys[(int)GlKeys.V]=false;tick.Invoke(system,new object[]{0.02f});
            Check(!system.LiftActive(entity)&&!entity.Controls.Gliding&&entity.Controls.GlideSpeed==0,"V release has no automatic forward flight");
            entity.OnGround=false;
            var gliding=new ModSystemGliding();AccessTools.Field(typeof(ModSystemGliding),"capi").SetValue(gliding,client);
            AccessTools.Method(typeof(ModSystemGliding),"Input_InWorldAction").Invoke(gliding,new object[]{EnumEntityAction.Jump,true,EnumHandling.PassThrough});
            Check(entity.Controls.Gliding&&entity.Controls.IsFlying,"original Space action deploys Mk II normally");
            entity.ServerControls.Gliding=true;entity.ServerControls.IsFlying=true;entity.WalkPitch=0.5f;
            Mk2System.KeepLiftUpright(entity);
            Check(entity.ServerControls.Gliding&&entity.ServerControls.IsFlying&&entity.WalkPitch==0.5f,
                "normal glider pose is untouched after V release and manual deployment");
            grabbed=false;keys[(int)GlKeys.V]=true;tick.Invoke(system,new object[]{0.02f});
            Check(!system.LiftActive(entity),"menu focus prevents lift");
        }
        finally{AccessTools.Field(typeof(Mk2System),"liftHeld").SetValue(system,false);clientField.SetValue(system,null);channelField.SetValue(system,null);}
    }
}

public class ProbePhysicsPipeline(Entity entity) : EntityBehaviorControlledPhysics(entity)
{
    public void Add(PModule module) => physicsModules.Add(module);
}

public class CountingBackpack : InventoryGeneric
{
    public int HeavyChanges;
    public int DirtyMarks;
    public CountingBackpack(ICoreAPI api):base(2,"backpack","mk2-test",api,(_,inv)=>new ItemSlotBackpack(inv)){}
    public override void OnItemSlotModified(ItemSlot slot){HeavyChanges++;base.OnItemSlotModified(slot);}
    public override void MarkSlotDirty(int slotId){DirtyMarks++;base.MarkSlotDirty(slotId);}
}
