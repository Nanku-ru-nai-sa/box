using Godot;
using System;
using System.Collections.Generic;

// ToolDefinition — static registry of all tools and their properties.
// To add a new tool, add a new ToolType entry and register it in BuildRegistry().
// Hit counts: lower = faster. Hand = 6, basic tools = 4, upgrades = 3, 2, 1.
//
// UPDATED: GetToolType() and GetHitsToBreak() now also recognize tools
// crafted at the Tool Bench, which aren't in the static _itemToolMap below
// (their ids are generated at runtime, e.g. "tool_pickaxe_flint_stick").
// Crafted tools' actual ToolSpeed stat (set from the head material's
// MiningSpeedMod in materials.json) now scales the hit count too - a
// material with MiningSpeedMod 1.0 mines at the same speed as before,
// anything higher mines faster.

public static class ToolDefinition
{
    public enum ToolType
    {
        Hand,
        Pickaxe,
        Axe,
        Shovel,
        Sword,
        Hoe,
        Hammer,
        // Future upgrades:
        // IronPickaxe, DiamondPickaxe, etc.
    }

    public class ToolData
    {
        public ToolType Type         { get; set; }
        public int      HitsToBreak  { get; set; } // hits needed when using the RIGHT tool
        public int      WrongToolHits { get; set; } // hits when using wrong tool (usually = Hand)
        public HashSet<string> EffectiveBlocks { get; set; } = new();
    }

    // Block → which tool type is correct for it
    private static readonly Dictionary<string, ToolType> _blockToolMap = new()
    {
        // Pickaxe blocks
        { "stone",       ToolType.Pickaxe },
        { "cobblestone", ToolType.Pickaxe },
        { "rock",        ToolType.Pickaxe },
        { "obsidian",    ToolType.Pickaxe },
        { "gravel",      ToolType.Pickaxe },
        { "clay",        ToolType.Pickaxe },
        { "bedrock",     ToolType.Pickaxe },
        { "snow",        ToolType.Pickaxe },

        // Axe blocks
        { "log",         ToolType.Axe },
        { "leaves",      ToolType.Axe },
        { "wood",        ToolType.Axe },

        // Shovel blocks
        { "dirt",        ToolType.Shovel },
        { "grass_block", ToolType.Shovel },
        { "sand",        ToolType.Shovel },

        // Hand blocks (no tool bonus, hand is always fine)
        { "rose",        ToolType.Hand },
        { "dandelion",   ToolType.Hand },
        { "clover",      ToolType.Hand },
        { "water",       ToolType.Hand },
    };

    // Tool registry — BASE hit counts per tool type, before a crafted
    // tool's own ToolSpeed stat is applied (see GetHitsToBreak).
    private static readonly Dictionary<ToolType, int> _toolHits = new()
    {
        { ToolType.Hand,    6 },
        { ToolType.Pickaxe, 4 },
        { ToolType.Axe,     4 },
        { ToolType.Shovel,  4 },
        { ToolType.Sword,   5 }, // sword is weak at breaking blocks
        { ToolType.Hoe,     4 },
        { ToolType.Hammer,  3 }, // heavier, hits harder - tune as needed
    };

    // Item ID → tool type, for hand-authored/static tool items.
    // Tool Bench crafted tools are NOT listed here - they're recognized
    // dynamically in GetToolType() instead, since their ids are generated.
    private static readonly Dictionary<string, ToolType> _itemToolMap = new()
    {
        { "pickaxe",         ToolType.Pickaxe },
        { "axe",             ToolType.Axe },
        { "shovel",          ToolType.Shovel },
        { "sword",           ToolType.Sword },
        // Future: iron_pickaxe, diamond_axe, etc.
    };

    // ── Public API ────────────────────────────────────────────────────────────

    // How many hits does it take to break this block with this item?
    // Pixels-per-hit for the break overlay (see BlockBreakOverlay). Same
    // right-tool/wrong-tool gating as GetHitsToBreak, but expressed as
    // "how many pixels does this swing advance" rather than a hit count -
    // this is now what actually determines break speed, GetHitsToBreak is
    // no longer used by the mining code but is left intact in case
    // anything else still reads it.
    public static int GetEffectiveMiningPower(string blockId, string heldItemId)
    {
        ToolType requiredTool = GetRequiredTool(blockId);
        ToolType heldTool     = GetToolType(heldItemId);

        // Hand-only blocks and wrong-tool swings both mine at the same
        // baseline pace: 1 pixel per hit.
        if (requiredTool == ToolType.Hand) return 1;
        if (heldTool != requiredTool) return 1;

        var item = ItemRegistry.Instance?.GetItem(heldItemId);
        return (item != null && item.MiningPower > 0) ? item.MiningPower : 1;
    }

    public static int GetHitsToBreak(string blockId, string heldItemId)
    {
        // Bedrock is unbreakable in Survival (handled in Player)
        ToolType requiredTool = GetRequiredTool(blockId);
        ToolType heldTool     = GetToolType(heldItemId);

        // Hand blocks always take hand speed regardless of tool
        if (requiredTool == ToolType.Hand) return _toolHits[ToolType.Hand];

        // Wrong tool — always hand speed
        if (heldTool != requiredTool) return _toolHits[ToolType.Hand];

        int baseHits = _toolHits[heldTool];

        // Crafted (Tool Bench) tools carry their own ToolSpeed multiplier -
        // scale the base hit count by it. Static/hand-authored items (not
        // in ItemRegistry, or ToolSpeed left at its 1f default) behave
        // exactly as before.
        var item = ItemRegistry.Instance?.GetItem(heldItemId);
        float speedMod = (item != null && item.ToolSpeed > 0f) ? item.ToolSpeed : 1f;

        return Mathf.Max(1, Mathf.RoundToInt(baseHits / speedMod));
    }

    // What tool type does this block require?
    public static ToolType GetRequiredTool(string blockId)
    {
        if (string.IsNullOrEmpty(blockId)) return ToolType.Hand;
        return _blockToolMap.TryGetValue(blockId, out var t) ? t : ToolType.Hand;
    }

    // What tool type is this item?
    public static ToolType GetToolType(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return ToolType.Hand;

        if (_itemToolMap.TryGetValue(itemId, out var t)) return t;

        // Not a static item - check if it's a Tool Bench crafted tool
        // (ItemRegistry.RegisterRuntime sets ToolType to the family name,
        // e.g. "Pickaxe", at craft time).
        var item = ItemRegistry.Instance?.GetItem(itemId);
        if (item != null && !string.IsNullOrEmpty(item.ToolType) &&
            Enum.TryParse<ToolType>(item.ToolType, true, out var parsed))
            return parsed;

        return ToolType.Hand;
    }

    // Is this item a tool?
    public static bool IsTool(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return false;
        return GetToolType(itemId) != ToolType.Hand;
    }

    // Register a new tool item (call from game init if adding tools dynamically)
    public static void RegisterTool(string itemId, ToolType type, int hitsToBreak)
    {
        _itemToolMap[itemId]  = type;
        _toolHits[type]       = hitsToBreak; // note: overrides the type's hit count
    }
}