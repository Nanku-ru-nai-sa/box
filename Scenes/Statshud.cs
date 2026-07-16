using Godot;
using System.Collections.Generic;

// StatsHud — three independently-fading pip bars (mana / health / stamina)
// shown above the hotbar. Each pip represents 4 points of a stat (leveling
// via PlayerStats.AllocatePoint adds whole pips). A bar is fully visible
// whenever its stat isn't at max; once full it lingers for FadeDelay
// seconds, then fades to invisible. Bars fade completely independently —
// e.g. Mana can be hidden while Stamina is shown mid-sprint.
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

    private TextureRect[] _manaPips    = new TextureRect[0];
    private TextureRect[] _healthPips  = new TextureRect[0];
    private TextureRect[] _staminaPips = new TextureRect[0];

    private float _healthCur, _healthMax;
    private float _staminaCur, _staminaMax;
    private float _manaCur, _manaMax;

    private float _healthFadeTimer;
    private float _staminaFadeTimer;
    private float _manaFadeTimer;

    private const int   PipSize   = 26;
    private const int   PipGap    = 2;
    private const int   GroupGap  = 18;
    private const float FadeDelay = 2.5f; // seconds a full bar stays visible before fading
    private const float FadeSpeed = 1.2f; // alpha units/sec fading out
    private const float ShowSpeed = 8f;   // alpha units/sec fading in (snappy)

    private readonly Dictionary<string, Texture2D> _texCache = new();

    // Called by Player BEFORE this node enters the tree (same pattern as
    // CraftingPanel.Init / EquipmentPanel.Init) — just stores the reference
    // and subscribes; _Ready() (runs later, once added to the tree) does the
    // actual building using the values already cached here.
    public void Init(PlayerStats stats)
    {
        _stats = stats;
        if (_stats == null) return;

        _healthCur  = _stats.Health;  _healthMax  = _stats.MaxHealth;
        _staminaCur = _stats.Stamina; _staminaMax = _stats.MaxStamina;
        _manaCur    = _stats.Mana;    _manaMax    = _stats.MaxMana;

        _stats.HealthChanged  += OnHealthChanged;
        _stats.StaminaChanged += OnStaminaChanged;
        _stats.ManaChanged    += OnManaChanged;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", GroupGap);

        _manaGroup    = MakeGroup();
        _healthGroup  = MakeGroup();
        _staminaGroup = MakeGroup();
        AddChild(_manaGroup);
        AddChild(_healthGroup);
        AddChild(_staminaGroup);

        RefreshGroupPips(_manaGroup,    ref _manaPips,    "mana",    _manaCur,    _manaMax);
        RefreshGroupPips(_healthGroup,  ref _healthPips,  "heart",   _healthCur,  _healthMax);
        RefreshGroupPips(_staminaGroup, ref _staminaPips, "stamina", _staminaCur, _staminaMax);

        _manaGroup.Modulate    = new Color(1, 1, 1, 1);
        _healthGroup.Modulate  = new Color(1, 1, 1, 1);
        _staminaGroup.Modulate = new Color(1, 1, 1, 1);
    }

    public override void _ExitTree()
    {
        if (_stats == null) return;
        _stats.HealthChanged  -= OnHealthChanged;
        _stats.StaminaChanged -= OnStaminaChanged;
        _stats.ManaChanged    -= OnManaChanged;
    }

    // =========================================================================
    // EVENTS
    // =========================================================================

    private void OnHealthChanged(float cur, float max)
    {
        _healthCur = cur; _healthMax = max;
        if (_healthGroup != null) RefreshGroupPips(_healthGroup, ref _healthPips, "heart", cur, max);
    }

    private void OnStaminaChanged(float cur, float max)
    {
        _staminaCur = cur; _staminaMax = max;
        if (_staminaGroup != null) RefreshGroupPips(_staminaGroup, ref _staminaPips, "stamina", cur, max);
    }

    private void OnManaChanged(float cur, float max)
    {
        _manaCur = cur; _manaMax = max;
        if (_manaGroup != null) RefreshGroupPips(_manaGroup, ref _manaPips, "mana", cur, max);
    }

    // =========================================================================
    // PROCESS — fade logic only. Each group fades completely independently.
    // =========================================================================

    public override void _Process(double delta)
    {
        if (_manaGroup == null) return;
        float dt = (float)delta;
        UpdateFade(_manaGroup,    _manaCur,    _manaMax,    ref _manaFadeTimer,    dt);
        UpdateFade(_healthGroup,  _healthCur,  _healthMax,  ref _healthFadeTimer,  dt);
        UpdateFade(_staminaGroup, _staminaCur, _staminaMax, ref _staminaFadeTimer, dt);
    }

    // Visible whenever the stat isn't full. Once full, stays visible for
    // FadeDelay seconds (grace period), then fades to 0 alpha. Dropping
    // below full at any point immediately shows it again.
    private void UpdateFade(Control group, float cur, float max, ref float fadeTimer, float dt)
    {
        bool full = cur >= max - 0.001f;
        float a   = group.Modulate.A;

        if (!full)
        {
            fadeTimer = FadeDelay;
            a = Mathf.MoveToward(a, 1f, dt * ShowSpeed);
        }
        else if (fadeTimer > 0f)
        {
            fadeTimer -= dt;
            a = Mathf.MoveToward(a, 1f, dt * ShowSpeed);
        }
        else
        {
            a = Mathf.MoveToward(a, 0f, dt * FadeSpeed);
        }

        group.Modulate = new Color(1f, 1f, 1f, a);
    }

    // =========================================================================
    // PIP BUILDING
    // =========================================================================

    private void RefreshGroupPips(HBoxContainer group, ref TextureRect[] pips, string prefix, float cur, float max)
    {
        int count = Mathf.Max(1, Mathf.CeilToInt(max / 4f));
        if (pips.Length != count)
        {
            foreach (Node c in group.GetChildren()) c.QueueFree();
            pips = new TextureRect[count];
            for (int i = 0; i < count; i++)
            {
                var tr = MakePip();
                group.AddChild(tr);
                pips[i] = tr;
            }
        }

        for (int i = 0; i < count; i++)
        {
            float remaining = Mathf.Clamp(cur - i * 4f, 0f, 4f);
            pips[i].Texture = GetPipTexture(prefix, remaining / 4f);
        }
    }

    private HBoxContainer MakeGroup()
    {
        var g = new HBoxContainer();
        g.AddThemeConstantOverride("separation", PipGap);
        g.MouseFilter = MouseFilterEnum.Ignore;
        return g;
    }

    private TextureRect MakePip()
    {
        var tr = new TextureRect();
        tr.CustomMinimumSize = new Vector2(PipSize, PipSize);
        tr.ExpandMode        = TextureRect.ExpandModeEnum.IgnoreSize;
        tr.StretchMode       = TextureRect.StretchModeEnum.KeepAspectCentered;
        tr.TextureFilter     = CanvasItem.TextureFilterEnum.Nearest;
        tr.MouseFilter       = MouseFilterEnum.Ignore;
        return tr;
    }

    private Texture2D GetPipTexture(string prefix, float fraction)
    {
        string state =
            fraction >= 1f    ? "full" :
            fraction >= 0.75f ? "threequarter" :
            fraction >= 0.5f  ? "half" :
            fraction >= 0.25f ? "quarter" : "empty";

        string key = prefix + "_" + state;
        if (_texCache.TryGetValue(key, out var cached)) return cached;

        string path = $"res://Assets/Textures/Stats/{key}.png";
        var tex = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
        _texCache[key] = tex;
        return tex;
    }
}