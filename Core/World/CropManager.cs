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
        {
            seasons.DayChanged += OnDayChanged;
            GD.Print("[CropManager] Connected to SeasonManager day changes.");
        }
        else
        {
            GD.PrintErr("[CropManager] SeasonManager not found; crops will not grow.");
        }

        GD.Print(
            $"[CropManager] Farming system ready. " +
            $"Stages: Wheat {BlockRegistry.Instance?.GetCropStageCount("wheat") ?? 0}, " +
            $"Corn {BlockRegistry.Instance?.GetCropStageCount("corn") ?? 0}, " +
            $"Tomato {BlockRegistry.Instance?.GetCropStageCount("tomato") ?? 0}."
        );
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

        // All tilled ground uses the single shared soil block.
        cm.SetBlockAtWorld(target, new BlockState("soil"));
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

        if (targetBlock.BlockId == "soil")
            cropPosition = target + Vector3I.Up;
        else if (cm.GetBlockAtWorld(target + Vector3I.Down).BlockId != "soil")
            return false;

        if (!cm.GetBlockAtWorld(cropPosition).IsAir())
            return false;

        cm.SetBlockAtWorld(cropPosition, new BlockState("crop_" + cropId + "_1"));
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

        int stageCount = BlockRegistry.Instance?.GetCropStageCount(cropId) ?? 0;
        bool mature = stageCount > 0 && stage >= stageCount;
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

    public bool IsTallCropAt(Vector3I position)
    {
        var cm = ChunkManager.Instance;
        if (cm == null) return false;

        BlockState crop = cm.GetBlockAtWorld(position);
        if (!TryParseCrop(crop.BlockId, out string cropId, out int stage))
            return false;

        BlockResource resource = BlockRegistry.Instance?.GetBlock(crop.BlockId);
        return resource != null && resource.CrossHeight > 1.0f;
    }

    private void OnDayChanged(int newDay, SeasonManager.Season season)
    {
        var cm = ChunkManager.Instance;
        if (cm == null) return;

        int cropsFound = 0;
        int cropsGrown = 0;
        int chunksScanned = 0;

        foreach (Chunk chunk in cm.GetLoadedChunks())
        {
            if (chunk == null || !GodotObject.IsInstanceValid(chunk)) continue;
            chunksScanned++;

            for (int x = 0; x < Chunk.SIZE; x++)
            for (int y = 0; y < Chunk.HEIGHT; y++)
            for (int z = 0; z < Chunk.SIZE; z++)
            {
                BlockState crop = chunk.GetBlock(x, y, z);
                if (!TryParseCrop(crop.BlockId, out string cropId, out int stage))
                    continue;

                cropsFound++;
                int stageCount = BlockRegistry.Instance?.GetCropStageCount(cropId) ?? 0;
                if (stageCount <= 0)
                {
                    GD.PrintErr($"[CropManager] No registered growth stages for {cropId}.");
                    continue;
                }

                if (stage >= stageCount || !IsSeasonAllowed(cropId, season))
                    continue;

                int growth = 1; // Every suitable season day gives one stage.

                Vector3I worldPos = new Vector3I(
                    chunk.ChunkPosition.X * Chunk.SIZE + x,
                    chunk.ChunkPosition.Y * Chunk.HEIGHT + y,
                    chunk.ChunkPosition.Z * Chunk.SIZE + z
                );

                BlockState soil = cm.GetBlockAtWorld(worldPos + Vector3I.Down);
                if (soil.BlockId == "soil")
                    growth++; // Tilled soil bonus.

                if (HasNearbyWater(cm, worldPos + Vector3I.Down))
                    growth++; // Hydration bonus.

                int nextStage = Math.Min(stageCount, stage + growth);

                // 16x32 sprites physically occupy two block heights. Do not
                // advance into one if another block is occupying the upper space.
                BlockResource nextResource = BlockRegistry.Instance?.GetBlock($"crop_{cropId}_{nextStage}");
                if (nextResource != null && nextResource.CrossHeight > 1.0f &&
                    !cm.GetBlockAtWorld(worldPos + Vector3I.Up).IsAir())
                    nextStage = stage;

                if (nextStage > stage)
                {
                    chunk.SetBlock(x, y, z, new BlockState($"crop_{cropId}_{nextStage}"));
                    cropsGrown++;
                }
            }
        }

        GD.Print(
            $"[CropManager] {season} day {newDay}: scanned {chunksScanned} chunks, " +
            $"found {cropsFound} crops, advanced {cropsGrown}."
        );
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
