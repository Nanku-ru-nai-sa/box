public class InventorySlot
{
    public string ItemId { get; set; } = "";
    public int Count { get; set; } = 0;

    // Tool instance data — only meaningful when ItemRegistry.GetItem(ItemId).HasDurability
    public int CurrentDurability { get; set; } = 0;
    public string CustomName { get; set; } = ""; // empty = use ItemResource.DisplayName

    public bool IsEmpty => string.IsNullOrEmpty(ItemId) || Count <= 0;

    public InventorySlot() { }
    public InventorySlot(string itemId, int count) { ItemId = itemId; Count = count; }

    public void Clear()
    {
        ItemId = ""; Count = 0; CurrentDurability = 0; CustomName = "";
    }
}