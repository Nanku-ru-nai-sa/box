using Godot;
using System;
using System.Collections.Generic;

public partial class Player : CharacterBody3D
{
    // =========================================================================
    // HOTBAR HUD
    // =========================================================================

    private void BuildHotbarHUD()
    {
        _hotbarLayer = new CanvasLayer();
        GetTree().Root.CallDeferred("add_child", _hotbarLayer);

        float totalWidth = HotbarSize * SlotSize + (HotbarSize - 1) * SlotGap;

        var container = new HBoxContainer();
        container.AnchorLeft   = 0.5f;
        container.AnchorRight  = 0.5f;
        container.AnchorTop    = 1.0f;
        container.AnchorBottom = 1.0f;

        container.OffsetLeft   = -totalWidth / 2f;
        container.OffsetRight  = totalWidth / 2f;
        container.OffsetTop    = -(SlotSize + 10);
        container.OffsetBottom = -10;

        container.AddThemeConstantOverride("separation", SlotGap);

        for (int i = 0; i < HotbarSize; i++)
        {
            var slot = MakeSlotPanel(SlotSize);

            slot.AddChild(MakeSlotTexRect());
            slot.AddChild(MakeCountLabel(9));
            slot.AddChild(MakeKeyHintLabel(i));

            _hotbarLabels[i] = slot.GetChild<Label>(1);

            container.AddChild(slot);
            _hotbarSlots[i] = slot;

            // Persistent HUD hotbar tooltip.
            slot.MouseFilter = Control.MouseFilterEnum.Stop;

            int idx = i;

            slot.MouseEntered += () =>
                OnHudHotbarSlotMouseEntered(idx);

            slot.MouseExited += () =>
                _hotbarTooltip?.HideTooltip();
        }

        _hotbarLayer.CallDeferred("add_child", container);

        _hotbarTooltip = new ItemTooltip();

        // Added after the container so it renders above the slots.
        _hotbarLayer.CallDeferred("add_child", _hotbarTooltip);
    }

    // =========================================================================
    // INVENTORY SCREEN
    // =========================================================================

