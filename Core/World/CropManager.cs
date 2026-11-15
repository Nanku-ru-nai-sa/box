using Godot;
using System;
using System.Collections.Generic;

public partial class CropManager : Node
{
    public static CropManager Instance { get; private set; }

    private const string PlantableSoilsPath = "res://Data/Farming/plantable_soils.json";
    private const string TilledSoilId = "soil";
    private readonly HashSet<string> _plantableSoils = new HashSet<string>();
    private static readonly string[] DefaultPlantableSoils =
    {
        "dirt", "grass_block", "silt", "clay", "mud"
    };

    public override void _Ready()
    {
        Instance = this;
        LoadPlantableSoils();

        var seasons = GetNodeOrNull<SeasonManager>("/root/SeasonManager");
        if (seasons != null)
            seasons.DayChanged += OnDayChanged;
        GD.Print("[CropManager] Farming system ready.");
    }

    public override void _ExitTree()
    {
        var seasons = GetNodeOrNull<SeasonManager>("/root/SeasonManager");
        if (seasons != null)
            seasons.DayChanged -= OnDayChanged;
        if (Instance == this) Instance = null;
    }


    private void LoadPlantableSoils()
    {
        _plantableSoils.Clear();
        foreach (string id in DefaultPlantableSoils)
            _plantableSoils.Add(id);

        if (!FileAccess.FileExists(PlantableSoilsPath))
        {
            GD.PrintErr($"[CropManager] Missing {PlantableSoilsPath}. Using defaults.");
            return;
        }

        Variant parsed = Json.ParseString(FileAccess.GetFileAsString(PlantableSoilsPath));
        if (parsed.VariantType != Variant.Type.Dictionary)
        {
            GD.PrintErr("[CropManager] Invalid plantable_soils.json. Using defaults.");
            return;
        }

        Godot.Collections.Dictionary data = parsed.AsGodotDictionary();
        if (!data.ContainsKey("plantable_blocks") ||
            data["plantable_blocks"].VariantType != Variant.Type.Array)
        {
            GD.PrintErr("[CropManager] plantable_blocks must be an array.");
            return;
        }

        _plantableSoils.Clear();
        foreach (Variant entry in data["plantable_blocks"].AsGodotArray())
        {
            string id = entry.AsString().Trim();
            if (!string.IsNullOrEmpty(id))
                _plantableSoils.Add(id);
        }

        GD.Print($"[CropManager] Loaded {_plantableSoils.Count} plantable soil blocks.");
    }

    private bool IsPlantableSoil(string blockId) =>
        _plantableSoils.Contains(blockId);

    private bool IsTilledSoil(string blockId) =>
        blockId == TilledSoilId || blockId.StartsWith("farmland_");

    private bool CanSupportCrops(string blockId) =>
        IsTilledSoil(blockId) || IsPlantableSoil(blockId);

    public bool TryUseHoe(Vector3I target)
    {
        var cm = ChunkManager.Instance;
        if (cm == null) return false;

        BlockState soil = cm.GetBlockAtWorld(target);
        if (!IsPlantableSoil(soil.BlockId))
            return false;

        if (!cm.GetBlockAtWorld(target + Vector3I.Up).IsAir())
            return false;

        cm.SetBlockAtWorld(target, new BlockState(TilledSoilId));
        return true;
    }

    public bool TryPlant(string seedId, Vector3I target)
    {
        string cropId = seedId switch
        {
            "wheat_seed" => "wheat",
            "corn_seed" => "corn",
            "tomato_seed" => "tomato",
            _ => ""
        };

        if (string.IsNullOrEmpty(cropId)) return false;

        var cm = ChunkManager.Instance;
        if (cm == null) return false;

        Vector3I cropPosition = target;
        BlockState targetBlock = cm.GetBlockAtWorld(target);

        if (CanSupportCrops(targetBlock.BlockId))
            cropPosition = target + Vector3I.Up;
        else if (!CanSupportCrops(cm.GetBlockAtWorld(target + Vector3I.Down).BlockId))
            return false;

        if (!cm.GetBlockAtWorld(cropPosition).IsAir())
            return false;

        cm.SetBlockAtWorld(cropPosition, new BlockState("crop_" + cropId + "_0"));
        return true;
    }

    // Right-click harvesting is intentionally limited to crops with special
    // harvest behaviour. Other crops keep their normal left-click break flow.
    public bool TryRightClickHarvest(
        Vector3I position,
        bool hasCornSeed,
        out string itemId,
        out int count,
        out bool consumeCornSeed)
    {
        itemId = "";
        count = 0;
        consumeCornSeed = false;

        var cm = ChunkManager.Instance;
        if (cm == null)
            return false;

        BlockState crop = cm.GetBlockAtWorld(position);
        if (!TryParseCrop(crop.BlockId, out string cropId, out int stage))
            return false;

        if (stage < GetMaxStage(cropId))
            return false;

        var rng = new RandomNumberGenerator();
        rng.Randomize();

        if (cropId == "tomato")
        {
            // Remove the ripe fruit, but leave the plant alive to regrow.
            itemId = "tomato";
            count = rng.RandiRange(2, 4);
            cm.SetBlockAtWorld(position, new BlockState("crop_tomato_2"));
            return true;
        }

        if (cropId == "corn")
        {
            itemId = "corn";
            count = rng.RandiRange(1, 2);

            if (hasCornSeed)
            {
                // Replant in the same action and consume one seed.
                consumeCornSeed = true;
                cm.SetBlockAtWorld(position, new BlockState("crop_corn_0"));
            }
            else
            {
                // Without a seed, harvest normally and clear the plant.
                cm.SetBlockAtWorld(position, BlockState.Air);
            }

            return true;
        }

        return false;
    }

