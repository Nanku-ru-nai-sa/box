using Godot;
using System;
using System.Collections.Generic;

// Attach to the root node of res://Scenes/MainMenu.tscn
// Everything is built in code.

public partial class MainMenu : Control
{
    private const string GameScene = "res://Scenes/Test.tscn";

    // World creation options
    private static readonly string[] ThemeOptions      = { "Normal", "Only Forest", "Only Desert" };
    private static readonly string[] StartBonusOptions = { "None" };
    private static readonly string[] TypeOptions        = { "Normal", "Flat", "Sky Islands", "One Block" };    
    private static readonly string[] SeasonOptions      = { "Spring", "Summer", "Autumn", "Winter" };

    // Character creation options
    private static readonly string[] CheatOptions      = { "Off", "On" };
    private static readonly string[] KeepInvOptions    = { "Off", "On" };
    private static readonly string[] GamemodeOptions   = { "Survival", "Create", "Story" };
    private static readonly string[] DifficultyOptions = { "Peaceful", "Easy", "Normal", "Hard" };

    private VBoxContainer _buttonCol;
    private VBoxContainer _settingsPanel;
    private VBoxContainer _characterPanel;
    private VBoxContainer _worldSelectPanel;
    private VBoxContainer _characterSelectPanel;
    private VBoxContainer _newWorldOptionsPanel;
    private VBoxContainer _newCharacterOptionsPanel;

    private VBoxContainer _worldListContainer;
    private VBoxContainer _characterListContainer;

    private Label _activeCharacterName;
    private Label _activeCharacterInfo;

    private LineEdit _worldNameEdit;
    private LineEdit _worldSeedEdit;
    private CycleSelector _themeSelector      = new CycleSelector();
    private CycleSelector _startBonusSelector = new CycleSelector();
    private CycleSelector _typeSelector       = new CycleSelector();
    private CycleSelector _seasonSelector     = new CycleSelector();

    private LineEdit _characterNameEdit;
    private CycleSelector _cheatSelector          = new CycleSelector();
    private CycleSelector _keepInvSelector        = new CycleSelector();
    private CycleSelector _charGamemodeSelector   = new CycleSelector();
    private CycleSelector _charDifficultySelector = new CycleSelector();

    // settings fields
    private HSlider  _sensitivitySlider;
    private HSlider  _fovSlider;
    private HSlider  _volumeSlider;
    private CheckButton _fullscreenToggle;
    private Label _sensitivityValue;
    private Label _fovValue;
    private Label _volumeValue;

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;

        AnchorRight  = 1f;
        AnchorBottom = 1f;

