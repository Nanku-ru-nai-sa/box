using Godot;

[GlobalClass]
public partial class ItemResource : Resource
{
    [Export] public string ItemId { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public string Description { get; set; } = "";
    [Export] public int GridWidth { get; set; } = 1;
    [Export] public int GridHeight { get; set; } = 1;
    [Export] public bool CanRotate { get; set; } = true;
    [Export] public bool IsStackable { get; set; } = true;
    [Export] public int MaxStackSize { get; set; } = 64;
    [Export] public string[] Tags { get; set; } = new string[0];
    [Export] public Texture2D Icon { get; set; }
    [Export] public bool IsEquippable { get; set; } = false;
    [Export] public string EquipSlot { get; set; } = "";
    [Export] public string PlacesBlockId { get; set; } = "";
    [Export] public float BonusHealth { get; set; } = 0f;
    [Export] public float BonusStamina { get; set; } = 0f;
    [Export] public float BonusMana { get; set; } = 0f;
    [Export] public float BonusMovementSpeed { get; set; } = 0f;
    [Export] public bool GrantsDoubleJump { get; set; } = false;
    [Export] public bool GrantsWallClimb { get; set; } = false;
    [Export] public bool GrantsSwimming { get; set; } = false;
    [Export] public bool GrantsGliding { get; set; } = false;
    [Export] public bool GrantsGrapple { get; set; } = false;
    [Export] public float AttackDamage { get; set; } = 0f;
    [Export] public float AttackSpeed { get; set; } = 0f;
    [Export] public float AttackRange { get; set; } = 0f;
    [Export] public float BlockAmount { get; set; } = 0f;
    [Export] public bool IsChisel { get; set; } = false;
    [Export] public string ToolType { get; set; } = "";
    [Export] public float ToolSpeed { get; set; } = 1f;
    [Export] public int ToolTier { get; set; } = 0;
    [Export] public bool HasDurability { get; set; } = false;
    [Export] public int MaxDurability { get; set; } = 0;
    [Export] public bool IsConsumable { get; set; } = false;
    [Export] public float HealHealth { get; set; } = 0f;
    [Export] public float RestoreStamina { get; set; } = 0f;
    [Export] public float RestoreMana { get; set; } = 0f;
    [Export] public string SoundPickup { get; set; } = "";
    [Export] public string SoundDrop { get; set; } = "";
    [Export] public string SoundUse { get; set; } = "";
    [Export] public string SoundEquip { get; set; } = "";
}