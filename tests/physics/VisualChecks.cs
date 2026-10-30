using System.Reflection;
using System.Runtime.CompilerServices;
using ElectricGliderMk2;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

internal static class VisualChecks
{
    public static void Run(EntityPlayer entity, CountingInventory bags, Action<bool, string> check, string game, bool optionalRenderer)
    {
        var logger = Proxy.Make<ILogger>((m, a) => {
            if (m.Name is "Error" or "Warning") throw new Exception("Shape validation: " + string.Join(" ", a!));
            return Proxy.Default(m.ReturnType);
        });
        string shapes = Path.Combine(AppContext.BaseDirectory, "assets/electricglidermk2/shapes/entity/mk2");
        var files = Directory.GetFiles(shapes, "glider-*.json");
        check(files.Length == 4, "four custom deployment shapes packaged");
        foreach (string file in files)
        {
            var shape = JsonUtil.FromString<Shape>(File.ReadAllText(file))!;
            var elements = shape.CollectAndResolveReferences(logger, file);
            check(elements.Count > 60, "custom shell, pods, hinges and wing ribs loaded");
            check(shape.Elements.Single().StepParentName == "UpperTorso", "native glider attachment joint retained");
            check(elements.Values.Where(e => e.FacesResolved!.Any(f => f != null)).All(e =>
                Enumerable.Range(0, 3).All(i => e.To![i] > e.From![i])), "all visible panels have physical thickness");
            check(elements.Values.SelectMany(e => e.FacesResolved!).Where(f => f != null).All(f =>
                shape.Textures.ContainsKey(f.Texture) && f.Uv.All(float.IsFinite)), "all faces resolve finite UVs and textures");
            foreach (var texture in shape.Textures.Values.Where(t => t.Domain == "game"))
                check(Directory.GetDirectories(Path.Combine(game, "assets")).Any(d =>
                    File.Exists(Path.Combine(d, "textures", texture.Path + ".png"))), "native metal/vent texture exists: " + texture);
            var seraph = JsonUtil.FromString<Shape>(File.ReadAllText(Path.Combine(game, "assets/game/shapes/entity/humanoid/seraph.json")))!;
            // Stock seraph has duplicate attachment-point warnings even without
            // an addon. Only our shape and attachment operations use strict logging.
            seraph.ResolveReferences(Proxy.Make<ILogger>((m, a) => Proxy.Default(m.ReturnType)), "seraph");
            check(seraph.StepParentShape(shape, "mk2-test-", "mk2", "seraph", logger, (_, _) => { }),
                "native step-parenting accepts " + Path.GetFileName(file));
        }
        ItemStack original = bags[0].Itemstack!;
        entity.Controls.Gliding = true; entity.Controls.Sneak = true;
        entity.LightHsv = null;
        check(entity.LightHsv.SequenceEqual(new byte[] { 4, 6, 11 }), "actual patched player getter emits warm thrust light");
        entity.Controls.Sneak = false;
        check(entity.LightHsv == null, "boost release immediately removes added light");
        entity.Controls.Gliding = false; entity.WatchedAttributes.SetBool("mk2VerticalLift", true);
        check(entity.LightHsv?[2] == ThrustLight.Level, "folded powered lift emits light");
        original.Attributes.SetInt("durability", 0);
        check(entity.LightHsv == null, "empty battery removes thrust light");
        original.Attributes.SetInt("durability", 4000);
        entity.WatchedAttributes.SetBool("mk2VerticalLift", false);
        entity.Controls.Gliding = true; entity.Controls.Sneak = true;
        entity.LightHsv = new byte[] { 20, 5, 15 };
        check(entity.LightHsv.SequenceEqual(new byte[] { 20, 5, 15 }), "stronger existing light remains unchanged");
        entity.Controls.Sneak = false;
        check(entity.LightHsv.SequenceEqual(new byte[] { 20, 5, 15 }), "other light persists after boost release");
        entity.LightHsv = null;
        entity.Controls.Sneak = true; entity.Alive = false;
        check(entity.LightHsv == null, "dead player emits no thrust light");
        entity.Alive = true; entity.FeetInLiquid = true;
        check(entity.LightHsv == null, "submerged thrust emits no added light");
        entity.FeetInLiquid = false;
        bags[0].Itemstack = new ItemStack(new Item { Code = new AssetLocation("electricalprogressiveequipment:glider") });
        check(entity.LightHsv == null, "original glider lighting unaffected");
        bags[0].Itemstack = null;
        check(entity.LightHsv == null, "unequipping immediately removes light");
        bags[0].Itemstack = original;
        if (optionalRenderer)
        {
            // Exercise VSPT's managed light discovery only, not Fill/native rendering.
            Type lights = AccessTools.TypeByName("VsRtx.Scene.EntityLights")!;
            var api = Proxy.Make<ICoreClientAPI>((m, a) => Proxy.Default(m.ReturnType));
            object instance = Activator.CreateInstance(lights, api)!;
            MethodInfo own = AccessTools.Method(lights, "OwnLight");
            var result = (ITuple?)own.Invoke(instance, new object[] { entity });
            check(result != null && ((byte[])result[0]!)[0] == 4 && (float)result[1]! > 0,
                "installed VSPT discovers orange Mk II entity emission");
            entity.Controls.Sneak = false;
            check(own.Invoke(instance, new object[] { entity }) == null, "VSPT light discovery clears when thrust ends");
        }
        entity.Controls.Gliding = false; entity.Controls.Sneak = false;
    }
}