        BuildUI();
    }

    private void BuildUI()
    {
        var bg = new ColorRect();
        bg.Color       = new Color(0.05f, 0.05f, 0.05f, 1f);
        bg.AnchorRight = 1f; bg.AnchorBottom = 1f;
        AddChild(bg);

        var title = new Label();
        title.Text                = "BOX";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AnchorLeft          = 0.5f; title.AnchorRight = 0.5f;
        title.AnchorTop           = 0.15f;
        title.OffsetLeft          = -200f; title.OffsetRight = 200f;
        title.AddThemeFontSizeOverride("font_size", 64);
        title.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f));
        AddChild(title);

        // Left button column
        _buttonCol = new VBoxContainer();
        _buttonCol.AnchorLeft   = 0.5f; _buttonCol.AnchorRight = 0.5f;
        _buttonCol.AnchorTop    = 0.4f; _buttonCol.AnchorBottom = 0.85f;
        _buttonCol.OffsetLeft   = -120f; _buttonCol.OffsetRight = 120f;
        _buttonCol.AddThemeConstantOverride("separation", 14);
        AddChild(_buttonCol);

        var worldsBtn   = MakeButton("Worlds");
        var zombiesBtn  = MakeButton("Zombies");
        var settingsBtn = MakeButton("Settings");
        var quitBtn     = MakeButton("Quit");

        // Not built yet — grayed out and non-interactive until there's something behind it
        zombiesBtn.Disabled = true;
        zombiesBtn.AddThemeColorOverride("font_color", new Color(0.45f, 0.45f, 0.45f));
        zombiesBtn.AddThemeColorOverride("font_disabled_color", new Color(0.45f, 0.45f, 0.45f));

        _buttonCol.AddChild(worldsBtn);
        _buttonCol.AddChild(zombiesBtn);
        _buttonCol.AddChild(settingsBtn);
        _buttonCol.AddChild(quitBtn);

        worldsBtn.Pressed   += OnPlayPressed;
        settingsBtn.Pressed += () => { _settingsPanel.Visible = true; SetMainScreenVisible(false); };
        quitBtn.Pressed     += () => GetTree().Quit();

        // Right-side character panel
        _characterPanel = BuildCharacterPanel();
        AddChild(_characterPanel);

        // Settings
        _settingsPanel = BuildSettingsPanel();
        AddChild(_settingsPanel);
        _settingsPanel.Visible = false;

        // World select
        _worldSelectPanel = BuildWorldSelectPanel();
        AddChild(_worldSelectPanel);
        _worldSelectPanel.Visible = false;

        // New World options
        _newWorldOptionsPanel = BuildNewWorldOptionsPanel();
        AddChild(_newWorldOptionsPanel);
        _newWorldOptionsPanel.Visible = false;

        // Character select
        _characterSelectPanel = BuildCharacterSelectPanel();
        AddChild(_characterSelectPanel);
        _characterSelectPanel.Visible = false;

        // New Character options
        _newCharacterOptionsPanel = BuildNewCharacterOptionsPanel();
        AddChild(_newCharacterOptionsPanel);
        _newCharacterOptionsPanel.Visible = false;

        RefreshCharacterPanel();
    }

    private void SetMainScreenVisible(bool visible)
    {
        _buttonCol.Visible = visible;
        _characterPanel.Visible = visible;
    }

    private void OnPlayPressed()
    {
        RefreshWorldList();
        _worldSelectPanel.Visible = true;
        SetMainScreenVisible(false);
    }

    // ── Character panel (right side) ────────────────────────────────────────

    private VBoxContainer BuildCharacterPanel()
    {
        var vbox = new VBoxContainer();
        vbox.AnchorLeft = 0.78f; vbox.AnchorRight  = 0.98f;
        vbox.AnchorTop  = 0.35f; vbox.AnchorBottom = 0.75f;
        vbox.AddThemeConstantOverride("separation", 10);

        var preview = new ColorRect();
        preview.Color = new Color(0.15f, 0.15f, 0.15f, 1f);
        preview.CustomMinimumSize = new Vector2(0f, 120f);
        vbox.AddChild(preview);

        _activeCharacterName = new Label();
        _activeCharacterName.HorizontalAlignment = HorizontalAlignment.Center;
        _activeCharacterName.AddThemeFontSizeOverride("font_size", 18);
        vbox.AddChild(_activeCharacterName);

        _activeCharacterInfo = new Label();
        _activeCharacterInfo.HorizontalAlignment = HorizontalAlignment.Center;
        _activeCharacterInfo.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        vbox.AddChild(_activeCharacterInfo);

        var switchBtn = MakeButton("Switch");
        switchBtn.Pressed += () =>
        {
            RefreshCharacterList();
            _characterSelectPanel.Visible = true;
            SetMainScreenVisible(false);
        };
        vbox.AddChild(switchBtn);

        return vbox;
    }

    private void RefreshCharacterPanel()
    {
        var characters = SaveManager.Instance.ListCharacters();

        if (string.IsNullOrEmpty(SaveManager.Instance.ActiveCharacterId) && characters.Count > 0)
            SaveManager.Instance.SetActiveCharacter(characters[0].Id);

        if (string.IsNullOrEmpty(SaveManager.Instance.ActiveCharacterId))
        {
            _activeCharacterName.Text = "No Character";
            _activeCharacterInfo.Text = "Create one to begin";
            return;
        }

        var meta = SaveManager.Instance.LoadCharacterMeta(SaveManager.Instance.ActiveCharacterId);
        _activeCharacterName.Text = meta.DisplayName;
        _activeCharacterInfo.Text = $"{meta.LockedGameMode} — {meta.LockedDifficulty}";
    }

    // ── World select ─────────────────────────────────────────────────────────

    private VBoxContainer BuildWorldSelectPanel()
    {
        var vbox = new VBoxContainer();
        vbox.AnchorLeft   = 0.5f; vbox.AnchorRight = 0.5f;
        vbox.AnchorTop    = 0.5f; vbox.AnchorBottom = 0.5f;
        vbox.OffsetLeft   = -260f; vbox.OffsetRight  = 260f;
        vbox.OffsetTop    = -220f; vbox.OffsetBottom = 220f;
        vbox.AddThemeConstantOverride("separation", 12);

        var title = new Label();
        title.Text = "Select World";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 20);
        vbox.AddChild(title);

        _worldListContainer = new VBoxContainer();
        _worldListContainer.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(_worldListContainer);

        var bottomRow = new HBoxContainer();
        bottomRow.AddThemeConstantOverride("separation", 8);

        var backBtn = MakeButton("Back");
        backBtn.Pressed += () => { _worldSelectPanel.Visible = false; SetMainScreenVisible(true); };

        var spacer = new Control();
        spacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var newWorldBtn = MakeButton("New World");
        newWorldBtn.Pressed += OnNewWorldPressed;

        bottomRow.AddChild(backBtn);
        bottomRow.AddChild(spacer);
        bottomRow.AddChild(newWorldBtn);
        vbox.AddChild(bottomRow);

        return vbox;
    }

    private void RefreshWorldList()
    {
        foreach (Node child in _worldListContainer.GetChildren())
            child.QueueFree();

        foreach (WorldMeta world in SaveManager.Instance.ListWorlds())
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);

            var nameBtn = MakeButton(world.DisplayName);
            nameBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            nameBtn.Pressed += () =>
            {
                SaveManager.Instance.SetActiveWorld(world.Id);
                GetTree().ChangeSceneToFile(GameScene);
            };

            var deleteBtn = new Button();
            deleteBtn.Text = "X";
            deleteBtn.CustomMinimumSize = new Vector2(44f, 44f);
            deleteBtn.AddThemeFontSizeOverride("font_size", 16);
            deleteBtn.Pressed += () =>
            {
                ConfirmDelete(world.DisplayName, () =>
                {
                    SaveManager.Instance.DeleteWorld(world.Id);
                    RefreshWorldList();
                });
            };

            row.AddChild(nameBtn);
            row.AddChild(deleteBtn);
            _worldListContainer.AddChild(row);
        }
    }

    // ── New World options screen ────────────────────────────────────────────

    private VBoxContainer BuildNewWorldOptionsPanel()
    {
        var vbox = new VBoxContainer();
        vbox.AnchorLeft   = 0.5f; vbox.AnchorRight = 0.5f;
        vbox.AnchorTop    = 0.5f; vbox.AnchorBottom = 0.5f;
        vbox.OffsetLeft   = -260f; vbox.OffsetRight  = 260f;
        vbox.OffsetTop    = -270f; vbox.OffsetBottom = 270f;
        vbox.AddThemeConstantOverride("separation", 14);

        var title = new Label();
        title.Text = "New World";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 20);
        vbox.AddChild(title);

        var nameLbl = new Label();
        nameLbl.Text = "World Name";
        vbox.AddChild(nameLbl);

        _worldNameEdit = new LineEdit();
        _worldNameEdit.PlaceholderText = "My World";
        _worldNameEdit.CustomMinimumSize = new Vector2(0f, 32f);
        vbox.AddChild(_worldNameEdit);

        var seedLbl = new Label();
        seedLbl.Text = "Seed";
        vbox.AddChild(seedLbl);

        _worldSeedEdit = new LineEdit();
        _worldSeedEdit.PlaceholderText = "Leave blank for random";
        _worldSeedEdit.CustomMinimumSize = new Vector2(0f, 32f);
        vbox.AddChild(_worldSeedEdit);

        var grid = new GridContainer();
        grid.Columns = 2;
        grid.AddThemeConstantOverride("h_separation", 24);
        grid.AddThemeConstantOverride("v_separation", 14);

        grid.AddChild(_themeSelector.Build("Theme", ThemeOptions));
        grid.AddChild(_startBonusSelector.Build("Start Bonus", StartBonusOptions));
        grid.AddChild(_typeSelector.Build("Type", TypeOptions));
        grid.AddChild(_seasonSelector.Build("Season", SeasonOptions, showLockToggle: true));

        vbox.AddChild(grid);

        var bottomRow = new HBoxContainer();
        bottomRow.AddThemeConstantOverride("separation", 8);

        var backBtn = MakeButton("Back");
        backBtn.Pressed += () =>
        {
            _newWorldOptionsPanel.Visible = false;
            _worldSelectPanel.Visible = true;
        };

        var spacer = new Control();
        spacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var createBtn = MakeButton("Create");
        createBtn.Pressed += OnCreateWorldConfirmed;

        bottomRow.AddChild(backBtn);
        bottomRow.AddChild(spacer);
        bottomRow.AddChild(createBtn);
        vbox.AddChild(bottomRow);

        return vbox;
    }

    private void OnNewWorldPressed()
    {
        _worldNameEdit.Text = "";
        _worldSeedEdit.Text = "";
        _themeSelector.Reset();
        _startBonusSelector.Reset();
        _typeSelector.Reset();
        _seasonSelector.Reset();

        _worldSelectPanel.Visible = false;
        _newWorldOptionsPanel.Visible = true;
    }

    private void OnCreateWorldConfirmed()
    {
        string worldName = string.IsNullOrWhiteSpace(_worldNameEdit.Text) ? "New World" : _worldNameEdit.Text;

        long seed;
        if (!long.TryParse(_worldSeedEdit.Text, out seed))
            seed = new Random().NextInt64();

        var world = SaveManager.Instance.CreateWorld(
    worldName,
    seed,
    _themeSelector.CurrentValue,
    _startBonusSelector.CurrentValue,
    _typeSelector.CurrentValue,
    _seasonSelector.CurrentValue,
    _seasonSelector.IsLocked
);

        SaveManager.Instance.SetActiveWorld(world.Id);
        GetTree().ChangeSceneToFile(GameScene);
    }

    // ── New Character options screen ────────────────────────────────────────

    private VBoxContainer BuildNewCharacterOptionsPanel()
    {
        var vbox = new VBoxContainer();
        vbox.AnchorLeft   = 0.5f; vbox.AnchorRight = 0.5f;
        vbox.AnchorTop    = 0.5f; vbox.AnchorBottom = 0.5f;
        vbox.OffsetLeft   = -260f; vbox.OffsetRight  = 260f;
        vbox.OffsetTop    = -260f; vbox.OffsetBottom = 260f;
        vbox.AddThemeConstantOverride("separation", 14);

        var title = new Label();
        title.Text = "New Character";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 20);
        vbox.AddChild(title);

        var nameLbl = new Label();
        nameLbl.Text = "Character Name";
        vbox.AddChild(nameLbl);

        _characterNameEdit = new LineEdit();
        _characterNameEdit.PlaceholderText = "My Character";
        _characterNameEdit.CustomMinimumSize = new Vector2(0f, 32f);
        vbox.AddChild(_characterNameEdit);

        var grid = new GridContainer();
        grid.Columns = 2;
        grid.AddThemeConstantOverride("h_separation", 24);
        grid.AddThemeConstantOverride("v_separation", 14);

        grid.AddChild(_cheatSelector.Build("Cheatcodes", CheatOptions));
        grid.AddChild(_keepInvSelector.Build("Keep Inv", KeepInvOptions));
        grid.AddChild(_charGamemodeSelector.Build("Gamemode", GamemodeOptions));
        grid.AddChild(_charDifficultySelector.Build("Difficulty", DifficultyOptions));

        vbox.AddChild(grid);

        var bottomRow = new HBoxContainer();
        bottomRow.AddThemeConstantOverride("separation", 8);

        var backBtn = MakeButton("Back");
        backBtn.Pressed += () =>
        {
            _newCharacterOptionsPanel.Visible = false;
            _characterSelectPanel.Visible = true;
        };

        var spacer = new Control();
        spacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var createBtn = MakeButton("Create");
        createBtn.Pressed += OnCreateCharacterConfirmed;

        bottomRow.AddChild(backBtn);
        bottomRow.AddChild(spacer);
        bottomRow.AddChild(createBtn);
        vbox.AddChild(bottomRow);

        return vbox;
    }

    private void OnNewCharacterPressed()
    {
        _characterNameEdit.Text = "";
        _cheatSelector.Reset();
        _keepInvSelector.Reset();
        _charGamemodeSelector.Reset();
        _charDifficultySelector.Reset();

        _characterSelectPanel.Visible = false;
        _newCharacterOptionsPanel.Visible = true;
    }

    private void OnCreateCharacterConfirmed()
    {
        string charName = string.IsNullOrWhiteSpace(_characterNameEdit.Text) ? "New Character" : _characterNameEdit.Text;

        var character = SaveManager.Instance.CreateCharacter(
            charName,
            _cheatSelector.CurrentValue,
            _keepInvSelector.CurrentValue,
            _charGamemodeSelector.CurrentValue,
            _charDifficultySelector.CurrentValue
        );

        SaveManager.Instance.SetActiveCharacter(character.Id);
        _newCharacterOptionsPanel.Visible = false;
        SetMainScreenVisible(true);
        RefreshCharacterPanel();
    }

    // Small reusable "< Value >" cycling control, optionally with a small lock
    // icon underneath to signal the setting can't be changed after creation.
    private class CycleSelector
{
    private string[] _options;
    private int _index;
    private Label _valueLabel;
    private Button _lockBtn;

