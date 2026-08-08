// NEW FILE
// Put this in Scripts/Tools/ToolCraftingRules.cs
//
// This answers "how many of X material does this slot need". Change the
// numbers in SlotQuantity to rebalance recipes (e.g. make pickaxe heads
// cost more flint than axe heads).

using System.Collections.Generic;

public static class ToolCraftingRules
{
    private static readonly Dictionary<(ToolFamily, PartSlot), int> SlotQuantity = new()
    {
        { (ToolFamily.Sword,   PartSlot.HeadA), 2 },
        { (ToolFamily.Pickaxe, PartSlot.HeadA), 3 },
        { (ToolFamily.Axe,     PartSlot.HeadA), 3 },
        { (ToolFamily.Shovel,  PartSlot.HeadA), 1 },
        { (ToolFamily.Hoe,     PartSlot.HeadA), 1 },
        { (ToolFamily.Hammer,  PartSlot.HeadA), 4 },
        // Anything not listed here (Handle, Binding, HeadB) defaults to 1 below.
    };

    public static int GetRequiredQuantity(ToolFamily family, PartSlot slot)
    {
        if (SlotQuantity.TryGetValue((family, slot), out var qty))
            return qty;
        return 1;
    }

    // Adds up durability across every filled slot in the recipe.
    public static int CalculateDurability(
        Dictionary<PartSlot, string> materialsBySlot,
        ToolFamily primaryFamily,
        ToolFamily? secondaryFamily)
    {
        int total = 0;

        foreach (var kvp in materialsBySlot)
        {
            PartSlot slot = kvp.Key;
            string materialId = kvp.Value;

            // A HeadB material belongs to the secondary family, everything else to primary.
            ToolFamily familyForSlot = (slot == PartSlot.HeadB && secondaryFamily.HasValue)
                ? secondaryFamily.Value
                : primaryFamily;

            int qty = GetRequiredQuantity(familyForSlot, slot);

            var stats = MaterialStatsDb.Get(materialId);
            int durabilityPerUnit = stats?.DurabilityPerUnit ?? 0;

            total += qty * durabilityPerUnit;
        }

        return total;
    }
}