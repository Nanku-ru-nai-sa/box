using Godot;
using System.Collections.Generic;

/// <summary>
/// Shared finite-liquid simulation. Liquids are registered by BlockResource.LiquidId,
/// so future liquids can use this same flow engine without adding per-liquid logic.
/// </summary>
public partial class LiquidManager : Node
{
    private ChunkManager _world;
    private readonly Queue<Vector3I> _queue = new();
    private readonly HashSet<Vector3I> _queued = new();
    private float _tickTimer;
    private const float TickInterval = 0.12f;
    private const int MaxUpdatesPerTick = 220;
    private static readonly Vector3I[] SideOffsets =
    {
        new Vector3I(1, 0, 0),
        new Vector3I(-1, 0, 0),
        new Vector3I(0, 0, 1),
        new Vector3I(0, 0, -1)
    };

    public LiquidManager()
    {
    }

    public void SetWorld(ChunkManager world)
    {
        _world = world;
    }

    public void Enqueue(Vector3I position)
    {
        if (_queued.Add(position))
            _queue.Enqueue(position);
    }

    public void ProcessLiquidTick(double delta)
    {
        _tickTimer += (float)delta;
        if (_tickTimer < TickInterval) return;
        _tickTimer = 0f;

        int processed = 0;
        while (_queue.Count > 0 && processed++ < MaxUpdatesPerTick)
        {
            Vector3I pos = _queue.Dequeue();
            _queued.Remove(pos);
            UpdateLiquid(pos);
        }
    }

    private void UpdateLiquid(Vector3I pos)
    {
        if (_world == null) return;

        BlockState state = _world.GetBlockAtWorld(pos);
        if (state.IsAir()) return;

        BlockResource resource = BlockRegistry.Instance.GetBlock(state.BlockId);
        if (resource == null || !resource.IsLiquid) return;

        string liquidId = resource.LiquidId;
        int level = state.LiquidLevel <= 0 ? 16 : Mathf.Clamp(state.LiquidLevel, 1, 16);

        // Downward flow always wins. A falling cell fills the entire target cell.
        Vector3I belowPos = pos + new Vector3I(0, -1, 0);
        BlockState below = _world.GetBlockAtWorld(belowPos);
        if (CanFlowInto(below, liquidId, 16))
        {
            WriteLiquid(belowPos, liquidId, 16, true, false);
            Enqueue(belowPos);
            Enqueue(pos);
            return;
        }

        // Once downward flow is blocked, spread horizontally in all four directions.
        // Every horizontal step loses one sixteenth of its height.
        int spreadLevel = level - 1;
        if (spreadLevel <= 0) return;

        foreach (Vector3I offset in SideOffsets)
        {
            Vector3I targetPos = pos + offset;
            BlockState target = _world.GetBlockAtWorld(targetPos);

            if (!CanFlowInto(target, liquidId, spreadLevel))
                continue;

            WriteLiquid(targetPos, liquidId, spreadLevel, false, false);
            Enqueue(targetPos);
        }
    }

    private bool CanFlowInto(BlockState target, string liquidId, int incomingLevel)
    {
        if (target.IsAir()) return true;

        BlockResource targetResource = BlockRegistry.Instance.GetBlock(target.BlockId);
        if (targetResource == null || !targetResource.IsLiquid)
            return false;

        if (targetResource.LiquidId != liquidId)
            return false;

        int currentLevel = target.LiquidLevel <= 0 ? 16 : target.LiquidLevel;
        return currentLevel < incomingLevel && !target.LiquidSource;
    }

    private void WriteLiquid(Vector3I pos, string liquidId, int level, bool falling, bool source)
    {
        string blockId = liquidId == "water" ? "water" : liquidId;
        var next = new BlockState
        {
            BlockId = blockId,
            BitMask = 0xFF,
            LiquidLevel = (byte)Mathf.Clamp(level, 1, 16),
            LiquidFalling = falling,
            LiquidSource = source
        };

        _world.SetBlockAtWorld(pos, next);
        Enqueue(pos);
    }
}
