// NEW FILE
// Put this in Scripts/Tools/ToolCrafting.cs
//
// This is the actual "craft a tool" function. Call ToolCrafting.CraftTool(...)
// from your Crafter/Tool Bench UI when the player confirms a craft.
// It builds a unique ItemId from the recipe, registers the tool as an
// ItemResource the first time that exact recipe is crafted (reused after
// that - crafting the same recipe twice does not make two different items),
// and returns the ItemId + durability so you can add it to the inventory.

using Godot;
using System.Collections.Generic;
using System.Linq;

public static class ToolCrafting
{
    // Builds a stable id like "tool_pickaxe_flint_stick" from the recipe.
    public static string BuildToolItemId(
        ToolFamily primaryFamily,
        ToolFamily? secondaryFamily,
        Dictionary<PartSlot, string> materialsBySlot)
    {
        var parts = new List<string> { "tool", primaryFamily.ToString().ToLower() };

        if (secondaryFamily.HasValue)
            parts.Add(secondaryFamily.Value.ToString().ToLower());

        // Fixed slot order so the same recipe always produces the same id
        // regardless of what order the player dropped materials in.
        foreach (var slot in new[] { PartSlot.HeadA, PartSlot.HeadB, PartSlot.Handle, PartSlot.Binding })
        {
            if (materialsBySlot.TryGetValue(slot, out var materialId))
                parts.Add(materialId.ToLower());
        }

        return string.Join("_", parts);
    }

    // Builds a display name like "Flint Pickaxe". You can replace this
    // later with something fancier (e.g. naming after the rarest material).
    public static string BuildToolDisplayName(
        ToolFamily primaryFamily,
        Dictionary<PartSlot, string> materialsBySlot)
    {
        string headMaterial = materialsBySlot.TryGetValue(PartSlot.HeadA, out var m) ? m : "";
        string headCapitalized = string.IsNullOrEmpty(headMaterial)
            ? ""
            : char.ToUpper(headMaterial[0]) + headMaterial.Substring(1);

        return $"{headCapitalized} {primaryFamily}".Trim();
    }

    // Registers (if needed) and returns the ItemId + calculated durability
    // for the given recipe. Does NOT touch the player's inventory - do that
    // separately with Inventory.AddCraftedTool(itemId, durability).
    public static (string itemId, int durability) CraftTool(
        ToolFamily primaryFamily,
        ToolFamily? secondaryFamily,
        Dictionary<PartSlot, string> materialsBySlot)
    {
        string itemId = BuildToolItemId(primaryFamily, secondaryFamily, materialsBySlot);
        int durability = ToolCraftingRules.CalculateDurability(materialsBySlot, primaryFamily, secondaryFamily);

        if (!ItemRegistry.Instance.ItemExists(itemId))
        {
            var item = new ItemResource
            {
                ItemId = itemId,
                DisplayName = BuildToolDisplayName(primaryFamily, materialsBySlot),
                GridWidth = 1,
                GridHeight = 1,
                IsStackable = false,
                IsEquippable = true,
                EquipSlot = "hand",
                ToolType = primaryFamily.ToString(),
                HasDurability = true,
                MaxDurability = durability,
                Tags = BuildRecipeTags(primaryFamily, secondaryFamily, materialsBySlot),
                // Icon is left null for now - set once the extrusion/compositor
                // pipeline for tool models exists.
            };

            ItemRegistry.Instance.RegisterRuntime(item);
        }

        return (itemId, durability);
    }

    // Encodes the recipe onto the item as Tags, e.g.
    // ["recipe:primary:pickaxe", "recipe:heada:flint", "recipe:handle:stick"].
    // This is how ToolBenchPanel.LoadExistingTool() disassembles an already-
    // crafted tool back into its parts when you drop it on the center diamond.
    private static string[] BuildRecipeTags(
        ToolFamily primaryFamily,
        ToolFamily? secondaryFamily,
        Dictionary<PartSlot, string> materialsBySlot)
    {
        var tags = new List<string> { $"recipe:primary:{primaryFamily}".ToLower() };

        if (secondaryFamily.HasValue)
            tags.Add($"recipe:secondary:{secondaryFamily.Value}".ToLower());

        foreach (var kvp in materialsBySlot)
            tags.Add($"recipe:{kvp.Key}:{kvp.Value}".ToLower());

        return tags.ToArray();
    }

    // Reverses BuildRecipeTags — reads a crafted tool's Tags back out into
    // the family + materials it was built from. Returns false if the item
    // has no recipe tags (e.g. it isn't a crafted tool at all).
    public static bool TryGetRecipe(
        ItemResource item,
        out ToolFamily primaryFamily,
        out ToolFamily? secondaryFamily,
        out Dictionary<PartSlot, string> materialsBySlot)
    {
        primaryFamily = default;
        secondaryFamily = null;
        materialsBySlot = new Dictionary<PartSlot, string>();

        if (item?.Tags == null) return false;

        bool foundPrimary = false;

        foreach (var tag in item.Tags)
        {
            if (tag.StartsWith("recipe:primary:"))
            {
                if (System.Enum.TryParse<ToolFamily>(tag.Substring("recipe:primary:".Length), true, out var f))
                {
                    primaryFamily = f;
                    foundPrimary = true;
                }
            }
            else if (tag.StartsWith("recipe:secondary:"))
            {
                if (System.Enum.TryParse<ToolFamily>(tag.Substring("recipe:secondary:".Length), true, out var f))
                    secondaryFamily = f;
            }
            else if (tag.StartsWith("recipe:"))
            {
                string rest = tag.Substring("recipe:".Length); // e.g. "heada:flint"
                int sep = rest.IndexOf(':');
                if (sep > 0)
                {
                    string slotPart = rest.Substring(0, sep);
                    string materialPart = rest.Substring(sep + 1);
                    if (System.Enum.TryParse<PartSlot>(slotPart, true, out var slot))
                        materialsBySlot[slot] = materialPart;
                }
            }
        }

        return foundPrimary;
    }
}