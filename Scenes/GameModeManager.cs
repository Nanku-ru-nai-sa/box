using Godot;
using System;

// GameModeManager — add to Project > Project Settings > Autoload as "GameModeManager"
// Holds the current gamemode and fires OnGameModeChanged when it switches.
// Gamemode is saved and loaded from the active world's WorldMeta.

public partial class GameModeManager : Node
{
    public static GameModeManager Instance { get; private set; }

    public enum GameMode
    {
        Survival,
        Create,
        Story
    }

    public GameMode Current { get; private set; } = GameMode.Survival;

    public event Action<GameMode> OnGameModeChanged;

    // Ordered list for F4 cycling
    private static readonly GameMode[] CycleOrder =
    {
        GameMode.Survival,
        GameMode.Create,
        GameMode.Story
    };

    public override void _Ready()
    {
        Instance = this;
    }

    // ============================================================
    // SET MODE
    // ============================================================

    public void SetMode(GameMode mode)
    {
        if (Current == mode)
            return;

        Current = mode;

        GD.Print(
            $"[GameModeManager] GameMode changed to: {mode}"
        );

        OnGameModeChanged?.Invoke(mode);

        // Save the new mode immediately.
        SaveToWorld();
    }

    // ============================================================
    // LOAD FROM WORLD
    // ============================================================

    public void LoadFromWorld()
    {
        if (SaveManager.Instance == null)
        {
            GD.PrintErr(
                "[GameModeManager] Cannot load gamemode. " +
                "SaveManager.Instance is null."
            );

            return;
        }

        if (string.IsNullOrEmpty(
            SaveManager.Instance.ActiveWorldId))
        {
            GD.PrintErr(
                "[GameModeManager] Cannot load gamemode. " +
                "There is no active world."
            );

            return;
        }

        WorldMeta meta =
            SaveManager.Instance.LoadWorldMeta(
                SaveManager.Instance.ActiveWorldId
            );

        if (meta == null)
        {
            GD.PrintErr(
                "[GameModeManager] Cannot load gamemode. " +
                "World metadata could not be loaded."
            );

            return;
        }

        GameMode loadedMode =
            ParseGameMode(meta.LockedGameMode);

        Current = loadedMode;

        GD.Print(
            $"[GameModeManager] Loaded gamemode: {Current}"
        );

        OnGameModeChanged?.Invoke(Current);
    }

    // ============================================================
    // SAVE TO WORLD
    // ============================================================

    public void SaveToWorld()
    {
        if (SaveManager.Instance == null)
        {
            GD.PrintErr(
                "[GameModeManager] Cannot save gamemode. " +
                "SaveManager.Instance is null."
            );

            return;
        }

        if (string.IsNullOrEmpty(
            SaveManager.Instance.ActiveWorldId))
        {
            GD.PrintErr(
                "[GameModeManager] Cannot save gamemode. " +
                "There is no active world."
            );

            return;
        }

        WorldMeta meta =
            SaveManager.Instance.LoadWorldMeta(
                SaveManager.Instance.ActiveWorldId
            );

        if (meta == null)
        {
            GD.PrintErr(
                "[GameModeManager] Cannot save gamemode. " +
                "World metadata could not be loaded."
            );

            return;
        }

        meta.LockedGameMode =
            Current.ToString();

        meta.LastPlayed =
            DateTime.UtcNow;

        SaveManager.Instance.SaveWorldMeta(meta);

        GD.Print(
            $"[GameModeManager] Saved gamemode: {Current}"
        );
    }

    // ============================================================
    // PARSE
    // ============================================================

    private GameMode ParseGameMode(
        string value)
    {
        if (string.IsNullOrEmpty(value))
            return GameMode.Survival;

        if (Enum.TryParse<GameMode>(
            value,
            true,
            out GameMode result))
        {
            return result;
        }

        GD.PrintErr(
            $"[GameModeManager] Unknown saved gamemode '{value}'. " +
            "Falling back to Survival."
        );

        return GameMode.Survival;
    }

    // ============================================================
    // CYCLE
    // ============================================================

    public void CycleNext()
    {
        int idx =
            Array.IndexOf(
                CycleOrder,
                Current
            );

        int next =
            (idx + 1) %
            CycleOrder.Length;

        SetMode(
            CycleOrder[next]
        );
    }

    // ============================================================
    // CONVENIENCE PROPERTIES
    // ============================================================

    public bool IsCreate =>
        Current == GameMode.Create;

    public bool IsSurvival =>
        Current == GameMode.Survival;

    public bool IsStory =>
        Current == GameMode.Story;
}