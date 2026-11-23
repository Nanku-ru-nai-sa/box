using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Handles renewable tree growth.
///
/// This is intentionally NOT a Godot Node.
/// SeasonManager calls OnDayChanged() once per in-game day.
///
/// Growth is tracked by the tree's root overlay:
///
///   Sapling
///      ↓
///   Twig + root1
///      ↓
///   Tiny Log + root2
///      ↓
///   Small Log + root3
///      ↓
///   Mature Tree + root block
///
/// A tree can advance at most one stage per in-game day.
///
/// root1/root2/root3 are overlays stored on the soil below the tree.
/// The final "root" is a normal full block.
/// </summary>
public partial class TreeGrowth
{
    // ============================================================
    // SETTINGS
    // ============================================================

    private const int WorldMaxY = 288;

    // Only chunks within this distance from the player are checked.
    private const int ChunkScanRadius = 7;

    // ============================================================
    // REFERENCES
    // ============================================================

    private readonly ChunkManager _chunkManager;

    private SeasonManager _seasonManager;

    private bool _initialized;

    // ============================================================
    // CONSTRUCTOR
    // ============================================================

    public TreeGrowth(ChunkManager chunkManager)
    {
        _chunkManager = chunkManager;

        if (_chunkManager == null)
        {
            GD.PrintErr("[TreeGrowth] ChunkManager was null.");
            return;
        }

        FindSeasonManager();

        if (_seasonManager != null)
        {
            _seasonManager.DayChanged += OnDayChanged;
            _initialized = true;

            GD.Print("[TreeGrowth] Initialized.");
        }
        else
        {
            GD.PrintErr("[TreeGrowth] Could not find SeasonManager.");
        }
    }

    // ============================================================
    // CLEANUP
    // ============================================================

    public void Dispose()
    {
        if (_seasonManager != null)
            _seasonManager.DayChanged -= OnDayChanged;

        _initialized = false;
    }

    // ============================================================
    // SEASON MANAGER
    // ============================================================

    private void FindSeasonManager()
    {
        SceneTree tree = Engine.GetMainLoop() as SceneTree;

        if (tree == null)
            return;

        _seasonManager =
            tree.Root.GetNodeOrNull<SeasonManager>("SeasonManager");

        if (_seasonManager != null)
            return;

        _seasonManager =
            tree.Root.GetNodeOrNull<SeasonManager>("/root/SeasonManager");
    }

    // ============================================================
    // DAILY GROWTH
    // ============================================================

    private void OnDayChanged(
        int newDay,
        SeasonManager.Season currentSeason)
    {
        if (!_initialized)
            return;

        if (_chunkManager == null)
            return;

        GrowLoadedTrees();
    }

    // ============================================================
    // FIND LOADED TREES
    // ============================================================