    public string CurrentValue => _options[_index];
    public bool IsLocked { get; private set; } = false;

    public void Reset()
    {
        _index = 0;
        IsLocked = false;
        if (_valueLabel != null) _valueLabel.Text = CurrentValue;
        if (_lockBtn != null) _lockBtn.Text = "🔓";
    }

    public VBoxContainer Build(string title, string[] options, bool showLockToggle = false)
    {
        _options = options;
        _index = 0;

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 4);

        var titleLbl = new Label();
        titleLbl.Text = title;
        titleLbl.HorizontalAlignment = HorizontalAlignment.Center;
        titleLbl.AddThemeFontSizeOverride("font_size", 14);
        vbox.AddChild(titleLbl);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        row.Alignment = BoxContainer.AlignmentMode.Center;

        var leftBtn = new Button();
        leftBtn.Text = "<";
        leftBtn.CustomMinimumSize = new Vector2(32f, 32f);
        leftBtn.Pressed += () =>
        {
            _index = (_index - 1 + _options.Length) % _options.Length;
            _valueLabel.Text = CurrentValue;
        };

        _valueLabel = new Label();
        _valueLabel.Text = CurrentValue;
        _valueLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _valueLabel.CustomMinimumSize = new Vector2(110f, 0f);

        var rightBtn = new Button();
        rightBtn.Text = ">";
        rightBtn.CustomMinimumSize = new Vector2(32f, 32f);
        rightBtn.Pressed += () =>
        {
            _index = (_index + 1) % _options.Length;
            _valueLabel.Text = CurrentValue;
        };

