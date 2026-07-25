using Godot;
using System;
using System.Collections.Generic;

// BlockDropManager — autoload singleton (add to Project > Autoloads as "BlockDropManager")
// Handles CUSTOM block drop rules — blocks that have a chance of dropping
// something other than themselves. Most blocks need no entry at all here;
// they just drop themselves as normal (see Player.GetDrop).
//
// Rules are loaded from individual JSON files in res://Core/BlockDrops/ —
// one file per block, same pattern as RecipeManager's res://Core/Recipes/.
// Edit or add files anytime; they're picked up automatically next time the
// game starts (no code changes needed).
//
// Example file, res://Core/BlockDrops/gravel.json:
// {
//   "block": "gravel",
//   "drops": [
//     { "item": "flint", "chance": 0.10 }
//   ]
// }
// Meaning: breaking gravel has a 10% chance to drop "flint" instead.
// The other 90% of the time (anything not covered by an entry) falls
// through to gravel's normal drop — you don't need to list gravel itself.
//
// Multiple entries are allowed and are rolled in order, e.g.:
// "drops": [
//   { "item": "diamond", "chance": 0.02 },
//   { "item": "raw_iron", "chance": 0.20, "count": 2 }
// ]
// — 2% diamond, else 20% chance of 2x raw_iron, else the block's normal drop.

public partial class BlockDropManager : Node
{
    public static BlockDropManager Instance { get; private set; }

    public class DropOption
    {
        public string ItemId { get; set; }
        public int    Count  { get; set; } = 1;
        public float  Chance { get; set; } // 0..1
    }

    public class BlockDropRule
    {
        public string          BlockId { get; set; }
        public List<DropOption> Options { get; set; } = new();
    }

    private Dictionary<string, BlockDropRule> _rules = new();
    private const string DropFolder = "res://Core/BlockDrops";

    public override void _Ready()
    {
        Instance = this;
        RegisterAllDrops();
    }

    // ── Registry — loads every .json file in DropFolder ─────────────────────

    private void RegisterAllDrops()
    {
        _rules.Clear();

        using var dir = DirAccess.Open(DropFolder);
        if (dir == null)
        {
            GD.PrintErr($"BlockDropManager: folder not found — {DropFolder}. Create it and add block drop .json files.");
            return;
        }

        dir.ListDirBegin();
        string fileName = dir.GetNext();
        while (fileName != "")
        {
            if (!dir.CurrentIsDir() && fileName.EndsWith(".json"))
                LoadDropFile($"{DropFolder}/{fileName}");
            fileName = dir.GetNext();
        }
        dir.ListDirEnd();

        GD.Print($"BlockDropManager: loaded {_rules.Count} custom block drop rules from {DropFolder}");
    }

    private void LoadDropFile(string path)
    {
        if (!FileAccess.FileExists(path)) return;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        string text = file.GetAsText();

        Godot.Collections.Dictionary data;
        try
        {
            data = Json.ParseString(text).AsGodotDictionary();
        }
        catch (Exception e)
        {
            GD.PrintErr($"BlockDropManager: bad JSON in {path} — {e.Message}");
            return;
        }

        try
        {
            var rule = ParseRule(data, path);
            if (rule != null) _rules[rule.BlockId] = rule;
        }
        catch (Exception e)
        {
            GD.PrintErr($"BlockDropManager: failed to parse {path} — {e.Message}");
        }
    }

    private BlockDropRule ParseRule(Godot.Collections.Dictionary data, string path)
    {
        if (!data.ContainsKey("block") || !data.ContainsKey("drops"))
        {
            GD.PrintErr($"BlockDropManager: '{path}' is missing 'block' or 'drops'.");
            return null;
        }

        var rule = new BlockDropRule { BlockId = (string)data["block"] };

        foreach (var entryVariant in data["drops"].AsGodotArray())
        {
            var entry = entryVariant.AsGodotDictionary();
            if (!entry.ContainsKey("item") || !entry.ContainsKey("chance"))
            {
                GD.PrintErr($"BlockDropManager: a drop entry in '{path}' is missing 'item' or 'chance' — skipped.");
                continue;
            }

            rule.Options.Add(new DropOption
            {
                ItemId = (string)entry["item"],
                Chance = (float)(double)entry["chance"],
                Count  = entry.ContainsKey("count") ? (int)entry["count"] : 1
            });
        }

        return rule;
    }

    // ── Rolling ──────────────────────────────────────────────────────────────

    // Rolls a custom drop for blockId, if a rule exists for it.
    // Returns true and fills in itemId/count if one of the rule's options hit.
    // Returns false if the block has no custom rule at all, OR it has a rule
    // but the roll didn't land on any of its options — in both cases, the
    // caller should fall through to its own normal default drop for the block.
    public bool TryRollDrop(string blockId, RandomNumberGenerator rng, out string itemId, out int count)
    {
        itemId = null;
        count  = 0;

        if (!_rules.TryGetValue(blockId, out var rule)) return false;

        float roll       = rng.Randf();
        float cumulative = 0f;
        foreach (var option in rule.Options)
        {
            cumulative += option.Chance;
            if (roll < cumulative)
            {
                itemId = option.ItemId;
                count  = option.Count;
                return true;
            }
        }

        return false; // none of the custom options hit — normal drop applies
    }

    public bool HasCustomDrop(string blockId) => _rules.ContainsKey(blockId);
}