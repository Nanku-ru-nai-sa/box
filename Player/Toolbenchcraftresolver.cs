using Godot;
using System.Collections.Generic;

// ToolBenchCraftResolver — bridges ToolBenchPanel's sockets to ToolCrafting.
//
// Family comes from ToolBenchPanel.PrimaryFamily/SecondaryFamily (set via
// the center-diamond family picker, or restored by LoadExistingTool when
// modifying an existing tool). Material for each socket is just whatever
// item's socketed there, matched directly against MaterialStatsDb by its
// ItemId — e.g. socket a "flint" item, "flint" needs to be a MaterialId in
// MaterialStatsDb. No special shaped "head" items needed for this to work.

public static class ToolBenchCraftResolver
{
    public class ResolveResult
    {
        public bool Success;
        public string FailReason;
        public string ItemId;
        public int Durability;
    }

    public static ResolveResult Resolve(ToolBenchPanel panel)
    {
        if (panel.PrimaryFamily == null)
            return Fail("Tap the center diamond to pick a tool type.");

        var filled = panel.GetFilledSockets(); // PartSlot -> InventorySlot

        if (!filled.ContainsKey(PartSlot.HeadA))
            return Fail("Needs a Head A.");
        if (!filled.ContainsKey(PartSlot.Handle))
            return Fail("Needs a Handle.");

        var materialsBySlot = new Dictionary<PartSlot, string>();

        foreach (var kvp in filled)
        {
            PartSlot slot = kvp.Key;
            InventorySlot invSlot = kvp.Value;
            string materialId = invSlot.ItemId; // the socketed item's own id IS the material id

            if (MaterialStatsDb.Get(materialId) == null)
                return Fail($"{ToolBenchPanel.SocketLabels[SlotIndex(slot)]}: '{materialId}' isn't set up as a crafting material yet.");

            ToolFamily familyForSlot = (slot == PartSlot.HeadB && panel.SecondaryFamily.HasValue)
                ? panel.SecondaryFamily.Value
                : panel.PrimaryFamily.Value;

            int required = ToolCraftingRules.GetRequiredQuantity(familyForSlot, slot);
            if (invSlot.Count < required)
                return Fail($"{ToolBenchPanel.SocketLabels[SlotIndex(slot)]} needs {required}, only have {invSlot.Count}.");

            materialsBySlot[slot] = materialId;
        }

        var (itemId, durability) = ToolCrafting.CraftTool(panel.PrimaryFamily.Value, panel.SecondaryFamily, materialsBySlot);
        durability += panel.DurabilityCarryOver; // bonus from a tool loaded in for modification

        return new ResolveResult
        {
            Success    = true,
            ItemId     = itemId,
            Durability = durability
        };
    }

    // Consumes the exact required quantities from the panel's sockets after
    // a successful craft. Call this AFTER Resolve() succeeds and you've
    // added the crafted tool to the inventory.
    public static void ConsumeIngredients(ToolBenchPanel panel)
    {
        var filled = panel.GetFilledSockets();
        foreach (var kvp in filled)
        {
            PartSlot slot = kvp.Key;
            ToolFamily familyForSlot = (slot == PartSlot.HeadB && panel.SecondaryFamily.HasValue)
                ? panel.SecondaryFamily.Value
                : panel.PrimaryFamily.Value;

            int required = ToolCraftingRules.GetRequiredQuantity(familyForSlot, slot);
            kvp.Value.Count -= required;
            if (kvp.Value.Count <= 0) kvp.Value.Clear();
        }
    }

    private static ResolveResult Fail(string reason) => new ResolveResult { Success = false, FailReason = reason };

    private static int SlotIndex(PartSlot slot) => slot switch
    {
        PartSlot.HeadA   => 0,
        PartSlot.HeadB   => 1,
        PartSlot.Handle  => 2,
        PartSlot.Binding => 3,
        _ => 0
    };
}