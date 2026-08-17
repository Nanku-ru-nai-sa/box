using Godot;
using System;

public partial class SeasonManager : Node
{
    // ============================================================
    // BOX SEASON SETTINGS
    // ============================================================

    // How many in-game days each season lasts.
    [Export]
    public int DaysPerSeason { get; set; } = 20;

    // Number of seasons in the Box calendar.
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

    // Prevents the seasonal calendar from advancing.
    // The DayNightCycle continues running normally.
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
    // CALENDAR SIGNALS
    // ============================================================

    // Fired whenever the calendar advances to a new day.
    //
    // Example:
    // Spring Day 4 -> Spring Day 5
    //
    // newDay = 5
    // currentSeason = Spring
    [Signal]
    public delegate void DayChangedEventHandler(
        int newDay,
        Season currentSeason
    );

    // Fired whenever the season changes.
    //
    // Example:
    // Spring -> Summer
    //
    // newSeason = Summer
    // oldSeason = Spring
    [Signal]
    public delegate void SeasonChangedEventHandler(
        Season newSeason,
        Season oldSeason
    );

    // Fired whenever a new year begins.
    //
    // Example:
    // Year 1 -> Year 2
    [Signal]
    public delegate void YearChangedEventHandler(
        int newYear
    );

    // ============================================================
    // TESTING
    // ============================================================

    // Set this to true while testing if you want to manually
    // advance the calendar without waiting for DayNightCycle.
    [Export]
    public bool DebugMode { get; set; } = false;

    // ============================================================
    // INITIALIZATION
    // ============================================================

    public override void _Ready()
    {
        DaysPerSeason =
            Mathf.Max(
                1,
                DaysPerSeason
            );

        // Make sure at least one season is enabled.
        EnsureAtLeastOneSeasonEnabled();

        // Make sure the starting season is valid.
        if (!IsSeasonEnabled(CurrentSeason))
        {
            CurrentSeason =
                GetNextEnabledSeason(
                    CurrentSeason
                );
        }

        // Make sure the starting day is valid.
        CurrentDay =
            Mathf.Clamp(
                CurrentDay,
                1,
                DaysPerSeason
            );

        // Make sure the starting year is valid.
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

    /// <summary>
    /// Advances the seasonal calendar by one full in-game day.
    /// DayNightCycle calls this when its clock reaches midnight.
    /// </summary>
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

        // ========================================================
        // NORMAL DAY
        // ========================================================

        if (CurrentDay <= DaysPerSeason)
        {
            EmitDayChanged();

            GD.Print(
                $"[SeasonManager] " +
                $"{GetSeasonName(CurrentSeason)} " +
                $"Day {CurrentDay}/{DaysPerSeason}"
            );

            return;
        }

        // ========================================================
        // SEASON ENDED
        // ========================================================

        CurrentDay = 1;

        AdvanceToNextEnabledSeason();
    }

    // ============================================================
    // SEASON ADVANCEMENT
    // ============================================================

    private void AdvanceToNextEnabledSeason()
    {
        Season oldSeason =
            CurrentSeason;

        Season nextSeason =
            GetNextEnabledSeason(
                CurrentSeason
            );

        // ========================================================
        // YEAR WRAP
        // ========================================================

        if (IsYearWrap(
            oldSeason,
            nextSeason
        ))
        {
            CurrentYear++;

            EmitSignal(
                SignalName.YearChanged,
                CurrentYear
            );

            GD.Print(
                $"[SeasonManager] New Year: " +
                $"{CurrentYear}"
            );
        }

        // ========================================================
        // CHANGE SEASON
        // ========================================================

        CurrentSeason =
            nextSeason;

        EmitSignal(
            SignalName.SeasonChanged,
            (int)CurrentSeason,
            (int)oldSeason
        );

        // A new season always begins on Day 1.
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

    public bool IsSeasonEnabled(
        Season season
    )
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
        Season current
    )
    {
        // Try the next three seasons.
        // Disabled seasons are automatically skipped.

        for (int i = 1; i <= SeasonsPerYear; i++)
        {
            Season candidate =
                (Season)(
                    ((int)current + i)
                    % SeasonsPerYear
                );

            if (IsSeasonEnabled(candidate))
                return candidate;
        }

        // Should never happen because at least one season
        // is guaranteed to be enabled.
        return current;
    }

    private bool IsYearWrap(
        Season oldSeason,
        Season newSeason
    )
    {
        // Normal four-season cycle:
        //
        // Winter -> Spring = new year

        if (oldSeason == Season.Winter &&
            newSeason == Season.Spring)
        {
            return true;
        }

        // If seasons are disabled, determine whether we've
        // wrapped around to the first enabled season.

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
            // Never allow an impossible calendar.
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
    // LOCK
    // ============================================================

    public void SetSeasonLocked(
        bool locked
    )
    {
        SeasonLocked =
            locked;

        GD.Print(
            $"[SeasonManager] Season lock: " +
            $"{(SeasonLocked ? "ON" : "OFF")}"
        );
    }

    public void ToggleSeasonLock()
    {
        SetSeasonLocked(
            !SeasonLocked
        );
    }

    // ============================================================
    // MANUAL SEASON CONTROL
    // ============================================================

    /// <summary>
    /// Immediately changes the current season.
    /// Useful for debug tools and future gameplay systems.
    /// </summary>
    public void SetSeason(
        Season newSeason
    )
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

        // Nothing to do if we're already in this season.
        if (oldSeason == newSeason)
        {
            CurrentDay = 1;

            EmitDayChanged();

            return;
        }

        CurrentSeason =
            newSeason;

        CurrentDay = 1;

        // Manual season changes do not automatically
        // change the year. The normal calendar handles
        // year progression.
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

    /// <summary>
    /// Returns the moon's progress through the current season.
    ///
    /// 0.0 = beginning/new moon
    /// 1.0 = end/new moon
    /// </summary>
    public float GetMoonProgress()
    {
        if (DaysPerSeason <= 0)
            return 0f;

        return
            (CurrentDay - 1) /
            (float)DaysPerSeason;
    }

    /// <summary>
    /// Returns a readable moon phase name.
    /// </summary>
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
        Season season
    )
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
}