    private void GrowLoadedTrees()
    {
        SceneTree tree = Engine.GetMainLoop() as SceneTree;

        if (tree == null)
            return;

        Node3D player =
            tree.GetFirstNodeInGroup("player") as Node3D;

        if (player == null)
        {
            GD.Print(
                "[TreeGrowth] No player found; skipping daily growth."
            );

            return;
        }

        Vector3I playerChunk =
            _chunkManager.WorldToChunk(
                player.GlobalPosition
            );

        Dictionary<Vector3I, Chunk> loadedChunks =
            GetLoadedChunks();

        if (loadedChunks == null)
        {
            GD.PrintErr(
                "[TreeGrowth] Could not access loaded chunks."
            );

            return;
        }

        // ========================================================
        // FIRST:
        //
        // Find trees that existed BEFORE this growth pass.
        // ========================================================

        List<(Vector3I BasePosition, int Stage)> treesToGrow =
            new List<(Vector3I BasePosition, int Stage)>();
        List<Vector3I> bambooToGrow =
            new List<Vector3I>();

        foreach (
            KeyValuePair<Vector3I, Chunk> pair
            in loadedChunks)
        {
            Vector3I chunkPosition = pair.Key;
            Chunk chunk = pair.Value;

            if (chunk == null)
                continue;

            if (
                Mathf.Abs(
                    chunkPosition.X - playerChunk.X
                ) > ChunkScanRadius)
            {
                continue;
            }

            if (
                Mathf.Abs(
                    chunkPosition.Z - playerChunk.Z
                ) > ChunkScanRadius)
            {
                continue;
            }

            int startY =
                Mathf.Max(
                    0,
                    Chunk.HEIGHT - 32
                );

            for (
                int y = startY;
                y < Chunk.HEIGHT;
                y++)
            {
                for (int x = 0; x < Chunk.SIZE; x++)
                {
                    for (int z = 0; z < Chunk.SIZE; z++)
                    {
                        BlockState block =
                            chunk.GetBlock(
                                x,
                                y,
                                z
                            );

                        // ------------------------------------------------
                        // BAMBOO
                        // Each shoot/form advances exactly one step per day.
                        // ------------------------------------------------

                        if (IsBambooPlant(block.BlockId))
                        {
                            Vector3I bambooPosition = new Vector3I(
                                chunkPosition.X * Chunk.SIZE + x,
                                chunkPosition.Y * Chunk.HEIGHT + y,
                                chunkPosition.Z * Chunk.SIZE + z
                            );

                            // Only process the bottom block of each stalk.
                            // Upper blocks are grown from the base automatically.
                            BlockState belowBamboo = _chunkManager.GetBlockAtWorld(
                                bambooPosition + Vector3I.Down
                            );

                            if (block.BlockId == "bamboo_shoot" ||
                                !IsBambooSegment(belowBamboo.BlockId))
                            {
                                bambooToGrow.Add(bambooPosition);
                            }

                            continue;
                        }

                        // ------------------------------------------------
                        // NEW TREE
                        // ------------------------------------------------

                        if (block.BlockId == "sapling")
                        {
                            Vector3I saplingPosition =
                                new Vector3I(
                                    chunkPosition.X *
                                        Chunk.SIZE + x,

                                    chunkPosition.Y *
                                        Chunk.HEIGHT + y,

                                    chunkPosition.Z *
                                        Chunk.SIZE + z
                                );

                            treesToGrow.Add(
                                (
                                    saplingPosition,
                                    1
                                )
                            );

                            continue;
                        }

                        // ------------------------------------------------
                        // EXISTING TREE
                        //
                        // Root overlays are the authoritative
                        // growth marker.
                        // ------------------------------------------------

                        int rootStage =
                            GetRootStage(block);

                        if (rootStage <= 0)
                            continue;

                        Vector3I rootPosition =
                            new Vector3I(
                                chunkPosition.X *
                                    Chunk.SIZE + x,

                                chunkPosition.Y *
                                    Chunk.HEIGHT + y,

                                chunkPosition.Z *
                                    Chunk.SIZE + z
                            );

                        Vector3I treeBasePosition =
                            rootPosition + Vector3I.Up;

                        int treeStage =
                            rootStage + 1;

                        treesToGrow.Add(
                            (
                                treeBasePosition,
                                treeStage
                            )
                        );
                    }
                }
            }
        }

        // ========================================================
        // NOW GROW.
        // ========================================================

        int treesFound =
            treesToGrow.Count;

        int treesGrown = 0;

        foreach (
            (Vector3I BasePosition, int Stage) treeData
            in treesToGrow)
        {
            if (treeData.Stage == 1)
            {
                BlockState currentBlock =
                    _chunkManager.GetBlockAtWorld(
                        treeData.BasePosition
                    );

                if (currentBlock.BlockId != "sapling")
                    continue;
            }
            else
            {
                Vector3I rootPosition =
                    treeData.BasePosition + Vector3I.Down;

                BlockState rootBlock =
                    _chunkManager.GetBlockAtWorld(
                        rootPosition
                    );

                int currentRootStage =
                    GetRootStage(rootBlock);

                int expectedRootStage =
                    treeData.Stage - 1;

                if (currentRootStage != expectedRootStage)
                    continue;
            }

            if (
                TryGrowTree(
                    treeData.BasePosition,
                    treeData.Stage
                ))
            {
                treesGrown++;
            }
        }

        int bambooGrown = 0;

        foreach (Vector3I bambooBasePosition in bambooToGrow)
        {
            if (TryGrowBamboo(bambooBasePosition))
                bambooGrown++;
        }

        GD.Print(
            $"[TreeGrowth] Daily growth check complete. " +
            $"Trees found: {treesFound}, " +
            $"trees grown: {treesGrown}, " +
            $"bamboo found: {bambooToGrow.Count}, " +
            $"bamboo grown: {bambooGrown}"
        );
    }

    // ============================================================
    // BAMBOO GROWTH
    // ============================================================

    private static bool IsBambooPlant(string blockId)
    {
        return blockId == "bamboo_shoot" || IsBambooSegment(blockId);
    }

