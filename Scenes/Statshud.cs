using Godot;
using System.Collections.Generic;

// StatsHud — three independently-fading pip bars (mana / health / stamina)
// shown above the hotbar. Each pip represents 4 points of a stat.
//
// When SettingsManager.ShowPlayerStats is TRUE:
//   HP / Mana / Stamina remain visible.
//
// When SettingsManager.ShowPlayerStats is FALSE:
//   The normal independent fade behavior is used.
//
// Texture naming (res://Assets/Textures/Stats/):
//   heart_full.png   heart_threequarter.png   heart_half.png   heart_quarter.png   heart_empty.png
//   stamina_full.png stamina_threequarter.png stamina_half.png stamina_quarter.png stamina_empty.png
//   mana_full.png    mana_threequarter.png    mana_half.png    mana_quarter.png    mana_empty.png

public partial class StatsHud : HBoxContainer
{
    private PlayerStats _stats;

    private HBoxContainer _manaGroup;
    private HBoxContainer _healthGroup;
    private HBoxContainer _staminaGroup;

    private TextureRect[] _manaPips = new TextureRect[0];
    private TextureRect[] _healthPips = new TextureRect[0];
    private TextureRect[] _staminaPips = new TextureRect[0];

    private float _healthCur;
    private float _healthMax;

    private float _staminaCur;
    private float _staminaMax;

    private float _manaCur;
    private float _manaMax;

    private float _healthFadeTimer;
    private float _staminaFadeTimer;
    private float _manaFadeTimer;

    private const int PipSize = 26;
    private const int PipGap = 2;
    private const int GroupGap = 18;

    private const float FadeDelay = 2.5f;
    private const float FadeSpeed = 1.2f;
    private const float ShowSpeed = 8f;

    private readonly Dictionary<string, Texture2D> _texCache = new();

    // ================================================================
    // INIT
    // ================================================================

    public void Init(PlayerStats stats)
    {
        _stats = stats;

        if (_stats == null)
            return;

        _healthCur = _stats.Health;
        _healthMax = _stats.MaxHealth;

        _staminaCur = _stats.Stamina;
        _staminaMax = _stats.MaxStamina;

        _manaCur = _stats.Mana;
        _manaMax = _stats.MaxMana;

        _stats.HealthChanged += OnHealthChanged;
        _stats.StaminaChanged += OnStaminaChanged;
        _stats.ManaChanged += OnManaChanged;

        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.OnSettingsChanged += OnSettingsChanged;
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        AddThemeConstantOverride("separation", GroupGap);

        _manaGroup = MakeGroup();
        _healthGroup = MakeGroup();
        _staminaGroup = MakeGroup();

        AddChild(_manaGroup);
        AddChild(_healthGroup);
        AddChild(_staminaGroup);

        RefreshGroupPips(
            _manaGroup,
            ref _manaPips,
            "mana",
            _manaCur,
            _manaMax);

        RefreshGroupPips(
            _healthGroup,
            ref _healthPips,
            "heart",
            _healthCur,
            _healthMax);

        RefreshGroupPips(
            _staminaGroup,
            ref _staminaPips,
            "stamina",
            _staminaCur,
            _staminaMax);

        SetGroupAlpha(_manaGroup, 1f);
        SetGroupAlpha(_healthGroup, 1f);
        SetGroupAlpha(_staminaGroup, 1f);
    }

