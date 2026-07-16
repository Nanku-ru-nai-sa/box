using Godot;

// EquipmentPanel — gear/appearance slot layout, plus a simple text readout of
// the player's core stats (Health / Stamina / Mana) in the panel's reserved
// bottom space. Slots are still placeholder-only (no equip logic yet); the
// stats readout is live and updates whenever PlayerStats changes.

public partial class EquipmentPanel : Control
{
    // Column 1 = actual gear (will affect stats/movement later)
    private static readonly string[] GearLabels = { "Helmet", "Armor", "Belt", "Boots" };
    // Column 2 = cosmetic appearance layer
    private static readonly string[] ApparelLabels = { "Hat", "Shirt", "Pants", "Boots" };

    private const int SlotSz  = 56;
    private const int SlotGap = 5;
    private const int PanelW  = 300;
    private const int PanelH  = 339;

    private Panel _bg;
    public Panel[] GearSlots    { get; private set; } = new Panel[4];
    public Panel[] ApparelSlots { get; private set; } = new Panel[4];

    private PlayerStats _stats;
    private Label _healthLabel;
    private Label _staminaLabel;
    private Label _manaLabel;

    // Called by Player before this node enters the tree (same pattern as
    // CraftingPanel.Init) — stores the reference; _Ready() reads it once
    // building the stats section, and events keep it live afterward.
    public void Init(PlayerStats stats)
    {
        _stats = stats;
        if (_stats == null) return;

        _stats.HealthChanged  += (cur, max) => UpdateStatLabel(_healthLabel,  "Health Points",  cur, max);
        _stats.StaminaChanged += (cur, max) => UpdateStatLabel(_staminaLabel, "Stamina Points", cur, max);
        _stats.ManaChanged    += (cur, max) => UpdateStatLabel(_manaLabel,    "Mana Points",    cur, max);
    }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(PanelW, PanelH);
        MouseFilter       = MouseFilterEnum.Stop;

        BuildBackground();
        BuildExternalTitle();
        BuildColumn(GearLabels,    0, GearSlots);
        BuildColumn(ApparelLabels, 1, ApparelSlots);
        BuildStatsSection();
    }

    private void BuildBackground()
    {
        _bg = new Panel();
        _bg.AnchorRight  = 1f;
        _bg.AnchorBottom = 1f;
        var s = new StyleBoxFlat();
        s.BgColor                = new Color(0.08f, 0.08f, 0.10f, 0.97f);
        s.BorderColor            = new Color(0.35f, 0.35f, 0.40f);
        s.BorderWidthTop         = 2; s.BorderWidthBottom = 2;
        s.BorderWidthLeft        = 2; s.BorderWidthRight  = 2;
        s.CornerRadiusTopLeft    = 4; s.CornerRadiusTopRight    = 4;
        s.CornerRadiusBottomLeft = 4; s.CornerRadiusBottomRight = 4;
        _bg.AddThemeStyleboxOverride("panel", s);
        AddChild(_bg);
    }

    private void BuildExternalTitle()
    {
        var title = new Label();
        title.Text = "Equipment";
        title.Position = new Vector2(0f, -22f);
        title.AddThemeFontSizeOverride("font_size", 13);
        title.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.75f));
        title.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(title);
    }

    // Reserved bottom strip, below the 4-slot columns (columns end at
    // 14 + 4*56 + 3*5 = 253px).
    private void BuildStatsSection()
    {
        float y = 262f;

        var vb = new VBoxContainer();
        vb.Position          = new Vector2(8f, y);
        vb.CustomMinimumSize = new Vector2(PanelW - 16f, PanelH - y - 8f);
        vb.AddThemeConstantOverride("separation", 6);
        AddChild(vb);

        _healthLabel  = MakeStatLabel();
        _staminaLabel = MakeStatLabel();
        _manaLabel    = MakeStatLabel();
        vb.AddChild(_healthLabel);
        vb.AddChild(_staminaLabel);
        vb.AddChild(_manaLabel);

        // Initial text — if Init() already ran (it does, before _Ready, per
        // Player's build order), _stats is already set here.
        if (_stats != null)
        {
            UpdateStatLabel(_healthLabel,  "Health Points",  _stats.Health,  _stats.MaxHealth);
            UpdateStatLabel(_staminaLabel, "Stamina Points", _stats.Stamina, _stats.MaxStamina);
            UpdateStatLabel(_manaLabel,    "Mana Points",    _stats.Mana,    _stats.MaxMana);
        }
    }

    private Label MakeStatLabel()
    {
        var lbl = new Label();
        lbl.AddThemeFontSizeOverride("font_size", 12);
        lbl.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.8f));
        lbl.MouseFilter = MouseFilterEnum.Ignore;
        return lbl;
    }

    private void UpdateStatLabel(Label lbl, string prefix, float cur, float max)
    {
        if (lbl == null) return;
        lbl.Text = $"{prefix}: {Mathf.RoundToInt(cur)}/{Mathf.RoundToInt(max)}";
    }

    private TextureRect MakeTexRect()
    {
        var tex = new TextureRect();
        tex.ExpandMode    = TextureRect.ExpandModeEnum.IgnoreSize;
        tex.StretchMode   = TextureRect.StretchModeEnum.KeepAspectCentered;
        tex.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        tex.AnchorRight   = 1f; tex.AnchorBottom = 1f;
        tex.OffsetLeft    = 6f; tex.OffsetTop    = 6f;
        tex.OffsetRight   = -6f; tex.OffsetBottom = -6f;
        tex.MouseFilter   = MouseFilterEnum.Ignore;
        return tex;
    }

    private void BuildColumn(string[] labels, int columnIndex, Panel[] target)
    {
        float startX = 8f + columnIndex * (SlotSz + SlotGap + 8f);
        float startY = 14f;

        for (int i = 0; i < labels.Length; i++)
        {
            var slot = new Panel();
            slot.Position           = new Vector2(startX, startY + i * (SlotSz + SlotGap));
            slot.CustomMinimumSize  = new Vector2(SlotSz, SlotSz);
            slot.Size               = new Vector2(SlotSz, SlotSz);
            slot.AddThemeStyleboxOverride("panel", MakeSlotStyle(new Color(0.4f, 0.4f, 0.4f)));
            slot.MouseFilter        = MouseFilterEnum.Stop;
            slot.MouseEntered += () => slot.AddThemeStyleboxOverride("panel", MakeSlotStyle(new Color(0.7f,0.7f,0.7f)));
            slot.MouseExited  += () => slot.AddThemeStyleboxOverride("panel", MakeSlotStyle(new Color(0.4f,0.4f,0.4f)));
            AddChild(slot);

            var lbl = new Label();
            lbl.Text        = labels[i];
            lbl.Position    = new Vector2(2f, 2f);
            lbl.AddThemeFontSizeOverride("font_size", 8);
            lbl.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.55f));
            lbl.MouseFilter = MouseFilterEnum.Ignore;
            slot.AddChild(lbl);

            target[i] = slot;
        }
    }

    private StyleBoxFlat MakeSlotStyle(Color border)
    {
        var s = new StyleBoxFlat();
        s.BgColor         = new Color(0.15f, 0.15f, 0.15f, 0.85f);
        s.BorderColor     = border;
        s.BorderWidthTop  = 2; s.BorderWidthBottom = 2;
        s.BorderWidthLeft = 2; s.BorderWidthRight  = 2;
        s.CornerRadiusTopLeft     = 3; s.CornerRadiusTopRight    = 3;
        s.CornerRadiusBottomLeft  = 3; s.CornerRadiusBottomRight = 3;
        return s;
    }
}