    private static bool IsBambooSegment(string blockId)
    {
        return blockId == "bamboo_stick" ||
               blockId == "bamboo" ||
               blockId == "big_bamboo";
    }

    private bool TryGrowBamboo(Vector3I basePosition)
    {
        BlockState baseBlock = _chunkManager.GetBlockAtWorld(basePosition);
        if (!IsBambooPlant(baseBlock.BlockId))
            return false;

        BlockState ground = _chunkManager.GetBlockAtWorld(basePosition + Vector3I.Down);
        BlockResource groundResource = BlockRegistry.Instance?.GetBlock(ground.BlockId);

        if (groundResource == null || !groundResource.IsSolid)
            return false;

        if (!HasGrowthLight(basePosition))
            return false;

        RandomNumberGenerator rng = new RandomNumberGenerator();
        rng.Randomize();

        // The shoot becomes a complete stalk in one growth event.
        // Save its random size and height on the base block so reloads
        // and later daily growth checks keep the same result.
        int targetHeight = GetBambooTargetHeight(baseBlock);

        if (targetHeight < 3 || targetHeight > 7)
        {
            targetHeight = (int)rng.RandiRange(3, 7);

            if (baseBlock.BlockId == "bamboo_shoot")
            {
                int sizeRoll = (int)rng.RandiRange(0, 2);
                baseBlock.BlockId = sizeRoll switch
                {
                    0 => "bamboo_stick",
                    1 => "bamboo",
                    _ => "big_bamboo"
                };
            }

            baseBlock.AddFeature("bamboo:height:" + targetHeight);
            _chunkManager.SetBlockAtWorld(basePosition, baseBlock);
        }

        string segmentBlockId = baseBlock.BlockId;
        if (!IsBambooSegment(segmentBlockId))
            return false;

        // If this is an older partially-grown stalk, finish it in this
        // same growth event rather than adding only one segment per day.
        int currentHeight = 0;
        while (basePosition.Y + currentHeight < WorldMaxY)
        {
            BlockState segment = _chunkManager.GetBlockAtWorld(
                basePosition + new Vector3I(0, currentHeight, 0)
            );

            if (!IsBambooSegment(segment.BlockId))
                break;

            currentHeight++;
        }

        if (currentHeight >= targetHeight)
            return false;

        // Do not grow into another block. The saved target stays fixed,
        // so the stalk can finish later if the obstruction is removed.
        for (int y = currentHeight; y < targetHeight; y++)
        {
            Vector3I position = basePosition + new Vector3I(0, y, 0);
            if (!_chunkManager.GetBlockAtWorld(position).IsAir())
                return false;
        }

        int leafBlockCount = (int)rng.RandiRange(1, 2);
        int firstLeafY = targetHeight - leafBlockCount;

        for (int y = 0; y < targetHeight; y++)
        {
            Vector3I position = basePosition + new Vector3I(0, y, 0);
            BlockState segment;

            if (y == 0)
            {
                segment = baseBlock;
            }
            else
            {
                segment = new BlockState(segmentBlockId);
            }

            if (y >= firstLeafY)
                segment.AddFeature("bamboo:leaves");

            _chunkManager.SetBlockAtWorld(position, segment);
        }

        GD.Print(
            $"[TreeGrowth] Bamboo at {basePosition} instantly grew to " +
            $"{targetHeight} blocks ({segmentBlockId}) with " +
            $"{leafBlockCount} leafy top block(s)."
        );

        return true;
    }

    private static int GetBambooTargetHeight(BlockState baseBlock)
    {
        if (baseBlock.Features == null)
            return 0;

        foreach (string feature in baseBlock.Features)
        {
            const string prefix = "bamboo:height:";
            if (feature != null &&
                feature.StartsWith(prefix, StringComparison.Ordinal) &&
                int.TryParse(feature.Substring(prefix.Length), out int height))
            {
                return height;
            }
        }

        return 0;
    }

    // ============================================================
    // LOADED CHUNK ACCESS
    // ============================================================

    private Dictionary<Vector3I, Chunk> GetLoadedChunks()
    {
        try
        {
            FieldInfo field =
                typeof(ChunkManager).GetField(
                    "_chunks",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic
                );

            if (field == null)
            {
                GD.PrintErr(
                    "[TreeGrowth] Could not find ChunkManager._chunks."
                );

                return null;
            }

            object value =
                field.GetValue(_chunkManager);

            return value as Dictionary<Vector3I, Chunk>;
        }
        catch (Exception ex)
        {
            GD.PrintErr(
                $"[TreeGrowth] Failed to access loaded chunks: {ex.Message}"
            );

            return null;
        }
    }

