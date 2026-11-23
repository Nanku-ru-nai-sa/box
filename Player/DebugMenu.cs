using Godot;
using System;

public partial class DebugMenu : CanvasLayer
{
    private Panel _panel;
    private Label _label;
    private Button _pauseTimeButton;
    private Button _advanceDayButton;
    private Button _advanceHourButton;
    private Button _seasonLockButton;
    private Button _chunkBordersButton;
    private Button _closeButton;
    private Input.MouseModeEnum _previousMouseMode = Input.MouseModeEnum.Captured;

    private bool _visible = false;

    private bool _f6Held = false;
    private bool _f7Held = false;
    private bool _f9Held = false;
    private bool _f10Held = false;

    public override void _Ready()
    {
        Layer = 100;

        BuildMenu();

        // Always start hidden.
        _visible = false;
        _panel.Visible = false;
    }

    public override void _Process(double delta)
    {
        // ========================================================
        // WORLD CHECK
        // ========================================================
        //
        // The Debug Menu is only available when a DayNightCycle
        // exists in the active world.
        //
        // Main menu = no DayNightCycle = debug menu unavailable.
        // ========================================================

        DayNightCycle dayNight =
            GetDayNightCycle();

        bool inWorld =
            dayNight != null &&
            IsInstanceValid(dayNight) &&
            dayNight.IsInsideTree();

        // ========================================================
        // NOT IN A WORLD
        // ========================================================

        if (!inWorld)
        {
            // Force the debug menu closed.
            if (_visible)
            {
                _visible = false;
                _panel.Visible = false;

                GD.Print(
                    "[DebugMenu] Closed - no active world."
                );
            }

            // Reset key states so returning to a world
            // doesn't accidentally trigger a key press.
            _f6Held = false;
            _f7Held = false;
            _f9Held = false;
            _f10Held = false;

            return;
        }

        // ========================================================
        // F6 - OPEN / CLOSE DEBUG MENU
        // ========================================================

        if (Input.IsKeyPressed(Key.F6))
        {
            if (!_f6Held)
            {
                _f6Held = true;

                SetMenuVisible(!_visible);
            }
        }
        else
        {
            _f6Held = false;
        }

        // ========================================================
        // MENU CLOSED
        // ========================================================

        if (!_visible)
            return;

        // ========================================================
        // F7 - ADVANCE ONE DAY
        // ========================================================

        if (Input.IsKeyPressed(Key.F7))
        {
            if (!_f7Held)
            {
                _f7Held = true;

                AdvanceDay();
            }
        }
        else
        {
            _f7Held = false;
        }

        // ========================================================
        // F9 - TOGGLE SEASON LOCK
        // ========================================================

        if (Input.IsKeyPressed(Key.F9))
        {
            if (!_f9Held)
            {
                _f9Held = true;

                ToggleSeasonLock();
            }
        }
        else
        {
            _f9Held = false;
        }

        // ========================================================
        // F10 - ADVANCE ONE HOUR
        // ========================================================

        if (Input.IsKeyPressed(Key.F10))
        {
            if (!_f10Held)
            {
                _f10Held = true;

                AdvanceHour();
            }
        }
        else
        {
            _f10Held = false;
        }

        // ========================================================
        // UPDATE DISPLAY
        // ========================================================

        UpdateDisplay();
    }

    // ============================================================
    // BUILD MENU
    // ============================================================

    private void BuildMenu()
    {
        _panel = new Panel();

        _panel.Position =
            new Vector2(20, 5);

        _panel.Size =
            new Vector2(375, 1070);
        _panel.ProcessMode = ProcessModeEnum.Always;
        _panel.MouseFilter = Control.MouseFilterEnum.Stop;

        AddChild(_panel);

        var style =
            new StyleBoxFlat();

        style.BgColor =
            new Color(
                0.03f,
                0.03f,
                0.03f,
                0.92f
            );

        style.BorderWidthLeft = 2;
        style.BorderWidthTop = 2;
        style.BorderWidthRight = 2;
        style.BorderWidthBottom = 2;

        style.BorderColor =
            new Color(
                0.5f,
                0.5f,
                0.5f,
                0.9f
            );

        style.CornerRadiusTopLeft = 6;
        style.CornerRadiusTopRight = 6;
        style.CornerRadiusBottomLeft = 6;
        style.CornerRadiusBottomRight = 6;

        _panel.AddThemeStyleboxOverride(
            "panel",
            style
        );

        _label = new Label();

        _label.Position =
            new Vector2(20, 15);

        _label.Size =
            new Vector2(335, 730);
        _label.MouseFilter = Control.MouseFilterEnum.Ignore;

        _label.AddThemeFontSizeOverride(
            "font_size",
            18
        );

        _panel.AddChild(_label);

        _pauseTimeButton = CreateTestButton("Pause Time", 740, OnPauseTimePressed);
        _advanceDayButton = CreateTestButton("Advance One Day", 795, AdvanceDay);
        _advanceHourButton = CreateTestButton("Advance One Hour", 850, AdvanceHour);
        _seasonLockButton = CreateTestButton("Toggle Season Lock", 905, ToggleSeasonLock);
        _chunkBordersButton = CreateTestButton("Chunk Borders: OFF", 960, ToggleChunkBorders);
        _closeButton = CreateTestButton("Close Debug Menu", 1015, () => SetMenuVisible(false));
    }

