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
    public MobFood food = new MobFood();
    public MobBreeding breeding = new MobBreeding();
    public MobGenderSettings gender = new MobGenderSettings();
    public MobSpawnSettings spawning = new MobSpawnSettings();
    public FleeDefinition flee = new FleeDefinition();

    // Baby appearance.
    public MobBabySettings baby = new MobBabySettings();

    // Items this mob can drop when it dies.
    public MobDrops drops = new MobDrops();
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