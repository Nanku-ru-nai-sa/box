using Godot;

[GlobalClass]
public partial class ItemResource : Resource
{
    // Basic Info
    [Export] public string ItemId { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public string Description { get; set; } = "";

    // Inventory Grid Size (EFT style)
    [Export] public int GridWidth { get; set; } = 1;
    [Export] public int GridHeight { get; set; } = 1;
    [Export] public bool CanRotate { get; set; } = true;

    // Stacking
    [Export] public bool IsStackable { get; set; } = true;
    [Export] public int MaxStackSize { get; set; } = 64;

    // Item Type Tags (weapon, tool, consumable, block, ammo etc)
    [Export] public string[] Tags { get; set; } = new string[0];

    // Visual
    [Export] public Texture2D Icon { get; set; }

    // Equipment
    [Export] public bool IsEquippable { get; set; } = false;
    [Export] public string EquipSlot { get; set; } = "";
    // e.g. "head", "chest", "legs", "feet",
    //      "mainhand", "offhand", "mount"

    // Block Placement
    // If this item places a block, put the block ID here
    [Export] public string PlacesBlockId { get; set; } = "";

    // Stats (gear bonuses, applied when equipped)
    [Export] public float BonusHealth { get; set; } = 0f;
    [Export] public float BonusStamina { get; set; } = 0f;
    [Export] public float BonusMana { get; set; } = 0f;
    [Export] public float BonusMovementSpeed { get; set; } = 0f;

    // Movement Modifiers (Zelda style gear upgrades)
    [Export] public bool GrantsDoubleJump { get; set; } = false;
    [Export] public bool GrantsWallClimb { get; set; } = false;
    [Export] public bool GrantsSwimming { get; set; } = false;
    [Export] public bool GrantsGliding { get; set; } = false;
    [Export] public bool GrantsGrapple { get; set; } = false;

    // Combat Stats
    [Export] public float AttackDamage { get; set; } = 0f;
    [Export] public float AttackSpeed { get; set; } = 0f;
    [Export] public float AttackRange { get; set; } = 0f;
    [Export] public float BlockAmount { get; set; } = 0f;

    // Tool Stats
    [Export] public bool IsChisel { get; set; } = false;
    [Export] public string ToolType { get; set; } = "";
    // e.g. "pickaxe", "axe", "shovel", "chisel"
    [Export] public float ToolSpeed { get; set; } = 1f;
    [Export] public int ToolTier { get; set; } = 0;
    // 0=wood, 1=stone, 2=iron, 3=diamond etc

    // Durability
    [Export] public bool HasDurability { get; set; } = false;
    [Export] public int MaxDurability { get; set; } = 0;

    // Consumable
    [Export] public bool IsConsumable { get; set; } = false;
    [Export] public float HealHealth { get; set; } = 0f;
    [Export] public float RestoreStamina { get; set; } = 0f;
    [Export] public float RestoreMana { get; set; } = 0f;

    // Sound
    [Export] public string SoundPickup { get; set; } = "";
    [Export] public string SoundDrop { get; set; } = "";
    [Export] public string SoundUse { get; set; } = "";
    [Export] public string SoundEquip { get; set; } = "";
}