        row.AddChild(leftBtn);
        row.AddChild(_valueLabel);
        row.AddChild(rightBtn);

        if (showLockToggle)
        {
            _lockBtn = new Button();
            _lockBtn.Text = "🔓";
            _lockBtn.TooltipText = "Lock to a single season";
            _lockBtn.CustomMinimumSize = new Vector2(32f, 32f);
            _lockBtn.Pressed += () =>
            {
                IsLocked = !IsLocked;
                _lockBtn.Text = IsLocked ? "🔒" : "🔓";
            };
            row.AddChild(_lockBtn);
        }

        vbox.AddChild(row);

        return vbox;
    }
}

    // ── Character select ─────────────────────────────────────────────────────

    private VBoxContainer BuildCharacterSelectPanel()
    {
        var vbox = new VBoxContainer();
        vbox.AnchorLeft   = 0.5f; vbox.AnchorRight = 0.5f;
        vbox.AnchorTop    = 0.5f; vbox.AnchorBottom = 0.5f;
        vbox.OffsetLeft   = -260f; vbox.OffsetRight  = 260f;
        vbox.OffsetTop    = -220f; vbox.OffsetBottom = 220f;
        vbox.AddThemeConstantOverride("separation", 12);

        var title = new Label();
        title.Text = "Select Character";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 20);
        vbox.AddChild(title);

        _characterListContainer = new VBoxContainer();
        _characterListContainer.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(_characterListContainer);

        var bottomRow = new HBoxContainer();
        bottomRow.AddThemeConstantOverride("separation", 8);

        var backBtn = MakeButton("Back");
        backBtn.Pressed += () => { _characterSelectPanel.Visible = false; SetMainScreenVisible(true); RefreshCharacterPanel(); };

        var spacer = new Control();
        spacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var newCharBtn = MakeButton("New Character");
        newCharBtn.Pressed += OnNewCharacterPressed;

        bottomRow.AddChild(backBtn);
        bottomRow.AddChild(spacer);
        bottomRow.AddChild(newCharBtn);
        vbox.AddChild(bottomRow);

        return vbox;
    }

    private void RefreshCharacterList()
    {
        foreach (Node child in _characterListContainer.GetChildren())
            child.QueueFree();

        foreach (CharacterMeta character in SaveManager.Instance.ListCharacters())
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);

            var nameBtn = MakeButton(character.DisplayName);
            nameBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            nameBtn.Pressed += () =>
            {
                SaveManager.Instance.SetActiveCharacter(character.Id);
                _characterSelectPanel.Visible = false;
                SetMainScreenVisible(true);
                RefreshCharacterPanel();
            };

            var deleteBtn = new Button();
            deleteBtn.Text = "X";
            deleteBtn.CustomMinimumSize = new Vector2(44f, 44f);
            deleteBtn.AddThemeFontSizeOverride("font_size", 16);
            deleteBtn.Pressed += () =>
            {
                ConfirmDelete(character.DisplayName, () =>
                {
                    SaveManager.Instance.DeleteCharacter(character.Id);
                    RefreshCharacterList();
                    RefreshCharacterPanel();
                });
            };

            row.AddChild(nameBtn);
            row.AddChild(deleteBtn);
            _characterListContainer.AddChild(row);
        }
    }

    // ── Delete confirmation ──────────────────────────────────────────────────

    private void ConfirmDelete(string itemName, Action onConfirmed)
    {
        var dialog = new ConfirmationDialog();
        dialog.DialogText = $"Delete '{itemName}'? This cannot be undone.";
        dialog.OkButtonText = "Delete";
        AddChild(dialog);

        dialog.Confirmed += () =>
        {
            onConfirmed();
            dialog.QueueFree();
        };
        dialog.Canceled += () => dialog.QueueFree();

        dialog.PopupCentered();
    }

    // ── Settings ─────────────────────────────────────────────────────────────

    private VBoxContainer BuildSettingsPanel()
    {
        var vbox = new VBoxContainer();
        vbox.AnchorLeft   = 0.5f; vbox.AnchorRight  = 0.5f;
        vbox.AnchorTop    = 0.5f; vbox.AnchorBottom = 0.5f;
        vbox.OffsetLeft   = -180f; vbox.OffsetRight  = 180f;
        vbox.OffsetTop    = -180f; vbox.OffsetBottom = 180f;
        vbox.AddThemeConstantOverride("separation", 16);

        var title = new Label();
        title.Text                = "Settings";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 22);
        vbox.AddChild(title);

        var sm = SettingsManager.Instance;

        (_sensitivitySlider, _sensitivityValue) = AddSlider(vbox, "Mouse Sensitivity",
            sm.MouseSensitivity, 0.01f, 1.0f, 0.01f);
        (_fovSlider, _fovValue) = AddSlider(vbox, "Field of View",
            sm.Fov, 50f, 120f, 1f);
        (_volumeSlider, _volumeValue) = AddSlider(vbox, "Master Volume",
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

        var closeBtn = MakeButton("Close");
        closeBtn.Pressed += () => { _settingsPanel.Visible = false; SetMainScreenVisible(true); };
        vbox.AddChild(closeBtn);

        return vbox;
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
        valEdit.CustomMinimumSize = new Vector2(48f, 0f);
        valEdit.Alignment         = HorizontalAlignment.Right;
        valEdit.Visible           = false;

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
        valEdit.TextSubmitted += _ => CommitEdit();
        valEdit.FocusExited   += CommitEdit;

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