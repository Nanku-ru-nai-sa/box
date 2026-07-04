using Godot;

// PauseMenu — built entirely in code, added as a child of Player in _Ready.
// Escape toggles it. Game tree is paused while open.
// Add [GlobalClass] isn't needed — Player instantiates this directly.

public partial class PauseMenu : Node
{
    private const string MainMenuScene = "res://Scenes/MainMenu.tscn";

    private CanvasLayer  _layer;
    private Panel        _panel;
    private Panel        _settingsPanel;
    private HSlider      _sensitivitySlider;
    private HSlider      _fovSlider;
    private HSlider      _volumeSlider;
    private CheckButton  _fullscreenToggle;
    private Label        _sensitivityValue;
    private Label        _fovValue;
    private Label        _volumeValue;

    public bool IsOpen { get; private set; } = false;

    // Player passes itself in so we can call save
    private Player _player;

    public void Init(Player player)
    {
        _player = player;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Ready()
    {
        _layer            = new CanvasLayer();
        _layer.Layer      = 50;
        _layer.ProcessMode = ProcessModeEnum.Always;
        AddChild(_layer);

        BuildPausePanel();
        _settingsPanel = BuildSettingsPanel();
        _layer.AddChild(_settingsPanel);
        _settingsPanel.Visible = false;

        _layer.Visible = false;
    }

    // ── Toggle ───────────────────────────────────────────────────────────────

    public void Open()
    {
        IsOpen             = true;
        _layer.Visible     = true;
        _settingsPanel.Visible = false;
        GetTree().Paused   = true;
        Input.MouseMode    = Input.MouseModeEnum.Visible;
    }

    public void Close()
    {
        IsOpen           = false;
        _layer.Visible   = false;
        GetTree().Paused = false;
        Input.MouseMode  = Input.MouseModeEnum.Captured;
    }

    // ── Build UI ─────────────────────────────────────────────────────────────

    private void BuildPausePanel()
    {
        // Dim overlay
        var overlay = new ColorRect();
        overlay.Color        = new Color(0f, 0f, 0f, 0.55f);
        overlay.AnchorRight  = 1f;
        overlay.AnchorBottom = 1f;
        overlay.MouseFilter  = Control.MouseFilterEnum.Ignore; // visual only, don't eat clicks
        _layer.AddChild(overlay);

        _panel = new Panel();
        _panel.AnchorLeft   = 0.5f; _panel.AnchorRight  = 0.5f;
        _panel.AnchorTop    = 0.5f; _panel.AnchorBottom = 0.5f;
        _panel.OffsetLeft   = -160f; _panel.OffsetRight  = 160f;
        _panel.OffsetTop    = -180f; _panel.OffsetBottom = 180f;

        var style = new StyleBoxFlat();
        style.BgColor          = new Color(0.1f, 0.1f, 0.1f, 0.97f);
        style.BorderColor      = new Color(0.45f, 0.45f, 0.45f);
        style.BorderWidthTop   = 2; style.BorderWidthBottom = 2;
        style.BorderWidthLeft  = 2; style.BorderWidthRight  = 2;
        _panel.AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.AnchorRight  = 1f; vbox.AnchorBottom = 1f;
        vbox.OffsetLeft   = 24f; vbox.OffsetRight  = -24f;
        vbox.OffsetTop    = 20f; vbox.OffsetBottom = -20f;
        vbox.AddThemeConstantOverride("separation", 14);
        _panel.AddChild(vbox);

        var title = new Label();
        title.Text                = "Paused";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 26);
        vbox.AddChild(title);

        var resumeBtn    = MakeButton("Resume");
        var settingsBtn  = MakeButton("Settings");
        var saveQuitBtn  = MakeButton("Save & Quit to Menu");

        vbox.AddChild(resumeBtn);
        vbox.AddChild(settingsBtn);
        vbox.AddChild(saveQuitBtn);

        resumeBtn.Pressed   += Close;
        settingsBtn.Pressed += () => { _settingsPanel.Visible = true; _panel.Visible = false; };
        saveQuitBtn.Pressed += OnSaveAndQuit;

        _layer.AddChild(_panel);
    }

    private void OnSaveAndQuit()
    {
        // Save world + inventory before leaving
        var cm = GetTree().Root.FindChild("ChunkManager", true, false) as ChunkManager;
        if (cm != null && _player != null)
        {
            cm.Call("SaveModifiedChunks");
            // Access inventory via reflection-safe public method
            _player.SaveInventoryFromPauseMenu(cm);
        }

        GetTree().Paused = false;
        GetTree().ChangeSceneToFile(MainMenuScene);
    }

    // ── Settings panel ───────────────────────────────────────────────────────