    private Button CreateTestButton(string text, float y, Action pressed)
    {
        var button = new Button();
        button.Position = new Vector2(20, y);
        button.Size = new Vector2(335, 45);
        button.Text = text;
        button.MouseFilter = Control.MouseFilterEnum.Stop;
        button.Pressed += pressed;
        _panel.AddChild(button);
        return button;
    }

    private void SetMenuVisible(bool visible)
    {
        _visible = visible;
        _panel.Visible = visible;

        if (visible)
        {
            _previousMouseMode = Input.MouseMode;
            Input.MouseMode = Input.MouseModeEnum.Visible;
            UpdateDisplay();
        }
        else
        {
            Input.MouseMode = _previousMouseMode == Input.MouseModeEnum.Captured
                ? Input.MouseModeEnum.Captured
                : _previousMouseMode;
        }
    }

    private void ToggleChunkBorders()
    {
        Player player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (player == null)
            return;

        player.SetChunkBordersFromDebugMenu(!player.AreChunkBordersVisible);
        UpdateDisplay();
    }

    private void OnPauseTimePressed()
    {
        DayNightCycle dayNight = GetDayNightCycle();

        if (dayNight == null || !IsInstanceValid(dayNight))
            return;

        dayNight.ToggleTimePause();
        UpdateDisplay();
    }

    // ============================================================
    // UPDATE DISPLAY
    // ============================================================

    private void UpdateDisplay()
    {
        // --------------------------------------------------------
        // Make absolutely sure we're still in a world.
        // --------------------------------------------------------

        DayNightCycle dayNight =
            GetDayNightCycle();

        if (dayNight == null ||
            !IsInstanceValid(dayNight) ||
            !dayNight.IsInsideTree())
        {
            _visible = false;

            if (IsInstanceValid(_panel))
                _panel.Visible = false;

            return;
        }

        SeasonManager seasonManager =
            GetSeasonManager();

        if (seasonManager == null)
        {
            _label.Text =
                "BOX BUG\n\n" +
                "SeasonManager not found.";

            return;
        }

        // ========================================================
        // SEASON STATES
        // ========================================================

        string spring =
            seasonManager.SpringEnabled
                ? "ON"
                : "OFF";

        string summer =
            seasonManager.SummerEnabled
                ? "ON"
                : "OFF";

        string autumn =
            seasonManager.AutumnEnabled
                ? "ON"
                : "OFF";

        string winter =
            seasonManager.WinterEnabled
                ? "ON"
                : "OFF";

        string lockState =
            seasonManager.SeasonLocked
                ? "LOCKED"
                : "UNLOCKED";

        // ========================================================
        // TIME
        // ========================================================

        string timeString =
            "N/A";

        string timeName =
            "N/A";

        float timeProgress =
            0f;

        if (dayNight != null)
        {
            timeString =
                dayNight.GetTimeString();

            timeName =
                dayNight.GetTimeOfDayName();

            timeProgress =
                dayNight.GetTimeOfDay();
        }

        // ========================================================
        // SEASON DAYLIGHT
        // ========================================================

        string daylight =
            GetSeasonDaylight(
                seasonManager.CurrentSeason
            );

        string night =
            GetSeasonNight(
                seasonManager.CurrentSeason
            );

        // ========================================================
        // DISPLAY
        // ========================================================

        _label.Text =
            "BOX DEBUG\n" +
            "══════════════════════════════════\n\n" +

            "TIME\n" +
            $"Time:        {timeString}\n" +
            $"Period:      {timeName}\n" +
            $"Progress:    {timeProgress * 100f:0.0}%\n\n" +

            "CALENDAR\n" +
            $"Year:        {seasonManager.CurrentYear}\n" +
            $"Season:      {seasonManager.GetSeasonName()}\n" +
            $"Day:         {seasonManager.CurrentDay} / {seasonManager.DaysPerSeason}\n\n" +

            "SEASON LIGHT\n" +
            $"Daylight:    {daylight}\n" +
            $"Night:       {night}\n\n" +

            "MOON\n" +
            $"Phase:       {seasonManager.GetMoonPhaseName()}\n" +
            $"Progress:    {seasonManager.GetMoonProgress() * 100f:0.0}%\n\n" +

            "SEASONS\n" +
            $"Spring:      {spring}\n" +
            $"Summer:      {summer}\n" +
            $"Autumn:      {autumn}\n" +
            $"Winter:      {winter}\n\n" +

            "SEASON LOCK\n" +
            $"{lockState}\n\n" +
            "TEST CONTROLS\n" +
            "Use the buttons below or keyboard shortcuts.";

        if (_pauseTimeButton != null)
            _pauseTimeButton.Text = dayNight.IsTimePaused ? "Resume Time" : "Pause Time";

        if (_seasonLockButton != null)
            _seasonLockButton.Text = seasonManager.SeasonLocked ? "Unlock Season" : "Lock Season";

        if (_chunkBordersButton != null)
        {
            Player player = GetTree().GetFirstNodeInGroup("player") as Player;
            _chunkBordersButton.Text =
                player != null && player.AreChunkBordersVisible
                    ? "Chunk Borders: ON"
                    : "Chunk Borders: OFF";
        }
    }

