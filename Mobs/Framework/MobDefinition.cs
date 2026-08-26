using System;

[Serializable]
public class MobDefinition
{
    public string id = "";
    public string displayName = "";

    public string model = "";

    public MobStats stats = new MobStats();
    public MobBehavior behavior = new MobBehavior();
    public MobFood food = new MobFood();
    public MobBreeding breeding = new MobBreeding();
    public MobGenderSettings gender = new MobGenderSettings();
    public MobSpawnSettings spawning = new MobSpawnSettings();
	public FleeDefinition flee = new FleeDefinition();
}

[Serializable]
public class FleeDefinition
{
    public bool enabled = true;
    public float distance = 6f;
    public float speedMultiplier = 1.8f;
}
public class MobStats
{
    public float maxHealth = 10.0f;
    public float moveSpeed = 2.5f;
    public float turnSpeed = 8.0f;
}

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

[Serializable]
public class MobFood
{
    public bool enabled = true;

    // Foods this mob can eat for healing.
    public string[] items = Array.Empty<string>();

    // How much HP normal food restores.
    public float healAmount = 5.0f;
}

[Serializable]
public class MobBreeding
{
    public bool enabled = true;

    // Foods that can actually trigger breeding.
    // These should normally also appear in the regular food list.
    public string[] foodItems = Array.Empty<string>();

    // Number of babies produced in one breeding event.
    public int litterMin = 1;
    public int litterMax = 3;

    // Time before this mob can breed again.
    public float breedCooldown = 60.0f;

    // Time required for a baby to become an adult.
    public float babyGrowthTime = 600.0f;
}

[Serializable]
public class MobGenderSettings
{
    public bool enabled = true;

    // 0.5 = 50% male / 50% female.
    public float maleChance = 0.5f;
    public float femaleChance = 0.5f;
}

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