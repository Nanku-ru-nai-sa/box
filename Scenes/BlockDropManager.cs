using Godot;
using System;
using System.Collections.Generic;

// BlockDropManager — autoload singleton.
//
// Loads custom drop rules from:
// res://Core/BlockDrops/
//
// Existing format:
//
// {
//   "block": "gravel",
//   "drops": [
//     { "item": "flint", "chance": 0.12 }
//   ]
// }
//
// Fixed quantity:
//
// {
//   "item": "raw_iron",
//   "chance": 0.20,
//   "count": 2
// }
//
// Quantity range:
//
// {
//   "item": "sun_shard",
//   "chance": 0.75,
//   "min": 3,
//   "max": 12
// }
//
// min/max are INCLUSIVE.

public partial class BlockDropManager : Node
{
    public static BlockDropManager Instance { get; private set; }


    // ============================================================
    // DROP OPTION
    // ============================================================

    public class DropOption
    {
        public string ItemId { get; set; }

        // Existing fixed-count support.
        public int Count { get; set; } = 1;

        // Quantity range.
        public int MinCount { get; set; } = 1;
        public int MaxCount { get; set; } = 1;

        // Chance from 0.0 to 1.0.
        public float Chance { get; set; }
    }


    // ============================================================
    // DROP RULE
    // ============================================================

    public class BlockDropRule
    {
        public string BlockId { get; set; }

        public List<DropOption> Options { get; set; } = new();
    }


    // ============================================================
    // STORAGE
    // ============================================================

    private readonly Dictionary<string, BlockDropRule> _rules = new();

    private const string DropFolder =
        "res://Core/BlockDrops";


    // ============================================================
    // READY
    // ============================================================

    public override void _Ready()
    {
        Instance = this;

        RegisterAllDrops();
    }


    // ============================================================
    // LOAD ALL DROP FILES
    // ============================================================

    private void RegisterAllDrops()
    {
        _rules.Clear();

        using var dir =
            DirAccess.Open(DropFolder);

        if (dir == null)
        {
            GD.PrintErr(
                $"BlockDropManager: folder not found — " +
                $"{DropFolder}"
            );

            return;
        }

        dir.ListDirBegin();

        string fileName =
            dir.GetNext();

        while (fileName != "")
        {
            if (!dir.CurrentIsDir() &&
                fileName.EndsWith(".json"))
            {
                LoadDropFile(
                    $"{DropFolder}/{fileName}"
                );
            }

            fileName =
                dir.GetNext();
        }

        dir.ListDirEnd();

        GD.Print(
            $"BlockDropManager: loaded " +
            $"{_rules.Count} custom block drop rules " +
            $"from {DropFolder}"
        );
    }


    // ============================================================
    // LOAD ONE JSON FILE
    // ============================================================

    private void LoadDropFile(string path)
    {
        if (!FileAccess.FileExists(path))
            return;

        using var file =
            FileAccess.Open(
                path,
                FileAccess.ModeFlags.Read
            );

        string text =
            file.GetAsText();

        Godot.Collections.Dictionary data;

        try
        {
            data =
                Json.ParseString(text)
                    .AsGodotDictionary();
        }
        catch (Exception e)
        {
            GD.PrintErr(
                $"BlockDropManager: bad JSON in " +
                $"{path} — {e.Message}"
            );

            return;
        }

        try
        {
            var rule =
                ParseRule(
                    data,
                    path
                );

            if (rule != null)
            {
                _rules[rule.BlockId] =
                    rule;
            }
        }
        catch (Exception e)
        {
            GD.PrintErr(
                $"BlockDropManager: failed to parse " +
                $"{path} — {e.Message}"
            );
        }
    }


    // ============================================================
    // PARSE RULE
    // ============================================================