    // ============================================================
    // SEASON DAYLIGHT DISPLAY
    // ============================================================

    private string GetSeasonDaylight(
        SeasonManager.Season season
    )
    {
        return season switch
        {
            SeasonManager.Season.Spring =>
                "10 min",

            SeasonManager.Season.Summer =>
                "11 min",

            SeasonManager.Season.Autumn =>
                "9 min",

            SeasonManager.Season.Winter =>
                "8 min",

            _ =>
                "Unknown"
        };
    }

    // ============================================================
    // SEASON NIGHT DISPLAY
    // ============================================================

    private string GetSeasonNight(
        SeasonManager.Season season
    )
    {
        return season switch
        {
            SeasonManager.Season.Spring =>
                "5 min",

            SeasonManager.Season.Summer =>
                "4 min",

            SeasonManager.Season.Autumn =>
                "6 min",

            SeasonManager.Season.Winter =>
                "7 min",

            _ =>
                "Unknown"
        };
    }

    // ============================================================
    // ADVANCE DAY
    // ============================================================

    private void AdvanceDay()
    {
        SeasonManager seasonManager =
            GetSeasonManager();

        if (seasonManager == null)
            return;

        seasonManager.AdvanceDay();

        UpdateDisplay();
    }

    // ============================================================
    // ADVANCE HOUR
    // ============================================================

    private void AdvanceHour()
    {
        DayNightCycle dayNight =
            GetDayNightCycle();

        if (dayNight == null)
        {
            GD.Print(
                "[DebugMenu] DayNightCycle not found."
            );

            return;
        }

        dayNight.AdvanceDebugHour();

        UpdateDisplay();
    }

    // ============================================================
    // TOGGLE SEASON LOCK
    // ============================================================

    private void ToggleSeasonLock()
    {
        SeasonManager seasonManager =
            GetSeasonManager();

        if (seasonManager == null)
            return;

        seasonManager.ToggleSeasonLock();

        UpdateDisplay();
    }

    // ============================================================
    // GET SEASON MANAGER
    // ============================================================

    private SeasonManager GetSeasonManager()
    {
        if (!IsInsideTree())
            return null;

        return GetNodeOrNull<SeasonManager>(
            "/root/SeasonManager"
        );
    }

    // ============================================================
    // GET DAY NIGHT CYCLE
    // ============================================================

    private DayNightCycle GetDayNightCycle()
    {
        if (!IsInsideTree())
            return null;

        Node node =
            GetTree()
                .GetFirstNodeInGroup(
                    "day_night_cycle"
                );

        return node as DayNightCycle;
    }
}