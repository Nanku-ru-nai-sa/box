using Godot;
using System;

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

    private void OnSlotMouseEntered(int slotIndex)
    {
        if (_invTooltip == null)
            return;

        if (slotIndex < 0 ||
            slotIndex >= _inventory.Slots.Length)
        {
            _invTooltip.HideTooltip();
            return;
        }

        var slot =
            _inventory.Slots[slotIndex];

        if (slot.IsEmpty)
        {
            _invTooltip.HideTooltip();
            return;
        }

        var item =
            ItemRegistry.Instance.GetItem(
                slot.ItemId
            );

        _invTooltip.ShowFor(
            item,
            GetSlotControl(slotIndex),
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

    private void BuildStationTabBar()
    {
        float gridW =
            HotbarSize * SlotSize +
            (HotbarSize - 1) * SlotGap;

        float pad = 16f;

        float totalW =
            gridW +
            pad * 2f;

        float invHalfW =
            totalW / 2f;

        _stationTabBar =
            new Control();

        _stationTabBar.AnchorLeft   = 0.5f;
        _stationTabBar.AnchorRight  = 0.5f;
        _stationTabBar.AnchorTop    = 0.5f;
        _stationTabBar.AnchorBottom = 0.5f;

        _stationTabBar.OffsetLeft =
            -invHalfW - 10f - 300f;

        _stationTabBar.OffsetRight =
            -invHalfW - 10f;

        _stationTabBar.OffsetTop =
            -180f - 28f;

        _stationTabBar.OffsetBottom =
            -180f - 4f;

        _stationTabBar.Visible = false;

        _tabCrafterStation =
            MakeStationTab(
                "Crafter",
                0f,
                out _tabCrafterStationLbl
            );

        _tabToolBenchStation =
            MakeStationTab(
                "Tool Bench",
                96f,
                out _tabToolBenchStationLbl
            );

        _tabCrafterStation.GuiInput +=
            (InputEvent ev) =>
            {
                if (IsStationTabClick(ev))
                    SwitchStation("crafter");
            };

        _tabToolBenchStation.GuiInput +=
            (InputEvent ev) =>
            {
                if (IsStationTabClick(ev))
                    SwitchStation("tool_bench");
            };

        _stationTabBar.AddChild(
            _tabCrafterStation
        );

        _stationTabBar.AddChild(
            _tabToolBenchStation
        );

        _craftingLayer.CallDeferred(
            "add_child",
            _stationTabBar
        );
    }

    private Panel MakeStationTab(
        string text,
        float x,
        out Label label)
    {
        var tab = new Panel();

        tab.Position =
            new Vector2(x, 0f);

        tab.CustomMinimumSize =
            new Vector2(90f, 24f);

        tab.MouseFilter =
            Control.MouseFilterEnum.Stop;

        label = new Label();

        label.Text = text;

        label.HorizontalAlignment =
            HorizontalAlignment.Center;

        label.VerticalAlignment =
            VerticalAlignment.Center;

        label.AnchorRight  = 1f;
        label.AnchorBottom = 1f;

        label.AddThemeFontSizeOverride(
            "font_size",
            11
        );

        label.MouseFilter =
            Control.MouseFilterEnum.Ignore;

        tab.AddChild(label);

        return tab;
    }

    private bool IsStationTabClick(InputEvent ev) =>
        ev is InputEventMouseButton mb &&
        mb.Pressed &&
        mb.ButtonIndex == MouseButton.Left;

    private void SwitchStation(string stationId)
    {
        _activeStation = stationId;

        if (_craftingPanel != null)
            _craftingPanel.Visible =
                stationId == "crafter";

        if (_toolBenchPanel != null)
            _toolBenchPanel.Visible =
                stationId == "tool_bench";

        SetStationTabStyle(
            _tabCrafterStation,
            _tabCrafterStationLbl,
            stationId == "crafter"
        );

        SetStationTabStyle(
            _tabToolBenchStation,
            _tabToolBenchStationLbl,
            stationId == "tool_bench"
        );
    }

    private void SetStationTabStyle(
        Panel tab,
        Label label,
        bool active)
    {
        var s = new StyleBoxFlat();

        s.BgColor =
            active
                ? new Color(0.2f, 0.2f, 0.25f)
                : new Color(0.12f, 0.12f, 0.15f);

        s.BorderColor =
            active
                ? new Color(0.6f, 0.6f, 0.7f)
                : new Color(0.3f, 0.3f, 0.35f);

        s.BorderWidthTop    = 2;
        s.BorderWidthBottom = 2;
        s.BorderWidthLeft   = 2;
        s.BorderWidthRight  = 2;

        tab.AddThemeStyleboxOverride(
            "panel",
            s
        );

        label.AddThemeColorOverride(
            "font_color",
            active
                ? new Color(1f, 1f, 1f)
                : new Color(0.55f, 0.55f, 0.55f)
        );
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
        return slotIndex < MainInvSize
            ? _invSlotPanels[slotIndex]
            : _invHotbarSlots[
                slotIndex - MainInvSize
            ];
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

    private void FireChanged() =>
        _inventory.OnInventoryChanged?.Invoke();
}