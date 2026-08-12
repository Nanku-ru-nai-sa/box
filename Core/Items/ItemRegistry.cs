// UPDATED FILE - replaces your existing ItemRegistry.cs
// Added: RegisterRuntime(), so crafted tools can be added to the registry
// after the game has already started (your original _items dictionary
// was private with no way to add to it after _Ready).
//
// FIXED: _Ready() previously did nothing but call LoadPersistedRecipes() -
// _items started completely empty and stayed that way for every plain
// material (dirt, stone, sand, etc.), which is why they were showing up
// as "Item not found" and had no tooltip. RegisterBaseItems() below
// rebuilds that base set automatically instead of needing every material
// hand-listed:
//   1. Every block in BlockRegistry gets a matching item (so ANY block,
//      including brand new ones, is always at least name+tooltip-ready,
//      even before a dedicated item icon exists for it).
//   2. Anything else with an icon in Assets/Textures/Items/ but no
//      matching block (stick, ores, food, crafting materials, etc.) gets
//      registered too, just not placeable.

using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class ItemRegistry : Node
{
    public static ItemRegistry Instance { get; private set; }
    private Dictionary<string, ItemResource> _items = new();

    public override void _Ready()
    {
        Instance = this;

        // BlockRegistry is also an autoload but isn't guaranteed to have
        // run its own _Ready() yet at this exact point - autoload _Ready()
        // order (ItemRegistry is actually listed BEFORE BlockRegistry in
        // project.godot) isn't something to rely on. CallDeferred pushes
        // this to right after every autoload's _Ready() has finished, so
        // BlockRegistry.Instance is safely populated by the time
        // RegisterBaseItems reads from it.
        CallDeferred(nameof(RegisterBaseItems));
    }

    private void RegisterBaseItems()
    {
        if (BlockRegistry.Instance != null)
        {
            foreach (var block in BlockRegistry.Instance.GetAllBlocks())
            {
                if (_items.ContainsKey(block.BlockId)) continue;
                var item = new ItemResource();
                item.ItemId       = block.BlockId;
                item.DisplayName  = block.DisplayName;
                item.IsStackable  = true;
                item.MaxStackSize = 64;
                item.PlacesBlockId = block.BlockId;
                _items[block.BlockId] = item;
            }
        }
        else
        {
            GD.PrintErr("ItemRegistry: BlockRegistry.Instance was still null on deferred init - block items were not registered.");
        }

        foreach (string id in ItemCatalog.GetAllItemIds())
        {
            if (_items.ContainsKey(id)) continue;
            var item = new ItemResource();
            item.ItemId        = id;
            item.DisplayName   = PrettifyId(id);
            item.IsStackable   = true;
            item.MaxStackSize  = 64;
            item.PlacesBlockId = ""; // no matching block - not placeable, just a held/crafting item
            _items[id] = item;
        }

        GD.Print($"ItemRegistry loaded {_items.Count} items.");

        // Rebuild any Tool Bench crafted tools from their persisted recipes
        // BEFORE anything (e.g. loading a save) tries to look one up by id.
        // Fixes: crafted tools showing no icon/stats after restarting the game.
        ToolCrafting.LoadPersistedRecipes();
    }

    // "tool_bench" -> "Tool Bench", "diamond_ore" -> "Diamond Ore". Only
    // used for items with no matching BlockResource (those use the
    // block's own DisplayName instead, which can be hand-tuned).
    private static string PrettifyId(string id)
    {
        string[] words = id.Split('_', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
            words[i] = words[i].Length > 0 ? char.ToUpper(words[i][0]) + words[i].Substring(1) : words[i];
        return string.Join(" ", words);
    }

    public ItemResource GetItem(string itemId)
    {
        if (_items.TryGetValue(itemId, out ItemResource item))
            return item;
        GD.PrintErr($"ItemRegistry: Item not found: {itemId}");
        return null;
    }

    public bool ItemExists(string itemId)
    {
        return _items.ContainsKey(itemId);
    }

    public IEnumerable<ItemResource> GetAllItems()
    {
        return _items.Values;
    }

    // NEW: registers an item created at runtime (e.g. a freshly-crafted
    // tool from ToolCrafting.CraftTool). Safe to call multiple times with
    // the same ItemId - it will only register the first time.
    public void RegisterRuntime(ItemResource item)
    {
        if (item == null || string.IsNullOrEmpty(item.ItemId))
        {
            GD.PrintErr("ItemRegistry: Tried to register a null item or one with no ItemId.");
            return;
        }

        if (!_items.ContainsKey(item.ItemId))
        {
            _items[item.ItemId] = item;
            GD.Print($"ItemRegistry: Runtime-registered {item.ItemId}");
        }
    }
}