    // ============================================================
    // ROOT STAGE
    // ============================================================

    private int GetRootStage(BlockState block)
    {
        if (block.Features == null)
            return 0;

        foreach (string feature in block.Features)
        {
            switch (feature)
            {
                case "root:root1":
                    return 1;

                case "root:root2":
                    return 2;

                case "root:root3":
                    return 3;
            }
        }

        return 0;
    }

    // ============================================================
    // GROW TREE
    // ============================================================

    private bool TryGrowTree(
        Vector3I basePosition,
        int currentStage)
    {
        if (currentStage < 1 || currentStage > 4)
            return false;

        int nextStage =
            currentStage + 1;

        if (!HasRoomForStage(
                basePosition,
                nextStage))
        {
            return false;
        }

        if (!HasGrowthLight(basePosition))
            return false;

        switch (nextStage)
        {
            case 2:
                GrowToTwig(basePosition);
                return true;

            case 3:
                GrowToTinyLog(basePosition);
                return true;

            case 4:
                GrowToSmallLog(basePosition);
                return true;

            case 5:
                GrowToMatureTree(basePosition);
                return true;
        }

        return false;
    }

    // ============================================================
    // STAGE 2
    // ============================================================

    private void GrowToTwig(
        Vector3I basePosition)
    {
        SetTreeBlock(
            basePosition,
            "twig"
        );

        SetTreeBlock(
            basePosition + Vector3I.Up,
            "leaves"
        );

        SetRootOverlay(
            basePosition + Vector3I.Down,
            "root:root1"
        );
    }

    // ============================================================
    // STAGE 3
    // ============================================================

    private void GrowToTinyLog(Vector3I basePosition)
{
    // ------------------------------------------------------------
    // TRUNK
    // ------------------------------------------------------------

    for (int y = 0; y < 3; y++)
    {
        SetTreeBlock(
            basePosition + Vector3I.Up * y,
            "tiny_log"
        );
    }

    // ------------------------------------------------------------
    // CANOPY - 3x3
    // ------------------------------------------------------------

    int canopyY = basePosition.Y + 2;

    for (int x = -1; x <= 1; x++)
    {
        for (int z = -1; z <= 1; z++)
        {
            Vector3I position =
                new Vector3I(
                    basePosition.X + x,
                    canopyY,
                    basePosition.Z + z
                );

            if (x == 0 && z == 0)
                SetTreeBlock(position, "tiny_log");
            else
                SetTreeBlock(position, "leaves");
        }
    }

    // ------------------------------------------------------------
    // TOP LEAF - SMALL PLUS SHAPE
    // ------------------------------------------------------------

    int topY = basePosition.Y + 3;

    SetTreeBlock(
        new Vector3I(basePosition.X, topY, basePosition.Z),
        "leaves"
    );

    SetTreeBlock(
        new Vector3I(basePosition.X + 1, topY, basePosition.Z),
        "leaves"
    );

    SetTreeBlock(
        new Vector3I(basePosition.X - 1, topY, basePosition.Z),
        "leaves"
    );

    SetTreeBlock(
        new Vector3I(basePosition.X, topY, basePosition.Z + 1),
        "leaves"
    );

    SetTreeBlock(
        new Vector3I(basePosition.X, topY, basePosition.Z - 1),
        "leaves"
    );

    // ------------------------------------------------------------
    // ROOT STAGE
    // ------------------------------------------------------------

    SetRootOverlay(
        basePosition + Vector3I.Down,
        "root:root2"
    );
}

    // ============================================================
    // STAGE 4
    // ============================================================

