using Vintagestory.API.Common;

namespace ElectricGliderMk2;

/// <summary>Use native entity lighting so raster and entity-aware renderers agree.</summary>
public static class ThrustLight
{
    public const byte Hue = 4;
    public const byte Saturation = 6;
    public const byte Level = 11;

    public static bool IsActive(EntityPlayer player)
    {
        if (!player.Alive || player.FeetInLiquid || player.Swimming || player.MountedOn != null
            || player.Controls.IsClimbing || player.Player?.WorldData?.FreeMove == true
            || player.Player?.WorldData?.CurrentGameMode == EnumGameMode.Spectator) return false;
        ItemStack? stack = Mk2System.FindGlider(player)?.Itemstack;
        if (stack?.Collectible is not ItemMk2Glider || stack.Attributes.GetInt("durability", 0) <= 0) return false;
        return (player.Controls.Gliding && player.Controls.Sneak) || Mk2System.UsesLiftPose(player);
    }

    public static void LightPostfix(EntityPlayer __instance, ref byte[]? __result)
    {
        if (!IsActive(__instance)) return;
        // Preserve stronger held lamps, fire and other mods' light. Never write
        // entity light state, inventory, blocks, or a shared collectible value.
        if (__result is { Length: >= 3 } && __result[2] >= Level) return;
        __result = new byte[] { Hue, Saturation, Level };
    }
}
