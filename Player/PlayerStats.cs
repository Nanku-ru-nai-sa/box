using Godot;

/// <summary>
/// Handles player stats - HP, Stamina, Mana
/// Skyrim inspired stat system. Values are grouped in 4s so the HUD can
/// display them as pips (hearts for Health, circles for Stamina/Mana) —
/// each pip = 4 points. Starting stats are 12/12/12 (3 pips each).
/// </summary>
public partial class PlayerStats : Node
{
    // Base stat values — kept in multiples of 4 so pip counts come out whole.
    [Export] public float MaxHealth  { get; set; } = 12f;
    [Export] public float MaxStamina { get; set; } = 12f;
    [Export] public float MaxMana    { get; set; } = 12f;

    // Current stat values
    public float Health  { get; private set; }
    public float Stamina { get; private set; }
    public float Mana    { get; private set; }

    // Regen rates per second
    [Export] public float HealthRegen  { get; set; } = 0.5f;
    [Export] public float StaminaRegen { get; set; } = 4f;
    [Export] public float ManaRegen    { get; set; } = 2f;

    // Stamina regen delay after use (seconds)
    [Export] public float StaminaRegenDelay { get; set; } = 1.5f;
    private float _staminaRegenTimer = 0f;

    // Is player dead
    public bool IsDead { get; private set; } = false;

    // ── Leveling / skill points ─────────────────────────────────────────────
    public enum StatType { Health, Stamina, Mana }

    public int Level       { get; private set; } = 1;
    public int SkillPoints { get; private set; } = 0;

    // How much Max*/current increase per allocated point. Kept as a multiple
    // of 4 so it always grants exactly one whole pip.
    [Export] public float PointsPerAllocation { get; set; } = 4f;

    // Signals
    [Signal] public delegate void HealthChangedEventHandler(float current, float max);
    [Signal] public delegate void StaminaChangedEventHandler(float current, float max);
    [Signal] public delegate void ManaChangedEventHandler(float current, float max);
    [Signal] public delegate void PlayerDiedEventHandler();
    [Signal] public delegate void SkillPointsChangedEventHandler(int skillPoints);
    [Signal] public delegate void LeveledUpEventHandler(int newLevel);

    public override void _Ready()
    {
        // Start at full stats
        Health  = MaxHealth;
        Stamina = MaxStamina;
        Mana    = MaxMana;
    }

    public override void _Process(double delta)
    {
        if (IsDead) return;

        float dt = (float)delta;

        // Health regen
        if (Health < MaxHealth)
        {
            Health = Mathf.Min(Health + HealthRegen * dt, MaxHealth);
            EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
        }

        // Stamina regen (delayed after use)
        if (_staminaRegenTimer > 0f)
        {
            _staminaRegenTimer -= dt;
        }
        else if (Stamina < MaxStamina)
        {
            Stamina = Mathf.Min(Stamina + StaminaRegen * dt, MaxStamina);
            EmitSignal(SignalName.StaminaChanged, Stamina, MaxStamina);
        }

        // Mana regen
        if (Mana < MaxMana)
        {
            Mana = Mathf.Min(Mana + ManaRegen * dt, MaxMana);
            EmitSignal(SignalName.ManaChanged, Mana, MaxMana);
        }
    }

    // Take damage
    public void TakeDamage(float amount)
    {
        if (IsDead) return;

        Health = Mathf.Max(Health - amount, 0f);
        EmitSignal(SignalName.HealthChanged, Health, MaxHealth);

        if (Health <= 0f)
            Die();
    }

    // Use stamina - returns false if not enough
    public bool UseStamina(float amount)
    {
        if (Stamina < amount) return false;

        Stamina -= amount;
        _staminaRegenTimer = StaminaRegenDelay;
        EmitSignal(SignalName.StaminaChanged, Stamina, MaxStamina);
        return true;
    }

    // Use mana - returns false if not enough
    public bool UseMana(float amount)
    {
        if (Mana < amount) return false;

        Mana -= amount;
        EmitSignal(SignalName.ManaChanged, Mana, MaxMana);
        return true;
    }

    // Heal health
    public void Heal(float amount)
    {
        Health = Mathf.Min(Health + amount, MaxHealth);
        EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
    }

    // Restore stamina
    public void RestoreStamina(float amount)
    {
        Stamina = Mathf.Min(Stamina + amount, MaxStamina);
        EmitSignal(SignalName.StaminaChanged, Stamina, MaxStamina);
    }

    // Restore mana
    public void RestoreMana(float amount)
    {
        Mana = Mathf.Min(Mana + amount, MaxMana);
        EmitSignal(SignalName.ManaChanged, Mana, MaxMana);
    }

    private void Die()
    {
        IsDead = true;
        EmitSignal(SignalName.PlayerDied);
        GD.Print("Player died.");
    }

    // ── Leveling ─────────────────────────────────────────────────────────────

    // Call this whenever your (future) XP/level system decides the player
    // has leveled up. Grants exactly 1 skill point per level for now.
    public void LevelUp()
    {
        Level++;
        GrantSkillPoint(1);
        EmitSignal(SignalName.LeveledUp, Level);
    }

    public void GrantSkillPoint(int amount = 1)
    {
        SkillPoints += amount;
        EmitSignal(SignalName.SkillPointsChanged, SkillPoints);
    }

    // Spends 1 skill point on the given stat: +1 pip (PointsPerAllocation,
    // default 4) to both Max and current. Returns false if no points to spend.
    public bool AllocatePoint(StatType type)
    {
        if (SkillPoints <= 0) return false;
        SkillPoints--;

        switch (type)
        {
            case StatType.Health:
                MaxHealth += PointsPerAllocation;
                Health    += PointsPerAllocation;
                EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
                break;
            case StatType.Stamina:
                MaxStamina += PointsPerAllocation;
                Stamina    += PointsPerAllocation;
                EmitSignal(SignalName.StaminaChanged, Stamina, MaxStamina);
                break;
            case StatType.Mana:
                MaxMana += PointsPerAllocation;
                Mana    += PointsPerAllocation;
                EmitSignal(SignalName.ManaChanged, Mana, MaxMana);
                break;
        }

        EmitSignal(SignalName.SkillPointsChanged, SkillPoints);
        return true;
    }

    // Apply stat bonuses from equipped gear
    public void ApplyGearBonuses(float bonusHealth, float bonusStamina, float bonusMana)
    {
        MaxHealth  += bonusHealth;
        MaxStamina += bonusStamina;
        MaxMana    += bonusMana;

        Health  = Mathf.Min(Health, MaxHealth);
        Stamina = Mathf.Min(Stamina, MaxStamina);
        Mana    = Mathf.Min(Mana, MaxMana);

        EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
        EmitSignal(SignalName.StaminaChanged, Stamina, MaxStamina);
        EmitSignal(SignalName.ManaChanged, Mana, MaxMana);
    }

    // Remove stat bonuses when gear is unequipped
    public void RemoveGearBonuses(float bonusHealth, float bonusStamina, float bonusMana)
    {
        MaxHealth  -= bonusHealth;
        MaxStamina -= bonusStamina;
        MaxMana    -= bonusMana;

        Health  = Mathf.Min(Health, MaxHealth);
        Stamina = Mathf.Min(Stamina, MaxStamina);
        Mana    = Mathf.Min(Mana, MaxMana);

        EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
        EmitSignal(SignalName.StaminaChanged, Stamina, MaxStamina);
        EmitSignal(SignalName.ManaChanged, Mana, MaxMana);
    }
}