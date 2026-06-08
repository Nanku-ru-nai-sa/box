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
}