    private Panel BuildSettingsPanel()
    {
        var panel = new Panel();
        panel.AnchorLeft   = 0.5f; panel.AnchorRight  = 0.5f;
        panel.AnchorTop    = 0.5f; panel.AnchorBottom = 0.5f;
        panel.OffsetLeft   = -280f; panel.OffsetRight  = 280f;
        panel.OffsetTop    = -230f; panel.OffsetBottom = 230f;

        var style = new StyleBoxFlat();
        style.BgColor          = new Color(0.1f, 0.1f, 0.1f, 0.97f);
        style.BorderColor      = new Color(0.4f, 0.4f, 0.4f);
        style.BorderWidthTop   = 2; style.BorderWidthBottom = 2;
        style.BorderWidthLeft  = 2; style.BorderWidthRight  = 2;
        panel.AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.AnchorRight  = 1f; vbox.AnchorBottom = 1f;
        vbox.OffsetLeft   = 24f; vbox.OffsetRight  = -24f;
        vbox.OffsetTop    = 20f; vbox.OffsetBottom = -20f;
        vbox.AddThemeConstantOverride("separation", 16);
        panel.AddChild(vbox);

        var title = new Label();
        title.Text                = "Settings";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 22);
        vbox.AddChild(title);

        var sm = SettingsManager.Instance;

        (_sensitivitySlider, _sensitivityValue) = AddSlider(vbox, "Mouse Sensitivity",
            sm.MouseSensitivity, 0f, 100f, 1f);
        (_fovSlider,         _fovValue)         = AddSlider(vbox, "Field of View",
            sm.Fov, 50f, 120f, 1f);
        (_volumeSlider,      _volumeValue)      = AddSlider(vbox, "Master Volume",
            sm.MasterVolume, 0f, 1f, 0.01f);

        var fsRow = new HBoxContainer();
        fsRow.AddThemeConstantOverride("separation", 12);
        var fsLabel = new Label(); fsLabel.Text = "Fullscreen";
        fsLabel.CustomMinimumSize = new Vector2(180f, 0f);
        _fullscreenToggle = new CheckButton();
        _fullscreenToggle.ButtonPressed = sm.Fullscreen;
        fsRow.AddChild(fsLabel);
        fsRow.AddChild(_fullscreenToggle);
        vbox.AddChild(fsRow);

        _sensitivitySlider.ValueChanged += v =>
        {
            SettingsManager.Instance.SetMouseSensitivity((float)v);
            _sensitivityValue.Text = ((int)v).ToString();
        };
        _fovSlider.ValueChanged += v =>
        {
            SettingsManager.Instance.SetFov((float)v);
            _fovValue.Text = ((int)v).ToString();
        };
        _volumeSlider.ValueChanged += v =>
        {
            SettingsManager.Instance.SetMasterVolume((float)v);
            _volumeValue.Text = ((int)(v * 100)).ToString() + "%";
        };
        _fullscreenToggle.Toggled += v => SettingsManager.Instance.SetFullscreen(v);

        var closeBtn = MakeButton("Back");
        closeBtn.Pressed += () => { panel.Visible = false; _panel.Visible = true; };
        vbox.AddChild(closeBtn);

        return panel;
    }

private (HSlider, Label) AddSlider(VBoxContainer parent, string labelText,
        float current, float min, float max, float step)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);

        var lbl = new Label();
        lbl.Text              = labelText;
        lbl.CustomMinimumSize = new Vector2(180f, 0f);

        var slider = new HSlider();
        slider.MinValue            = min;
        slider.MaxValue            = max;
        slider.Step                = step;
        slider.Value               = current;
        slider.CustomMinimumSize   = new Vector2(140f, 24f);
        slider.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        // Value display — click to type a number directly
        string FormatVal(float v) => labelText == "Field of View"
            ? ((int)v).ToString()
            : labelText == "Master Volume"
                ? ((int)(v * 100)).ToString() + "%"
                : ((int)v).ToString();

        var valLbl = new Label();
        valLbl.Text                = FormatVal(current);
        valLbl.CustomMinimumSize   = new Vector2(48f, 0f);
        valLbl.HorizontalAlignment = HorizontalAlignment.Right;

        var valEdit = new LineEdit();
        valEdit.CustomMinimumSize   = new Vector2(48f, 0f);
        valEdit.Alignment = HorizontalAlignment.Right;
        valEdit.Visible             = false;

        // Click label → show LineEdit
        valLbl.GuiInput += (InputEvent ev) =>
        {
            if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                valEdit.Text    = slider.Value.ToString("F0");
                valLbl.Visible  = false;
                valEdit.Visible = true;
                valEdit.GrabFocus();
                valEdit.SelectAll();
            }
        };
        valLbl.MouseFilter = Control.MouseFilterEnum.Stop;

        // Confirm typed value on Enter or focus lost
        void CommitEdit()
        {
            if (float.TryParse(valEdit.Text, out float typed))
            {
                typed        = Mathf.Clamp(typed, min, max);
                slider.Value = typed;
                valLbl.Text  = FormatVal(typed);
            }
            valEdit.Visible = false;
            valLbl.Visible  = true;
        }
        valEdit.TextSubmitted  += _ => CommitEdit();
        valEdit.FocusExited    += CommitEdit;

        row.AddChild(lbl);
        row.AddChild(slider);
        row.AddChild(valLbl);
        row.AddChild(valEdit);
        parent.AddChild(row);

        return (slider, valLbl);
    }

    private Button MakeButton(string text)
    {
        var btn = new Button();
        btn.Text              = text;
        btn.CustomMinimumSize = new Vector2(240f, 44f);
        btn.AddThemeFontSizeOverride("font_size", 16);
        return btn;
    }
}