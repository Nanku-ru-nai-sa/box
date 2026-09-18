using Godot;
using System;
using System.Collections.Generic;

public partial class Player : CharacterBody3D
{
    // =========================================================================
    // INVENTORY SLOT INPUT
    // =========================================================================

    private void OnInvSlotInput(InputEvent ev, int slotIndex)
    {
        if (!_inventoryOpen) return;
        HandleSlotInput(ev, slotIndex);
    }

    // Is this item allowed to stack by Count? False for crafted tools
    // (IsStackable=false on their ItemResource) - unknown/unregistered
    // item ids default to stackable, matching the old blanket behavior.
    private bool IsStackableItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return true;

        var item = ItemRegistry.Instance?.GetItem(itemId);

        return item == null || item.IsStackable;
    }

    // Copies EVERYTHING about a slot's contents - ItemId, Count, and
    // CurrentDurability and CustomName too.
    private void CopySlot(InventorySlot from, InventorySlot to)
    {
        to.ItemId           = from.ItemId;
        to.Count             = from.Count;
        to.CurrentDurability = from.CurrentDurability;
        to.CustomName        = from.CustomName;
    }

    private void OnHotbarInvSlotInput(InputEvent ev, int hotbarIndex)
    {
        if (!_inventoryOpen) return;
        HandleSlotInput(ev, MainInvSize + hotbarIndex);
    }

    private void HandleSlotInput(InputEvent ev, int slotIndex)
    {
        if (ev is InputEventMouseButton mb && mb.Pressed)
        {
            bool shift = Input.IsKeyPressed(Key.Shift);

            switch (mb.ButtonIndex)
            {
                case MouseButton.Left when shift:
                    ShiftClick(slotIndex);
                    break;

                case MouseButton.Left when mb.DoubleClick:
                    DoubleClickCollect(slotIndex);
                    break;

                case MouseButton.Left:
                    HandleLeftClick(slotIndex);
                    break;

                case MouseButton.Right:
                    HandleRightClick(slotIndex);
                    break;

                case MouseButton.WheelUp:
                    ScrollSlot(slotIndex, up: true);
                    break;

                case MouseButton.WheelDown:
                    ScrollSlot(slotIndex, up: false);
                    break;
            }
        }
    }

    // =========================================================================
    // LEFT CLICK
    // =========================================================================

    private void HandleLeftClick(int slotIndex)
    {
        var slot = _inventory.Slots[slotIndex];

        if (_heldSlot.IsEmpty)
        {
            if (slot.IsEmpty) return;

            _heldFromSlot = slotIndex;
            CopySlot(slot, _heldSlot);

            _dragOrigCount = slot.Count;

            slot.Clear();

            _dragMode = DragMode.LmbWithItem;
            _dragVisited.Clear();
            _dragVisited.Add(slotIndex);
            _dragLastSlot = slotIndex;
        }
        else
        {
            if (slot.IsEmpty)
            {
                CopySlot(_heldSlot, slot);
                _heldSlot.Clear();
            }
            else if (slot.ItemId == _heldSlot.ItemId &&
                     IsStackableItem(slot.ItemId))
            {
                int space = _inventory.MaxStackSize - slot.Count;

                int transfer =
                    Mathf.Min(space, _heldSlot.Count);

                slot.Count += transfer;
                _heldSlot.Count -= transfer;

                if (_heldSlot.Count <= 0)
                    _heldSlot.Clear();
            }
            else if (slot.ItemId == _heldSlot.ItemId &&
                     !IsStackableItem(slot.ItemId))
            {
                // Non-stackable tools can merge durability if their
                // custom names match.
                if (slot.CustomName == _heldSlot.CustomName)
                {
                    slot.CurrentDurability +=
                        _heldSlot.CurrentDurability;

                    _heldSlot.Clear();
                }
            }
            else
            {
                (
                    slot.ItemId,
                    _heldSlot.ItemId
                ) =
                (
                    _heldSlot.ItemId,
                    slot.ItemId
                );

                (
                    slot.Count,
                    _heldSlot.Count
                ) =
                (
                    _heldSlot.Count,
                    slot.Count
                );

                (
                    slot.CurrentDurability,
                    _heldSlot.CurrentDurability
                ) =
                (
                    _heldSlot.CurrentDurability,
                    slot.CurrentDurability
                );

                (
                    slot.CustomName,
                    _heldSlot.CustomName
                ) =
                (
                    _heldSlot.CustomName,
                    slot.CustomName
                );
            }

            EndDrag();
        }

        FireChanged();
    }

    // =========================================================================
    // RIGHT CLICK
    // =========================================================================

    private void HandleRightClick(int slotIndex)
    {
        var slot = _inventory.Slots[slotIndex];

        if (_heldSlot.IsEmpty)
        {
            if (slot.IsEmpty) return;

            int half =
                Mathf.CeilToInt(slot.Count / 2f);

            _heldSlot.ItemId =
                slot.ItemId;

            _heldSlot.Count =
                half;

            _heldSlot.CurrentDurability =
                slot.CurrentDurability;

            _heldSlot.CustomName =
                slot.CustomName;

            _heldFromSlot =
                slotIndex;

            slot.Count -= half;

            if (slot.Count <= 0)
                slot.Clear();

            _dragMode =
                DragMode.RmbDrag;

            _dragVisited.Clear();
            _dragVisited.Add(slotIndex);
            _dragLastSlot = slotIndex;
        }
        else
        {
            PlaceOneIntoSlot(slotIndex);
        }

        FireChanged();
    }

    private bool PlaceOneIntoSlot(int slotIndex)
    {
        if (_heldSlot.IsEmpty)
            return false;

        var slot =
            _inventory.Slots[slotIndex];

        if (!slot.IsEmpty &&
            slot.ItemId != _heldSlot.ItemId)
        {
            return false;
        }

        if (!IsStackableItem(_heldSlot.ItemId))
        {
            // Non-stackable item: move the whole item into an empty slot.
            if (!slot.IsEmpty)
                return false;

            CopySlot(_heldSlot, slot);
            _heldSlot.Clear();

            return true;
        }

        if (!slot.IsEmpty &&
            slot.Count >= _inventory.MaxStackSize)
        {
            return false;
        }

        if (slot.IsEmpty)
            slot.ItemId = _heldSlot.ItemId;

        slot.Count++;

        _heldSlot.Count--;

        if (_heldSlot.Count <= 0)
            _heldSlot.Clear();

        return true;
    }

    // =========================================================================
    // DRAG
    // =========================================================================

    private void OnDragEnterSlot(int slotIndex)
    {
        // Check if dragging over a crafting panel slot.
        if (_craftingPanel != null && _inventoryOpen)
        {
            int count =
                _craftingPanel.GetActiveSlotCount();

            for (int i = 0; i < count; i++)
            {
                var panel =
                    _craftingPanel.GetSlotPanel(i);

                if (panel == null)
                    continue;

                var mouse =
                    GetViewport().GetMousePosition();

                if (new Rect2(
                        panel.GlobalPosition,
                        panel.Size
                    ).HasPoint(mouse))
                {
                    OnDragEnterCraftSlot(i);
                    return;
                }
            }
        }

        switch (_dragMode)
        {
            case DragMode.LmbWithItem:
            {
                if (_heldSlot.IsEmpty)
                    return;

                var slot =
                    _inventory.Slots[slotIndex];

                if (!IsStackableItem(_heldSlot.ItemId))
                {
                    if (!slot.IsEmpty)
                        return;

                    CopySlot(_heldSlot, slot);

                    _heldSlot.Clear();

                    _dragVisited.Clear();
                    _dragVisited.Add(slotIndex);

                    break;
                }

                if (!slot.IsEmpty &&
                    slot.ItemId != _heldSlot.ItemId)
                {
                    return;
                }

                if (!slot.IsEmpty &&
                    slot.Count >= _inventory.MaxStackSize)
                {
                    return;
                }

                if (!_dragVisited.Contains(slotIndex))
                    _dragVisited.Add(slotIndex);

                int n =
                    _dragVisited.Count;

                int perSlot =
                    _dragOrigCount / n;

                int leftover =
                    _dragOrigCount -
                    perSlot * n;

                foreach (int idx in _dragVisited)
                {
                    var s =
                        _inventory.Slots[idx];

                    s.ItemId =
                        _heldSlot.ItemId;

                    s.Count = 0;
                }

                int remaining =
                    _dragOrigCount;

                for (int i = 0;
                     i < _dragVisited.Count;
                     i++)
                {
                    int give =
                        perSlot +
                        (i == 0 ? leftover : 0);

                    give =
                        Mathf.Min(
                            give,
                            _inventory.MaxStackSize
                        );

                    _inventory
                        .Slots[_dragVisited[i]]
                        .Count = give;

                    remaining -= give;
                }

                _heldSlot.Count =
                    Mathf.Max(0, remaining);

                if (_heldSlot.Count <= 0)
                    _heldSlot.Clear();

                break;
            }

            case DragMode.RmbDrag:
            {
                if (_heldSlot.IsEmpty)
                    return;

                if (_dragVisited.Contains(slotIndex))
                    return;

                if (PlaceOneIntoSlot(slotIndex))
                    _dragVisited.Add(slotIndex);

                break;
            }

            case DragMode.LmbNoItem:
            {
                if (_dragVisited.Contains(slotIndex))
                    return;

                var slot =
                    _inventory.Slots[slotIndex];

                if (slot.IsEmpty)
                    return;

                if (!_heldSlot.IsEmpty &&
                    slot.ItemId != _heldSlot.ItemId)
                {
                    return;
                }

                if (!IsStackableItem(slot.ItemId))
                {
                    if (!_heldSlot.IsEmpty)
                        return;

                    CopySlot(slot, _heldSlot);
                    slot.Clear();

                    _dragVisited.Add(slotIndex);

                    break;
                }

                int space =
                    _inventory.MaxStackSize -
                    (_heldSlot.IsEmpty
                        ? 0
                        : _heldSlot.Count);

                if (space <= 0)
                    return;

                _dragVisited.Add(slotIndex);

                if (_heldSlot.IsEmpty)
                    _heldSlot.ItemId =
                        slot.ItemId;

                int take =
                    Mathf.Min(
                        slot.Count,
                        space
                    );

                _heldSlot.Count += take;
                slot.Count -= take;

                if (slot.Count <= 0)
                    slot.Clear();

                break;
            }

            case DragMode.ShiftLmbNoItem:
            {
                if (_dragVisited.Contains(slotIndex))
                    return;

                _dragVisited.Add(slotIndex);

                ShiftMoveSlot(slotIndex);

                break;
            }
        }

        FireChanged();
        UpdateCursorVisual();
    }

    // =========================================================================
    // DRAG INTO CRAFTING
    // =========================================================================

    private void OnDragEnterCraftSlot(int craftIdx)
    {
        if (_dragMode != DragMode.LmbWithItem &&
            _dragMode != DragMode.RmbDrag)
        {
            return;
        }

        if (_heldSlot.IsEmpty)
            return;

        if (_craftingPanel.TryPlaceHeldItem(
                craftIdx,
                _heldSlot.ItemId))
        {
            _heldSlot.Count--;

            if (_heldSlot.Count <= 0)
                _heldSlot.Clear();
        }

        FireChanged();
        UpdateCursorVisual();
    }

    private void EndDrag()
    {
        _dragMode =
            DragMode.None;

        _dragVisited.Clear();

        _dragOrigCount = 0;

        _dragLastSlot = -1;

        _heldFromSlot = -1;
    }

    // =========================================================================
    // SHIFT CLICK
    // =========================================================================

    private void ShiftClick(int slotIndex)
    {
        ShiftMoveSlot(slotIndex);

        FireChanged();
    }

    private void ShiftMoveSlot(int slotIndex)
    {
        var src =
            _inventory.Slots[slotIndex];

        if (src.IsEmpty)
            return;

        bool isHotbar =
            slotIndex >= MainInvSize;

        int destStart =
            isHotbar ? 0 : MainInvSize;

        int destEnd =
            isHotbar ? MainInvSize : TotalSlots;

        if (IsStackableItem(src.ItemId))
        {
            for (
                int i = destStart;
                i < destEnd && src.Count > 0;
                i++
            )
            {
                var dst =
                    _inventory.Slots[i];

                if (dst.IsEmpty ||
                    dst.ItemId != src.ItemId)
                {
                    continue;
                }

                int t =
                    Mathf.Min(
                        _inventory.MaxStackSize -
                        dst.Count,
                        src.Count
                    );

                dst.Count += t;
                src.Count -= t;
            }
        }

        for (
            int i = destStart;
            i < destEnd && src.Count > 0;
            i++
        )
        {
            var dst =
                _inventory.Slots[i];

            if (!dst.IsEmpty)
                continue;

            CopySlot(src, dst);
            src.Clear();
        }

        if (src.Count <= 0)
            src.Clear();
    }

    // =========================================================================
    // DOUBLE CLICK
    // =========================================================================

    private void DoubleClickCollect(int slotIndex)
    {
        if (_heldSlot.IsEmpty)
        {
            var s =
                _inventory.Slots[slotIndex];

            if (s.IsEmpty)
                return;

            CopySlot(s, _heldSlot);

            s.Clear();
        }

        if (!IsStackableItem(_heldSlot.ItemId))
        {
            FireChanged();
            UpdateCursorVisual();
            return;
        }

        if (_heldSlot.Count >= _inventory.MaxStackSize)
        {
            FireChanged();
            UpdateCursorVisual();
            return;
        }

        string id =
            _heldSlot.ItemId;

        for (
            int i = 0;
            i < TotalSlots &&
            _heldSlot.Count < _inventory.MaxStackSize;
            i++
        )
        {
            if (i == slotIndex)
                continue;

            var s =
                _inventory.Slots[i];

            if (s.IsEmpty ||
                s.ItemId != id)
            {
                continue;
            }

            int take =
                Mathf.Min(
                    s.Count,
                    _inventory.MaxStackSize -
                    _heldSlot.Count
                );

            _heldSlot.Count += take;
            s.Count -= take;

            if (s.Count <= 0)
                s.Clear();
        }

        FireChanged();
        UpdateCursorVisual();
    }

    // =========================================================================
    // SCROLL WHEEL IN INVENTORY
    // =========================================================================

    private void ScrollSlot(int slotIndex, bool up)
    {
        bool isHotbar =
            slotIndex >= MainInvSize;

        var src =
            _inventory.Slots[slotIndex];

        if (up)
        {
            int fromStart =
                isHotbar ? 0 : MainInvSize;

            int fromEnd =
                isHotbar ? MainInvSize : TotalSlots;

            for (
                int i = fromStart;
                i < fromEnd;
                i++
            )
            {
                var other =
                    _inventory.Slots[i];

                if (other.IsEmpty)
                    continue;

                if (!src.IsEmpty &&
                    other.ItemId != src.ItemId)
                {
                    continue;
                }

                if (!IsStackableItem(other.ItemId))
                {
                    if (!src.IsEmpty)
                        continue;

                    CopySlot(other, src);
                    other.Clear();

                    FireChanged();
                    return;
                }

                if (src.Count >= _inventory.MaxStackSize)
                    continue;

                if (src.IsEmpty)
                    src.ItemId = other.ItemId;

                src.Count++;
                other.Count--;

                if (other.Count <= 0)
                    other.Clear();

                FireChanged();
                return;
            }
        }
        else
        {
            if (src.IsEmpty)
                return;

            if (!IsStackableItem(src.ItemId))
            {
                int toStart =
                    isHotbar ? 0 : MainInvSize;

                int toEnd =
                    isHotbar ? MainInvSize : TotalSlots;

                for (
                    int i = toStart;
                    i < toEnd;
                    i++
                )
                {
                    var dst =
                        _inventory.Slots[i];

                    if (!dst.IsEmpty)
                        continue;

                    CopySlot(src, dst);
                    src.Clear();

                    FireChanged();
                    return;
                }

                return;
            }

            int stackToStart =
                isHotbar ? 0 : MainInvSize;

            int stackToEnd =
                isHotbar ? MainInvSize : TotalSlots;

            for (
                int i = stackToStart;
                i < stackToEnd;
                i++
            )
            {
                var dst =
                    _inventory.Slots[i];

                if (dst.IsEmpty ||
                    dst.ItemId != src.ItemId ||
                    dst.Count >= _inventory.MaxStackSize)
                {
                    continue;
                }

                dst.Count++;
                src.Count--;

                if (src.Count <= 0)
                    src.Clear();

                FireChanged();
                return;
            }

            for (
                int i = stackToStart;
                i < stackToEnd;
                i++
            )
            {
                var dst =
                    _inventory.Slots[i];

                if (!dst.IsEmpty)
                    continue;

                dst.ItemId = src.ItemId;
                dst.Count = 1;

                src.Count--;

                if (src.Count <= 0)
                    src.Clear();

                FireChanged();
                return;
            }
        }
    }

    // =========================================================================
    // ADD ITEM
    // =========================================================================

    private int AddItemToInventory(
        string itemId,
        int count,
        int? durability = null,
        string customName = "")
    {
        if (string.IsNullOrEmpty(itemId) ||
            count <= 0)
        {
            return count;
        }

        int rem = count;

        bool stackable =
            IsStackableItem(itemId);

        void TryAdd(
            int start,
            int end,
            bool stackOnly)
        {
            for (
                int i = start;
                i < end && rem > 0;
                i++
            )
            {
                var s =
                    _inventory.Slots[i];

                if (stackOnly)
                {
                    if (!stackable)
                        continue;

                    if (s.IsEmpty ||
                        s.ItemId != itemId)
                    {
                        continue;
                    }

                    int add =
                        Mathf.Min(
                            _inventory.MaxStackSize -
                            s.Count,
                            rem
                        );

                    s.Count += add;
                    rem -= add;
                }
                else
                {
                    if (!s.IsEmpty)
                        continue;

                    int add =
                        stackable
                            ? Mathf.Min(
                                _inventory.MaxStackSize,
                                rem)
                            : 1;

                    s.ItemId = itemId;
                    s.Count = add;

                    rem -= add;

                    if (!stackable)
                    {
                        var item =
                            ItemRegistry.Instance?
                                .GetItem(itemId);

                        s.CurrentDurability =
                            durability ??
                            item?.MaxDurability ??
                            0;

                        s.CustomName =
                            customName;
                    }
                }
            }
        }

        // Hotbar first.
        TryAdd(
            MainInvSize,
            TotalSlots,
            true
        );

        TryAdd(
            MainInvSize,
            TotalSlots,
            false
        );

        // Then main inventory.
        TryAdd(
            0,
            MainInvSize,
            true
        );

        TryAdd(
            0,
            MainInvSize,
            false
        );

        _inventory.OnInventoryChanged?.Invoke();

        return rem;
    }

    // =========================================================================
    // PICKUP
    // =========================================================================

    // Called by ItemPickup when the player walks close enough to collect it.
    // Returns how many items didn't fit anywhere.
    public int CollectPickup(
        string itemId,
        int count,
        int? durability = null,
        string customName = "")
    {
        return AddItemToInventory(
            itemId,
            count,
            durability,
            customName
        );
    }

    // =========================================================================
    // WORLD ITEM DROPS
    // =========================================================================

    private void SpawnItemDrop(
        string itemId,
        int count,
        Vector3 worldPosition,
        Vector3? tossVelocity = null)
    {
        if (string.IsNullOrEmpty(itemId) ||
            count <= 0)
        {
            return;
        }

        var pickup =
            new ItemPickup();

        pickup.ItemId = itemId;
        pickup.Count = count;
        pickup.TossVelocity = tossVelocity;

        GetTree().Root.AddChild(pickup);

        pickup.GlobalPosition =
            worldPosition;
    }

    private void DropOneItem()
    {
        var gm =
            GameModeManager.Instance;

        if (gm != null && gm.IsStory)
            return;

        var slot =
            _inventory.Slots[
                MainInvSize + _selectedSlot
            ];

        if (slot.IsEmpty)
            return;

        string itemId =
            slot.ItemId;

        slot.Count--;

        if (slot.Count <= 0)
            slot.Clear();

        FireChanged();

        Vector3 forward =
            -_rayCast.GlobalTransform.Basis.Z;

        Vector3 spawnPos =
            GlobalPosition +
            new Vector3(0, DropSpawnHeight, 0) +
            forward * 0.6f;

        Vector3 tossVel =
            forward * DropForwardSpeed +
            Vector3.Up * DropUpSpeed;

        SpawnItemDrop(
            itemId,
            1,
            spawnPos,
            tossVel
        );
    }

    // =========================================================================
    // INVENTORY MOUSE INPUT
    // =========================================================================

    public override void _Input(InputEvent @event)
    {
        if (!_inventoryOpen)
            return;

        if (@event is InputEventMouseButton mb &&
            !mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.Left ||
                mb.ButtonIndex == MouseButton.Right)
            {
                EndDrag();
                FireChanged();
                UpdateCursorVisual();
            }
        }

        if (@event is InputEventMouseButton startMb &&
            startMb.Pressed &&
            startMb.ButtonIndex == MouseButton.Left &&
            _heldSlot.IsEmpty &&
            _dragMode == DragMode.None)
        {
            _dragMode =
                Input.IsKeyPressed(Key.Shift)
                    ? DragMode.ShiftLmbNoItem
                    : DragMode.LmbNoItem;

            _dragVisited.Clear();
            _dragLastSlot = -1;
        }
    }

    private int GetSlotUnderMouse()
    {
        var mouse =
            GetViewport().GetMousePosition();

        for (int i = 0; i < MainInvSize; i++)
        {
            if (_invSlotPanels[i] == null)
                continue;

            if (new Rect2(
                    _invSlotPanels[i].GlobalPosition,
                    _invSlotPanels[i].Size
                ).HasPoint(mouse))
            {
                return i;
            }
        }

        for (int i = 0; i < HotbarSize; i++)
        {
            if (_invHotbarSlots[i] == null)
                continue;

            if (new Rect2(
                    _invHotbarSlots[i].GlobalPosition,
                    _invHotbarSlots[i].Size
                ).HasPoint(mouse))
            {
                return MainInvSize + i;
            }
        }

        return -1;
    }

    // =========================================================================
    // SAVE / LOAD
    // =========================================================================

    private void LoadInventoryFromSave()
    {
        var cm =
            GetTree().Root.FindChild(
                "ChunkManager",
                true,
                false
            ) as ChunkManager;

        if (cm == null)
            return;

        cm.LoadInventory(_inventory);

        RefreshAllSlotVisuals();
    }
}