    private void BuildInventoryScreen()
    {
        _inventoryLayer = new CanvasLayer();
        _inventoryLayer.Layer = 10;

        GetTree().Root.CallDeferred(
            "add_child",
            _inventoryLayer
        );

        float gridW =
            HotbarSize * SlotSize +
            (HotbarSize - 1) * SlotGap;

        float mainH =
            4 * SlotSize +
            3 * SlotGap;

        float sectionGap = 14f;
        float topPad      = 14f;
        float pad         = 16f;

        float totalW =
            gridW +
            pad * 2f;

        float totalH =
            topPad +
            mainH +
            sectionGap +
            SlotSize +
            pad;

        _inventoryScreen = new Panel();

        _inventoryScreen.AnchorLeft   = 0.5f;
        _inventoryScreen.AnchorRight  = 0.5f;
        _inventoryScreen.AnchorTop    = 0.5f;
        _inventoryScreen.AnchorBottom = 0.5f;

        _inventoryScreen.OffsetLeft   = -totalW / 2f;
        _inventoryScreen.OffsetRight  = totalW / 2f;
        _inventoryScreen.OffsetTop    = -180f;
        _inventoryScreen.OffsetBottom = -180f + totalH;

        _inventoryScreen.AddThemeStyleboxOverride(
            "panel",
            MakePanelStyle(
                new Color(0.08f, 0.08f, 0.08f, 0.95f),
                new Color(0.5f, 0.5f, 0.5f)
            )
        );

        // ---------------------------------------------------------------------
        // Main inventory grid
        // ---------------------------------------------------------------------

        var mainGrid = new GridContainer();

        mainGrid.Columns  = HotbarSize;
        mainGrid.Position = new Vector2(pad, topPad);

        mainGrid.AddThemeConstantOverride(
            "h_separation",
            SlotGap
        );

        mainGrid.AddThemeConstantOverride(
            "v_separation",
            SlotGap
        );

        for (int i = 0; i < MainInvSize; i++)
        {
            var slot = MakeSlotPanel(SlotSize);

            slot.AddChild(MakeSlotTexRect());
            slot.AddChild(MakeCountLabel(10));

            _invSlotPanels[i] = slot;
            _invSlotLabels[i] = slot.GetChild<Label>(1);

            int idx = i;

            slot.GuiInput +=
                (InputEvent ev) =>
                    OnInvSlotInput(ev, idx);

            slot.MouseEntered +=
                () =>
                    OnSlotMouseEntered(idx);

            slot.MouseFilter =
                Control.MouseFilterEnum.Stop;

            slot.MouseEntered += () =>
                slot.AddThemeStyleboxOverride(
                    "panel",
                    MakePanelStyle(
                        new Color(0.15f, 0.15f, 0.15f, 0.85f),
                        new Color(0.75f, 0.75f, 0.75f)
                    )
                );

            slot.MouseExited += () =>
                slot.AddThemeStyleboxOverride(
                    "panel",
                    MakePanelStyle(
                        new Color(0.15f, 0.15f, 0.15f, 0.85f),
                        new Color(0.4f, 0.4f, 0.4f)
                    )
                );

            slot.MouseExited += () =>
                _invTooltip?.HideTooltip();

            mainGrid.AddChild(slot);
        }

        _inventoryScreen.AddChild(mainGrid);

        // ---------------------------------------------------------------------
        // Divider
        // ---------------------------------------------------------------------

        var divider = new ColorRect();

        divider.Color =
            new Color(
                0.35f,
                0.35f,
                0.35f
            );

        divider.Position =
            new Vector2(
                pad,
                topPad +
                mainH +
                sectionGap / 2f -
                1f
            );

        divider.Size =
            new Vector2(
                gridW,
                2f
            );

        _inventoryScreen.AddChild(divider);

        // ---------------------------------------------------------------------
        // Inventory hotbar row
        // ---------------------------------------------------------------------

        var hotbarRow = new HBoxContainer();

        hotbarRow.Position =
            new Vector2(
                pad,
                topPad +
                mainH +
                sectionGap
            );

        hotbarRow.AddThemeConstantOverride(
            "separation",
            SlotGap
        );

        for (int i = 0; i < HotbarSize; i++)
        {
            var slot = MakeSlotPanel(SlotSize);

            slot.AddChild(MakeSlotTexRect());
            slot.AddChild(MakeCountLabel(10));
            slot.AddChild(MakeKeyHintLabel(i));

            _invHotbarSlots[i]  = slot;
            _invHotbarLabels[i] = slot.GetChild<Label>(1);

            int idx = i;

            slot.GuiInput +=
                (InputEvent ev) =>
                    OnHotbarInvSlotInput(ev, idx);

            slot.MouseEntered +=
                () =>
                    OnSlotMouseEntered(
                        MainInvSize + idx
                    );

            slot.MouseExited += () =>
                _invTooltip?.HideTooltip();

            slot.MouseFilter =
                Control.MouseFilterEnum.Stop;

            hotbarRow.AddChild(slot);
        }

        _inventoryScreen.AddChild(hotbarRow);

        // Tooltip goes last so it renders above every slot.
        _invTooltip = new ItemTooltip();

        _inventoryScreen.AddChild(_invTooltip);

        _inventoryScreen.Visible = false;

        _inventoryLayer.CallDeferred(
            "add_child",
            _inventoryScreen
        );
    }

// =========================================================================
// CONTAINER WINDOW
// =========================================================================




private void BuildContainerWindow()
{
    // Remove the previous crate window and title if reopening.
    if (_containerPanel != null &&
        GodotObject.IsInstanceValid(_containerPanel))
    {
        _containerPanel.QueueFree();
    }

    if (_containerTitle != null &&
        GodotObject.IsInstanceValid(_containerTitle))
    {
        _containerTitle.QueueFree();
    }

    _containerPanel = null;
    _containerTitle = null;

    _containerSlotPanels.Clear();
    _containerSlotLabels.Clear();

    if (_activeContainerSlots == null ||
        _activeContainerSlots.Count == 0)
        return;

   int slotCount = _activeContainerSlots.Count;

int columns;
int rows;

if (_openedContainerBlockId == ContainerManager.BarrelBlockId)
{
    // Barrels always display as a 4x4 grid.
    columns = 4;
    rows = 4;
}
else
{
    // Preserve the existing connected crate layout.
    int crateCount = Mathf.CeilToInt(slotCount / 4f);
    columns = Mathf.Min(12, Mathf.Max(2, crateCount * 2));
    rows = Mathf.CeilToInt(slotCount / (float)columns);
}

    float pad = 16f;

    float gridWidth =
        columns * SlotSize +
        (columns - 1) * SlotGap;

    float gridHeight =
        rows * SlotSize +
        (rows - 1) * SlotGap;

    float panelWidth = gridWidth + pad * 2f;

    float panelHeight =
        pad +
        gridHeight +
        pad;

    _containerPanel = new Panel();

    _containerPanel.AnchorLeft = 0.5f;
    _containerPanel.AnchorRight = 0.5f;
    _containerPanel.AnchorTop = 0.5f;
    _containerPanel.AnchorBottom = 0.5f;

    _containerPanel.OffsetLeft = -panelWidth / 2f;
    _containerPanel.OffsetRight = panelWidth / 2f;

    _containerPanel.OffsetTop =
        -190f - panelHeight;

    _containerPanel.OffsetBottom = -190f;

    _containerPanel.AddThemeStyleboxOverride(
        "panel",
        MakePanelStyle(
            new Color(0.08f, 0.08f, 0.08f, 0.95f),
            new Color(0.5f, 0.5f, 0.5f)
        )
    );

    // Crate heading OUTSIDE the panel.
    _containerTitle = new Label();
    _containerTitle.Text =
    _openedContainerBlockId == ContainerManager.BarrelBlockId
        ? "Barrel"
        : "Crate";

    _containerTitle.AnchorLeft = 0.5f;
    _containerTitle.AnchorRight = 0.5f;
    _containerTitle.AnchorTop = 0.5f;
    _containerTitle.AnchorBottom = 0.5f;

    _containerTitle.OffsetLeft = -panelWidth / 2f;
    _containerTitle.OffsetRight = panelWidth / 2f;

    // Match the Craft panel title position.
    _containerTitle.OffsetTop =
        -190f - panelHeight - 24f;

    _containerTitle.OffsetBottom =
        -190f - panelHeight;

    _containerTitle.HorizontalAlignment =
        HorizontalAlignment.Left;

    _containerTitle.VerticalAlignment =
        VerticalAlignment.Center;

    _containerTitle.AddThemeFontSizeOverride("font_size", 13);

    _containerTitle.AddThemeColorOverride(
        "font_color",
        new Color(0.75f, 0.75f, 0.75f)
    );

    _containerTitle.MouseFilter =
        Control.MouseFilterEnum.Ignore;

    // Storage grid.
    _containerGrid = new GridContainer();
    _containerGrid.Columns = columns;

    _containerGrid.Position = new Vector2(
        pad,
        pad
    );

    _containerGrid.AddThemeConstantOverride(
        "h_separation",
        SlotGap
    );

    _containerGrid.AddThemeConstantOverride(
        "v_separation",
        SlotGap
    );

    for (int i = 0; i < slotCount; i++)
    {
        var slotPanel = MakeSlotPanel(SlotSize);

        slotPanel.AddChild(MakeSlotTexRect());
        slotPanel.AddChild(MakeCountLabel(10));

        int idx = i;

        slotPanel.GuiInput += (InputEvent ev) =>
            OnContainerSlotInput(ev, idx);

        slotPanel.MouseEntered += () =>
            OnSlotMouseEntered(TotalSlots + idx);

        slotPanel.MouseExited += () =>
            _invTooltip?.HideTooltip();

        slotPanel.MouseFilter =
            Control.MouseFilterEnum.Stop;

        slotPanel.MouseEntered += () =>
            slotPanel.AddThemeStyleboxOverride(
                "panel",
                MakePanelStyle(
                    new Color(0.15f, 0.15f, 0.15f, 0.85f),
                    new Color(0.75f, 0.75f, 0.75f)
                )
            );

        slotPanel.MouseExited += () =>
            slotPanel.AddThemeStyleboxOverride(
                "panel",
                MakePanelStyle(
                    new Color(0.15f, 0.15f, 0.15f, 0.85f),
                    new Color(0.4f, 0.4f, 0.4f)
                )
            );

        _containerSlotPanels.Add(slotPanel);
        _containerSlotLabels.Add(slotPanel.GetChild<Label>(1));

        _containerGrid.AddChild(slotPanel);
    }

    _containerPanel.AddChild(_containerGrid);

    _inventoryLayer.AddChild(_containerPanel);
    _inventoryLayer.AddChild(_containerTitle);

    RefreshContainerSlotVisuals();
}


// =========================================================================
// CONTAINER SLOT VISUALS
// =========================================================================

private void RefreshContainerSlotVisuals()
{
    if (!_containerOpen ||
        _activeContainerSlots == null)
        return;

    int count = Mathf.Min(
        _activeContainerSlots.Count,
        _containerSlotPanels.Count
    );

    for (int i = 0; i < count; i++)
    {
        var slot = _activeContainerSlots[i];

        var panel = _containerSlotPanels[i];

        if (slot == null ||
            panel == null ||
            !GodotObject.IsInstanceValid(panel))
            continue;

        panel.GetChild<TextureRect>(0).Texture =
            slot.IsEmpty
                ? null
                : GetItemIcon(slot.ItemId);

        _containerSlotLabels[i].Text =
            !slot.IsEmpty && slot.Count > 1
                ? slot.Count.ToString()
                : "";
    }
}

    
private void OnSlotMouseEntered(int slotIndex)
{
    if (_invTooltip == null)
        return;

    var slot = GetUiSlot(slotIndex);
    var control = GetSlotControl(slotIndex);

    if (slot == null ||
        slot.IsEmpty ||
        control == null)
    {
        _invTooltip.HideTooltip();
        return;
    }

    var item = ItemRegistry.Instance?.GetItem(slot.ItemId);

    if (item == null)
    {
        _invTooltip.HideTooltip();
        return;
    }

    _invTooltip.ShowFor(
        item,
        control,
        slot.CurrentDurability
    );
}


