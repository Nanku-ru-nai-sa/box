using Godot;
using System;

public partial class SeasonManager : Node
{
    // ============================================================
    // BOX SEASON SETTINGS
    // ============================================================

    [Export]
    public int DaysPerSeason { get; set; } = 20;

    public const int SeasonsPerYear = 4;

    // ============================================================
    // SEASONS
    // ============================================================

    public enum Season
    {
        Spring,
        Summer,
        Autumn,
        Winter
    }

    // ============================================================
    // CURRENT CALENDAR STATE
    // ============================================================

    [Export]
    public Season CurrentSeason { get; private set; } = Season.Spring;

    [Export]
    public int CurrentDay { get; private set; } = 1;

    [Export]
    public int CurrentYear { get; private set; } = 1;

    [Export]
    public bool SeasonLocked { get; private set; } = false;

    // ============================================================
    // ENABLED SEASONS
    // ============================================================

    [Export]
    public bool SpringEnabled { get; set; } = true;

    [Export]
    public bool SummerEnabled { get; set; } = true;

    [Export]
    public bool AutumnEnabled { get; set; } = true;

    [Export]
    public bool WinterEnabled { get; set; } = true;

    // ============================================================
    // SIGNALS
    // ============================================================

    [Signal]
    public delegate void DayChangedEventHandler(
        int newDay,
        Season currentSeason
    );

    [Signal]
    public delegate void SeasonChangedEventHandler(
        Season newSeason,
        Season oldSeason
    );

    [Signal]
    public delegate void YearChangedEventHandler(
        int newYear
    );

    // ============================================================
    // TESTING
    // ============================================================

    [Export]
    public bool DebugMode { get; set; } = false;

    // ============================================================
    // READY
    // ============================================================

    public override void _Ready()
    {
        DaysPerSeason = Mathf.Max(1, DaysPerSeason);

        EnsureAtLeastOneSeasonEnabled();

        if (!IsSeasonEnabled(CurrentSeason))
        {
            CurrentSeason =
                GetNextEnabledSeason(CurrentSeason);
        }

        CurrentDay =
            Mathf.Clamp(
                CurrentDay,
                1,
                DaysPerSeason
            );

        CurrentYear =
            Mathf.Max(
                1,
                CurrentYear
            );

        GD.Print(
            $"[SeasonManager] Starting Year {CurrentYear}, " +
            $"{GetSeasonName(CurrentSeason)}, " +
            $"Day {CurrentDay}/{DaysPerSeason}"
        );
    }

    // ============================================================
    // DAY ADVANCEMENT
    // ============================================================

    public void AdvanceDay()
    {
        if (SeasonLocked)
        {
            GD.Print(
                "[SeasonManager] Day advance blocked - " +
                "season calendar is locked."
            );

            return;
        }

        CurrentDay++;

if (CurrentDay <= DaysPerSeason)
{
    var dayNight =
        GetTree().GetFirstNodeInGroup("day_night_cycle");

    if (dayNight is DayNightCycle cycle)
    {
        cycle.ResetCelestialHarvest();
    }

    EmitDayChanged();

    GD.Print(
        $"[SeasonManager] " +
        $"{GetSeasonName(CurrentSeason)} " +
        $"Day {CurrentDay}/{DaysPerSeason}"
    );

    return;
}

        CurrentDay = 1;

        AdvanceToNextEnabledSeason();
    }

    // ============================================================
    // SEASON ADVANCEMENT
    // ============================================================

    private void AdvanceToNextEnabledSeason()
    {
        Season oldSeason = CurrentSeason;

        Season nextSeason =
            GetNextEnabledSeason(CurrentSeason);

        if (IsYearWrap(
            oldSeason,
            nextSeason))
        {
            CurrentYear++;

            EmitSignal(
                SignalName.YearChanged,
                CurrentYear
            );

            GD.Print(
                $"[SeasonManager] New Year: {CurrentYear}"
            );
        }

        CurrentSeason = nextSeason;

        EmitSignal(
            SignalName.SeasonChanged,
            (int)CurrentSeason,
            (int)oldSeason
        );

        EmitDayChanged();

        GD.Print(
            $"[SeasonManager] " +
            $"{GetSeasonName(oldSeason)} -> " +
            $"{GetSeasonName(CurrentSeason)}"
        );
    }

    // ============================================================
    // ENABLED SEASON LOGIC
    // ============================================================

    public bool IsSeasonEnabled(Season season)
    {
        return season switch
        {
            Season.Spring => SpringEnabled,
            Season.Summer => SummerEnabled,
            Season.Autumn => AutumnEnabled,
            Season.Winter => WinterEnabled,
            _ => false
        };
    }

    private Season GetNextEnabledSeason(
        Season current)
    {
        for (int i = 1;
             i <= SeasonsPerYear;
             i++)
        {
            Season candidate =
                (Season)(
                    ((int)current + i)
                    % SeasonsPerYear
                );

            if (IsSeasonEnabled(candidate))
                return candidate;
        }

        return current;
    }

    private bool IsYearWrap(
        Season oldSeason,
        Season newSeason)
    {
        if (oldSeason == Season.Winter &&
            newSeason == Season.Spring)
        {
            return true;
        }

        Season firstEnabled =
            GetFirstEnabledSeason();

        return
            oldSeason != firstEnabled &&
            newSeason == firstEnabled;
    }

