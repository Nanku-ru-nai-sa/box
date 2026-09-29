using System;

[Serializable]
public class MobDefinition
{
    public string id = "";
    public string displayName = "";

    // Default adult model.
    public string model = "";

    public MobStats stats = new MobStats();
    public MobBehavior behavior = new MobBehavior();
    public MobSleepSettings sleep = new MobSleepSettings();
    public MobFood food = new MobFood();
    public MobBreeding breeding = new MobBreeding();
    public MobGenderSettings gender = new MobGenderSettings();
    public MobSpawnSettings spawning = new MobSpawnSettings();
    public FleeDefinition flee = new FleeDefinition();

    public MobFurSettings fur = new MobFurSettings();
    public MobShearingSettings shearing = new MobShearingSettings();

    // Mount / riding settings.
    public MobMountSettings mount = new MobMountSettings();

    // Baby appearance.
    public MobBabySettings baby = new MobBabySettings();

    // Items this mob can drop when it dies.
    public MobDrops drops = new MobDrops();

public MobAppearanceSettings appearance = new MobAppearanceSettings();
}


// =============================================================
// SLEEP
// =============================================================

[Serializable]
public class MobSleepSettings
{
    public bool enabled = false;

    // Game-clock hours.
    // Example: 20 = 8 PM, 6 = 6 AM.
    public float startHour = 20f;
    public float endHour = 6f;
}


// =============================================================
// FUR
// =============================================================

[Serializable]
public class MobFurSettings
{
    public bool enabled = false;
    public string item = "simple_fur";
    public int shearAmount = 1;
    public int deathAmount = 1;
}


// =============================================================
// SHEARING
// =============================================================

[Serializable]
public class MobShearingSettings
{
    public bool enabled = false;
    public bool angerWhenAwake = false;
}


// =============================================================
// MOUNT / RIDING
// =============================================================

[Serializable]
public class MobMountSettings
{
    // Can this mob be ridden?
    public bool enabled = false;

    // Can this mob accept a saddle?
    public bool saddleAllowed = false;

    // Multiplier applied to the mob's normal moveSpeed.
    public float speedMultiplier = 1.0f;

    // Additional multiplier while the player is sprinting.
    public float sprintMultiplier = 1.5f;

    // Multiplier applied to the player's normal jump strength.
    public float jumpMultiplier = 1.0f;

    // Can the player sprint while riding this mob?
    public bool canSprint = true;
}


// =============================================================
// FLEE
// =============================================================

[Serializable]
public class FleeDefinition
{
    public bool enabled = true;
    public float distance = 6f;
    public float speedMultiplier = 1.8f;
}
// =============================================================
// APPEARANCE
// =============================================================

[Serializable]
public class MobAppearanceSettings
{
    // Chance for this mob to spawn shiny.
    // 0.01 = 1%
    public bool shinyEnabled = false;
    public float shinyChance = 0.01f;

    // Chance for this mob to spawn big.
    // 0.01 = 1%
    public bool bigEnabled = false;
    public float bigChance = 0.01f;

    // Big mob is twice the normal size.
    public float bigScale = 2.0f;
}

// =============================================================
// STATS
// =============================================================

[Serializable]
public class MobStats
{
    public float maxHealth = 10.0f;
    public float moveSpeed = 2.5f;
    public float turnSpeed = 8.0f;
}


// =============================================================
// BEHAVIOR
// =============================================================

[Serializable]
public class MobBehavior
{
    public string type = "passive";

    public float wanderRadius = 6.0f;
    public float minIdleTime = 2.0f;
    public float maxIdleTime = 6.0f;

    public float detectionRange = 8.0f;
    public float attackRange = 1.3f;
    public float attackDamage = 2.0f;
    public float attackInterval = 1.2f;
}


// =============================================================
// FOOD
// =============================================================

[Serializable]
public class MobFood
{
    public bool enabled = true;

    // Foods this mob can eat for healing.
    public string[] items = Array.Empty<string>();

    // How much HP normal food restores.
    public float healAmount = 5.0f;
}


// =============================================================
// BREEDING
// =============================================================

[Serializable]
public class MobBreeding
{
    public bool enabled = true;

    public string[] foodItems = Array.Empty<string>();

    public int litterMin = 1;
    public int litterMax = 3;

    // Time before this mob can breed again.
    public float breedCooldown = 900.0f;

    // Random time for a baby to grow into an adult.
    public float babyGrowthMin = 300.0f;
    public float babyGrowthMax = 900.0f;

    // Seasons when this mob can breed.
    // Empty = can breed during every season.
    public string[] seasons = Array.Empty<string>();
}


// =============================================================
// GENDER
// =============================================================

[Serializable]
public class MobGenderSettings
{
    public bool enabled = true;

    // 0.5 = 50% male / 50% female.
    public float maleChance = 0.5f;
    public float femaleChance = 0.5f;

    // Optional male-specific model.
    // Empty = use normal model.
    public string maleModel = "";

    // Optional female-specific model.
    // Empty = use normal model.
    public string femaleModel = "";

    // Optional male-specific texture.
    // Empty = keep model's normal texture.
    public string maleTexture = "";

    // Optional female-specific texture.
    // Empty = keep model's normal texture.
    public string femaleTexture = "";
}


// =============================================================
// BABY
// =============================================================

[Serializable]
public class MobBabySettings
{
    // Optional custom baby model.
    // Empty = use the normal adult model scaled down.
    public string model = "";

    // Optional custom baby texture.
    // Empty = keep the model's normal texture.
    public string texture = "";
}


// =============================================================
// SPAWNING
// =============================================================

[Serializable]
public class MobSpawnSettings
{
    public bool enabled = true;

    public int minGroupSize = 1;
    public int maxGroupSize = 3;

    public float minSpawnDistance = 12.0f;
    public float maxSpawnDistance = 48.0f;

    public int maxWorldCount = 20;

    // Seasons when this mob can naturally spawn.
    // Empty = can spawn during every season.
    public string[] seasons = Array.Empty<string>();
}


// =============================================================
// MOB DROPS
// =============================================================

[Serializable]
public class MobDrops
{
    public bool enabled = true;

    public MobDrop[] items = Array.Empty<MobDrop>();
}


[Serializable]
public class MobDrop
{
    // Item ID from your item registry.
    public string item = "";

    // Minimum amount dropped.
    public int min = 1;

    // Maximum amount dropped.
    public int max = 1;

    // 1.0 = 100%
    // 0.5 = 50%
    // 0.1 = 10%
    public float chance = 1.0f;
}