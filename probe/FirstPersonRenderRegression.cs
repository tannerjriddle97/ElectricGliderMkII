using System.Reflection;
using System.Runtime.CompilerServices;
using ElectricGliderMk2;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

public partial class Probe
{
    private void TestFirstPersonFlightRendering(EntityPlayer entity,CountingBackpack bags,ItemStack stock)
    {
        entity.Controls.Gliding=true;
        foreach(RenderMode mode in new[]{RenderMode.FirstPerson,RenderMode.ImmersiveFirstPerson,RenderMode.ThirdPerson})
        foreach(bool self in new[]{true,false})
        foreach(bool shadow in new[]{true,false})
            Check(Mk2System.HideFirstPersonFlightBody(entity,self,mode,shadow)==(self&&!shadow&&mode!=RenderMode.ThirdPerson),
                $"flight rendering gate: {mode}, self={self}, shadow={shadow}");

        var player=TestProxy.Make<IClientPlayer>((m,a)=>m.Name switch {
            "get_Entity"=>entity,"get_CameraMode"=>EnumCameraMode.FirstPerson,_=>TestProxy.Default(m.ReturnType)});
        var world=TestProxy.Make<IClientWorldAccessor>((m,a)=>m.Name=="get_Player"?player:TestProxy.Default(m.ReturnType));
        var client=TestProxy.Make<ICoreClientAPI>((m,a)=>m.Name=="get_World"?world:TestProxy.Default(m.ReturnType));
        // An uninitialized renderer has no GPU state. Each patched render method
        // must return before touching it when our flight camera is hidden.
        var renderer=(EntityPlayerShapeRenderer)RuntimeHelpers.GetUninitializedObject(typeof(EntityPlayerShapeRenderer));
        AccessTools.Field(typeof(EntityPlayerShapeRenderer),"entityPlayer").SetValue(renderer,entity);
        AccessTools.Field(typeof(EntityPlayerShapeRenderer),"entity").SetValue(renderer,entity);
        AccessTools.Field(typeof(EntityPlayerShapeRenderer),"capi").SetValue(renderer,client);
        foreach(RenderMode mode in new[]{RenderMode.FirstPerson,RenderMode.ImmersiveFirstPerson})
        {
            AccessTools.Field(typeof(EntityPlayerShapeRenderer),"renderMode").SetValue(renderer,mode);
            foreach(string name in new[]{"DoRender3DOpaque","DoRender3DOpaqueBatched"})
            {
                MethodInfo method=AccessTools.Method(typeof(EntityPlayerShapeRenderer),name);
                Check(Harmony.GetPatchInfo(method)?.Prefixes.Any(p=>p.owner=="obbra.electricglidermk2")==true,
                    $"flight render hook installed: {name} {mode}");
                entity.selfNowShadowPass=true;
                method.Invoke(renderer,new object[]{0.016f,false});
                Check(!entity.selfNowShadowPass,$"hidden camera render safely skipped: {name} {mode}");
            }
        }
        entity.Controls.Gliding=false;
        Check(!Mk2System.HideFirstPersonFlightBody(entity,true,RenderMode.FirstPerson,false),"arms restored immediately when gliding ends");
        entity.Controls.Gliding=true;
        ItemStack upgraded=bags[0].Itemstack!;
        bags[0].Itemstack=stock;
        Check(!Mk2System.HideFirstPersonFlightBody(entity,true,RenderMode.FirstPerson,false),"original EP glider rendering unchanged");
        bags[0].Itemstack=upgraded;
        entity.Controls.Gliding=false;
    }
}
