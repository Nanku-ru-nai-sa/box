using Godot;
using System;
using System.Collections.Generic;

// Browsable panel listing every item in the game (via ItemRegistry), with
// category tabs (still sourced from ItemCatalog's categories.json, which
// is purely display-grouping metadata - ItemRegistry has no concept of
// categories) and a live search box. Doesn't touch inventory logic
// itself — it just fires OnItemChosen and lets Player.cs decide what to
// do with that (give it to the player). Opened with the V key while in
// Create mode; see Player.cs's ToggleCreativeMenu().
public partial class CreativeMenu : Panel
{
    // itemId, count. Left-click on a slot = full stack, right-click = one.
    public event Action<string, int> OnItemChosen;

    public const int Columns      = 9;
    public const int VisibleRows  = 5;
    private const int SlotSize    = 56;
    private const int SlotGap     = 5;
    private const float Pad       = 16f;
    private const float TabH      = 28f;
    private const float SearchH   = 28f;
    private const float SectionGap = 6f;

    // Player.cs reads these to center the panel before Init() builds the
    // actual contents, so the on-screen size is known up front.
    public static float TotalWidth =>
        Columns * SlotSize + (Columns - 1) * SlotGap + Pad * 2f;

    public static float TotalHeight =>
        Pad + TabH + SectionGap + SearchH + SectionGap
        + (VisibleRows * SlotSize + (VisibleRows - 1) * SlotGap) + Pad;

    private HFlowContainer _tabBar;
    private LineEdit       _searchBox;
    private ScrollContainer _scroll;
    private GridContainer  _grid;

    private string _activeCategory = ItemCatalog.AllCategory;
    private string _searchText     = "";

    private readonly Dictionary<string, Texture2D> _iconCache = new();

    public void Init()
    {
        BuildLayout();
        RefreshTabs();
        RefreshGrid();
    }

    private void BuildLayout()
    {
        float gridW = Columns * SlotSize + (Columns - 1) * SlotGap;
        float gridH = VisibleRows * SlotSize + (VisibleRows - 1) * SlotGap;

        AddThemeStyleboxOverride("panel",
            MakePanelStyle(new Color(0.08f, 0.08f, 0.08f, 0.95f), new Color(0.5f, 0.5f, 0.5f)));

        _tabBar = new HFlowContainer();
        _tabBar.Position = new Vector2(Pad, Pad);
        _tabBar.Size     = new Vector2(gridW, TabH);
        AddChild(_tabBar);

        _searchBox = new LineEdit();
        _searchBox.PlaceholderText = "Search items...";
        _searchBox.Position = new Vector2(Pad, Pad + TabH + SectionGap);
        _searchBox.Size     = new Vector2(gridW, SearchH);
        _searchBox.TextChanged += OnSearchChanged;
        AddChild(_searchBox);

        _scroll = new ScrollContainer();
        _scroll.Position = new Vector2(Pad, Pad + TabH + SectionGap + SearchH + SectionGap);
        _scroll.Size     = new Vector2(gridW, gridH);
        _scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        AddChild(_scroll);

        _grid = new GridContainer();
        _grid.Columns = Columns;
        _grid.AddThemeConstantOverride("h_separation", SlotGap);
        _grid.AddThemeConstantOverride("v_separation", SlotGap);
        _scroll.AddChild(_grid);
    }

    private void RefreshTabs()
    {
        foreach (Node child in _tabBar.GetChildren()) child.QueueFree();

        var categories = new List<string> { ItemCatalog.AllCategory };
        categories.AddRange(ItemCatalog.GetAllCategoryNames());

        foreach (var cat in categories)
        {
            var btn = new Button();
            btn.Text           = cat;
            btn.ToggleMode     = true;
            btn.ButtonPressed  = cat == _activeCategory;
            string capturedCat = cat;
            btn.Pressed += () =>
            {
                _activeCategory = capturedCat;
                RefreshTabs();
                RefreshGrid();
            };
            _tabBar.AddChild(btn);
        }
    }

    private void OnSearchChanged(string text)
    {
        _searchText = text.Trim().ToLowerInvariant();
        RefreshGrid();
    }

