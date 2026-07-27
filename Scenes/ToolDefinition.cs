using Godot;
using System.Collections.Generic;

// ToolDefinition — static registry of all tools and their properties.
// To add a new tool, add a new ToolType entry and register it in BuildRegistry().
// Hit counts: lower = faster. Hand = 6, basic tools = 4, upgrades = 3, 2, 1.

public static class ToolDefinition
{
    public enum ToolType
    {
        Hand,
        Pickaxe,
        Axe,
        Shovel,
        Sword,
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

    // Tool registry — hit counts per tool type
    private static readonly Dictionary<ToolType, int> _toolHits = new()
    {
        { ToolType.Hand,    6 },
        { ToolType.Pickaxe, 4 },
        { ToolType.Axe,     4 },
        { ToolType.Shovel,  4 },
        { ToolType.Sword,   5 }, // sword is weak at breaking blocks
    };

    // Item ID → tool type (what tool is this item?)
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
    public static int GetHitsToBreak(string blockId, string heldItemId)
    {
        // Bedrock is unbreakable in Survival (handled in Player)
        ToolType requiredTool = GetRequiredTool(blockId);
        ToolType heldTool     = GetToolType(heldItemId);

        // Hand blocks always take hand speed regardless of tool
        if (requiredTool == ToolType.Hand) return _toolHits[ToolType.Hand];

        // Right tool — use that tool's hit count
        if (heldTool == requiredTool) return _toolHits[heldTool];

        // Wrong tool — always hand speed
        return _toolHits[ToolType.Hand];
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
        return _itemToolMap.TryGetValue(itemId, out var t) ? t : ToolType.Hand;
    }

    // Is this item a tool?
    public static bool IsTool(string itemId)
    {
        return !string.IsNullOrEmpty(itemId) && _itemToolMap.ContainsKey(itemId);
    }

    // Register a new tool item (call from game init if adding tools dynamically)
    public static void RegisterTool(string itemId, ToolType type, int hitsToBreak)
    {
        _itemToolMap[itemId]  = type;
        _toolHits[type]       = hitsToBreak; // note: overrides the type's hit count
    }
}