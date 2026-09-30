using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Handles renewable tree growth.
///
/// This is intentionally NOT a Godot Node.
/// SeasonManager calls OnDayChanged() once per in-game day.
///
/// Growth stages:
///   Stage 1 = Sapling
///   Stage 2 = Twig
///   Stage 3 = Tiny Log
///   Stage 4 = Small Log
///   Stage 5 = Mature Tree
///
/// A tree can advance at most one stage per in-game day.
/// </summary>
public partial class TreeGrowth
{
// ============================================================
// SETTINGS
// ============================================================
private const int WorldMaxY = 288;

// Only chunks within this distance from the player are checked.
// This matches the normal horizontal streaming area reasonably well.
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
// FIND LOADED CHUNKS
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

    // ============================================================
    // IMPORTANT:
    //
    // First collect all trees that existed BEFORE growth began.
    // We must not grow newly-created tree blocks during this
    // same scan.
    // ============================================================

    List<(Vector3I Position, int Stage)> treesToGrow =
        new List<(Vector3I Position, int Stage)>();

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

                    int stage =
                        GetTreeStage(
                            block.BlockId
                        );

                    if (stage <= 0)
                        continue;

                    Vector3I worldPosition =
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
                            worldPosition,
                            stage
                        )
                    );
                }
            }
        }
    }

    // ============================================================
    // NOW grow the trees.
    //
    // Because the list above was created before any changes,
    // newly-created Tiny Logs / Small Logs / Logs cannot grow
    // again during this same day.
    // ============================================================

    int treesFound =
        treesToGrow.Count;

    int treesGrown = 0;

    foreach (
        (Vector3I Position, int Stage) treeData
        in treesToGrow)
    {
        // The tree may have changed since the scan,
        // so only grow the exact stage we found.
        BlockState currentBlock =
            _chunkManager.GetBlockAtWorld(
                treeData.Position
            );

        int currentStage =
            GetTreeStage(
                currentBlock.BlockId
            );

        if (currentStage != treeData.Stage)
            continue;

        if (
            TryGrowTree(
                treeData.Position,
                treeData.Stage
            ))
        {
            treesGrown++;
        }
    }

    GD.Print(
        $"[TreeGrowth] Daily growth check complete. " +
        $"Trees found: {treesFound}, " +
        $"trees grown: {treesGrown}"
    );
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

        object value = field.GetValue(_chunkManager);

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
// TREE STAGES
// ============================================================

private int GetTreeStage(string blockId)
{
    return blockId switch
    {
        "sapling" => 1,
        "twig" => 2,
        "tiny_log" => 3,
        "small_log" => 4,

        _ => 0
    };
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

    int nextStage = currentStage + 1;

    if (!HasRoomForStage(basePosition, nextStage))
        return false;

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

private void GrowToTwig(Vector3I basePosition)
{
    SetTreeBlock(
        basePosition,
        "twig"
    );

    SetTreeBlock(
        basePosition + Vector3I.Up,
        "leaves"
    );
}

// ============================================================
// STAGE 3
// ============================================================

private void GrowToTinyLog(Vector3I basePosition)
{
    SetTreeBlock(
        basePosition,
        "tiny_log"
    );

    SetTreeBlock(
        basePosition + Vector3I.Up,
        "tiny_log"
    );

    SetTreeBlock(
        basePosition + Vector3I.Up * 2,
        "leaves"
    );

    SetTreeBlock(
        basePosition + new Vector3I(1, 2, 0),
        "leaves"
    );
}

// ============================================================
// STAGE 4
// ============================================================

private void GrowToSmallLog(Vector3I basePosition)
{
    SetTreeBlock(
        basePosition,
        "small_log"
    );

    SetTreeBlock(
        basePosition + Vector3I.Up,
        "small_log"
    );

    SetTreeBlock(
        basePosition + Vector3I.Up * 2,
        "small_log"
    );

    SetTreeBlock(
        basePosition + new Vector3I(1, 3, 0),
        "leaves"
    );

    SetTreeBlock(
        basePosition + new Vector3I(-1, 3, 0),
        "leaves"
    );

    SetTreeBlock(
        basePosition + new Vector3I(0, 3, 1),
        "leaves"
    );
}

// ============================================================
// STAGE 5
// ============================================================

private void GrowToMatureTree(Vector3I basePosition)
{
    const int trunkHeight = 5;

    for (int y = 0; y < trunkHeight; y++)
    {
        SetTreeBlock(
            basePosition + Vector3I.Up * y,
            "log"
        );
    }

    int canopyY =
        basePosition.Y + trunkHeight - 1;

    for (int x = -1; x <= 1; x++)
    {
        for (int z = -1; z <= 1; z++)
        {
            if (x == 0 && z == 0)
                continue;

            SetTreeBlock(
                new Vector3I(
                    basePosition.X + x,
                    canopyY,
                    basePosition.Z + z
                ),
                "leaves"
            );
        }
    }

    for (int x = -1; x <= 1; x++)
    {
        for (int z = -1; z <= 1; z++)
        {
            if (Mathf.Abs(x) == 1 &&
                Mathf.Abs(z) == 1)
            {
                continue;
            }

            SetTreeBlock(
                new Vector3I(
                    basePosition.X + x,
                    canopyY + 1,
                    basePosition.Z + z
                ),
                "leaves"
            );
        }
    }

    SetTreeBlock(
        basePosition +
        new Vector3I(
            0,
            trunkHeight + 1,
            0
        ),
        "leaves"
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

                if (checkPosition.Y < 0 ||
                    checkPosition.Y >= WorldMaxY)
                {
                    return false;
                }

                BlockState block =
                    _chunkManager.GetBlockAtWorld(
                        checkPosition
                    );

                if (IsTreeGrowthSpace(block.BlockId))
                    continue;

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

private bool IsTreeGrowthSpace(string blockId)
{
    if (string.IsNullOrEmpty(blockId))
        return true;

    return blockId switch
    {
        "air" => true,
        "leaves" => true,

        // Existing tree stages can be replaced as the tree grows.
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

private bool HasGrowthLight(Vector3I basePosition)
{
    for (
        int y = basePosition.Y + 1;
        y < WorldMaxY;
        y++
    )
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
            continue;

        if (block.BlockId == "leaves")
            continue;

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
    if (worldPosition.Y < 0 ||
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
