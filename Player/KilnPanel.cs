using Godot;
using System;
using System.Collections.Generic;

// Compact three-slot kiln interface. Slot data is owned by KilnManager.
public partial class KilnPanel : Control
{
    public Action<int, MouseButton, bool> OnSlotClicked;
    public Action OnOutputClicked;

    private List<InventorySlot> _slots = new();
    private Panel[] _slotPanels = new Panel[3];
    private TextureRect[] _slotIcons = new TextureRect[3];
    private Label[] _slotCounts = new Label[3];
    private ProgressBar _progress;
    private Label _status;
    private Vector3I _worldPosition;
    private const int PanelWidth = 300;
    private const int SlotSize = 56;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(PanelWidth, 339);
        MouseFilter = MouseFilterEnum.Stop;
        BuildUi();
    }

    public void SetKiln(Vector3I position, List<InventorySlot> slots)
    {
        _worldPosition = position;
        _slots = slots ?? new List<InventorySlot>();
        Refresh();
    }

    public InventorySlot GetSlot(int index)
    {
        return index >= 0 && index < 2 && _slots.Count > index ? _slots[index] : null;
    }

    public void Refresh()
    {
        if (_slotPanels[0] == null) return;

        for (int i = 0; i < 3; i++)
        {
            InventorySlot slot = _slots.Count > i ? _slots[i] : null;
            _slotIcons[i].Texture = slot == null || slot.IsEmpty ? null : GetIcon(slot.ItemId);
            _slotCounts[i].Text = slot != null && slot.Count > 1 ? slot.Count.ToString() : "";
        }

        float progress = KilnManager.Instance?.GetProgress(_worldPosition) ?? 0f;
        _progress.Value = progress * 100.0;
        _status.Text = progress > 0f ? "Firing..." : "Ready";
    }

    private void BuildUi()
    {
        var bg = new Panel();
        bg.AnchorRight = 1f;
        bg.AnchorBottom = 1f;
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.10f, 0.97f),
            BorderColor = new Color(0.35f, 0.35f, 0.40f),
            BorderWidthTop = 2, BorderWidthBottom = 2,
            BorderWidthLeft = 2, BorderWidthRight = 2
        };
        bg.AddThemeStyleboxOverride("panel", style);
        AddChild(bg);

        AddLabel("Kiln", new Vector2(0, -24), 13, new Color(0.8f, 0.8f, 0.8f));

        AddLabel("Input", new Vector2(34, 42), 11);
        AddLabel("Fuel", new Vector2(34, 130), 11);
        AddLabel("Output", new Vector2(202, 84), 11);

        CreateSlot(0, new Vector2(34, 62), "Input");
        CreateSlot(1, new Vector2(34, 150), "Fuel");
        CreateSlot(2, new Vector2(202, 106), "Output");

        var arrow = AddLabel("→", new Vector2(126, 105), 22);
        arrow.Size = new Vector2(45, 40);
        arrow.HorizontalAlignment = HorizontalAlignment.Center;

        _progress = new ProgressBar();
        _progress.Position = new Vector2(34, 236);
        _progress.Size = new Vector2(232, 16);
        _progress.MinValue = 0;
        _progress.MaxValue = 100;
        _progress.ShowPercentage = false;
        AddChild(_progress);

        _status = AddLabel("Ready", new Vector2(34, 258), 11, new Color(0.7f, 0.7f, 0.7f));
    }

    private void CreateSlot(int index, Vector2 position, string tooltip)
    {
        var panel = new Panel();
        panel.Position = position;
        panel.CustomMinimumSize = new Vector2(SlotSize, SlotSize);
        panel.TooltipText = tooltip;
        panel.MouseFilter = MouseFilterEnum.Stop;
        panel.AddThemeStyleboxOverride("panel", MakeSlotStyle(false));

        var icon = new TextureRect();
        icon.AnchorRight = 1f;
        icon.AnchorBottom = 1f;
        icon.OffsetLeft = 5;
        icon.OffsetTop = 5;
        icon.OffsetRight = -5;
        icon.OffsetBottom = -5;
        icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        icon.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        icon.MouseFilter = MouseFilterEnum.Ignore;
        panel.AddChild(icon);

        var count = new Label();
        count.AnchorRight = 1f;
        count.AnchorBottom = 1f;
        count.OffsetRight = -4;
        count.OffsetBottom = -3;
        count.HorizontalAlignment = HorizontalAlignment.Right;
        count.VerticalAlignment = VerticalAlignment.Bottom;
        count.AddThemeFontSizeOverride("font_size", 10);
        count.MouseFilter = MouseFilterEnum.Ignore;
        panel.AddChild(count);

        panel.MouseEntered += () => panel.AddThemeStyleboxOverride("panel", MakeSlotStyle(true));
        panel.MouseExited += () => panel.AddThemeStyleboxOverride("panel", MakeSlotStyle(false));
        panel.GuiInput += (InputEvent ev) =>
        {
            if (ev is InputEventMouseButton mb && mb.Pressed)
            {
                if (index == 2 && mb.ButtonIndex == MouseButton.Left)
                    OnOutputClicked?.Invoke();
                else if (index < 2 && (mb.ButtonIndex == MouseButton.Left || mb.ButtonIndex == MouseButton.Right))
                    OnSlotClicked?.Invoke(index, mb.ButtonIndex, Input.IsKeyPressed(Key.Shift));
            }
        };

        _slotPanels[index] = panel;
        _slotIcons[index] = icon;
        _slotCounts[index] = count;
        AddChild(panel);
    }

    private Label AddLabel(string text, Vector2 position, int size, Color? color = null)
    {
        var label = new Label();
        label.Text = text;
        label.Position = position;
        label.AddThemeFontSizeOverride("font_size", size);
        if (color.HasValue) label.AddThemeColorOverride("font_color", color.Value);
        label.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(label);
        return label;
    }

    private static StyleBoxFlat MakeSlotStyle(bool hover)
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.12f, 0.15f, 0.95f),
            BorderColor = hover ? new Color(0.7f, 0.7f, 0.75f) : new Color(0.3f, 0.3f, 0.35f),
            BorderWidthTop = 2, BorderWidthBottom = 2,
            BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3
        };
    }

    private static Texture2D GetIcon(string itemId)
    {
        string itemPath = $"res://Assets/Textures/Items/{itemId}.png";
        if (ResourceLoader.Exists(itemPath)) return ResourceLoader.Load<Texture2D>(itemPath);

        string blockPath = $"res://Assets/Textures/Blocks/{itemId}.png";
        return ResourceLoader.Exists(blockPath) ? ResourceLoader.Load<Texture2D>(blockPath) : null;
    }
}