    // =========================================================================
    // CURSOR ITEM PANEL
    // =========================================================================

    private void BuildCursorPanel()
    {
        _cursorLayer = new CanvasLayer();
        _cursorLayer.Layer = 20;

        GetTree().Root.CallDeferred(
            "add_child",
            _cursorLayer
        );

        _cursorPanel =
            MakeSlotPanel(SlotSize);

        _cursorPanel.AddThemeStyleboxOverride(
            "panel",
            MakePanelStyle(
                new Color(0.25f, 0.25f, 0.25f, 0.9f),
                new Color(1f, 0.9f, 0.2f),
                2
            )
        );

        _cursorPanel.MouseFilter =
            Control.MouseFilterEnum.Ignore;

        _cursorPanel.Visible = false;

        _cursorTex =
            MakeSlotTexRect();

        _cursorLabel =
            MakeCountLabel(11);

        _cursorPanel.AddChild(_cursorTex);
        _cursorPanel.AddChild(_cursorLabel);

        _cursorLayer.CallDeferred(
            "add_child",
            _cursorPanel
        );
    }

    // =========================================================================
    // CALENDAR
    // =========================================================================

    private void BuildCalendarPanel()
    {
        _calendarLayer = new CanvasLayer();
        _calendarLayer.Layer = 12;

        GetTree().Root.CallDeferred(
            "add_child",
            _calendarLayer
        );

        _calendarPanel =
            new CalendarPanel();

        _calendarLayer.CallDeferred(
            "add_child",
            _calendarPanel
        );
    }

