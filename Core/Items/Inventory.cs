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
}