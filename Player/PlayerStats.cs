using Godot;

/// <summary>
/// Handles player stats - HP, Stamina, Mana
/// Skyrim inspired stat system
/// </summary>
public partial class PlayerStats : Node
{
    // Base stat values
    [Export] public float MaxHealth { get; set; } = 100f;
    [Export] public float MaxStamina { get; set; } = 100f;
    [Export] public float MaxMana { get; set; } = 100f;

    // Current stat values
    public float Health { get; private set; }
    public float Stamina { get; private set; }
    public float Mana { get; private set; }

    // Regen rates per second
    [Export] public float HealthRegen { get; set; } = 1f;
    [Export] public float StaminaRegen { get; set; } = 10f;
    [Export] public float ManaRegen { get; set; } = 5f;

    // Stamina regen delay after use (seconds)
    [Export] public float StaminaRegenDelay { get; set; } = 1.5f;
    private float _staminaRegenTimer = 0f;

    // Is player dead
    public bool IsDead { get; private set; } = false;

    // Signals
    [Signal] public delegate void HealthChangedEventHandler(
        float current, float max);
    [Signal] public delegate void StaminaChangedEventHandler(
        float current, float max);
    [Signal] public delegate void ManaChangedEventHandler(
        float current, float max);
    [Signal] public delegate void PlayerDiedEventHandler();

    public override void _Ready()
    {
        // Start at full stats
        Health = MaxHealth;
        Stamina = MaxStamina;
        Mana = MaxMana;
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
            Stamina = Mathf.Min(
                Stamina + StaminaRegen * dt, MaxStamina);
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

    // Apply stat bonuses from equipped gear
    public void ApplyGearBonuses(float bonusHealth,
        float bonusStamina, float bonusMana)
    {
        MaxHealth += bonusHealth;
        MaxStamina += bonusStamina;
        MaxMana += bonusMana;

        // Clamp current values to new max
        Health = Mathf.Min(Health, MaxHealth);
        Stamina = Mathf.Min(Stamina, MaxStamina);
        Mana = Mathf.Min(Mana, MaxMana);
    }

    // Remove stat bonuses when gear is unequipped
    public void RemoveGearBonuses(float bonusHealth,
        float bonusStamina, float bonusMana)
    {
        MaxHealth -= bonusHealth;
        MaxStamina -= bonusStamina;
        MaxMana -= bonusMana;

        // Clamp current values to new max
        Health = Mathf.Min(Health, MaxHealth);
        Stamina = Mathf.Min(Stamina, MaxStamina);
        Mana = Mathf.Min(Mana, MaxMana);
    }
}