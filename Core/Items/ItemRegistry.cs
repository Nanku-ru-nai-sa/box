// UPDATED FILE - replaces your existing ItemRegistry.cs
// Added: RegisterRuntime(), so crafted tools can be added to the registry
// after the game has already started (your original _items dictionary
// was private with no way to add to it after _Ready).

using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class ItemRegistry : Node
{
    public static ItemRegistry Instance { get; private set; }
    private Dictionary<string, ItemResource> _items = new();

    public override void _Ready()
    {
        Instance = this;
        GD.Print($"ItemRegistry loaded {_items.Count} items.");
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