    private Season GetFirstEnabledSeason()
    {
        if (SpringEnabled)
            return Season.Spring;

        if (SummerEnabled)
            return Season.Summer;

        if (AutumnEnabled)
            return Season.Autumn;

        return Season.Winter;
    }

    private void EnsureAtLeastOneSeasonEnabled()
    {
        if (!SpringEnabled &&
            !SummerEnabled &&
            !AutumnEnabled &&
            !WinterEnabled)
        {
            SpringEnabled = true;
        }
    }

    // ============================================================
    // SIGNAL HELPERS
    // ============================================================

    private void EmitDayChanged()
    {
        EmitSignal(
            SignalName.DayChanged,
            CurrentDay,
            (int)CurrentSeason
        );
    }

    // ============================================================
    // SEASON LOCK
    // ============================================================

    public void SetSeasonLocked(bool locked)
    {
        SeasonLocked = locked;

        GD.Print(
            $"[SeasonManager] Season lock: " +
            $"{(SeasonLocked ? "ON" : "OFF")}"
        );
    }

    public void ToggleSeasonLock()
    {
        SetSeasonLocked(!SeasonLocked);
    }

    // ============================================================
    // MANUAL SEASON CONTROL
    // ============================================================

    public void SetSeason(Season newSeason)
    {
        if (!IsSeasonEnabled(newSeason))
        {
            GD.Print(
                $"[SeasonManager] Cannot change to " +
                $"{GetSeasonName(newSeason)} " +
                $"because it is disabled."
            );

            return;
        }

        Season oldSeason =
            CurrentSeason;

        if (oldSeason == newSeason)
        {
            CurrentDay = 1;

            EmitDayChanged();

            return;
        }

        CurrentSeason =
            newSeason;

        CurrentDay =
            1;

        EmitSignal(
            SignalName.SeasonChanged,
            (int)CurrentSeason,
            (int)oldSeason
        );

        EmitDayChanged();

        GD.Print(
            $"[SeasonManager] " +
            $"Season manually changed: " +
            $"{GetSeasonName(oldSeason)} -> " +
            $"{GetSeasonName(CurrentSeason)}"
        );
    }

    // ============================================================
    // MOON
    // ============================================================

    public float GetMoonProgress()
    {
        if (DaysPerSeason <= 1)
            return 0f;

        return Mathf.Clamp(
            (CurrentDay - 1) /
            (float)(DaysPerSeason - 1),
            0f,
            1f
        );
    }

    public string GetMoonPhaseName()
    {
        float progress =
            GetMoonProgress();

        if (progress < 0.0625f)
            return "New Moon";

        if (progress < 0.1875f)
            return "Waxing Crescent";

        if (progress < 0.3125f)
            return "First Quarter";

        if (progress < 0.4375f)
            return "Waxing Gibbous";

        if (progress < 0.5625f)
            return "Full Moon";

        if (progress < 0.6875f)
            return "Waning Gibbous";

        if (progress < 0.8125f)
            return "Last Quarter";

        if (progress < 0.9375f)
            return "Waning Crescent";

        return "New Moon";
    }

    // ============================================================
    // DISPLAY HELPERS
    // ============================================================

    public string GetSeasonName()
    {
        return GetSeasonName(
            CurrentSeason
        );
    }

    public string GetSeasonName(
        Season season)
    {
        return season switch
        {
            Season.Spring => "Spring",
            Season.Summer => "Summer",
            Season.Autumn => "Autumn",
            Season.Winter => "Winter",
            _ => "Unknown"
        };
    }

    public string GetCalendarString()
    {
        return
            $"Year {CurrentYear} - " +
            $"{GetSeasonName(CurrentSeason)} - " +
            $"Day {CurrentDay}/{DaysPerSeason}";
    }

    // ============================================================
    // SAVE / LOAD
    // ============================================================

    public void LoadCalendarState(
        int year,
        string season,
        int day,
        bool seasonLocked,
        bool springEnabled,
        bool summerEnabled,
        bool autumnEnabled,
        bool winterEnabled)
    {
        SpringEnabled =
            springEnabled;

        SummerEnabled =
            summerEnabled;

        AutumnEnabled =
            autumnEnabled;

        WinterEnabled =
            winterEnabled;

        EnsureAtLeastOneSeasonEnabled();

        CurrentYear =
            Mathf.Max(
                1,
                year
            );

        Season loadedSeason =
            Season.Spring;

        if (!string.IsNullOrEmpty(season))
        {
            if (!Enum.TryParse(
                season,
                true,
                out loadedSeason))
            {
                loadedSeason =
                    Season.Spring;
            }
        }

        if (!IsSeasonEnabled(
            loadedSeason))
        {
            loadedSeason =
                GetFirstEnabledSeason();
        }

        CurrentSeason =
            loadedSeason;

        CurrentDay =
            Mathf.Clamp(
                day,
                1,
                DaysPerSeason
            );

        SeasonLocked =
            seasonLocked;

        GD.Print(
            $"[SeasonManager] Loaded calendar: " +
            $"Year {CurrentYear}, " +
            $"{GetSeasonName(CurrentSeason)}, " +
            $"Day {CurrentDay}/{DaysPerSeason}"
        );
    }
}
