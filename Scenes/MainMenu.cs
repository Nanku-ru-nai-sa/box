using Godot;

// Attach to the root node of res://Scenes/MainMenu.tscn
// That scene just needs this script — everything is built in code.

public partial class MainMenu : Control
{
    private const string GameScene = "res://Scenes/Test.tscn";

    private Panel        _settingsPanel;
    private VBoxContainer _buttonCol;
    private HSlider      _sensitivitySlider;
    private HSlider      _fovSlider;
    private HSlider      _volumeSlider;
    private CheckButton  _fullscreenToggle;
    private Label        _sensitivityValue;
    private Label        _fovValue;
    private Label        _volumeValue;

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;

        // Fill the entire viewport
        AnchorRight  = 1f;
        AnchorBottom = 1f;

        BuildUI();
    }

    // Safe accessor — returns defaults if SettingsManager not yet autoloaded
    private float GetSens()   => SettingsManager.Instance?.MouseSensitivity ?? 0.15f;
    private float GetFov()    => SettingsManager.Instance?.Fov              ?? 75f;
    private float GetVol()    => SettingsManager.Instance?.MasterVolume     ?? 1.0f;
    private bool  GetFs()     => SettingsManager.Instance?.Fullscreen       ?? false;

    private void BuildUI()
    {
        // Dark background
        var bg = new ColorRect();
        bg.Color       = new Color(0.05f, 0.05f, 0.05f, 1f);
        bg.AnchorRight = 1f; bg.AnchorBottom = 1f;
        AddChild(bg);

        // Game title
        var title = new Label();
        title.Text                = "BOX";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AnchorLeft          = 0.5f; title.AnchorRight = 0.5f;
        title.AnchorTop           = 0.15f;
        title.OffsetLeft          = -200f; title.OffsetRight = 200f;
        title.AddThemeFontSizeOverride("font_size", 64);
        title.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f));
        AddChild(title);

        // Button column
        _buttonCol = new VBoxContainer();
        var col = _buttonCol;
        col.AnchorLeft   = 0.5f; col.AnchorRight = 0.5f;
        col.AnchorTop    = 0.4f; col.AnchorBottom = 0.85f;
        col.OffsetLeft   = -120f; col.OffsetRight = 120f;
        col.AddThemeConstantOverride("separation", 14);
        AddChild(col);

        var playBtn     = MakeButton("Play");
        var settingsBtn = MakeButton("Settings");
        var quitBtn     = MakeButton("Quit");

        col.AddChild(playBtn);
        col.AddChild(settingsBtn);
        col.AddChild(quitBtn);

        playBtn.Pressed     += OnPlay;
        settingsBtn.Pressed += () => { _settingsPanel.Visible = true; _buttonCol.Visible = false; };
        quitBtn.Pressed     += () => GetTree().Quit();

        // Settings panel (hidden by default)
        _settingsPanel = BuildSettingsPanel();
        AddChild(_settingsPanel);
        _settingsPanel.Visible = false;
    }

    private void OnPlay()
    {
        GetTree().ChangeSceneToFile(GameScene);
    }

    // ── Settings panel ───────────────────────────────────────────────────────

    private Panel BuildSettingsPanel()
    {
        var panel = new Panel();
        panel.AnchorLeft   = 0.5f; panel.AnchorRight  = 0.5f;
        panel.AnchorTop    = 0.5f; panel.AnchorBottom = 0.5f;
        panel.OffsetLeft   = -280f; panel.OffsetRight  = 280f;
        panel.OffsetTop    = -220f; panel.OffsetBottom = 220f;

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

        // Title
        var title = new Label();
        title.Text                = "Settings";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 22);
        vbox.AddChild(title);

        // Sliders
        var sm = SettingsManager.Instance;

        (_sensitivitySlider, _sensitivityValue) = AddSlider(vbox, "Mouse Sensitivity",
            sm.MouseSensitivity, 0.01f, 1.0f, 0.01f);
        (_fovSlider,         _fovValue)         = AddSlider(vbox, "Field of View",
            sm.Fov, 50f, 120f, 1f);
        (_volumeSlider,      _volumeValue)      = AddSlider(vbox, "Master Volume",
            sm.MasterVolume, 0f, 1f, 0.01f);

        // Fullscreen toggle
        var fsRow = new HBoxContainer();
        fsRow.AddThemeConstantOverride("separation", 12);
        var fsLabel = new Label(); fsLabel.Text = "Fullscreen";
        fsLabel.CustomMinimumSize = new Vector2(180f, 0f);
        _fullscreenToggle         = new CheckButton();
        _fullscreenToggle.ButtonPressed = sm.Fullscreen;
        fsRow.AddChild(fsLabel);
        fsRow.AddChild(_fullscreenToggle);
        vbox.AddChild(fsRow);

        // Wire slider changes
        _sensitivitySlider.ValueChanged += v =>
        {
            SettingsManager.Instance?.SetMouseSensitivity((float)v);
            _sensitivityValue.Text = ((int)v).ToString();
        };
        _fovSlider.ValueChanged += v =>
        {
            SettingsManager.Instance?.SetFov((float)v);
            _fovValue.Text = ((int)v).ToString();
        };
        _volumeSlider.ValueChanged += v =>
        {
            SettingsManager.Instance?.SetMasterVolume((float)v);
            _volumeValue.Text = ((int)(v * 100)).ToString() + "%";
        };
        _fullscreenToggle.Toggled += v => SettingsManager.Instance?.SetFullscreen(v);

        // Close button
        var closeBtn = MakeButton("Close");
        closeBtn.Pressed += () => { panel.Visible = false; _buttonCol.Visible = true; };
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
        btn.Text                  = text;
        btn.CustomMinimumSize     = new Vector2(240f, 44f);
        btn.AddThemeFontSizeOverride("font_size", 16);
        return btn;
    }
}