    private void RefreshGrid()
    {
        foreach (Node child in _grid.GetChildren()) child.QueueFree();

        foreach (var item in ItemRegistry.Instance.GetAllItems())
        {
            string id = item.ItemId;

            if (_activeCategory != ItemCatalog.AllCategory &&
                ItemCatalog.GetCategory(id) != _activeCategory)
                continue;

            if (_searchText.Length > 0 &&
                !id.ToLowerInvariant().Contains(_searchText) &&
                !item.DisplayName.ToLowerInvariant().Contains(_searchText))
                continue;

            _grid.AddChild(MakeItemSlot(item));
        }
    }

    private Panel MakeItemSlot(ItemResource item)
    {
        string itemId = item.ItemId;
        var slot = new Panel();
        slot.CustomMinimumSize = new Vector2(SlotSize, SlotSize);
        slot.TooltipText       = item.DisplayName; // was the raw itemId - now the real display name
        slot.MouseFilter       = Control.MouseFilterEnum.Stop;
        slot.AddThemeStyleboxOverride("panel",
            MakePanelStyle(new Color(0.15f, 0.15f, 0.15f, 0.85f), new Color(0.4f, 0.4f, 0.4f)));
        slot.MouseEntered += () => slot.AddThemeStyleboxOverride("panel",
            MakePanelStyle(new Color(0.2f, 0.2f, 0.2f, 0.85f), new Color(0.75f, 0.75f, 0.75f)));
        slot.MouseExited += () => slot.AddThemeStyleboxOverride("panel",
            MakePanelStyle(new Color(0.15f, 0.15f, 0.15f, 0.85f), new Color(0.4f, 0.4f, 0.4f)));

        var tex = new TextureRect();
        tex.Texture        = GetIcon(item);
        tex.ExpandMode     = TextureRect.ExpandModeEnum.IgnoreSize;
        tex.StretchMode    = TextureRect.StretchModeEnum.KeepAspectCentered;
        tex.TextureFilter  = CanvasItem.TextureFilterEnum.Nearest;
        tex.AnchorRight    = 1f; tex.AnchorBottom = 1f;
        tex.OffsetLeft     = 6;  tex.OffsetTop    = 6;
        tex.OffsetRight    = -6; tex.OffsetBottom = -6;
        tex.MouseFilter    = Control.MouseFilterEnum.Ignore;
        slot.AddChild(tex);

        string capturedId = itemId;
        slot.GuiInput += (InputEvent ev) => OnSlotInput(ev, capturedId);
        return slot;
    }

    private void OnSlotInput(InputEvent ev, string itemId)
    {
        if (ev is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.Left)
                OnItemChosen?.Invoke(itemId, 100);   // full stack — Minecraft-creative-style left-click
            else if (mb.ButtonIndex == MouseButton.Right)
                OnItemChosen?.Invoke(itemId, 1);
        }
    }

    // Same fallback chain as GetItemIcon in Player.cs: prefer an Icon
    // already set on the ItemResource (crafted tools), then a matching
    // file in Assets/Textures/Items/, then the "unknown item" placeholder
    // rather than showing a blank slot.
    private Texture2D GetIcon(ItemResource item)
    {
        if (_iconCache.TryGetValue(item.ItemId, out var cached)) return cached;

        Texture2D tex = item.Icon;

        if (tex == null)
        {
            string path = $"res://Assets/Textures/Items/{item.ItemId}.png";
            tex = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
        }

        if (tex == null)
            tex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Items/tool/chalk/unknown.png");

        _iconCache[item.ItemId] = tex;
        return tex;
    }

    private StyleBoxFlat MakePanelStyle(Color bg, Color border, int bw = 2)
    {
        var s = new StyleBoxFlat();
        s.BgColor         = bg;    s.BorderColor       = border;
        s.BorderWidthTop  = bw;    s.BorderWidthBottom = bw;
        s.BorderWidthLeft = bw;    s.BorderWidthRight  = bw;
        s.CornerRadiusTopLeft = 3; s.CornerRadiusTopRight    = 3;
        s.CornerRadiusBottomLeft = 3; s.CornerRadiusBottomRight = 3;
        return s;
    }
}