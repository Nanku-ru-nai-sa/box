using Godot;

public class InventorySlot
{
    public string ItemId { get; set; } = "";
    public int Count { get; set; } = 0;

    public bool IsEmpty => string.IsNullOrEmpty(ItemId) || Count <= 0;

    public InventorySlot() { }

    public InventorySlot(string itemId, int count)
    {
        ItemId = itemId;
        Count = count;
    }

    public void Clear()
    {
        ItemId = "";
        Count = 0;
    }
}