    public override void _ExitTree()
    {
        if (_stats != null)
        {
            _stats.HealthChanged -= OnHealthChanged;
            _stats.StaminaChanged -= OnStaminaChanged;
            _stats.ManaChanged -= OnManaChanged;
        }

        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.OnSettingsChanged -= OnSettingsChanged;
        }
    }

    // ================================================================
    // SETTINGS
    // ================================================================

    private bool ShouldKeepStatsVisible()
    {
        // If the SettingsManager isn't available for some reason,
        // preserve the normal fade behavior.
        if (SettingsManager.Instance == null)
            return false;

        return SettingsManager.Instance.ShowPlayerStats;
    }

    private void OnSettingsChanged()
    {
        if (!ShouldKeepStatsVisible())
            return;

        SetGroupAlpha(_manaGroup, 1f);
        SetGroupAlpha(_healthGroup, 1f);
        SetGroupAlpha(_staminaGroup, 1f);

        _manaFadeTimer = FadeDelay;
        _healthFadeTimer = FadeDelay;
        _staminaFadeTimer = FadeDelay;
    }

    private void SetGroupAlpha(Control group, float alpha)
    {
        if (group == null)
            return;

        group.Modulate = new Color(
            1f,
            1f,
            1f,
            alpha);
    }

    // ================================================================
    // EVENTS
    // ================================================================

    private void OnHealthChanged(float cur, float max)
    {
        _healthCur = cur;
        _healthMax = max;

        if (_healthGroup != null)
        {
            RefreshGroupPips(
                _healthGroup,
                ref _healthPips,
                "heart",
                cur,
                max);
        }
    }

    private void OnStaminaChanged(float cur, float max)
    {
        _staminaCur = cur;
        _staminaMax = max;

        if (_staminaGroup != null)
        {
            RefreshGroupPips(
                _staminaGroup,
                ref _staminaPips,
                "stamina",
                cur,
                max);
        }
    }

    private void OnManaChanged(float cur, float max)
    {
        _manaCur = cur;
        _manaMax = max;

        if (_manaGroup != null)
        {
            RefreshGroupPips(
                _manaGroup,
                ref _manaPips,
                "mana",
                cur,
                max);
        }
    }

    // ================================================================
    // PROCESS — FADE LOGIC
    // ================================================================

    public override void _Process(double delta)
    {
        if (_manaGroup == null)
            return;

        float dt = (float)delta;

        UpdateFade(
            _manaGroup,
            _manaCur,
            _manaMax,
            ref _manaFadeTimer,
            dt);

        UpdateFade(
            _healthGroup,
            _healthCur,
            _healthMax,
            ref _healthFadeTimer,
            dt);

        UpdateFade(
            _staminaGroup,
            _staminaCur,
            _staminaMax,
            ref _staminaFadeTimer,
            dt);
    }

    private void UpdateFade(
        Control group,
        float cur,
        float max,
        ref float fadeTimer,
        float dt)
    {
        if (group == null)
            return;

        // ============================================================
        // PLAYER STATS SETTING ON
        // ============================================================

        if (ShouldKeepStatsVisible())
        {
            fadeTimer = FadeDelay;
            SetGroupAlpha(group, 1f);
            return;
        }

        // ============================================================
        // NORMAL FADE BEHAVIOR
        // ============================================================

        bool full = cur >= max - 0.001f;

        float alpha = group.Modulate.A;

        if (!full)
        {
            // Any stat below maximum immediately becomes visible.
            fadeTimer = FadeDelay;

            alpha = Mathf.MoveToward(
                alpha,
                1f,
                dt * ShowSpeed);
        }
        else if (fadeTimer > 0f)
        {
            // Full stat gets a short grace period.
            fadeTimer -= dt;

            alpha = Mathf.MoveToward(
                alpha,
                1f,
                dt * ShowSpeed);
        }
        else
        {
            // Full stat fades away.
            alpha = Mathf.MoveToward(
                alpha,
                0f,
                dt * FadeSpeed);
        }

        SetGroupAlpha(group, alpha);
    }

    // ================================================================
    // PIP BUILDING
    // ================================================================

    private void RefreshGroupPips(
        HBoxContainer group,
        ref TextureRect[] pips,
        string prefix,
        float cur,
        float max)
    {
        int count = Mathf.Max(
            1,
            Mathf.CeilToInt(max / 4f));

        if (pips.Length != count)
        {
            foreach (Node child in group.GetChildren())
            {
                child.QueueFree();
            }

            pips = new TextureRect[count];

            for (int i = 0; i < count; i++)
            {
                var pip = MakePip();

                group.AddChild(pip);

                pips[i] = pip;
            }
        }

        for (int i = 0; i < count; i++)
        {
            float remaining = Mathf.Clamp(
                cur - i * 4f,
                0f,
                4f);

            float fraction = remaining / 4f;

            pips[i].Texture =
                GetPipTexture(prefix, fraction);
        }
    }

    private HBoxContainer MakeGroup()
    {
        var group = new HBoxContainer();

        group.AddThemeConstantOverride(
            "separation",
            PipGap);

        group.MouseFilter =
            MouseFilterEnum.Ignore;

        return group;
    }

    private TextureRect MakePip()
    {
        var tr = new TextureRect();

        tr.CustomMinimumSize =
            new Vector2(PipSize, PipSize);

        tr.ExpandMode =
            TextureRect.ExpandModeEnum.IgnoreSize;

        tr.StretchMode =
            TextureRect.StretchModeEnum.KeepAspectCentered;

        tr.TextureFilter =
            CanvasItem.TextureFilterEnum.Nearest;

        tr.MouseFilter =
            MouseFilterEnum.Ignore;

        return tr;
    }

    // ================================================================
    // TEXTURES
    // ================================================================

    private Texture2D GetPipTexture(
        string prefix,
        float fraction)
    {
        string state =
            fraction >= 1f
                ? "full"
                : fraction >= 0.75f
                    ? "threequarter"
                    : fraction >= 0.5f
                        ? "half"
                        : fraction >= 0.25f
                            ? "quarter"
                            : "empty";

        string key = prefix + "_" + state;

        if (_texCache.TryGetValue(key, out var cached))
            return cached;

        string path =
            $"res://Assets/Textures/Stats/{key}.png";

        var tex =
            ResourceLoader.Exists(path)
                ? ResourceLoader.Load<Texture2D>(path)
                : null;

        _texCache[key] = tex;

        return tex;
    }
}