using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace ElectricGliderMk2;

// A wearable glider bag recognized by vanilla deployment and EP's boost effects.
public class ItemMk2Glider : ItemGlider
{
    public override void OnCreatedByCrafting(ItemSlot[] inputs, ItemSlot outputSlot, IRecipeBase recipe)
    {
        base.OnCreatedByCrafting(inputs, outputSlot, recipe);
        ItemStack? output = outputSlot.Itemstack;
        if (output == null) return;
        ItemStack? battery = inputs.Select(s => s.Itemstack).FirstOrDefault(s => s?.Collectible.Code.ToString() == "electricalprogressivebasics:accumulator-tier2-normal");

        // The schematic is a reusable blueprint. Only the battery contributes
        // charge; new gliders do not inherit attributes or contents from it.
        int charge = StoredCharge(battery);
        output.Attributes.SetInt("durability", Math.Min(output.Collectible.GetMaxDurability(output), charge));
        output.Attributes.RemoveAttribute("mk2FractionalUse");

        ITreeAttribute bag = new TreeAttribute();
        ITreeAttribute slots = new TreeAttribute();
        for (int i = 0; i < 6; i++)
            if (!slots.HasAttribute("slot-" + i)) slots["slot-" + i] = new ItemstackAttribute(null);
        bag["slots"] = slots;
        output.Attributes["backpack"] = bag;
    }

    private static int StoredCharge(ItemStack? stack)
        => stack == null ? 0 : Math.Clamp(stack.Attributes.GetInt("durability", 0), 0, stack.Collectible.GetMaxDurability(stack));

    public override void GetHeldItemInfo(ItemSlot slot, StringBuilder description, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(slot, description, world, withDebugInfo);
        ItemStack? stack = slot.Itemstack;
        if (stack == null) return;
        description.AppendLine(Lang.Get("electricglidermk2:charge", stack.Attributes.GetInt("durability", 0) * 20, GetMaxDurability(stack) * 20));
        description.AppendLine(Lang.Get("electricglidermk2:controls"));
    }
}
