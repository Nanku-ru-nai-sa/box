using Godot;
using System.Collections.Generic;

public partial class Player
{
    // =========================================================
    // CONTAINER STATE
    // =========================================================

    private bool _containerOpen = false;

    private Vector3I _openedCratePosition;

    // These are live references to the slots owned by ContainerManager.
    private List<InventorySlot> _activeContainerSlots = new();

    // Container UI.
    private Panel _containerPanel;
    private GridContainer _containerGrid;
    private Label _containerTitle;

    private readonly List<Panel> _containerSlotPanels = new();
    private readonly List<Label> _containerSlotLabels = new();

    // =========================================================
    // SHARED SLOT ACCESS
    // Player slots: 0-59
    // Container slots: 60+
    // =========================================================

    private int GetUiSlotCount()
    {
        return TotalSlots +
            (_containerOpen ? _activeContainerSlots.Count : 0);
    }

    private InventorySlot GetUiSlot(int index)
    {
        if (index < 0)
            return null;

        if (index < TotalSlots)
            return _inventory.Slots[index];

        int containerIndex = index - TotalSlots;

        if (!_containerOpen ||
            containerIndex < 0 ||
            containerIndex >= _activeContainerSlots.Count)
        {
            return null;
        }

        return _activeContainerSlots[containerIndex];
    }

    private bool IsContainerSlot(int index)
    {
        return _containerOpen &&
            index >= TotalSlots &&
            index < GetUiSlotCount();
    }

    // =========================================================
    // FIND THE BLOCK UNDER THE CROSSHAIR
    // =========================================================

    private bool TryGetTargetBlock(
        out Vector3I blockPosition,
        out string blockId)
    {
        blockPosition = Vector3I.Zero;
        blockId = "";

        if (_rayCast == null ||
            !_rayCast.IsColliding())
        {
            return false;
        }

        Node collider = _rayCast.GetCollider() as Node;

        if (collider == null ||
            !collider.HasMeta("chunk"))
        {
            return false;
        }

        Chunk chunk =
            collider.GetMeta("chunk").AsGodotObject() as Chunk;

        if (chunk == null)
            return false;

        Vector3 target =
            _rayCast.GetCollisionPoint() -
            _rayCast.GetCollisionNormal() * 0.5f;

        Vector3 local =
            target - chunk.GlobalPosition;

        int x = Mathf.FloorToInt(local.X);
        int y = Mathf.FloorToInt(local.Y);
        int z = Mathf.FloorToInt(local.Z);

        var block = chunk.GetBlock(x, y, z);

        blockPosition = new Vector3I(
            Mathf.FloorToInt(target.X),
            Mathf.FloorToInt(target.Y),
            Mathf.FloorToInt(target.Z)
        );

        blockId = block.BlockId;

        return true;
    }

    // =========================================================
    // TRY OPEN CRATE
    // =========================================================

    private bool TryOpenCrate()
    {
        if (!TryGetTargetBlock(
                out Vector3I position,
                out string blockId))
        {
            return false;
        }

        if (blockId != "crate")
            return false;

        var manager = ContainerManager.Instance;

        if (manager == null)
        {
            GD.PrintErr("ContainerManager is missing.");
            return true;
        }

        // Register in case this crate has not been registered yet.
        manager.RegisterCrate(position);

        var slots = manager.GetConnectedSlots(position);

        if (slots == null)
        {
            GD.PrintErr("Could not retrieve crate inventory.");
            return true;
        }

        _openedCratePosition = position;
        _activeContainerSlots = slots;

        OpenContainerWindow();

        return true;
    }

    // =========================================================
    // OPEN WINDOW
    // =========================================================

    private void OpenContainerWindow()
    {
        _containerOpen = true;
        _inventoryOpen = true;

        _inventoryScreen.Visible = true;

        if (_hotbarLayer != null)
            _hotbarLayer.Visible = false;

        if (_statsHudLayer != null)
            _statsHudLayer.Visible = false;

        if (_craftingPanel != null)
            _craftingPanel.Visible = false;

        if (_equipmentPanel != null)
            _equipmentPanel.Visible = false;

        if (_toolBenchPanel != null)
            _toolBenchPanel.Visible = false;

        if (_stationTabBar != null)
            _stationTabBar.Visible = false;

        EndDrag();

        Input.MouseMode = Input.MouseModeEnum.Visible;

        BuildContainerWindow();
        RefreshAllSlotVisuals();
    }

    // =========================================================
    // CLOSE WINDOW
    // =========================================================

    private void CloseContainerWindow()
    {
        if (!_containerOpen)
            return;

        ReturnHeldItemSafely();

        _containerOpen = false;
        _inventoryOpen = false;

        _activeContainerSlots.Clear();

        if (_containerPanel != null)
            _containerPanel.Visible = false;

        if (_inventoryScreen != null)
            _inventoryScreen.Visible = false;

        EndDrag();

        if (_hotbarLayer != null)
            _hotbarLayer.Visible = _hudVisible;

        if (_statsHudLayer != null)
            _statsHudLayer.Visible = _hudVisible;

        Input.MouseMode = Input.MouseModeEnum.Captured;

        RefreshAllSlotVisuals();
    }

    // =========================================================
    // RETURN HELD ITEM WITHOUT LOSING ITEM DATA
    // =========================================================

    private void ReturnHeldItemSafely()
    {
        if (_heldSlot == null ||
            _heldSlot.IsEmpty)
        {
            return;
        }

        bool returned = false;

        // Try the original slot first.
        InventorySlot original = GetUiSlot(_heldFromSlot);

        if (original != null)
        {
            if (original.IsEmpty)
            {
                CopySlot(_heldSlot, original);
                returned = true;
            }
            else if (
                original.ItemId == _heldSlot.ItemId &&
                original.Count < _inventory.MaxStackSize)
            {
                int amount = Mathf.Min(
                    _inventory.MaxStackSize - original.Count,
                    _heldSlot.Count
                );

                original.Count += amount;
                _heldSlot.Count -= amount;

                returned = _heldSlot.Count <= 0;
            }
        }

        // Try the player's inventory if the original slot cannot take it.
        if (!returned && !_heldSlot.IsEmpty)
        {
            int remaining = AddItemToInventory(
                _heldSlot.ItemId,
                _heldSlot.Count
            );

            returned = remaining <= 0;

            if (!returned)
            {
                SpawnItemDrop(_heldSlot.ItemId, remaining, GlobalPosition + Vector3.Up);
            }
        }

        _heldSlot.Clear();
        _heldFromSlot = -1;

        UpdateCursorVisual();

        ContainerManager.Instance?.SaveContainers();
    }
}