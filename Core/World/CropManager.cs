using Godot;
using System;

public partial class CropManager : Node
{
    public static CropManager Instance { get; private set; }

    private static readonly string[] FarmableSoils =
    {
        "dirt", "grass_block", "silt", "clay"
    };

    public override void _Ready()
    {
        Instance = this;
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

    public bool TryUseHoe(Vector3I target)
    {
        var cm = ChunkManager.Instance;
        if (cm == null) return false;

        BlockState soil = cm.GetBlockAtWorld(target);
        if (Array.IndexOf(FarmableSoils, soil.BlockId) < 0)
            return false;

        if (!cm.GetBlockAtWorld(target + Vector3I.Up).IsAir())
            return false;

        var tilled = new BlockState("farmland_" + soil.BlockId);
        cm.SetBlockAtWorld(target, tilled);
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

        if (targetBlock.BlockId.StartsWith("farmland_"))
            cropPosition = target + Vector3I.Up;
        else if (!cm.GetBlockAtWorld(target + Vector3I.Down).BlockId.StartsWith("farmland_"))
            return false;

        if (!cm.GetBlockAtWorld(cropPosition).IsAir())
            return false;

        cm.SetBlockAtWorld(cropPosition, new BlockState("crop_" + cropId + "_0"));
        return true;
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

        bool mature = stage >= 5;
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
                if (!TryParseCrop(crop.BlockId, out string cropId, out int stage) || stage >= 5)
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
                if (soil.BlockId.StartsWith("farmland_"))
                    growth++; // Tilled soil bonus.

                if (HasNearbyWater(cm, worldPos + Vector3I.Down))
                    growth++; // Hydration bonus.

                int nextStage = Math.Min(5, stage + growth);
                chunk.SetBlock(x, y, z, new BlockState($"crop_{cropId}_{nextStage}"));
            }
        }
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
