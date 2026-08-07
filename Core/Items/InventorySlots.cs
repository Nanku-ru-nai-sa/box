// UPDATED FILE - replaces your existing InventorySlot.cs
// (You mentioned you already added durability yourself - this version
// matches what the other new scripts expect, so replace yours with this
// to be safe, or just double check your field names match exactly.)

using Godot;

public class InventorySlot
{
    public string ItemId { get; set; } = "";
    public int Count { get; set; } = 0;

    // Only meaningful when ItemRegistry.GetItem(ItemId).HasDurability is true.
    public int CurrentDurability { get; set; } = 0;

    // Player-given name, Minecraft anvil style. Empty = use the item's
    // default DisplayName.
    public string CustomName { get; set; } = "";

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
        CurrentDurability = 0;
        CustomName = "";
    }
}