    // =========================================================================
    // CRAFTING
    // =========================================================================

    private void BuildCraftingPanel()
    {
        _craftingLayer = new CanvasLayer();
        _craftingLayer.Layer = 11;

        GetTree().Root.CallDeferred(
            "add_child",
            _craftingLayer
        );

        _craftingPanel =
            new CraftingPanel();

        _craftingPanel.Init(_inventory);

        _craftingPanel.OnSlotClicked +=
            HandleCraftSlotClicked;

        _craftingPanel.OnOutputClicked +=
            HandleOutputClicked;

        _craftingPanel.OnLearnedCraftClicked +=
            HandleLearnedCraftClicked;

        float gridW =
            HotbarSize * SlotSize +
            (HotbarSize - 1) * SlotGap;

        float pad = 16f;

        float totalW =
            gridW +
            pad * 2f;

        float invHalfW =
            totalW / 2f;

        _craftingPanel.AnchorLeft   = 0.5f;
        _craftingPanel.AnchorRight  = 0.5f;
        _craftingPanel.AnchorTop    = 0.5f;
        _craftingPanel.AnchorBottom = 0.5f;

        _craftingPanel.OffsetLeft =
            -invHalfW - 10f - 300f;

        _craftingPanel.OffsetRight =
            -invHalfW - 10f;

        _craftingPanel.OffsetTop =
            -180f;

        _craftingPanel.OffsetBottom =
            159f;

        _craftingPanel.Visible = false;

        _craftingLayer.CallDeferred(
            "add_child",
            _craftingPanel
        );
    }

    // =========================================================================
    // TOOL BENCH
    // =========================================================================

    private void BuildToolBenchPanel()
    {
        // Shares the crafting layer so both panels
        // occupy the same UI layer.

        _toolBenchPanel =
            new ToolBenchPanel();

        _toolBenchPanel.Init(_inventory);

        _toolBenchPanel.OnSlotClicked +=
            HandleToolBenchSlotClicked;

        _toolBenchPanel.OnCenterClicked +=
            HandleToolBenchCenterClicked;

        _toolBenchPanel.OnOutputClicked +=
            HandleToolBenchOutputClicked;

        float gridW =
            HotbarSize * SlotSize +
            (HotbarSize - 1) * SlotGap;

        float pad = 16f;

        float totalW =
            gridW +
            pad * 2f;

        float invHalfW =
            totalW / 2f;

        _toolBenchPanel.AnchorLeft   = 0.5f;
        _toolBenchPanel.AnchorRight  = 0.5f;
        _toolBenchPanel.AnchorTop    = 0.5f;
        _toolBenchPanel.AnchorBottom = 0.5f;

        _toolBenchPanel.OffsetLeft =
            -invHalfW - 10f - 300f;

        _toolBenchPanel.OffsetRight =
            -invHalfW - 10f;

        _toolBenchPanel.OffsetTop =
            -180f;

        _toolBenchPanel.OffsetBottom =
            159f;

        _toolBenchPanel.Visible = false;

        _craftingLayer.CallDeferred(
            "add_child",
            _toolBenchPanel
        );
    }

    // =========================================================================
    // STATION TAB BAR
    // =========================================================================

    // =========================================================================
    // KILN
    // =========================================================================

    private void BuildKilnPanel()
    {
        _kilnPanel = new KilnPanel();
        _kilnPanel.OnSlotClicked += HandleKilnSlotClicked;
        _kilnPanel.OnOutputClicked += HandleKilnOutputClicked;

        float gridW = HotbarSize * SlotSize + (HotbarSize - 1) * SlotGap;
        float totalW = gridW + 32f;
        float invHalfW = totalW / 2f;

        _kilnPanel.AnchorLeft = 0.5f;
        _kilnPanel.AnchorRight = 0.5f;
        _kilnPanel.AnchorTop = 0.5f;
        _kilnPanel.AnchorBottom = 0.5f;
        _kilnPanel.OffsetLeft = -invHalfW - 10f - 300f;
        _kilnPanel.OffsetRight = -invHalfW - 10f;
        _kilnPanel.OffsetTop = -180f;
        _kilnPanel.OffsetBottom = 159f;
        _kilnPanel.Visible = false;
        _craftingLayer.CallDeferred("add_child", _kilnPanel);
    }

