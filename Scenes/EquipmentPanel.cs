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
    private const int PanelW  = 271;
    private const int PanelH  = 339;

    private Panel _bg;
    public Panel[] GearSlots    { get; private set; } = new Panel[4];
    public Panel[] ApparelSlots { get; private set; } = new Panel[4];

    private PlayerStats _stats;
    private Label _healthLabel;
    private Label _staminaLabel;
    private Label _manaLabel;

    private SubViewportContainer _previewContainer;
    private Node3D _previewPivot;
    private Node3D _previewHeadPivot;
    private float _previewTargetYaw;
    private float _previewTargetPitch;

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
        BuildPlayerPreview();
        BuildStatsSection();
    }

    private void BuildPlayerPreview()
    {
        _previewContainer = new SubViewportContainer();
        _previewContainer.Position = new Vector2(70f, 28f);
        _previewContainer.Size = new Vector2(131f, 239f);
        _previewContainer.Stretch = true;
        _previewContainer.MouseFilter = MouseFilterEnum.Ignore;
        // Draw the player preview at the front of the equipment UI.
        // The equipment CanvasLayer sits below the cursor-item layer, so
        // held items and the operating-system mouse cursor remain on top.
        _previewContainer.ZIndex = 4096;
        _previewContainer.ZAsRelative = false;
        AddChild(_previewContainer);

        var viewport = new SubViewport();
        viewport.Size = new Vector2I(262, 478);
        viewport.World3D = new World3D();
        viewport.TransparentBg = true;
        viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;

        // A private preview world has no ambient lighting by default. Keep
        // the background transparent, but add soft white ambient light so
        // the model's original textures and colors remain visible.
        var previewEnvironment = new Environment();
        previewEnvironment.BackgroundMode = Environment.BGMode.Canvas;
        previewEnvironment.AmbientLightSource = Environment.AmbientSource.Color;
        previewEnvironment.AmbientLightColor = Colors.White;
        previewEnvironment.AmbientLightEnergy = 0.85f;

        var worldEnvironment = new WorldEnvironment();
        worldEnvironment.Environment = previewEnvironment;
        viewport.AddChild(worldEnvironment);
        _previewContainer.AddChild(viewport);

        _previewPivot = new Node3D();
        viewport.AddChild(_previewPivot);

        PackedScene playerScene = GD.Load<PackedScene>(
            "res://Assets/Models/Player/player.gltf"
        );

        if (playerScene == null)
        {
            GD.PrintErr("[EquipmentPanel] Could not load player preview model.");
            return;
        }

        Node3D model = playerScene.Instantiate<Node3D>();
        _previewPivot.AddChild(model);
        model.Position = new Vector3(0f, -0.9f, 0f);

        Node3D playerParts = model.FindChild("player", true, false) as Node3D;
        Node3D bones = model.FindChild("bones", true, false) as Node3D;

        if (bones != null)
            bones.Visible = false;

        if (playerParts != null)
        {
            SetPreviewMeshesVisible(playerParts, true);
            BuildHeadPreviewPivot(model, playerParts);
        }

        var camera = new Camera3D();
        camera.Fov = 35f;
        camera.Position = new Vector3(0f, 0.05f, -3.5f);
        viewport.AddChild(camera);
        camera.LookAt(Vector3.Zero, Vector3.Up);
        camera.Current = true;

        var light = new DirectionalLight3D();
        light.RotationDegrees = new Vector3(-25f, -25f, 0f);
        light.LightEnergy = 1.5f;
        viewport.AddChild(light);
    }

    private void BuildHeadPreviewPivot(Node3D model, Node3D playerParts)
    {
        Node3D headNode = playerParts.FindChild("head", true, false) as Node3D;
        if (headNode == null)
            return;

        // Keep every facial piece on the same pivot so the inventory preview
        // head turns as one model instead of leaving the eyes and pupils behind.
        Node3D[] faceParts =
        {
            headNode,
            playerParts.FindChild("left eye", true, false) as Node3D,
            playerParts.FindChild("left pupil", true, false) as Node3D,
            playerParts.FindChild("right eye", true, false) as Node3D,
            playerParts.FindChild("right pupil", true, false) as Node3D
        };

        Vector3 headCenter = Vector3.Zero;
        if (headNode is MeshInstance3D headMesh && headMesh.Mesh != null)
            headCenter = headMesh.GetAabb().GetCenter();

        // Place the shared pivot at the head centre and preserve each imported
        // mesh's world transform as it is reparented beneath the pivot.
        Vector3 headCenterInModel = model.ToLocal(headNode.ToGlobal(headCenter));
        _previewHeadPivot = new Node3D { Name = "PreviewHeadPivot" };
        model.AddChild(_previewHeadPivot);
        _previewHeadPivot.Position = headCenterInModel;

        foreach (Node3D facePart in faceParts)
        {
            if (facePart != null && IsInstanceValid(facePart))
                facePart.Reparent(_previewHeadPivot, true);
        }
    }

    private void SetPreviewMeshesVisible(Node parent, bool visible)
    {
        foreach (Node child in parent.GetChildren())
        {
            if (child is GeometryInstance3D visual)
                visual.Visible = visible;

            SetPreviewMeshesVisible(child, visible);
        }
    }

    public override void _Process(double delta)
    {
        if (_previewPivot == null || !Visible || _previewContainer == null)
            return;

        // Use the main game viewport, not this small equipment panel, so the
        // preview keeps following the cursor anywhere while the inventory is open.
        Vector2 mouse = GetViewport().GetMousePosition();
        Vector2 viewportSize = GetViewportRect().Size;

        float normalizedX =
            (mouse.X - viewportSize.X * 0.5f) / (viewportSize.X * 0.5f);
        float normalizedY =
            (mouse.Y - viewportSize.Y * 0.5f) / (viewportSize.Y * 0.5f);

        // Positive X/Y now follow the cursor direction rather than opposing it.
        // Keep the body movement subtle; Minecraft-style menu movement is
        // mostly the head tracking the cursor vertically.
        _previewTargetYaw = Mathf.Clamp(normalizedX, -1f, 1f) * 0.28f;
        _previewTargetPitch = -Mathf.Clamp(normalizedY, -1f, 1f) * 0.24f;

        float weight = 1f - Mathf.Exp(-(float)delta * 12f);
        Vector3 bodyRotation = _previewPivot.Rotation;
        bodyRotation.Y = Mathf.LerpAngle(bodyRotation.Y, _previewTargetYaw, weight);
        _previewPivot.Rotation = bodyRotation;

        if (_previewHeadPivot != null && IsInstanceValid(_previewHeadPivot))
        {
            Vector3 headRotation = _previewHeadPivot.Rotation;
            headRotation.X = Mathf.LerpAngle(headRotation.X, _previewTargetPitch, weight);
            _previewHeadPivot.Rotation = headRotation;
        }
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
        float startX = columnIndex == 0
            ? 8f
            : PanelW - SlotSz - 8f;
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