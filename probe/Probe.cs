using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using System.Text.Json;
using ElectricGliderMk2;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

public partial class Probe : ModSystem
{
    private readonly List<string> passed = new();
    public override double ExecuteOrder() => 100;
    public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI api)
        => api.Event.ServerRunPhase(EnumServerRunPhase.RunGame, () => RunTests(api));

    private void RunTests(ICoreAPI api)
    {
        string? report = Environment.GetEnvironmentVariable("GLIDER_MK2_TEST_REPORT");
        if (report == null) return;
        try
        {
            TestAssets(api);
            File.WriteAllText(report, JsonSerializer.Serialize(new { success = true, passed }, new JsonSerializerOptions { WriteIndented=true }));
            api.Logger.Notification("[Mk2Probe] PASS: " + passed.Count + " game integration checks.");
        }
        catch (Exception error)
        {
            File.WriteAllText(report, JsonSerializer.Serialize(new { success = false, passed, error = error.ToString() }, new JsonSerializerOptions { WriteIndented=true }));
            api.Logger.Error("[Mk2Probe] FAIL: " + error);
        }
        finally
        {
            ((ICoreServerAPI)api).Event.RegisterCallback(_ => ((ICoreServerAPI)api).Server.ShutDown(), 1000);
        }
    }

    private void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        passed.Add(name);
    }

    private void TestAssets(ICoreAPI api)
    {
        Item mk2 = api.World.GetItem(new AssetLocation("electricglidermk2:glider-mk2")) ?? throw new Exception("Mk II asset missing");
        Check(mk2 is ItemMk2Glider, "Mk II registered and loaded");
        Item original = api.World.GetItem(new AssetLocation("electricalprogressiveequipment:static-glider")) ?? throw new Exception("EP glider missing");
        Check(original.Durability == 1000 && original.Attributes["backpack"]["quantitySlots"].AsInt() == 4, "original glider unchanged");
        Check(mk2.Durability == 4000 && mk2.Attributes["backpack"]["quantitySlots"].AsInt() == 6, "fourfold battery and six slots");
        Check(mk2.Attributes["chargable"].AsBool(), "accepted by EP charger's chargeable-item rule");
        Check(mk2 is ItemGlider, "recognized by vanilla gliding even when empty");
        GridRecipe recipe = api.World.GridRecipes.Single(r => r.Output?.ResolvedItemStack?.Collectible == mk2);
        // Use the game's actual handbook-stack generator and the exact predicate
        // used by addCreatedByInfo before it creates the native recipe component.
        ItemStack handbookStack = mk2.GetHandBookStacks(null!).Single();
        Check(recipe.ShowInCreatedBy && recipe.RecipeOutput.ResolvedItemStack!.Satisfies(handbookStack),
            "native handbook Created by selector includes the Mk II crafting grid");
        ItemStack chargedHandbookStack = handbookStack.Clone();
        chargedHandbookStack.Attributes.SetInt("durability", 2000);
        Check(recipe.RecipeOutput.ResolvedItemStack!.Satisfies(chargedHandbookStack),
            "handbook recipe remains visible regardless of glider charge");
        ItemStack previousOutput = recipe.RecipeOutput.ResolvedItemStack!.Clone();
        previousOutput.Attributes.SetInt("durability", 0);
        Check(!previousOutput.Satisfies(handbookStack),
            "previous empty-charge output attribute reproduces missing handbook grid");
        Check(recipe.ResolvedIngredients?.Length == 9, "3x3 upgrade recipe resolves all ingredients");
        ItemSlot[] input = new ItemSlot[9];
        for(int i=0;i<9;i++) input[i] = new DummySlot(recipe.ResolvedIngredients![i]?.ResolvedItemStack?.Clone());
        Check(input[0].StackSize == 4 && input[2].StackSize == 4 && input[3].StackSize == 2 && input[5].StackSize == 2,
            "linen and steel split into correct stack quantities");
        Check(input[1].Itemstack?.Collectible.Code.ToString() == "electricalprogressivebasics:accumulator-tier2-normal"
            && input[6].Itemstack?.Collectible.Code.ToString() == "electricalprogressivebasics:engineshaft"
            && input[8].Itemstack?.Collectible.Code.ToString() == "electricalprogressivebasics:engineshaft", "128V accumulator and two rotors resolved");
        Check(input[4].Itemstack?.Collectible.Code.ToString()=="game:schematic-glider","glider schematic replaces electric glider in center");
        var craftingGrid=new Vintagestory.Common.InventoryCraftingGrid("craftinggrid-mk2-probe",api);
        for(int i=0;i<9;i++)
            if(!input[i].Empty)Check(craftingGrid[i].CanHold(input[i]),"ingredient can be placed into actual crafting slot "+i);

        MethodInfo match = typeof(RecipeBase).GetMethods(BindingFlags.Instance|BindingFlags.NonPublic)
            .Single(m => m.Name == "MatchesAtPosition" && m.GetParameters().Length == 6);
        bool Matches() => (bool)match.Invoke(recipe, new object?[] {0,0,input,3,3,recipe.ResolvedIngredients})!;
        Check(Matches(), "actual game recipe matches agreed layout");
        input[0].Itemstack!.StackSize=3;
        Check(!Matches(), "insufficient linen rejected");
        input[0].Itemstack!.StackSize=4;
        ItemStack battery = input[1].Itemstack!;
        input[1].Itemstack=new ItemStack(api.World.GetBlock(new AssetLocation("electricalprogressivebasics:accumulator-tier1-normal")));
        Check(!Matches(), "32V battery rejected");
        input[1].Itemstack=battery;

        ItemStack schematic = input[4].Itemstack!;
        input[4].Itemstack=new ItemStack(original);
        Check(!Matches(),"old electric-glider ingredient no longer matches");
        input[4].Itemstack=schematic;
        battery.Attributes.SetInt("durability", 1200);
        var output=new DummySlot(new ItemStack(mk2));
        mk2.OnCreatedByCrafting(input,output,recipe);
        Check(output.Itemstack!.Attributes.GetInt("durability") == 1200, "crafting transfers battery charge without creating free energy");
        ITreeAttribute savedSlots=output.Itemstack.Attributes.GetTreeAttribute("backpack").GetTreeAttribute("slots");
        Check(savedSlots.Count == 6 && savedSlots.All(s=>s.Value.GetValue()==null), "newly crafted glider has six empty storage slots");
        battery.Attributes.SetInt("durability",0);
        mk2.OnCreatedByCrafting(input,output,recipe);
        Check(output.Itemstack.Attributes.GetInt("durability") == 0, "empty ingredients cannot create free charge");
        battery.Attributes.SetInt("durability",12800);
        mk2.OnCreatedByCrafting(input,output,recipe);
        Check(output.Itemstack.Attributes.GetInt("durability") == 4000, "charge transfer capped at Mk II capacity");
        var roundtrip=new ItemStack(output.Itemstack.ToBytes()); roundtrip.ResolveBlockOrItem(api.World);
        Check(roundtrip.Attributes.GetTreeAttribute("backpack").GetTreeAttribute("slots").Count==6,
            "upgraded contents and slot count survive save serialization");

        Check(recipe.ConsumeInput(null!,input,3) && input[4].Itemstack?.Collectible.Code.ToString()=="game:schematic-glider"
            && input[4].StackSize==1 && input.Where((s,i)=>i!=4).All(s=>s.Empty),
            "actual crafting consumes materials and retains the reusable schematic");

        TestPhysics((ICoreServerAPI)api, mk2, original);
    }

}