    private void BuildStationTabBar()
    {
        _stationTabBar = new Control();

        _stationTabBar.AnchorLeft = 0.5f;
        _stationTabBar.AnchorRight = 0.5f;
        _stationTabBar.AnchorTop = 0.5f;
        _stationTabBar.AnchorBottom = 0.5f;

        float gridW = HotbarSize * SlotSize + (HotbarSize - 1) * SlotGap;
        float totalW = gridW + 32f;
        float invHalfW = totalW / 2f;

        _stationTabBar.OffsetLeft = -invHalfW - 10f - 300f;
        _stationTabBar.OffsetRight = -invHalfW - 10f;
        _stationTabBar.OffsetTop = -180f - 62f;
        _stationTabBar.OffsetBottom = -180f - 4f;
        _stationTabBar.Visible = false;

        const float slotSize = 56f;
        const float gap = 5f;

        for (int i = 0; i < 4; i++)
        {
            int index = i;
            var slot = new Panel();
            slot.Position = new Vector2(i * (slotSize + gap), 0f);
            slot.CustomMinimumSize = new Vector2(slotSize, slotSize);
            slot.MouseFilter = Control.MouseFilterEnum.Stop;
            slot.TooltipText = "";

            var icon = new TextureRect();
            icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            icon.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
            icon.AnchorLeft = 0.5f;
            icon.AnchorRight = 0.5f;
            icon.AnchorTop = 0.5f;
            icon.AnchorBottom = 0.5f;
            icon.OffsetLeft = -20f;
            icon.OffsetRight = 20f;
            icon.OffsetTop = -20f;
            icon.OffsetBottom = 20f;
            icon.MouseFilter = Control.MouseFilterEnum.Ignore;
            slot.AddChild(icon);

            slot.GuiInput += (InputEvent ev) =>
            {
                if (IsStationTabClick(ev))
                    HandleStationSlotClick(index);
            };

            slot.MouseEntered += () =>
            {
                if (_stationSlotIds[index] != null)
                    SetStationTabStyle(slot, true);
            };
            slot.MouseExited += () => RefreshStationSlotStyles();

            _stationSlots[i] = slot;
            _stationSlotIcons[i] = icon;
            _stationTabBar.AddChild(slot);
        }

        _craftingLayer.CallDeferred("add_child", _stationTabBar);
        UpdateStationSelector(new List<string>());
    }

    private void UpdateStationSelector(List<(string Id, Vector3I Position, float Distance)> nearbyStations)
    {
        if (_stationTabBar == null)
            return;

        _stationSlotIds[0] = "default";
        SetStationSlot(0, "crafter", "Hand Crafting (2x2)");

        for (int i = 1; i < 4; i++)
        {
            if (i - 1 < nearbyStations.Count)
            {
                var station = nearbyStations[i - 1];
                string id = station.Id;
                string name = id switch
                {
                    "crafter" => "Crafter (3x3)",
                    "tool_bench" => "Tool Bench",
                    "kiln" => "Kiln",
                    _ => id
                };
                _stationSlotIds[i] = id;
                _stationSlotPositions[i] = station.Position;
                SetStationSlot(i, id, name);
            }
            else
            {
                _stationSlotIds[i] = null;
                _stationSlots[i].Visible = false;
            }
        }

        _stationSlots[0].Visible = true;
        _stationTabBar.Visible = _inventoryOpen;
        RefreshStationSlotStyles();
    }

    private void SetStationSlot(int index, string itemId, string tooltip)
    {
        _stationSlots[index].Visible = true;
        _stationSlots[index].TooltipText = tooltip;
        _stationSlotIcons[index].Texture = GetItemIcon(itemId);
    }

    private void HandleStationSlotClick(int index)
    {
        if (index < 0 || index >= _stationSlotIds.Length)
            return;

        string stationId = _stationSlotIds[index];
        if (string.IsNullOrEmpty(stationId))
            return;

        if (stationId == "default")
        {
            if (_activeStation == "default")
                _craftingPanel?.ToggleRecipeMemory();
            else
            {
                SwitchStation("default");
                _craftingPanel?.ShowCraftingGrid();
            }
            return;
        }

        _activeStationPosition = _stationSlotPositions[index];

        if (stationId == "kiln")
        {
            _kilnPanel?.SetKiln(
                _activeStationPosition,
                KilnManager.Instance?.GetSlots(_activeStationPosition)
            );
            _kilnPanelHasPosition = true;
        }

        SwitchStation(stationId);
    }

