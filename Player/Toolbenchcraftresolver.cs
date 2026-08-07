using Godot;
using System.Collections.Generic;
using System.Linq;

// ToolBenchCraftResolver — bridges ToolBenchPanel's sockets to ToolCrafting.
//
// IMPORTANT: this only works once your shaped part items exist as
// ItemResources with the right Tags (see the convention documented at the
// top of ToolBenchPanel.cs). You don't have those yet - creating them
// (e.g. "flint_pickaxe_head" with Tags = ["slot:head","family:pickaxe","material:flint"])
// is the next piece of work. Until then, Resolve() will just report
// "Missing family tag" or similar, which is expected.

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
        var filled = panel.GetFilledSockets(); // PartSlot -> InventorySlot

        if (!filled.ContainsKey(PartSlot.HeadA))
            return Fail("Needs at least a Head A and a Handle.");
        if (!filled.ContainsKey(PartSlot.Handle))
            return Fail("Needs a Handle.");

        // Family comes from HeadA's "family:<x>" tag.
        ToolFamily? primaryFamily = ReadFamilyTag(filled[PartSlot.HeadA].ItemId);
        if (primaryFamily == null)
            return Fail("Head A item has no family tag - not shaped yet.");

        ToolFamily? secondaryFamily = null;
        if (filled.TryGetValue(PartSlot.HeadB, out var headB))
        {
            secondaryFamily = ReadFamilyTag(headB.ItemId);
            if (secondaryFamily == null)
                return Fail("Head B item has no family tag - not shaped yet.");
        }

        // Build materialsBySlot from "material:<x>" tags, and validate
        // quantity against ToolCraftingRules.
        var materialsBySlot = new Dictionary<PartSlot, string>();
        foreach (var kvp in filled)
        {
            PartSlot slot = kvp.Key;
            InventorySlot invSlot = kvp.Value;

            string materialId = ReadMaterialTag(invSlot.ItemId);
            if (materialId == null)
                return Fail($"{ToolBenchPanel.SocketLabels[SlotIndex(slot)]} item has no material tag.");

            ToolFamily familyForSlot = (slot == PartSlot.HeadB && secondaryFamily.HasValue)
                ? secondaryFamily.Value
                : primaryFamily.Value;

            int required = ToolCraftingRules.GetRequiredQuantity(familyForSlot, slot);
            if (invSlot.Count < required)
                return Fail($"{ToolBenchPanel.SocketLabels[SlotIndex(slot)]} needs {required}, only have {invSlot.Count}.");

            materialsBySlot[slot] = materialId;
        }

        var (itemId, durability) = ToolCrafting.CraftTool(primaryFamily.Value, secondaryFamily, materialsBySlot);

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
    public static void ConsumeIngredients(ToolBenchPanel panel, ToolFamily primaryFamily, ToolFamily? secondaryFamily)
    {
        var filled = panel.GetFilledSockets();
        foreach (var kvp in filled)
        {
            PartSlot slot = kvp.Key;
            ToolFamily familyForSlot = (slot == PartSlot.HeadB && secondaryFamily.HasValue)
                ? secondaryFamily.Value
                : primaryFamily;

            int required = ToolCraftingRules.GetRequiredQuantity(familyForSlot, slot);
            kvp.Value.Count -= required;
            if (kvp.Value.Count <= 0) kvp.Value.Clear();
        }
        panel.RefreshAllVisuals();
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

    private static ToolFamily? ReadFamilyTag(string itemId)
    {
        var item = ItemRegistry.Instance.GetItem(itemId);
        if (item?.Tags == null) return null;

        foreach (var tag in item.Tags)
        {
            if (tag.StartsWith("family:"))
            {
                string val = tag.Substring("family:".Length);
                if (System.Enum.TryParse<ToolFamily>(val, true, out var fam))
                    return fam;
            }
        }
        return null;
    }

    private static string ReadMaterialTag(string itemId)
    {
        var item = ItemRegistry.Instance.GetItem(itemId);
        if (item?.Tags == null) return null;

        foreach (var tag in item.Tags)
        {
            if (tag.StartsWith("material:"))
                return tag.Substring("material:".Length);
        }
        return null;
    }
}