public class TestProxy : DispatchProxy
{
    public System.Func<MethodInfo,object?[]?,object?> Handler=null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args)=>Handler(method!,args);
    public static T Make<T>(System.Func<MethodInfo,object?[]?,object?> handler) where T:class
    {
        // VS 1.22 exposes an internal abstract member on IPlayer. Allow the
        // isolated test proxy assembly to implement that member as the game does.
        Type generator=typeof(DispatchProxy).Assembly.GetType("System.Reflection.DispatchProxyGenerator")!;
        object map=generator.GetField("s_alcProxyAssemblyMap",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
        object?[] lookup={AssemblyLoadContext.GetLoadContext(typeof(TestProxy).Assembly),null};
        if((bool)map.GetType().GetMethod("TryGetValue")!.Invoke(map,lookup)!)
        {
            var builder=(AssemblyBuilder)lookup[1]!.GetType().GetField("_ab",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(lookup[1])!;
            if(!builder.GetCustomAttributesData().Any(a=>a.AttributeType.FullName=="System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute"))
                builder.SetCustomAttribute(new CustomAttributeBuilder(typeof(System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute).GetConstructor(new[]{typeof(string)})!,new object[]{"VintagestoryAPI"}));
        }
        T proxy=Create<T,TestProxy>(); ((TestProxy)(object)proxy).Handler=handler; return proxy;
    }
    public static object? Default(Type type)=>type==typeof(void) ? null : type.IsValueType ? Activator.CreateInstance(type) : null;
}