    private bool IsStationTabClick(InputEvent ev) =>
        ev is InputEventMouseButton mb &&
        mb.Pressed &&
        mb.ButtonIndex == MouseButton.Left;

    private void SwitchStation(string stationId)
    {
        _activeStation = stationId;

        if (_craftingPanel != null)
        {
            _craftingPanel.Visible =
                stationId == "default" || stationId == "crafter";

            if (stationId == "default")
            {
                _craftingPanel.SetGridSize(2, false);
                _craftingPanel.ShowCraftingGrid();
            }
            else if (stationId == "crafter")
            {
                _craftingPanel.SetGridSize(3, true);
                _craftingPanel.ShowCraftingGrid();
            }
        }

        if (_toolBenchPanel != null)
            _toolBenchPanel.Visible = stationId == "tool_bench";

        if (_kilnPanel != null)
            _kilnPanel.Visible = stationId == "kiln";

        RefreshStationSlotStyles();
    }

    private void RefreshStationSlotStyles()
    {
        for (int i = 0; i < _stationSlots.Length; i++)
        {
            if (_stationSlots[i] == null || !_stationSlots[i].Visible)
                continue;

            SetStationTabStyle(
                _stationSlots[i],
                !string.IsNullOrEmpty(_stationSlotIds[i]) &&
                _stationSlotIds[i] == _activeStation
            );
        }
    }

    private void SetStationTabStyle(Panel slot, bool active)
    {
        var style = new StyleBoxFlat();
        style.BgColor = active
            ? new Color(0.2f, 0.2f, 0.25f)
            : new Color(0.12f, 0.12f, 0.15f);
        style.BorderColor = active
            ? new Color(0.75f, 0.75f, 0.85f)
            : new Color(0.3f, 0.3f, 0.35f);
        style.BorderWidthTop = 2;
        style.BorderWidthBottom = 2;
        style.BorderWidthLeft = 2;
        style.BorderWidthRight = 2;
        style.CornerRadiusTopLeft = 3;
        style.CornerRadiusTopRight = 3;
        style.CornerRadiusBottomLeft = 3;
        style.CornerRadiusBottomRight = 3;
        slot.AddThemeStyleboxOverride("panel", style);
    }

    // =========================================================================
    // EQUIPMENT
    // =========================================================================

    private void BuildEquipmentPanel()
    {
        _equipmentLayer = new CanvasLayer();
        _equipmentLayer.Layer = 11;

        GetTree().Root.CallDeferred(
            "add_child",
            _equipmentLayer
        );

        _equipmentPanel =
            new EquipmentPanel();

        float gridW =
            HotbarSize * SlotSize +
            (HotbarSize - 1) * SlotGap;

        float pad = 16f;

        float totalW =
            gridW +
            pad * 2f;

        float invHalfW =
            totalW / 2f;

        _equipmentPanel.AnchorLeft   = 0.5f;
        _equipmentPanel.AnchorRight  = 0.5f;
        _equipmentPanel.AnchorTop    = 0.5f;
        _equipmentPanel.AnchorBottom = 0.5f;

        _equipmentPanel.OffsetLeft =
            invHalfW + 10f;

        _equipmentPanel.OffsetRight =
            invHalfW + 10f + 300f;

        _equipmentPanel.OffsetTop =
            -180f;

        _equipmentPanel.OffsetBottom =
            159f;

        _equipmentPanel.Visible = false;

        _equipmentLayer.CallDeferred(
            "add_child",
            _equipmentPanel
        );
    }

    // =========================================================================
    // CREATIVE MENU
    // =========================================================================

    private void BuildCreativeMenu()
    {
        _creativeMenuLayer = new CanvasLayer();
        _creativeMenuLayer.Layer = 12;

        GetTree().Root.CallDeferred(
            "add_child",
            _creativeMenuLayer
        );

        _creativeMenu =
            new CreativeMenu();

        float w =
            CreativeMenu.TotalWidth;

        float h =
            CreativeMenu.TotalHeight;

        _creativeMenu.AnchorLeft   = 0.5f;
        _creativeMenu.AnchorRight  = 0.5f;
        _creativeMenu.AnchorTop    = 0.5f;
        _creativeMenu.AnchorBottom = 0.5f;

        _creativeMenu.OffsetLeft =
            -w / 2f;

        _creativeMenu.OffsetRight =
            w / 2f;

        _creativeMenu.OffsetTop =
            -h / 2f;

        _creativeMenu.OffsetBottom =
            h / 2f;

        _creativeMenu.Visible = false;

        _creativeMenu.OnItemChosen +=
            OnCreativeItemChosen;

        _creativeMenuLayer.CallDeferred(
            "add_child",
            _creativeMenu
        );

        _creativeMenu.CallDeferred(
            nameof(CreativeMenu.Init)
        );
    }

    // =========================================================================
    // STATS HUD
    // =========================================================================