    public bool TryHarvest(Vector3I position, out string itemId, out int count, out string bonusItem, out int bonusCount)
    {
        itemId = "";
        count = 0;
        bonusItem = "";
        bonusCount = 0;

        var cm = ChunkManager.Instance;
        if (cm == null) return false;

        BlockState crop = cm.GetBlockAtWorld(position);
        if (!TryParseCrop(crop.BlockId, out string cropId, out int stage))
            return false;

        int maxStage = GetMaxStage(cropId);
        bool mature = stage >= maxStage;
        var rng = new RandomNumberGenerator();
        rng.Randomize();

        if (cropId == "tomato" && mature)
        {
            itemId = "tomato";
            count = rng.RandiRange(2, 4);
            bonusItem = "tomato_seed";
            bonusCount = 1;
            cm.SetBlockAtWorld(position, new BlockState("crop_tomato_2"));
        }
        else
        {
            itemId = mature ? cropId : cropId + "_seed";
            count = mature
                ? (cropId == "wheat" ? rng.RandiRange(2, 3) : rng.RandiRange(1, 2))
                : 1;

            if (mature)
            {
                bonusItem = cropId + "_seed";
                bonusCount = cropId == "wheat" ? rng.RandiRange(1, 2) : 1;
            }

            cm.SetBlockAtWorld(position, BlockState.Air);
        }

        return true;
    }

    private void OnDayChanged(int newDay, SeasonManager.Season season)
    {
        var cm = ChunkManager.Instance;
        if (cm == null) return;

        foreach (Chunk chunk in cm.GetLoadedChunks())
        {
            if (chunk == null || !GodotObject.IsInstanceValid(chunk)) continue;

            for (int x = 0; x < Chunk.SIZE; x++)
            for (int y = 0; y < Chunk.HEIGHT; y++)
            for (int z = 0; z < Chunk.SIZE; z++)
            {
                BlockState crop = chunk.GetBlock(x, y, z);
                if (!TryParseCrop(crop.BlockId, out string cropId, out int stage) || stage >= GetMaxStage(cropId))
                    continue;

                if (!IsSeasonAllowed(cropId, season))
                    continue;

                int growth = 1; // Every suitable season day gives one stage.

                Vector3I worldPos = new Vector3I(
                    chunk.ChunkPosition.X * Chunk.SIZE + x,
                    chunk.ChunkPosition.Y * Chunk.HEIGHT + y,
                    chunk.ChunkPosition.Z * Chunk.SIZE + z
                );

                BlockState soil = cm.GetBlockAtWorld(worldPos + Vector3I.Down);
                if (IsTilledSoil(soil.BlockId))
                    growth++; // Tilled soil bonus.

                if (HasNearbyWater(cm, worldPos + Vector3I.Down))
                    growth++; // Hydration bonus.

                int nextStage = Math.Min(GetMaxStage(cropId), stage + growth);
                chunk.SetBlock(x, y, z, new BlockState($"crop_{cropId}_{nextStage}"));
            }
        }
    }

    private int GetMaxStage(string cropId)
    {
        int stageCount = BlockRegistry.Instance?.GetCropStageCount(cropId) ?? 0;
        return Math.Max(0, stageCount - 1);
    }

    private bool HasNearbyWater(ChunkManager cm, Vector3I soilPosition)
    {
        for (int dx = -4; dx <= 4; dx++)
        for (int dz = -4; dz <= 4; dz++)
        {
            if (Math.Abs(dx) + Math.Abs(dz) > 4) continue;

            for (int dy = 0; dy <= 1; dy++)
            {
                BlockState nearby = cm.GetBlockAtWorld(
                    soilPosition + new Vector3I(dx, dy, dz)
                );

                if (nearby.BlockId == "water" || nearby.BlockId == "water_spring")
                    return true;
            }
        }

        return false;
    }

    private bool IsSeasonAllowed(string cropId, SeasonManager.Season season)
    {
        if (cropId == "wheat")
            return season == SeasonManager.Season.Spring ||
                   season == SeasonManager.Season.Summer ||
                   season == SeasonManager.Season.Autumn;

        return season == SeasonManager.Season.Spring ||
               season == SeasonManager.Season.Summer;
    }

    private bool TryParseCrop(string blockId, out string cropId, out int stage)
    {
        cropId = "";
        stage = 0;

        if (string.IsNullOrEmpty(blockId) || !blockId.StartsWith("crop_"))
            return false;

        string[] parts = blockId.Split('_');
        if (parts.Length != 3 || !int.TryParse(parts[2], out stage))
            return false;

        cropId = parts[1];
        return cropId == "wheat" || cropId == "corn" || cropId == "tomato";
    }
}
