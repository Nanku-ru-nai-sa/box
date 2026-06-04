using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class ItemRegistry : Node
{
    // Singleton - accessible from anywhere in the game
    public static ItemRegistry Instance { get; private set; }

    // Master dictionary of all items
    private Dictionary<string, ItemResource> _items = new();

    // Called when the game starts
    public override void _Ready()
    {
        Instance = this;
        LoadAllItems();
        GD.Print($"ItemRegistry loaded {_items.Count} items.");
    }

    // Loads every ItemResource from the Items data folder
    private void LoadAllItems()
{
    string path = "res://Assets/Data/Items/";

    using var dir = DirAccess.Open(path);
    if (dir == null)
    {
        GD.Print("ItemRegistry: No items loaded yet.");
        return;
    }

    dir.ListDirBegin();
    string fileName = dir.GetNext();

    while (fileName != "")
    {
        if (fileName.EndsWith(".tres"))
        {
            string fullPath = path + fileName;
            var item = GD.Load<ItemResource>(fullPath);

            if (item != null && item.ItemId != "")
            {
                _items[item.ItemId] = item;
                GD.Print($"  Loaded item: {item.ItemId}");
            }
        }
        fileName = dir.GetNext();
    }
}

    // Get an item by its ID
    public ItemResource GetItem(string itemId)
    {
        if (_items.TryGetValue(itemId, out ItemResource item))
            return item;

        GD.PrintErr($"ItemRegistry: Item not found: {itemId}");
        return null;
    }

    // Check if an item exists
    public bool ItemExists(string itemId)
    {
        return _items.ContainsKey(itemId);
    }

    // Get all items with a specific tag
    // e.g. GetItemsByTag("ammo") returns all ammo items
    public List<ItemResource> GetItemsByTag(string tag)
    {
        List<ItemResource> results = new();

        foreach (var item in _items.Values)
        {
            foreach (var t in item.Tags)
            {
                if (t == tag)
                {
                    results.Add(item);
                    break;
                }
            }
        }

        return results;
    }

    // Get all equippable items for a specific slot
    // e.g. GetItemsForSlot("chest") returns all chest armor
    public List<ItemResource> GetItemsForSlot(string equipSlot)
    {
        List<ItemResource> results = new();

        foreach (var item in _items.Values)
        {
            if (item.IsEquippable && item.EquipSlot == equipSlot)
                results.Add(item);
        }

        return results;
    }

    // Get all registered items - useful for creative mode
    public IEnumerable<ItemResource> GetAllItems()
    {
        return _items.Values;
    }
}