    private void BuildStatsHud()
    {
        _statsHudLayer =
            new CanvasLayer();

        GetTree().Root.CallDeferred(
            "add_child",
            _statsHudLayer
        );

        _statsHud =
            new StatsHud();

        _statsHud.AnchorLeft     = 0.5f;
        _statsHud.AnchorRight    = 0.5f;
        _statsHud.AnchorTop      = 1f;
        _statsHud.AnchorBottom   = 1f;

        _statsHud.OffsetLeft     = 0f;
        _statsHud.OffsetRight    = 0f;
        _statsHud.OffsetTop      = -74f;
        _statsHud.OffsetBottom   = -74f;

        _statsHud.GrowHorizontal =
            Control.GrowDirection.Both;

        _statsHud.GrowVertical =
            Control.GrowDirection.Begin;

        _statsHud.MouseFilter =
            Control.MouseFilterEnum.Ignore;

        _statsHud.Init(_stats);

        _statsHudLayer.CallDeferred(
            "add_child",
            _statsHud
        );
    }

    // =========================================================================
    // SLOT FACTORY
    // =========================================================================

    private Panel MakeSlotPanel(int size)
    {
        var slot = new Panel();

        slot.CustomMinimumSize =
            new Vector2(size, size);

        slot.AddThemeStyleboxOverride(
            "panel",
            MakePanelStyle(
                new Color(
                    0.15f,
                    0.15f,
                    0.15f,
                    0.85f
                ),
                new Color(
                    0.4f,
                    0.4f,
                    0.4f
                )
            )
        );

        return slot;
    }

    private TextureRect MakeSlotTexRect()
    {
        var tex =
            new TextureRect();

        tex.ExpandMode =
            TextureRect.ExpandModeEnum.IgnoreSize;

        tex.StretchMode =
            TextureRect.StretchModeEnum.KeepAspectCentered;

        tex.TextureFilter =
            CanvasItem.TextureFilterEnum.Nearest;

        tex.AnchorRight  = 1f;
        tex.AnchorBottom = 1f;

        tex.OffsetLeft   = 6;
        tex.OffsetTop    = 6;
        tex.OffsetRight  = -6;
        tex.OffsetBottom = -6;

        tex.MouseFilter =
            Control.MouseFilterEnum.Ignore;

        return tex;
    }

    private Label MakeCountLabel(int fontSize)
    {
        var lbl = new Label();

        lbl.HorizontalAlignment =
            HorizontalAlignment.Right;

        lbl.VerticalAlignment =
            VerticalAlignment.Bottom;

        lbl.AnchorRight  = 1.0f;
        lbl.AnchorBottom = 1.0f;

        lbl.OffsetRight  = -3f;
        lbl.OffsetBottom = -3f;

        lbl.AddThemeFontSizeOverride(
            "font_size",
            fontSize
        );

        lbl.AutowrapMode =
            TextServer.AutowrapMode.Off;

        lbl.MouseFilter =
            Control.MouseFilterEnum.Ignore;

        return lbl;
    }

    private Label MakeKeyHintLabel(int i)
    {
        var lbl = new Label();

        lbl.Text =
            i < 9
                ? (i + 1).ToString()
                : i == 9
                    ? "0"
                    : i == 10
                        ? "-"
                        : "+";

        lbl.HorizontalAlignment =
            HorizontalAlignment.Right;

        lbl.AnchorRight = 1.0f;

        lbl.OffsetRight = -3f;
        lbl.OffsetTop   = 2f;

        lbl.AddThemeFontSizeOverride(
            "font_size",
            8
        );

        lbl.AddThemeColorOverride(
            "font_color",
            new Color(
                0.55f,
                0.55f,
                0.55f
            )
        );

        lbl.MouseFilter =
            Control.MouseFilterEnum.Ignore;

        return lbl;
    }

    private StyleBoxFlat MakePanelStyle(
        Color bg,
        Color border,
        int bw = 2)
    {
        var s =
            new StyleBoxFlat();

        s.BgColor =
            bg;

        s.BorderColor =
            border;

        s.BorderWidthTop =
            bw;

        s.BorderWidthBottom =
            bw;

        s.BorderWidthLeft =
            bw;

        s.BorderWidthRight =
            bw;

        s.CornerRadiusTopLeft     = 3;
        s.CornerRadiusTopRight    = 3;
        s.CornerRadiusBottomLeft  = 3;
        s.CornerRadiusBottomRight = 3;

        return s;
    }

    // =========================================================================
    // SLOT LOOKUP / TOOLTIP
    // =========================================================================

    
private Control GetSlotControl(int slotIndex)
{
    // Container slots
    if (_containerOpen && IsContainerSlot(slotIndex))
    {
        int containerIndex = slotIndex - TotalSlots;

        if (containerIndex >= 0 &&
            containerIndex < _containerSlotPanels.Count)
        {
            return _containerSlotPanels[containerIndex];
        }

        return null;
    }

    // Main inventory
    if (slotIndex >= 0 && slotIndex < MainInvSize)
        return _invSlotPanels[slotIndex];

    // Hotbar
    int hotbarIndex = slotIndex - MainInvSize;

    if (hotbarIndex >= 0 && hotbarIndex < HotbarSize)
        return _invHotbarSlots[hotbarIndex];

    return null;
}


