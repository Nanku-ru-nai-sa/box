using Godot;

public partial class DebugMenu : CanvasLayer
{
    private Panel _panel;
    private Label _label;

    private bool _visible = false;

    private bool _f6Held = false;
    private bool _f7Held = false;
    private bool _f9Held = false;
    private bool _f10Held = false;

    public override void _Ready()
    {
        Layer = 100;

        BuildMenu();

        _panel.Visible = false;
    }

    public override void _Process(double delta)
    {
        // ========================================================
        // F6 - OPEN / CLOSE DEBUG MENU
        // ========================================================

        if (Input.IsKeyPressed(Key.F6))
        {
            if (!_f6Held)
            {
                _f6Held = true;

                _visible = !_visible;
                _panel.Visible = _visible;

                if (_visible)
                    UpdateDisplay();
            }
        }
        else
        {
            _f6Held = false;
        }

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
        // UPDATE
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
        new Vector2(20, 20);

    _panel.Size =
        new Vector2(400, 1000);

    AddChild(_panel);

    // ========================================================
    // PANEL STYLE
    // ========================================================

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

    // ========================================================
    // LABEL
    // ========================================================

    _label = new Label();

    _label.Position =
        new Vector2(20, 15);

    _label.Size =
        new Vector2(400, 1000);

    _label.AddThemeFontSizeOverride(
        "font_size",
        18
    );

    _panel.AddChild(_label);
}

    // ============================================================
    // UPDATE DISPLAY
    // ============================================================

    private void UpdateDisplay()
    {
        SeasonManager seasonManager =
            GetSeasonManager();

        if (seasonManager == null)
        {
            _label.Text =
                "BOX DEBUG\n\n" +
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
        // DAY / NIGHT
        // ========================================================

        DayNightCycle dayNight =
            GetTree().GetFirstNodeInGroup(
                "day_night_cycle"
            ) as DayNightCycle;

        string timeString =
            "N/A";

        string timeOfDayName =
            "N/A";

        string rawTime =
            "N/A";

        if (dayNight != null)
        {
            timeString =
                dayNight.GetTimeString();

            timeOfDayName =
                dayNight.GetTimeOfDayName();

            rawTime =
                $"{dayNight.GetTimeOfDay():0.000}";
        }

        // ========================================================
        // DISPLAY
        // ========================================================

        _label.Text =
            "BOX DEBUG\n" +
            "════════════════════════════════\n\n" +

            // ====================================================
            // TIME
            // ====================================================

            "TIME\n" +
            $"Time:        {timeString}\n" +
            $"Period:      {timeOfDayName}\n" +
            $"Raw Time:    {rawTime}\n\n" +

            // ====================================================
            // CALENDAR
            // ====================================================

            "CALENDAR\n" +
            $"Year:        {seasonManager.CurrentYear}\n" +
            $"Season:      {seasonManager.GetSeasonName()}\n" +
            $"Day:         {seasonManager.CurrentDay} / {seasonManager.DaysPerSeason}\n\n" +

            // ====================================================
            // MOON
            // ====================================================

            "MOON\n" +
            $"Phase:       {seasonManager.GetMoonPhaseName()}\n" +
            $"Progress:    {seasonManager.GetMoonProgress() * 100f:0.0}%\n\n" +

            // ====================================================
            // SEASONS
            // ====================================================

            "SEASONS\n" +
            $"Spring:      {spring}\n" +
            $"Summer:      {summer}\n" +
            $"Autumn:      {autumn}\n" +
            $"Winter:      {winter}\n\n" +

            // ====================================================
            // SEASON LOCK
            // ====================================================

            "SEASON LOCK\n" +
            $"{lockState}\n\n" +

            // ====================================================
            // TEST CONTROLS
            // ====================================================

            "TEST CONTROLS\n" +
            "F6   Close Debug Menu\n" +
            "F7   Advance One Day\n" +
            "F9   Toggle Season Lock\n" +
            "F10  Advance One Hour";
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
            GetTree().GetFirstNodeInGroup(
                "day_night_cycle"
            ) as DayNightCycle;

        if (dayNight == null)
        {
            GD.Print(
                "[DebugMenu] DayNightCycle not found " +
                "in group 'day_night_cycle'."
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
        return GetNodeOrNull<SeasonManager>(
            "/root/SeasonManager"
        );
    }
}