    private void GrowToSmallLog(Vector3I basePosition)
{
    // ------------------------------------------------------------
    // TRUNK
    // ------------------------------------------------------------

    for (int y = 0; y < 4; y++)
    {
        SetTreeBlock(
            basePosition + Vector3I.Up * y,
            "small_log"
        );
    }

    // ------------------------------------------------------------
    // LOWER CANOPY - 5x5
    // ------------------------------------------------------------

    int lowerCanopyY = basePosition.Y + 2;

    for (int x = -2; x <= 2; x++)
    {
        for (int z = -2; z <= 2; z++)
        {
            // Cut the four corners to keep the canopy rounded.
            if (Mathf.Abs(x) == 2 && Mathf.Abs(z) == 2)
                continue;

            Vector3I position =
                new Vector3I(
                    basePosition.X + x,
                    lowerCanopyY,
                    basePosition.Z + z
                );

            if (x == 0 && z == 0)
                SetTreeBlock(position, "small_log");
            else
                SetTreeBlock(position, "leaves");
        }
    }

    // ------------------------------------------------------------
    // UPPER CANOPY - 3x3
    // ------------------------------------------------------------

    int upperCanopyY = basePosition.Y + 3;

    for (int x = -1; x <= 1; x++)
    {
        for (int z = -1; z <= 1; z++)
        {
            Vector3I position =
                new Vector3I(
                    basePosition.X + x,
                    upperCanopyY,
                    basePosition.Z + z
                );

            if (x == 0 && z == 0)
                SetTreeBlock(position, "small_log");
            else
                SetTreeBlock(position, "leaves");
        }
    }

    // ------------------------------------------------------------
// TOP LEAF - SMALL PLUS SHAPE
// ------------------------------------------------------------

int topY = basePosition.Y + 4;

SetTreeBlock(
    new Vector3I(basePosition.X, topY, basePosition.Z),
    "leaves"
);

SetTreeBlock(
    new Vector3I(basePosition.X + 1, topY, basePosition.Z),
    "leaves"
);

SetTreeBlock(
    new Vector3I(basePosition.X - 1, topY, basePosition.Z),
    "leaves"
);

SetTreeBlock(
    new Vector3I(basePosition.X, topY, basePosition.Z + 1),
    "leaves"
);

SetTreeBlock(
    new Vector3I(basePosition.X, topY, basePosition.Z - 1),
    "leaves"
);

    // ------------------------------------------------------------
    // ROOT STAGE
    // ------------------------------------------------------------

    SetRootOverlay(
        basePosition + Vector3I.Down,
        "root:root3"
    );
}

    // ============================================================
    // STAGE 5
    // ============================================================

    private void GrowToMatureTree(Vector3I basePosition)
{
    // Remove the leaves created by Stage 4.
    for (int y = 2; y <= 4; y++)
    {
        for (int x = -2; x <= 2; x++)
        {
            for (int z = -2; z <= 2; z++)
            {
                Vector3I position =
                    basePosition + new Vector3I(x, y, z);

                BlockState block =
                    _chunkManager.GetBlockAtWorld(position);

                if (block.BlockId == "leaves")
                {
                    SetTreeBlock(position, "air");
                }
            }
        }
    }

    // Mature trees naturally vary between 3 and 5 total logs.
    int trunkHeight = GD.RandRange(3, 5);

    // ------------------------------------------------------------
    // TRUNK
    // ------------------------------------------------------------

    for (int y = 0; y < trunkHeight; y++)
    {
        SetTreeBlock(
            basePosition + Vector3I.Up * y,
            "log"
        );
    }

    // The canopy begins one block below the top of the trunk.
    int canopyBaseY = basePosition.Y + trunkHeight - 1;

    // Same canopy pattern used by ChunkGen:
    // bottom two layers are wider, upper two layers are smaller.
    int[] canopyRadii = { 2, 2, 1, 1 };

    for (int layer = 0; layer < canopyRadii.Length; layer++)
    {
        int radius = canopyRadii[layer];
        int y = canopyBaseY + layer;

        for (int x = -radius; x <= radius; x++)
        {
            for (int z = -radius; z <= radius; z++)
            {
                // Remove the four corners from radius-2 layers.
                if (
                    radius > 1 &&
                    Mathf.Abs(x) == radius &&
                    Mathf.Abs(z) == radius
                )
                {
                    continue;
                }

                Vector3I position =
                    new Vector3I(
                        basePosition.X + x,
                        y,
                        basePosition.Z + z
                    );

                // The center remains part of the trunk.
                if (x == 0 && z == 0)
                {
                    SetTreeBlock(position, "log");
                }
                else
                {
                    SetTreeBlock(position, "leaves");
                }
            }
        }
    }

    // Extra leaf directly above the trunk.
    SetTreeBlock(
        new Vector3I(
            basePosition.X,
            basePosition.Y + trunkHeight + 2,
            basePosition.Z
        ),
        "leaves"
    );

    // Extra leaf above the canopy.
    int canopyTopY = canopyBaseY + canopyRadii.Length;

    SetTreeBlock(
        new Vector3I(
            basePosition.X,
            canopyTopY,
            basePosition.Z
        ),
        "leaves"
    );

    // Mature tree gets the final full root block.
    SetTreeBlock(
        basePosition + Vector3I.Down,
        "root"
    );
}