    private void OnHudHotbarSlotMouseEntered(int hotbarIdx)
    {
        if (_hotbarTooltip == null)
            return;

        int slotIndex =
            MainInvSize +
            hotbarIdx;

        if (slotIndex < 0 ||
            slotIndex >= _inventory.Slots.Length)
        {
            _hotbarTooltip.HideTooltip();
            return;
        }

        var slot =
            _inventory.Slots[slotIndex];

        if (slot.IsEmpty)
        {
            _hotbarTooltip.HideTooltip();
            return;
        }

        var item =
            ItemRegistry.Instance.GetItem(
                slot.ItemId
            );

        _hotbarTooltip.ShowFor(
            item,
            _hotbarSlots[hotbarIdx],
            slot.CurrentDurability
        );
    }

    // =========================================================================
    // CREATIVE MENU VISIBILITY
    // =========================================================================

    private void ToggleCreativeMenu()
    {
        _creativeMenuOpen =
            !_creativeMenuOpen;

        _creativeMenu.Visible =
            _creativeMenuOpen;

        if (_creativeMenuOpen)
        {
            _hotbarLayer.Visible = false;

            if (_statsHudLayer != null)
                _statsHudLayer.Visible = false;

            Input.MouseMode =
                Input.MouseModeEnum.Visible;
        }
        else
        {
            _hotbarLayer.Visible =
                _hudVisible;

            if (_statsHudLayer != null)
                _statsHudLayer.Visible =
                    _hudVisible;

            Input.MouseMode =
                Input.MouseModeEnum.Captured;
        }
    }

    // =========================================================================
    // SLOT VISUAL REFRESH
    // =========================================================================

    private void RefreshAllSlotVisuals()
    {
        for (int i = 0; i < MainInvSize; i++)
        {
            var s =
                _inventory.Slots[i];

            _invSlotPanels[i]
                .GetChild<TextureRect>(0)
                .Texture =
                    s.IsEmpty
                        ? null
                        : GetItemIcon(s.ItemId);

            _invSlotLabels[i].Text =
                (!s.IsEmpty && s.Count > 1)
                    ? s.Count.ToString()
                    : "";
        }

        for (int i = 0; i < HotbarSize; i++)
        {
            var s =
                _inventory.Slots[
                    MainInvSize + i
                ];

            var icon =
                s.IsEmpty
                    ? null
                    : GetItemIcon(s.ItemId);

            _hotbarSlots[i]
                .GetChild<TextureRect>(0)
                .Texture = icon;

            _invHotbarSlots[i]
                .GetChild<TextureRect>(0)
                .Texture = icon;

            string t =
                (!s.IsEmpty && s.Count > 1)
                    ? s.Count.ToString()
                    : "";

            _hotbarLabels[i].Text =
                t;

            _invHotbarLabels[i].Text =
                t;
        }

        var sel =
            _inventory.Slots[
                MainInvSize + _selectedSlot
            ];

        _selectedBlockId =
            sel.IsEmpty
                ? ""
                : sel.ItemId;

        UpdateHotbarSelectionBorder();
        UpdateCursorVisual();
        RefreshContainerSlotVisuals();
    }

    private void UpdateHotbarSelectionBorder()
    {
        for (int i = 0; i < HotbarSize; i++)
        {
            bool selected =
                i == _selectedSlot;

            var border =
                selected
                    ? new Color(1f, 1f, 1f)
                    : new Color(0.3f, 0.3f, 0.3f);

            int bw =
                selected
                    ? 3
                    : 2;

            _hotbarSlots[i]
                .AddThemeStyleboxOverride(
                    "panel",
                    MakePanelStyle(
                        new Color(
                            0.15f,
                            0.15f,
                            0.15f,
                            0.85f
                        ),
                        border,
                        bw
                    )
                );

            _invHotbarSlots[i]
                .AddThemeStyleboxOverride(
                    "panel",
                    MakePanelStyle(
                        new Color(
                            0.15f,
                            0.15f,
                            0.15f,
                            0.85f
                        ),
                        border,
                        bw
                    )
                );
        }
    }

    private void UpdateCursorVisual()
    {
        if (_heldSlot.IsEmpty)
        {
            _cursorPanel.Visible = false;
            return;
        }

        _cursorTex.Texture =
            GetItemIcon(
                _heldSlot.ItemId
            );

        _cursorLabel.Text =
            _heldSlot.Count > 1
                ? _heldSlot.Count.ToString()
                : "";

        _cursorPanel.Visible =
            _inventoryOpen;
    }

    private void FireChanged()
{
    _inventory.OnInventoryChanged?.Invoke();

    if (_containerOpen)
    {
        RefreshContainerSlotVisuals();
        ContainerManager.Instance?.SaveContainers();
    }
}
}