    private BlockDropRule ParseRule(
        Godot.Collections.Dictionary data,
        string path)
    {
        if (!data.ContainsKey("block") ||
            !data.ContainsKey("drops"))
        {
            GD.PrintErr(
                $"BlockDropManager: '{path}' is missing " +
                $"'block' or 'drops'."
            );

            return null;
        }

        string blockId =
            data["block"].AsString();

        if (string.IsNullOrEmpty(blockId))
        {
            GD.PrintErr(
                $"BlockDropManager: '{path}' has an empty block id."
            );

            return null;
        }

        var rule =
            new BlockDropRule
            {
                BlockId = blockId
            };


        foreach (
            var entryVariant
            in data["drops"].AsGodotArray())
        {
            var entry =
                entryVariant.AsGodotDictionary();


            // ----------------------------------------------------
            // Required fields
            // ----------------------------------------------------

            if (!entry.ContainsKey("item") ||
                !entry.ContainsKey("chance"))
            {
                GD.PrintErr(
                    $"BlockDropManager: a drop entry in " +
                    $"'{path}' is missing 'item' or 'chance' " +
                    $"— skipped."
                );

                continue;
            }


            // ----------------------------------------------------
            // ITEM
            // ----------------------------------------------------

            string itemId =
                entry["item"].AsString();

            if (string.IsNullOrEmpty(itemId))
            {
                GD.PrintErr(
                    $"BlockDropManager: empty item id in " +
                    $"'{path}' — skipped."
                );

                continue;
            }


            // ----------------------------------------------------
            // CHANCE
            // ----------------------------------------------------

            float chance =
                entry["chance"].AsSingle();

            chance =
                Mathf.Clamp(
                    chance,
                    0f,
                    1f
                );


            // ----------------------------------------------------
            // QUANTITY
            // ----------------------------------------------------

            int minCount = 1;
            int maxCount = 1;


            // ----------------------------------------------------
            // NEW min/max FORMAT
            // ----------------------------------------------------

            if (entry.ContainsKey("min") ||
                entry.ContainsKey("max"))
            {
                if (entry.ContainsKey("min"))
                {
                    minCount =
                        entry["min"].AsInt32();
                }

                if (entry.ContainsKey("max"))
                {
                    maxCount =
                        entry["max"].AsInt32();
                }

                minCount =
                    Mathf.Max(
                        1,
                        minCount
                    );

                maxCount =
                    Mathf.Max(
                        minCount,
                        maxCount
                    );
            }


            // ----------------------------------------------------
            // EXISTING count FORMAT
            // ----------------------------------------------------

            else if (entry.ContainsKey("count"))
            {
                int fixedCount =
                    entry["count"].AsInt32();

                fixedCount =
                    Mathf.Max(
                        1,
                        fixedCount
                    );

                minCount =
                    fixedCount;

                maxCount =
                    fixedCount;
            }


            // ----------------------------------------------------
            // ADD OPTION
            // ----------------------------------------------------

            rule.Options.Add(
                new DropOption
                {
                    ItemId =
                        itemId,

                    Chance =
                        chance,

                    Count =
                        minCount,

                    MinCount =
                        minCount,

                    MaxCount =
                        maxCount
                }
            );
        }


        return rule;
    }


    // ============================================================
    // ROLL CUSTOM DROP
    // ============================================================

    public bool TryRollDrop(
        string blockId,
        RandomNumberGenerator rng,
        out string itemId,
        out int count)
    {
        itemId = null;
        count = 0;


        if (!_rules.TryGetValue(
            blockId,
            out var rule))
        {
            return false;
        }


        float roll =
            rng.Randf();

        float cumulative =
            0f;


        foreach (
            var option
            in rule.Options)
        {
            cumulative +=
                option.Chance;


            if (roll < cumulative)
            {
                itemId =
                    option.ItemId;


                // Fixed amount.
                if (option.MinCount ==
                    option.MaxCount)
                {
                    count =
                        option.MinCount;
                }

                // Random amount.
                else
                {
                    count =
                        rng.RandiRange(
                            option.MinCount,
                            option.MaxCount
                        );
                }


                return true;
            }
        }


        return false;
    }


    // ============================================================
    // HAS CUSTOM DROP
    // ============================================================

    public bool HasCustomDrop(
        string blockId)
    {
        return _rules.ContainsKey(
            blockId
        );
    }
}
