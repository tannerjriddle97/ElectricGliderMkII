using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace ElectricGliderMk2;

public static class FlightVisibility
{
    public static bool Hide(Entity? entity, ICoreClientAPI? api, bool shadow)
    {
        var local = api?.World?.Player;
        return entity is EntityPlayer player && local?.Entity?.EntityId == player.EntityId
            && local.CameraMode == EnumCameraMode.FirstPerson
            && Mk2System.HideFirstPersonFlightBody(player, true, RenderMode.FirstPerson, shadow);
    }

    public static void Install(Harmony harmony)
    {
        // A direct base call bypasses a patched override. Cover both entry points
        // and the shared body/item draws; never trust the cached renderMode.
        foreach (Type type in new[] { typeof(EntityPlayerShapeRenderer), typeof(EntityShapeRenderer) })
        foreach (string name in new[] { "DoRender3DOpaque", "DoRender3DOpaqueBatched", "RenderHeldItem", "RenderItem" })
        {
            MethodInfo? method = AccessTools.DeclaredMethod(type, name);
            if (method == null) continue;
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(FlightVisibility), nameof(RenderPrefix))
                { priority = Priority.First });
        }
    }

    private static bool RenderPrefix(Entity ___entity, ICoreClientAPI ___capi, bool isShadowPass)
    {
        if (!Hide(___entity, ___capi, isShadowPass)) return true;
        ((EntityPlayer)___entity).selfNowShadowPass = false;
        return false;
    }

    // Optional renderer compatibility: reuse its own camera-only exclusion,
    // preserving secondary/shadow poses. No settings or native buffers change.
    [ThreadStatic] private static bool hideOptionalCamera;

    public static void OptionalFramePrefix(ICoreClientAPI ___api, out bool __state)
    {
        __state = hideOptionalCamera;
        hideOptionalCamera = Hide(___api.World?.Player?.Entity, ___api, false);
    }

    public static void OptionalFrameFinalizer(bool __state) => hideOptionalCamera = __state;

    public static void OptionalZoomPostfix(ref bool __result) => __result |= hideOptionalCamera;

    public static bool InstallOptionalRenderer(Harmony harmony, ILogger? logger)
    {
        Type? type = AccessTools.TypeByName("VsRtx.Scene.EntityLink");
        if (type == null) return false;
        try
        {
            MethodInfo? frame = AccessTools.DeclaredMethod(type, "Frame", new[] { typeof(float) });
            MethodInfo? render = AccessTools.DeclaredMethod(type, "OnRenderFrame", new[] { typeof(float), typeof(EnumRenderStage) });
            FieldInfo? api = AccessTools.Field(type, "api");
            Type? fov = AccessTools.TypeByName("VsRtx.Scene.ViewFov");
            MethodInfo? zoom = fov == null ? null : AccessTools.PropertyGetter(fov, "Zoomed");
            if (frame == null || render == null || api?.FieldType != typeof(ICoreClientAPI) || zoom?.ReturnType != typeof(bool)
                || !zoom.IsStatic || PatchProcessor.GetOriginalInstructions(frame).Count(i => i.Calls(zoom)) != 1
                || PatchProcessor.GetOriginalInstructions(render).Count(i => i.Calls(frame)) != 1)
            {
                logger?.Warning("[Mk II] Optional renderer camera hook not recognized; vanilla visibility hooks remain active.");
                return false;
            }
            // Frame contains exception filters that this game's Harmony cannot
            // re-emit. Scope the existing camera-exclusion flag to its render
            // callback instead; a finalizer restores it even if rendering fails.
            if (Harmony.GetPatchInfo(zoom)?.Postfixes.Any(p => p.owner == harmony.Id) != true)
                harmony.Patch(zoom, postfix: new HarmonyMethod(typeof(FlightVisibility), nameof(OptionalZoomPostfix)));
            if (Harmony.GetPatchInfo(render)?.Prefixes.Any(p => p.owner == harmony.Id) != true)
                harmony.Patch(render,
                    prefix: new HarmonyMethod(typeof(FlightVisibility), nameof(OptionalFramePrefix)),
                    finalizer: new HarmonyMethod(typeof(FlightVisibility), nameof(OptionalFrameFinalizer)));
            return true;
        }
        catch (Exception error)
        {
            logger?.Warning("[Mk II] Optional renderer camera hook unavailable: {0}", error.Message);
            return false;
        }
    }

}
