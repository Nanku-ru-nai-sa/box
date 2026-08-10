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
using System.Text.Json;
using System.Text.Json.Serialization;

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
            // Head material drives the tool's combat/mining stats for now -
            // simplest reasonable rule until Binding/Handle contribute their
            // own weighted slice (see the original Tetra-style design notes).
            var headMat = materialsBySlot.TryGetValue(PartSlot.HeadA, out var headMatId)
                ? MaterialStatsDb.Get(headMatId)
                : null;

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
                ToolSpeed = headMat?.MiningSpeedMod ?? 1f,
                ToolTier = headMat?.Tier ?? 0,
                AttackDamage = headMat?.AttackDamageMod ?? 1f,
                AttackSpeed = 1f, // placeholder - not derived from anything yet (no weight-class system built)
                MiningPower = headMat?.MiningPower ?? 1,
                CooldownSeconds = headMat?.CooldownSeconds ?? 1f,
                HasDurability = true,
                MaxDurability = durability,
                Tags = BuildRecipeTags(primaryFamily, secondaryFamily, materialsBySlot),
                Icon = BuildToolIcon(primaryFamily, secondaryFamily, materialsBySlot),
            };

            ItemRegistry.Instance.RegisterRuntime(item);
            PersistRecipe(itemId, primaryFamily, secondaryFamily, materialsBySlot);
        }

        return (itemId, durability);
    }

    // =========================================================================
    // PERSISTENCE — crafted tools only exist as ItemResource templates in
    // memory. On restart, ItemRegistry starts empty except for the hardcoded
    // static items, so a saved inventory referencing e.g. "tool_pickaxe_flint_stick"
    // points at nothing and shows no icon. Fix: persist just the RECIPE (tiny -
    // family + materials) here, and replay every recipe at startup via
    // LoadPersistedRecipes(), which deterministically rebuilds the exact
    // same item (same id, same icon, same stats) before anything tries to
    // look it up.
    // =========================================================================

    private const string RecipeSavePath = "user://crafted_tools.json";

    private class RecipeRecord
    {
        [JsonPropertyName("itemId")]    public string ItemId { get; set; } = "";
        [JsonPropertyName("primary")]   public string Primary { get; set; } = "";
        [JsonPropertyName("secondary")] public string Secondary { get; set; } = null;
        [JsonPropertyName("materials")] public Dictionary<string, string> Materials { get; set; } = new();
    }

    private class RecipeFile
    {
        [JsonPropertyName("recipes")] public List<RecipeRecord> Recipes { get; set; } = new();
    }

    private static void PersistRecipe(
        string itemId,
        ToolFamily primaryFamily,
        ToolFamily? secondaryFamily,
        Dictionary<PartSlot, string> materialsBySlot)
    {
        RecipeFile data = LoadRecipeFile();

        if (data.Recipes.Any(r => r.ItemId == itemId)) return; // already persisted

        var record = new RecipeRecord
        {
            ItemId    = itemId,
            Primary   = primaryFamily.ToString().ToLower(),
            Secondary = secondaryFamily?.ToString().ToLower(),
        };
        foreach (var kvp in materialsBySlot)
            record.Materials[kvp.Key.ToString().ToLower()] = kvp.Value;

        data.Recipes.Add(record);
        SaveRecipeFile(data);
    }

    private static RecipeFile LoadRecipeFile()
    {
        if (!Godot.FileAccess.FileExists(RecipeSavePath)) return new RecipeFile();

        using var f = Godot.FileAccess.Open(RecipeSavePath, Godot.FileAccess.ModeFlags.Read);
        string text = f.GetAsText();

        try { return JsonSerializer.Deserialize<RecipeFile>(text) ?? new RecipeFile(); }
        catch (System.Exception e)
        {
            GD.PrintErr($"ToolCrafting: failed to parse {RecipeSavePath}: {e.Message}");
            return new RecipeFile();
        }
    }

    private static void SaveRecipeFile(RecipeFile data)
    {
        using var f = Godot.FileAccess.Open(RecipeSavePath, Godot.FileAccess.ModeFlags.Write);
        f.StoreString(JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    }

    // Call this once at startup (see ItemRegistry._Ready()) BEFORE anything
    // tries to load a saved inventory that might reference a crafted tool.
    public static void LoadPersistedRecipes()
    {
        var data = LoadRecipeFile();
        if (data.Recipes.Count == 0) return;

        int rebuilt = 0;
        foreach (var r in data.Recipes)
        {
            if (!System.Enum.TryParse<ToolFamily>(r.Primary, true, out var primary)) continue;

            ToolFamily? secondary = null;
            if (!string.IsNullOrEmpty(r.Secondary) && System.Enum.TryParse<ToolFamily>(r.Secondary, true, out var sec))
                secondary = sec;

            var materialsBySlot = new Dictionary<PartSlot, string>();
            foreach (var kvp in r.Materials)
                if (System.Enum.TryParse<PartSlot>(kvp.Key, true, out var slot))
                    materialsBySlot[slot] = kvp.Value;

            CraftTool(primary, secondary, materialsBySlot); // idempotent - just re-registers
            rebuilt++;
        }

        GD.Print($"ToolCrafting: rebuilt {rebuilt} persisted tool recipes.");
    }

    // Composites a tool's icon from its part shape textures:
    //   Head:   res://Assets/Textures/Items/tool/{family}/{material}_head.png
    //   Handle: res://Assets/Textures/Items/tool/stick/{material}.png
    // Draw order is handle first (sits behind), then head(s) on top.
    // Returns null if none of the expected files exist yet (safe to leave
    // unset - the UI just shows a blank icon slot until the art's in).
    private static Texture2D BuildToolIcon(
        ToolFamily primaryFamily,
        ToolFamily? secondaryFamily,
        Dictionary<PartSlot, string> materialsBySlot)
    {
        Image canvas = null;

        void Blend(string path)
        {
            if (!ResourceLoader.Exists(path)) return;
            var partTex = ResourceLoader.Load<Texture2D>(path);
            var partImg = partTex.GetImage();
            if (partImg == null) return;

            if (canvas == null)
                canvas = Image.CreateEmpty(partImg.GetWidth(), partImg.GetHeight(), false, Image.Format.Rgba8);

            canvas.BlendRect(partImg, new Rect2I(Vector2I.Zero, partImg.GetSize()), Vector2I.Zero);
        }

        if (materialsBySlot.TryGetValue(PartSlot.Handle, out var handleMat))
            Blend($"res://Assets/Textures/Items/tool/stick/{handleMat}.png");

        if (materialsBySlot.TryGetValue(PartSlot.HeadA, out var headAMat))
            Blend($"res://Assets/Textures/Items/tool/{primaryFamily.ToString().ToLower()}/{headAMat}_head.png");

        if (secondaryFamily.HasValue && materialsBySlot.TryGetValue(PartSlot.HeadB, out var headBMat))
            Blend($"res://Assets/Textures/Items/tool/{secondaryFamily.Value.ToString().ToLower()}/{headBMat}_head.png");

        if (canvas != null)
            return ImageTexture.CreateFromImage(canvas);

        // Nothing found at all (no head/handle art yet for this recipe) -
        // fall back to the same "unknown" chalk texture the center diamond
        // uses when no tool type is selected, rather than a blank icon.
        const string unknownPath = "res://Assets/Textures/Items/tool/chalk/unknown.png";
        return ResourceLoader.Exists(unknownPath) ? ResourceLoader.Load<Texture2D>(unknownPath) : null;
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