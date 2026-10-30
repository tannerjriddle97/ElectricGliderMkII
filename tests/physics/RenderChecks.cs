using System.Reflection;
using System.Runtime.CompilerServices;
using ElectricGliderMk2;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

internal static class RenderChecks
{
    public static void Run(EntityPlayer entity, CountingInventory bags, Action<bool, string> check, string? references)
    {
        EnumCameraMode camera = EnumCameraMode.FirstPerson;
        EntityPlayer local = entity;
        var player = Proxy.Make<IClientPlayer>((m, a) => m.Name switch {
            "get_Entity" => local, "get_CameraMode" => camera, _ => Proxy.Default(m.ReturnType) });
        var world = Proxy.Make<IClientWorldAccessor>((m, a) => m.Name == "get_Player" ? player : Proxy.Default(m.ReturnType));
        var client = Proxy.Make<ICoreClientAPI>((m, a) => m.Name == "get_World" ? world : Proxy.Default(m.ReturnType));
        entity.Controls.Gliding = true;
        foreach (var mode in new[] { EnumCameraMode.FirstPerson, EnumCameraMode.ThirdPerson })
        foreach (bool self in new[] { true, false })
        foreach (bool shadow in new[] { true, false })
        {
            camera = mode; local = self ? entity : new EntityPlayer { EntityId = entity.EntityId - 1 };
            check(FlightVisibility.Hide(entity, client, shadow) == (self && !shadow && mode == EnumCameraMode.FirstPerson),
                $"camera render gate {mode}, self={self}, shadow={shadow}");
        }
        camera = EnumCameraMode.FirstPerson; local = entity;
        check(!FlightVisibility.Hide(null, client, false), "missing entity keeps original renderer");
        check(!FlightVisibility.Hide(entity, null, false), "missing client keeps original renderer");
        // No renderer construction or GPU context. A missed guard reaches native
        // render state and throws. Exercise every declared method, including base calls.
        foreach (Type type in new[] { typeof(EntityPlayerShapeRenderer), typeof(EntityShapeRenderer) })
        {
            object renderer = RuntimeHelpers.GetUninitializedObject(type);
            AccessTools.Field(type, "entity").SetValue(renderer, entity);
            AccessTools.Field(type, "capi").SetValue(renderer, client);
            if (type == typeof(EntityPlayerShapeRenderer))
                AccessTools.Field(type, "entityPlayer").SetValue(renderer, entity);
            foreach (RenderMode staleMode in new[] { RenderMode.FirstPerson, RenderMode.ImmersiveFirstPerson, RenderMode.ThirdPerson })
            {
                if (type == typeof(EntityPlayerShapeRenderer))
                    AccessTools.Field(type, "renderMode").SetValue(renderer, staleMode);
                foreach (string name in new[] { "DoRender3DOpaque", "DoRender3DOpaqueBatched", "RenderHeldItem", "RenderItem" })
                {
                    MethodInfo? method = AccessTools.DeclaredMethod(type, name);
                    if (method == null) continue;
                    check(Harmony.GetPatchInfo(method)?.Prefixes.Any(p => p.owner == "obbra.electricglidermk2") == true,
                        $"render guard installed {type.Name}.{name}");
                    object?[] args = method.GetParameters().Select(p => p.ParameterType == typeof(float) ? (object)0.016f
                        : p.ParameterType == typeof(bool) ? false : null).ToArray();
                    entity.selfNowShadowPass = true;
                    method.Invoke(renderer, args);
                    check(!entity.selfNowShadowPass, $"hidden flight draw skipped {type.Name}.{name}, cached={staleMode}");
                }
            }
        }
        bool Zoom(bool value) { FlightVisibility.OptionalZoomPostfix(ref value); return value; }
        FlightVisibility.OptionalFramePrefix(client, out bool previous);
        check(Zoom(false), "optional renderer hides flight body without zoom");
        FlightVisibility.OptionalFrameFinalizer(previous);
        check(!Zoom(false), "optional exclusion never leaks outside entity frame");
        entity.Controls.Gliding = false;
        check(!FlightVisibility.Hide(entity, client, false), "on-foot arms restored");
        FlightVisibility.OptionalFramePrefix(client, out previous);
        check(!Zoom(false), "optional renderer restores on-foot body");
        check(Zoom(true), "optional renderer preserves normal zoom exclusion");
        FlightVisibility.OptionalFrameFinalizer(previous);
        entity.WatchedAttributes.SetBool("mk2VerticalLift", true);
        check(FlightVisibility.Hide(entity, client, false), "lift camera hidden while folded");
        entity.WatchedAttributes.SetBool("mk2VerticalLift", false);
        entity.Controls.Gliding = true;
        ItemStack upgraded = bags[0].Itemstack!;
        bags[0].Itemstack = new ItemStack(new Item { Code = new AssetLocation("electricalprogressiveequipment:glider") });
        check(!FlightVisibility.Hide(entity, client, false), "original glider model preserved");
        bags[0].Itemstack = upgraded;
        if (references != null)
        {
            Assembly.LoadFrom(Path.Combine(references, "VsRtx.Scene.dll"));
            Type type = AccessTools.TypeByName("VsRtx.Scene.EntityLink")!;
            MethodInfo render = AccessTools.DeclaredMethod(type, "OnRenderFrame");
            MethodInfo zoom = AccessTools.PropertyGetter(AccessTools.TypeByName("VsRtx.Scene.ViewFov"), "Zoomed");
            // No construction or native rendering: stopped callbacks return before Frame.
            var logger = Proxy.Make<ILogger>((m, a) => {
                if (m.Name == "Warning") Console.WriteLine("Optional renderer: " + string.Join(" ", a!.Select(v => v is object[] values ? string.Join(" ", values) : v)));
                return Proxy.Default(m.ReturnType);
            });
            check(FlightVisibility.InstallOptionalRenderer(new Harmony("obbra.electricglidermk2"), logger),
                "optional camera hooks install on actual renderer methods");
            object renderer = RuntimeHelpers.GetUninitializedObject(type);
            AccessTools.Field(type, "api").SetValue(renderer, client);
            AccessTools.Field(type, "stopped").SetValue(renderer, true);
            render.Invoke(renderer, new object[] { 0.016f, EnumRenderStage.Opaque });
            check(!(bool)zoom.Invoke(null, null)!, "real callback finalizer clears camera exclusion after early return");
            FlightVisibility.OptionalFramePrefix(client, out previous);
            check((bool)zoom.Invoke(null, null)!, "real optional getter applies flight exclusion inside scope");
            FlightVisibility.OptionalFrameFinalizer(previous);
            check(!(bool)zoom.Invoke(null, null)!, "real optional getter restores original zoom outside scope");
            var failureHook = new Harmony("mk2.offline-render-failure");
            failureHook.Patch(render, postfix: new HarmonyMethod(typeof(RenderChecks), nameof(ThrowRenderFailure)));
            try
            {
                bool failed = false;
                try { render.Invoke(renderer, new object[] { 0.016f, EnumRenderStage.Opaque }); }
                catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { failed = true; }
                check(failed, "renderer errors remain visible to the caller");
                check(!(bool)zoom.Invoke(null, null)!, "real finalizer restores exclusion after renderer exception");
            }
            finally { failureHook.UnpatchAll(failureHook.Id); }
        }
        entity.Controls.Gliding = false;
    }

    private static void ThrowRenderFailure() => throw new InvalidOperationException("Offline render failure test");
}
