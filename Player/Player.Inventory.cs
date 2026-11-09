
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

    private bool IsStackableItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return true;

        var item = ItemRegistry.Instance?.GetItem(itemId);
        return item == null || item.IsStackable;
    }

    private void CopySlot(InventorySlot from, InventorySlot to)
    {
        to.ItemId = from.ItemId;
        to.Count = from.Count;
        to.CurrentDurability = from.CurrentDurability;
        to.CustomName = from.CustomName;
    }

    private void OnHotbarInvSlotInput(InputEvent ev, int hotbarIndex)
    {
        if (!_inventoryOpen) return;
        HandleSlotInput(ev, MainInvSize + hotbarIndex);
    }

    // Connect crate slot panels to this method.
    private void OnContainerSlotInput(InputEvent ev, int containerIndex)
    {
        if (!_inventoryOpen || !_containerOpen) return;
        HandleSlotInput(ev, TotalSlots + containerIndex);
    }

    private void HandleSlotInput(InputEvent ev, int slotIndex)
    {
        if (ev is not InputEventMouseButton mb || !mb.Pressed)
            return;

        if (GetUiSlot(slotIndex) == null)
            return;

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

    // =========================================================================
    // LEFT CLICK
    // =========================================================================

    private void HandleLeftClick(int slotIndex)
    {
        var slot = GetUiSlot(slotIndex);
        if (slot == null) return;

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
                int transfer = Mathf.Min(space, _heldSlot.Count);

                slot.Count += transfer;
                _heldSlot.Count -= transfer;

                if (_heldSlot.Count <= 0)
                    _heldSlot.Clear();
            }
            else if (slot.ItemId == _heldSlot.ItemId &&
                     !IsStackableItem(slot.ItemId))
            {
                if (slot.CustomName == _heldSlot.CustomName)
                {
                    slot.CurrentDurability += _heldSlot.CurrentDurability;
                    _heldSlot.Clear();
                }
            }
            else
            {
                string itemId = slot.ItemId;
                int count = slot.Count;
                int durability = slot.CurrentDurability;
                string customName = slot.CustomName;

                CopySlot(_heldSlot, slot);

                _heldSlot.ItemId = itemId;
                _heldSlot.Count = count;
                _heldSlot.CurrentDurability = durability;
                _heldSlot.CustomName = customName;
            }

            EndDrag();
        }

        FireChanged();
        UpdateCursorVisual();
    }

    // =========================================================================
    // RIGHT CLICK
    // =========================================================================

    private void HandleRightClick(int slotIndex)
    {
        var slot = GetUiSlot(slotIndex);
        if (slot == null) return;

        if (_heldSlot.IsEmpty)
        {
            if (slot.IsEmpty) return;

            int half = Mathf.CeilToInt(slot.Count / 2f);

            _heldSlot.ItemId = slot.ItemId;
            _heldSlot.Count = half;
            _heldSlot.CurrentDurability = slot.CurrentDurability;
            _heldSlot.CustomName = slot.CustomName;

            _heldFromSlot = slotIndex;

            slot.Count -= half;

            if (slot.Count <= 0)
                slot.Clear();

            _dragMode = DragMode.RmbDrag;
            _dragVisited.Clear();
            _dragVisited.Add(slotIndex);
            _dragLastSlot = slotIndex;
        }
        else
        {
            PlaceOneIntoSlot(slotIndex);
        }

        FireChanged();
        UpdateCursorVisual();
    }

    private bool PlaceOneIntoSlot(int slotIndex)
    {
        if (_heldSlot.IsEmpty)
            return false;

        var slot = GetUiSlot(slotIndex);
        if (slot == null)
            return false;

        if (!slot.IsEmpty && slot.ItemId != _heldSlot.ItemId)
            return false;

        if (!IsStackableItem(_heldSlot.ItemId))
        {
            if (!slot.IsEmpty)
                return false;

            CopySlot(_heldSlot, slot);
            _heldSlot.Clear();
            return true;
        }

        if (!slot.IsEmpty && slot.Count >= _inventory.MaxStackSize)
            return false;

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
        if (_craftingPanel != null && _inventoryOpen)
        {
            int count = _craftingPanel.GetActiveSlotCount();

            for (int i = 0; i < count; i++)
            {
                var panel = _craftingPanel.GetSlotPanel(i);
                if (panel == null) continue;

                var mouse = GetViewport().GetMousePosition();

                if (new Rect2(panel.GlobalPosition, panel.Size).HasPoint(mouse))
                {
                    OnDragEnterCraftSlot(i);
                    return;
                }
            }
        }

        var slot = GetUiSlot(slotIndex);
        if (slot == null) return;

        switch (_dragMode)
        {
            case DragMode.LmbWithItem:
            {
                if (_heldSlot.IsEmpty)
                    return;

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

                if (!slot.IsEmpty && slot.ItemId != _heldSlot.ItemId)
                    return;

                if (!slot.IsEmpty && slot.Count >= _inventory.MaxStackSize)
                    return;

                if (!_dragVisited.Contains(slotIndex))
                    _dragVisited.Add(slotIndex);

                int n = _dragVisited.Count;
                int perSlot = _dragOrigCount / n;
                int leftover = _dragOrigCount - perSlot * n;

                foreach (int idx in _dragVisited)
                {
                    var s = GetUiSlot(idx);
                    if (s == null) continue;

                    s.ItemId = _heldSlot.ItemId;
                    s.Count = 0;
                }

                int remaining = _dragOrigCount;

                for (int i = 0; i < _dragVisited.Count; i++)
                {
                    var s = GetUiSlot(_dragVisited[i]);
                    if (s == null) continue;

                    int give = perSlot + (i == 0 ? leftover : 0);
                    give = Mathf.Min(give, _inventory.MaxStackSize);

                    s.Count = give;
                    remaining -= give;
                }

                _heldSlot.Count = Mathf.Max(0, remaining);

                if (_heldSlot.Count <= 0)
                    _heldSlot.Clear();

                break;
            }

            case DragMode.RmbDrag:
            {
                if (_heldSlot.IsEmpty || _dragVisited.Contains(slotIndex))
                    return;

                if (PlaceOneIntoSlot(slotIndex))
                    _dragVisited.Add(slotIndex);

                break;
            }

            case DragMode.LmbNoItem:
            {
                if (_dragVisited.Contains(slotIndex) || slot.IsEmpty)
                    return;

                if (!_heldSlot.IsEmpty && slot.ItemId != _heldSlot.ItemId)
                    return;

                if (!IsStackableItem(slot.ItemId))
                {
                    if (!_heldSlot.IsEmpty)
                        return;

                    CopySlot(slot, _heldSlot);
                    slot.Clear();
                    _dragVisited.Add(slotIndex);
                    break;
                }

                int space = _inventory.MaxStackSize -
                    (_heldSlot.IsEmpty ? 0 : _heldSlot.Count);

                if (space <= 0)
                    return;

                _dragVisited.Add(slotIndex);

                if (_heldSlot.IsEmpty)
                    _heldSlot.ItemId = slot.ItemId;

                int take = Mathf.Min(slot.Count, space);

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
            return;

        if (_heldSlot.IsEmpty)
            return;

        if (_craftingPanel.TryPlaceHeldItem(craftIdx, _heldSlot.ItemId))
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
        _dragMode = DragMode.None;
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
        var src = GetUiSlot(slotIndex);
        if (src == null || src.IsEmpty)
            return;

        // When a crate is open, shift-click moves items between
        // the crate and the player's inventory.
        if (_containerOpen && IsContainerSlot(slotIndex))
        {
            MoveSlotContents(src, 0, TotalSlots);
            return;
        }

        if (_containerOpen && !IsContainerSlot(slotIndex))
        {
            MoveSlotContents(src, TotalSlots, GetUiSlotCount());
            return;
        }

        // Normal inventory behavior when no crate is open.
        bool isHotbar = slotIndex >= MainInvSize;

        int destStart = isHotbar ? 0 : MainInvSize;
        int destEnd = isHotbar ? MainInvSize : TotalSlots;

        MoveSlotContents(src, destStart, destEnd);
    }

    private void MoveSlotContents(InventorySlot src, int destStart, int destEnd)
    {
        if (src == null || src.IsEmpty)
            return;

        if (IsStackableItem(src.ItemId))
        {
            for (int i = destStart; i < destEnd && src.Count > 0; i++)
            {
                var dst = GetUiSlot(i);

                if (dst == null || dst.IsEmpty || dst.ItemId != src.ItemId)
                    continue;

                int transfer = Mathf.Min(
                    _inventory.MaxStackSize - dst.Count,
                    src.Count
                );

                if (transfer <= 0)
                    continue;

                dst.Count += transfer;
                src.Count -= transfer;
            }
        }

        for (int i = destStart; i < destEnd && src.Count > 0; i++)
        {
            var dst = GetUiSlot(i);

            if (dst == null || !dst.IsEmpty)
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
        var clickedSlot = GetUiSlot(slotIndex);
        if (clickedSlot == null)
            return;

        if (_heldSlot.IsEmpty)
        {
            if (clickedSlot.IsEmpty)
                return;

            CopySlot(clickedSlot, _heldSlot);
            clickedSlot.Clear();
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

        string id = _heldSlot.ItemId;
        int slotCount = GetUiSlotCount();

        for (int i = 0; i < slotCount &&
             _heldSlot.Count < _inventory.MaxStackSize; i++)
        {
            if (i == slotIndex)
                continue;

            var slot = GetUiSlot(i);

            if (slot == null || slot.IsEmpty || slot.ItemId != id)
                continue;

            int take = Mathf.Min(
                slot.Count,
                _inventory.MaxStackSize - _heldSlot.Count
            );

            _heldSlot.Count += take;
            slot.Count -= take;

            if (slot.Count <= 0)
                slot.Clear();
        }

        FireChanged();
        UpdateCursorVisual();
    }

    // =========================================================================
    // SCROLL WHEEL IN INVENTORY
    // =========================================================================

    private void ScrollSlot(int slotIndex, bool up)
    {
        var src = GetUiSlot(slotIndex);
        if (src == null)
            return;

        bool isContainer = _containerOpen && IsContainerSlot(slotIndex);

        int fromStart;
        int fromEnd;

        if (isContainer)
        {
            fromStart = 0;
            fromEnd = TotalSlots;
        }
        else
        {
            bool isHotbar = slotIndex >= MainInvSize;
            fromStart = isHotbar ? 0 : MainInvSize;
            fromEnd = isHotbar ? MainInvSize : TotalSlots;
        }

        if (up)
        {
            for (int i = fromStart; i < fromEnd; i++)
            {
                var other = GetUiSlot(i);

                if (other == null || other.IsEmpty)
                    continue;

                if (!src.IsEmpty && other.ItemId != src.ItemId)
                    continue;

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
                for (int i = fromStart; i < fromEnd; i++)
                {
                    var dst = GetUiSlot(i);

                    if (dst == null || !dst.IsEmpty)
                        continue;

                    CopySlot(src, dst);
                    src.Clear();

                    FireChanged();
                    return;
                }

                return;
            }

            for (int i = fromStart; i < fromEnd; i++)
            {
                var dst = GetUiSlot(i);

                if (dst == null || dst.IsEmpty ||
                    dst.ItemId != src.ItemId ||
                    dst.Count >= _inventory.MaxStackSize)
                    continue;

                dst.Count++;
                src.Count--;

                if (src.Count <= 0)
                    src.Clear();

                FireChanged();
                return;
            }

            for (int i = fromStart; i < fromEnd; i++)
            {
                var dst = GetUiSlot(i);

                if (dst == null || !dst.IsEmpty)
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
        if (string.IsNullOrEmpty(itemId) || count <= 0)
            return count;

        int remaining = count;
        bool stackable = IsStackableItem(itemId);

        void TryAdd(int start, int end, bool stackOnly)
        {
            for (int i = start; i < end && remaining > 0; i++)
            {
                var slot = _inventory.Slots[i];

                if (stackOnly)
                {
                    if (!stackable || slot.IsEmpty || slot.ItemId != itemId)
                        continue;

                    int add = Mathf.Min(
                        _inventory.MaxStackSize - slot.Count,
                        remaining
                    );

                    if (add <= 0)
                        continue;

                    slot.Count += add;
                    remaining -= add;
                }
                else
                {
                    if (!slot.IsEmpty)
                        continue;

                    int add = stackable
                        ? Mathf.Min(_inventory.MaxStackSize, remaining)
                        : 1;

                    slot.ItemId = itemId;
                    slot.Count = add;
                    remaining -= add;

                    if (!stackable)
                    {
                        var item = ItemRegistry.Instance?.GetItem(itemId);

                        slot.CurrentDurability =
                            durability ?? item?.MaxDurability ?? 0;

                        slot.CustomName = customName;
                    }
                }
            }
        }

        // Hotbar first.
        TryAdd(MainInvSize, TotalSlots, true);
        TryAdd(MainInvSize, TotalSlots, false);

        // Then main inventory.
        TryAdd(0, MainInvSize, true);
        TryAdd(0, MainInvSize, false);

        _inventory.OnInventoryChanged?.Invoke();

        return remaining;
    }

    // =========================================================================
    // PICKUP
    // =========================================================================

    public int CollectPickup(
        string itemId,
        int count,
        int? durability = null,
        string customName = "")
    {
        return AddItemToInventory(itemId, count, durability, customName);
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
        if (string.IsNullOrEmpty(itemId) || count <= 0)
            return;

        var pickup = new ItemPickup();

        pickup.ItemId = itemId;
        pickup.Count = count;
        pickup.TossVelocity = tossVelocity;

        GetTree().Root.AddChild(pickup);
        pickup.GlobalPosition = worldPosition;
    }

    private void DropOneItem()
    {
        var gm = GameModeManager.Instance;

        if (gm != null && gm.IsStory)
            return;

        var slot = _inventory.Slots[MainInvSize + _selectedSlot];

        if (slot.IsEmpty)
            return;

        string itemId = slot.ItemId;

        slot.Count--;

        if (slot.Count <= 0)
            slot.Clear();

        FireChanged();

        Vector3 forward = -_rayCast.GlobalTransform.Basis.Z;

        Vector3 spawnPos =
            GlobalPosition +
            new Vector3(0, DropSpawnHeight, 0) +
            forward * 0.6f;

        Vector3 tossVel =
            forward * DropForwardSpeed +
            Vector3.Up * DropUpSpeed;

        SpawnItemDrop(itemId, 1, spawnPos, tossVel);
    }

    // =========================================================================
    // INVENTORY MOUSE INPUT
    // =========================================================================

    public override void _Input(InputEvent @event)
    {
        if (!_inventoryOpen)
            return;

        if (@event is InputEventMouseButton mb && !mb.Pressed)
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
            _dragMode = Input.IsKeyPressed(Key.Shift)
                ? DragMode.ShiftLmbNoItem
                : DragMode.LmbNoItem;

            _dragVisited.Clear();
            _dragLastSlot = -1;
        }
    }

    private int GetSlotUnderMouse()
    {
        var mouse = GetViewport().GetMousePosition();

        // Container slots first.
        if (_containerOpen)
        {
            for (int i = 0; i < _containerSlotPanels.Count; i++)
            {
                var panel = _containerSlotPanels[i];

                if (panel == null || !GodotObject.IsInstanceValid(panel))
                    continue;

                if (new Rect2(panel.GlobalPosition, panel.Size).HasPoint(mouse))
                    return TotalSlots + i;
            }
        }

        // Main inventory.
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

        // Hotbar.
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
        var cm = GetTree().Root.FindChild(
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
