// UPDATED FILE - replaces ItemTooltip.cs
//
// Fixes two bugs:
// 1. The background box was anchor-stretched to a fixed tiny CustomMinimumSize
//    (150x10) instead of sizing to fit its actual text content, so it showed
//    as a thin bar with the real text overflowing outside it.
// 2. ShowFor() only ever displayed the item's static MaxDurability template
//    value - it never had a way to show a specific tool INSTANCE's current
//    wear, so the durability line could never actually change/update.
//    ShowFor() now takes an optional currentDurability parameter for that.

using Godot;

public partial class ItemTooltip : Control
{
    private Panel           _bg;
    private VBoxContainer   _vbox;
    private Label _titleLabel;
    private Label _miningRow;
    private Label _cooldownRow;
    private Label _damageRow;
    private Label _durabilityRow;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore; // never blocks input - it's just a readout
        Visible = false;
        ZIndex = 100; // draw above everything else in the panel

        _bg = new Panel();
        // No CustomMinimumSize here - size is set explicitly after content
        // updates, in ResizeBackground(), based on what the text actually needs.
        var s = new StyleBoxFlat();
        s.BgColor         = new Color(0.05f, 0.08f, 0.10f, 0.96f);
        s.BorderColor     = new Color(0.25f, 0.65f, 0.75f); // cyan-ish, matches the reference image
        s.BorderWidthTop  = 2; s.BorderWidthBottom = 2;
        s.BorderWidthLeft = 2; s.BorderWidthRight  = 2;
        s.CornerRadiusTopLeft     = 4; s.CornerRadiusTopRight    = 4;
        s.CornerRadiusBottomLeft  = 4; s.CornerRadiusBottomRight = 4;
        _bg.AddThemeStyleboxOverride("panel", s);
        AddChild(_bg);

        // NOT anchor-stretched - sized naturally by its children's content,
        // which is what lets us read its real size back out afterward.
        _vbox = new VBoxContainer();
        _vbox.Position = new Vector2(8f, 6f);
        _vbox.AddThemeConstantOverride("separation", 3);
        _bg.AddChild(_vbox);

        _titleLabel = new Label();
        _titleLabel.AddThemeFontSizeOverride("font_size", 13);
        _titleLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.85f, 0.95f));
        _vbox.AddChild(_titleLabel);

        _miningRow     = MakeRow(_vbox);
        _cooldownRow   = MakeRow(_vbox);
        _damageRow     = MakeRow(_vbox);
        _durabilityRow = MakeRow(_vbox);
    }

    private Label MakeRow(VBoxContainer parent)
    {
        var lbl = new Label();
        lbl.AddThemeFontSizeOverride("font_size", 11);
        lbl.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f));
        parent.AddChild(lbl);
        return lbl;
    }

    private Control _anchor;

    // Call on hover, passing the slot/socket Control itself (not a mouse
    // position) - the tooltip anchors to that control and appears just
    // below it, flipping to appear above if there isn't room below (e.g.
    // the hotbar sits right at the bottom of the screen). currentDurability
    // is the specific tool INSTANCE's remaining durability (e.g. from an
    // InventorySlot) - pass null (or omit) when there isn't a specific
    // instance yet (like a fresh Tool Bench craft preview), which shows
    // max/max.
    public void ShowFor(ItemResource item, Control anchor, int? currentDurability = null)
    {
        if (item == null) { HideTooltip(); return; }
        _anchor = anchor;

        _titleLabel.Text = item.DisplayName;

        // Only tools show mining/cooldown/damage rows - a raw material
        // (like flint sitting in a socket) just shows its name.
        bool isTool = item.HasDurability && !string.IsNullOrEmpty(item.ToolType);

        _miningRow.Visible   = isTool;
        _cooldownRow.Visible = isTool;
        _damageRow.Visible   = isTool;
        if (isTool)
        {
            // Row label uses the tool's own family name (e.g. "Pickaxe: 2")
            // rather than a generic "Pixels" label - matches the reference image.
            _miningRow.Text   = $"Pixels: {item.MiningPower}";
            _cooldownRow.Text = $"Cooldown: {item.CooldownSeconds:0.00}s";
            _damageRow.Text   = $"Damage: {item.AttackDamage:0.0}";
        }

        _durabilityRow.Visible = item.HasDurability;
        if (item.HasDurability)
        {
            int current = currentDurability ?? item.MaxDurability;
            _durabilityRow.Text = $"Durability: {current}/{item.MaxDurability}";
        }

        Visible = true;

        // Deferred so the labels' text (set just above) has already been
        // through a layout pass and GetCombinedMinimumSize() is accurate -
        // ResizeBackground both sizes the box AND positions it relative to
        // the anchor, since it needs the real size to decide above-vs-below.
        CallDeferred(nameof(ResizeBackground));
    }

    private void ResizeBackground()
    {
        Vector2 contentSize = _vbox.GetCombinedMinimumSize();
        Vector2 boxSize     = contentSize + new Vector2(16f, 12f); // 8px left/right + 6px top/bottom margins
        _bg.Size = boxSize;

        if (_anchor == null || !IsInstanceValid(_anchor)) return;

        Vector2 anchorPos  = _anchor.GlobalPosition;
        Vector2 anchorSize = _anchor.Size;
        Vector2 viewport   = GetViewport().GetVisibleRect().Size;
        const float gap = 4f;

        // Centered below the slot by default; flip above it if that would
        // run past the bottom of the screen.
        float x      = anchorPos.X; // left edge flush with the icon's left edge - tooltip grows right/down from there, not centered
        float yBelow = anchorPos.Y + anchorSize.Y + gap;
        float y      = (yBelow + boxSize.Y <= viewport.Y) ? yBelow : (anchorPos.Y - boxSize.Y - gap);

        x = Mathf.Clamp(x, 4f, Mathf.Max(4f, viewport.X - boxSize.X - 4f)); // keep it on-screen horizontally too

        GlobalPosition = new Vector2(x, y);
    }

    public void HideTooltip() => Visible = false;
}