    // ============================================================
    // ROOT OVERLAY
    // ============================================================

    private void SetRootOverlay(
        Vector3I worldPosition,
        string rootFeature)
    {
        if (
            worldPosition.Y < 0 ||
            worldPosition.Y >= WorldMaxY)
        {
            return;
        }

        BlockState soil =
            _chunkManager.GetBlockAtWorld(
                worldPosition
            );

        if (soil.IsAir())
        {
            GD.PrintErr(
                $"[TreeGrowth] Cannot place {rootFeature}: " +
                $"soil position {worldPosition} is air."
            );

            return;
        }

        // Remove any previous root stage.
        if (soil.Features != null)
        {
            string[] existingFeatures =
                (string[])soil.Features.Clone();

            foreach (string feature in existingFeatures)
            {
                if (
                    feature != null &&
                    feature.StartsWith("root:")
                )
                {
                    soil.RemoveFeature(feature);
                }
            }
        }

        // Add the new root stage.
        soil.AddFeature(rootFeature);

        // Natural growth keeps this as a world modification and
        // forces the chunk to rebuild.
        _chunkManager.SetBlockAtWorldNaturalGrowth(
            worldPosition,
            soil
        );

        GD.Print(
            $"[TreeGrowth] Placed {rootFeature} at {worldPosition}."
        );
    }

    // ============================================================
    // SPACE CHECK
    // ============================================================

    private bool HasRoomForStage(
        Vector3I basePosition,
        int targetStage)
    {
        int requiredHeight =
            GetRequiredHeight(targetStage);

        if (requiredHeight <= 0)
            return false;

        for (int y = 0; y < requiredHeight; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    Vector3I checkPosition =
                        basePosition +
                        new Vector3I(x, y, z);

                    if (
                        checkPosition.Y < 0 ||
                        checkPosition.Y >= WorldMaxY)
                    {
                        return false;
                    }

                    BlockState block =
                        _chunkManager.GetBlockAtWorld(
                            checkPosition
                        );

                    if (
                        IsTreeGrowthSpace(
                            block.BlockId))
                    {
                        continue;
                    }

                    return false;
                }
            }
        }

        return true;
    }

    // ============================================================
    // REQUIRED HEIGHT
    // ============================================================

    private int GetRequiredHeight(int stage)
    {
        return stage switch
        {
            2 => 2,
            3 => 3,
            4 => 4,
            5 => 7,

            _ => 0
        };
    }

    // ============================================================
    // GROWTH SPACE
    // ============================================================

    private bool IsTreeGrowthSpace(
        string blockId)
    {
        if (string.IsNullOrEmpty(blockId))
            return true;

        return blockId switch
        {
            "air" => true,
            "leaves" => true,

            "sapling" => true,
            "twig" => true,
            "tiny_log" => true,
            "small_log" => true,

            _ => false
        };
    }

    // ============================================================
    // LIGHT CHECK
    // ============================================================

    private bool HasGrowthLight(
        Vector3I basePosition)
    {
        // We only care whether the tree has open sky above it.
        //
        // Tree blocks themselves obviously occupy this column,
        // so we start ABOVE the current tree instead of immediately
        // treating the trunk as an obstruction.
        //
        // Leaves are also allowed because they are part of the tree
        // canopy and should not prevent future growth.

        for (
            int y = basePosition.Y + 1;
            y < WorldMaxY;
            y++)
        {
            BlockState block =
                _chunkManager.GetBlockAtWorld(
                    new Vector3I(
                        basePosition.X,
                        y,
                        basePosition.Z
                    )
                );

            if (block.IsAir())
                return true;

            if (block.BlockId == "leaves")
                continue;

            if (
                block.BlockId == "twig" ||
                block.BlockId == "tiny_log" ||
                block.BlockId == "small_log" ||
                block.BlockId == "log" ||
                IsBambooSegment(block.BlockId))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    // ============================================================
    // SET TREE BLOCK
    // ============================================================

    private void SetTreeBlock(
        Vector3I worldPosition,
        string blockId)
    {
        if (
            worldPosition.Y < 0 ||
            worldPosition.Y >= WorldMaxY)
        {
            return;
        }

        _chunkManager.SetBlockAtWorld(
            worldPosition,
            new BlockState
            {
                BlockId = blockId,
                BitMask = 0xFF
            }
        );
    }
}