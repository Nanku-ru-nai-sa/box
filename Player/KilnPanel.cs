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
    private const int PanelWidth = 271;
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

    public Vector3I GetKilnPosition() => _worldPosition;

    public InventorySlot GetSlot(int index)
    {
        return index >= 0 && index < 2 && _slots.Count > index ? _slots[index] : null;
    }

    public void Refresh()
    {
        if (_slotPanels[0] == null) return;
        var latestSlots = KilnManager.Instance?.GetSlots(_worldPosition);
        if (latestSlots != null) _slots = latestSlots;

        for (int i = 0; i < 3; i++)
        {
            InventorySlot slot = _slots.Count > i ? _slots[i] : null;
            Texture2D itemTexture = slot == null || slot.IsEmpty ? null : GetIcon(slot.ItemId);
            FitSlotIcon(_slotIcons[i], itemTexture);
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

        AddLabel("Input", new Vector2(16, -6), 11);
        AddLabel("Fuel", new Vector2(16, 116), 11);
        AddLabel("Output", new Vector2(199, 55), 11);

        CreateSlot(0, new Vector2(16, 14), "Input");
        CreateSlot(1, new Vector2(16, 136), "Fuel");
        CreateSlot(2, new Vector2(199, 75), "Output");

        var arrow = AddLabel("→", new Vector2(111, 72), 22);
        arrow.Size = new Vector2(45, 40);
        arrow.HorizontalAlignment = HorizontalAlignment.Center;

        _progress = new ProgressBar();
        _progress.Position = new Vector2(19, 236);
        _progress.Size = new Vector2(232, 16);
        _progress.MinValue = 0;
        _progress.MaxValue = 100;
        _progress.ShowPercentage = false;
        AddChild(_progress);

        _status = AddLabel("Ready", new Vector2(19, 258), 11, new Color(0.7f, 0.7f, 0.7f));
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
        // The icon is sized by FitSlotIcon so pixel art can use whole-number
        // scaling where possible instead of being stretched to an arbitrary
        // 46x46 rectangle.
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

    private static void FitSlotIcon(TextureRect icon, Texture2D texture)
    {
        icon.Texture = texture;

        if (texture == null)
        {
            icon.Size = Vector2.Zero;
            return;
        }

        Vector2 sourceSize = texture.GetSize();
        const float maxIconSize = SlotSize - 8f;

        float maxScale = Mathf.Min(
            maxIconSize / Mathf.Max(1f, sourceSize.X),
            maxIconSize / Mathf.Max(1f, sourceSize.Y)
        );

        // Pixel art should be enlarged by whole pixels whenever it fits.
        // Only use fractional scaling when the source image is larger than
        // the available slot area and must be reduced to fit.
        float scale = maxScale >= 1f ? Mathf.Floor(maxScale) : maxScale;
        Vector2 displaySize = sourceSize * scale;

        icon.AnchorLeft = 0f;
        icon.AnchorTop = 0f;
        icon.AnchorRight = 0f;
        icon.AnchorBottom = 0f;
        icon.Position = new Vector2(
            (SlotSize - displaySize.X) * 0.5f,
            (SlotSize - displaySize.Y) * 0.5f
        );
        icon.Size = displaySize;
    }

    private static Texture2D GetIcon(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        string stickPath = $"res://Assets/Textures/Items/stick/{itemId}.png";
        if (ResourceLoader.Exists(stickPath))
            return ResourceLoader.Load<Texture2D>(stickPath);

        string textureId = itemId switch
        {
            "wheat_seed" => "wheat_seeds",
            "corn_seed" => "corn_seeds",
            "tomato_seed" => "tomato_seeds",
            _ => itemId
        };

        string itemPath = FindItemTexturePath("res://Assets/Textures/Items", textureId);
        if (!string.IsNullOrEmpty(itemPath) && ResourceLoader.Exists(itemPath))
            return ResourceLoader.Load<Texture2D>(itemPath);

        if (itemId == "sapling")
        {
            const string plantPath = "res://Assets/Textures/Plants/sapling.png";
            if (ResourceLoader.Exists(plantPath))
                return ResourceLoader.Load<Texture2D>(plantPath);
        }

        Texture2D itemIcon = ItemRegistry.Instance?.GetItem(itemId)?.Icon;
        if (itemIcon != null)
            return itemIcon;

        return ResourceLoader.Load<Texture2D>(
            "res://Assets/Textures/Items/tool/chalk/unknown.png"
        );
    }

    private static string FindItemTexturePath(string folderPath, string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return "";

        DirAccess dir = DirAccess.Open(folderPath);
        if (dir == null)
            return "";

        foreach (string fileName in dir.GetFiles())
        {
            if (!fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                continue;

            string baseName = System.IO.Path.GetFileNameWithoutExtension(fileName);
            if (string.Equals(baseName, itemId, StringComparison.OrdinalIgnoreCase))
            {
                string fullPath = $"{folderPath}/{fileName}";
                if (ResourceLoader.Exists(fullPath))
                    return fullPath;
            }
        }

        foreach (string directoryName in dir.GetDirectories())
        {
            string nestedPath = FindItemTexturePath($"{folderPath}/{directoryName}", itemId);
            if (!string.IsNullOrEmpty(nestedPath))
                return nestedPath;
        }

        return "";
    }
}
