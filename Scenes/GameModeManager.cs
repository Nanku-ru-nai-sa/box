using Godot;
using System;

// GameModeManager — add to Project > Project Settings > Autoload as "GameModeManager"
// Holds the current gamemode and fires OnGameModeChanged when it switches.

public partial class GameModeManager : Node
{
    public static GameModeManager Instance { get; private set; }

    public enum GameMode { Survival, Create, Story }

    public GameMode Current { get; private set; } = GameMode.Survival;

    public event Action<GameMode> OnGameModeChanged;

    // Ordered list for F4 cycling
    private static readonly GameMode[] CycleOrder = {
        GameMode.Survival,
        GameMode.Create,
        GameMode.Story
    };

    public override void _Ready()
    {
        Instance = this;
    }

    public void SetMode(GameMode mode)
    {
        if (Current == mode) return;
        Current = mode;
        GD.Print($"GameMode changed to: {mode}");
        OnGameModeChanged?.Invoke(mode);
    }

    public void CycleNext()
    {
        int idx  = Array.IndexOf(CycleOrder, Current);
        int next = (idx + 1) % CycleOrder.Length;
        SetMode(CycleOrder[next]);
    }

    // Convenience properties used throughout the codebase
    public bool IsCreate  => Current == GameMode.Create;
    public bool IsSurvival => Current == GameMode.Survival;
    public bool IsStory    => Current == GameMode.Story;
}