// UPDATED FILE - replaces your existing Inventory.cs
// Added at the bottom: AddCraftedTool() and TryMergeTools().
// Everything above the "=== NEW ===" marker is unchanged from what you had.

using Godot;
using System;

public partial class Inventory : Node
{
    [Export] public int SlotCount { get; set; } = 40;
    [Export] public int MaxStackSize { get; set; } = 1024;

    public InventorySlot[] Slots { get; private set; }

    public Action OnInventoryChanged;

    public override void _Ready()
    {
        Slots = new InventorySlot[SlotCount];
        for (int i = 0; i < SlotCount; i++)
            Slots[i] = new InventorySlot();
    }

    // Returns true if the item was fully added, false if inventory was full
    public bool AddItem(string itemId, int count)
    {
        if (string.IsNullOrEmpty(itemId) || count <= 0) return false;

        int remaining = count;

        // First pass: try to stack onto existing matching slots
        for (int i = 0; i < Slots.Length && remaining > 0; i++)
        {
            if (Slots[i].ItemId == itemId && Slots[i].Count < MaxStackSize)
            {
                int spaceInSlot = MaxStackSize - Slots[i].Count;
                int amountToAdd = Mathf.Min(spaceInSlot, remaining);
                Slots[i].Count += amountToAdd;
                remaining -= amountToAdd;
            }
        }

        // Second pass: place leftover into empty slots
        for (int i = 0; i < Slots.Length && remaining > 0; i++)
        {
            if (Slots[i].IsEmpty)
            {
                int amountToAdd = Mathf.Min(MaxStackSize, remaining);
                Slots[i].ItemId = itemId;
                Slots[i].Count = amountToAdd;
                remaining -= amountToAdd;
            }
        }

        OnInventoryChanged?.Invoke();
        return remaining == 0;
    }

    // Removes up to 'count' items, returns how many were actually removed
    public int RemoveItem(string itemId, int count)
    {
        int remaining = count;

        for (int i = 0; i < Slots.Length && remaining > 0; i++)
        {
            if (Slots[i].ItemId == itemId)
            {
                int amountToRemove = Mathf.Min(Slots[i].Count, remaining);
                Slots[i].Count -= amountToRemove;
                remaining -= amountToRemove;

                if (Slots[i].Count <= 0)
                    Slots[i].Clear();
            }
        }

        OnInventoryChanged?.Invoke();
        return count - remaining;
    }

    public int GetItemCount(string itemId)
    {
        int total = 0;
        foreach (var slot in Slots)
        {
            if (slot.ItemId == itemId)
                total += slot.Count;
        }
        return total;
    }

    public bool HasItem(string itemId, int count = 1)
    {
        return GetItemCount(itemId) >= count;
    }

    // === NEW ===

    // Adds a freshly-crafted tool into the first empty slot. Tools never
    // stack automatically (that's what TryMergeTools is for, as a manual
    // player action), so this always claims a whole new slot.
    // Returns true if there was room, false if the inventory was full.
    public bool AddCraftedTool(string itemId, int durability, string customName = "")
    {
        for (int i = 0; i < Slots.Length; i++)
        {
            if (Slots[i].IsEmpty)
            {
                Slots[i].ItemId = itemId;
                Slots[i].Count = 1;
                Slots[i].CurrentDurability = durability;
                Slots[i].CustomName = customName;
                OnInventoryChanged?.Invoke();
                return true;
            }
        }

        return false; // inventory full
    }

    // Merges 'source' tool into 'target' tool, Minecraft-anvil style,
    // but instant and allowed to exceed max durability.
    // Call this from your drag-and-drop UI code when the player drags
    // one tool onto another matching one.
    // Returns true if the merge happened, false if they didn't match.
    public bool TryMergeTools(InventorySlot source, InventorySlot target)
    {
        if (source == null || target == null) return false;
        if (source.IsEmpty || target.IsEmpty) return false;
        if (source.ItemId != target.ItemId) return false;
        if (source.CustomName != target.CustomName) return false; // both "" counts as matching

        var item = ItemRegistry.Instance.GetItem(source.ItemId);
        if (item == null || !item.HasDurability) return false;

        target.CurrentDurability += source.CurrentDurability; // intentionally can exceed MaxDurability
        source.Clear();

        OnInventoryChanged?.Invoke();
        return true;
    }
}