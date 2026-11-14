using Godot;
using System.Collections.Generic;

/// <summary>
/// Shared finite-liquid simulation.
/// Ordinary source blocks create finite flowing liquid. A liquid resource marked
/// IsLiquidSource (such as water_spring) is a permanent source.
/// Flowing cells are recalculated from their neighbors so they drain away when
/// their original source is removed.
/// </summary>
public partial class LiquidManager : Node
{
    private ChunkManager _world;
    private readonly Queue<Vector3I> _queue = new();
    private readonly HashSet<Vector3I> _queued = new();
    private float _tickTimer;

    private const float TickInterval = 0.12f;
    private const int MaxUpdatesPerTick = 220;
    private const int HorizontalFlowStrength = 8; // Source plus seven horizontal blocks, like Minecraft.

    private static readonly Vector3I[] SideOffsets =
    {
        new Vector3I(1, 0, 0),
        new Vector3I(-1, 0, 0),
        new Vector3I(0, 0, 1),
        new Vector3I(0, 0, -1)
    };

    public LiquidManager() { }

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
        if (_world == null || !_world.IsWorldPositionLoaded(pos))
            return;

        BlockState state = _world.GetBlockAtWorld(pos);
        if (state.IsAir())
            return;

        BlockResource resource = BlockRegistry.Instance.GetBlock(state.BlockId);
        if (resource == null || !resource.IsLiquid)
            return;

        string liquidId = resource.LiquidId;
        bool permanentSource = resource.IsLiquidSource;
        bool ordinarySource = state.LiquidSource;
        bool isSource = permanentSource || ordinarySource;

        int level = isSource
            ? 16
            : Mathf.Clamp(state.LiquidLevel <= 0 ? 16 : state.LiquidLevel, 1, 16);

        bool falling = state.LiquidFalling;

        // Flowing cells must be supported by a source or another stronger
        // flowing cell. This is what makes a finite flow disappear after its
        // original source is removed, instead of sustaining itself forever.
        if (!isSource)
        {
            int supportedLevel = 0;
            bool supportedByAbove = false;

            Vector3I abovePos = pos + new Vector3I(0, 1, 0);
            if (_world.IsWorldPositionLoaded(abovePos))
            {
                BlockState above = _world.GetBlockAtWorld(abovePos);
                if (IsSameLiquid(above, liquidId))
                {
                    supportedLevel = 16;
                    supportedByAbove = true;
                }
            }

            foreach (Vector3I offset in SideOffsets)
            {
                Vector3I neighborPos = pos + offset;
                if (!_world.IsWorldPositionLoaded(neighborPos))
                    continue;

                BlockState neighbor = _world.GetBlockAtWorld(neighborPos);
                if (!IsSameLiquid(neighbor, liquidId))
                    continue;

                BlockResource neighborResource =
                    BlockRegistry.Instance.GetBlock(neighbor.BlockId);

                // Sources and falling columns provide the normal seven-block
                // horizontal range; their full vertical level must not turn
                // into a fifteen-block sideways flow.
                bool neighborIsSource =
                    neighbor.LiquidSource ||
                    (neighborResource != null && neighborResource.IsLiquidSource);

                int neighborLevel = neighborIsSource || neighbor.LiquidFalling
                    ? HorizontalFlowStrength
                    : (neighbor.LiquidLevel <= 0
                        ? HorizontalFlowStrength
                        : Mathf.Clamp(neighbor.LiquidLevel, 1, HorizontalFlowStrength));

                if (neighborLevel > 1)
                    supportedLevel = Mathf.Max(supportedLevel, neighborLevel - 1);
            }

            if (supportedLevel <= 0)
            {
                _world.SetBlockAtWorld(pos, BlockState.Air);
                return;
            }

            level = supportedLevel;
            falling = supportedByAbove;

            if (state.LiquidLevel != level || state.LiquidFalling != falling)
            {
                state.LiquidLevel = (byte)level;
                state.LiquidFalling = falling;
                _world.SetBlockAtWorld(pos, state);
            }
        }

        // Falling takes priority: fill the block below before spreading sideways.
        Vector3I belowPos = pos + new Vector3I(0, -1, 0);
        if (!_world.IsWorldPositionLoaded(belowPos))
            return;

        BlockState below = _world.GetBlockAtWorld(belowPos);
        if (CanFlowInto(below, liquidId, 16))
        {
            WriteLiquid(belowPos, liquidId, 16, true, false);
            return;
        }

        // Once the block below is full or blocked, spread horizontally.
        // A falling column spreads sideways with the same range as a source.
        // Horizontal flow has seven steps instead of tiny 1/16-block height changes.
        int spreadLevel = isSource || falling
            ? HorizontalFlowStrength - 1
            : level - 1;
        if (spreadLevel <= 0)
            return;

        // Prefer directions that reach a drop, like Minecraft. Search up to
        // four blocks away from each possible direction; if no nearby drop
        // exists, water spreads evenly across every available side.
        List<Vector3I> flowDirections =
            GetBestFlowDirections(pos, liquidId, spreadLevel);

        foreach (Vector3I offset in flowDirections)
        {
            Vector3I targetPos = pos + offset;
            if (!_world.IsWorldPositionLoaded(targetPos))
                continue;

            BlockState target = _world.GetBlockAtWorld(targetPos);
            if (CanFlowInto(target, liquidId, spreadLevel))
                WriteLiquid(targetPos, liquidId, spreadLevel, false, false);
        }
    }

    private List<Vector3I> GetBestFlowDirections(
        Vector3I origin,
        string liquidId,
        int spreadLevel)
    {
        var available = new List<Vector3I>();
        int bestDistance = int.MaxValue;
        var best = new List<Vector3I>();

        foreach (Vector3I offset in SideOffsets)
        {
            Vector3I candidate = origin + offset;
            if (!_world.IsWorldPositionLoaded(candidate))
                continue;

            BlockState target = _world.GetBlockAtWorld(candidate);
            if (!CanFlowInto(target, liquidId, spreadLevel))
                continue;

            available.Add(offset);

            int dropDistance = FindDropDistance(candidate, liquidId, spreadLevel);
            if (dropDistance < 0)
                continue;

            if (dropDistance < bestDistance)
            {
                bestDistance = dropDistance;
                best.Clear();
                best.Add(offset);
            }
            else if (dropDistance == bestDistance)
            {
                best.Add(offset);
            }
        }

        // No drop found nearby: spread in all valid directions.
        return best.Count > 0 ? best : available;
    }

    private int FindDropDistance(
        Vector3I start,
        string liquidId,
        int spreadLevel)
    {
        var positions = new Queue<(Vector3I position, int distance)>();
        var visited = new HashSet<Vector3I>();

        positions.Enqueue((start, 0));
        visited.Add(start);

        while (positions.Count > 0)
        {
            var current = positions.Dequeue();
            Vector3I belowPos = current.position + new Vector3I(0, -1, 0);

            if (_world.IsWorldPositionLoaded(belowPos))
            {
                BlockState below = _world.GetBlockAtWorld(belowPos);
                if (CanFlowInto(below, liquidId, 16))
                    return current.distance;
            }

            if (current.distance >= 4)
                continue;

            foreach (Vector3I offset in SideOffsets)
            {
                Vector3I next = current.position + offset;
                if (!visited.Add(next) || !_world.IsWorldPositionLoaded(next))
                    continue;

                BlockState nextState = _world.GetBlockAtWorld(next);
                if (nextState.IsAir())
                {
                    positions.Enqueue((next, current.distance + 1));
                    continue;
                }

                if (!IsSameLiquid(nextState, liquidId))
                    continue;

                BlockResource nextResource =
                    BlockRegistry.Instance.GetBlock(nextState.BlockId);

                // Do not route through another source block or a blocked cell.
                if (nextState.LiquidSource ||
                    (nextResource != null && nextResource.IsLiquidSource))
                    continue;

                positions.Enqueue((next, current.distance + 1));
            }
        }

        return -1;
    }

    public Vector3 GetFlowVector(Vector3I pos)
    {
        if (_world == null || !_world.IsWorldPositionLoaded(pos))
            return Vector3.Zero;

        BlockState state = _world.GetBlockAtWorld(pos);
        if (state.IsAir())
            return Vector3.Zero;

        BlockResource resource = BlockRegistry.Instance.GetBlock(state.BlockId);
        if (resource == null || !resource.IsLiquid)
            return Vector3.Zero;

        if (state.LiquidFalling)
            return Vector3.Down;

        Vector3I belowPos = pos + new Vector3I(0, -1, 0);
        if (_world.IsWorldPositionLoaded(belowPos))
        {
            BlockState below = _world.GetBlockAtWorld(belowPos);
            if (CanFlowInto(below, resource.LiquidId, 16))
                return Vector3.Down;
        }

        int ownLevel = state.LiquidSource || resource.IsLiquidSource
            ? HorizontalFlowStrength
            : Mathf.Clamp(state.LiquidLevel <= 0 ? HorizontalFlowStrength : state.LiquidLevel,
                1, HorizontalFlowStrength);

        Vector3 direction = Vector3.Zero;

        foreach (Vector3I offset in SideOffsets)
        {
            Vector3I neighborPos = pos + offset;
            if (!_world.IsWorldPositionLoaded(neighborPos))
                continue;

            BlockState neighbor = _world.GetBlockAtWorld(neighborPos);
            if (!IsSameLiquid(neighbor, resource.LiquidId))
                continue;

            BlockResource neighborResource =
                BlockRegistry.Instance.GetBlock(neighbor.BlockId);

            int neighborLevel = neighbor.LiquidSource ||
                (neighborResource != null && neighborResource.IsLiquidSource)
                    ? HorizontalFlowStrength
                    : Mathf.Clamp(neighbor.LiquidLevel <= 0
                        ? HorizontalFlowStrength
                        : neighbor.LiquidLevel, 1, HorizontalFlowStrength);

            // Combine incoming and outgoing flow. This lets currents turn
            // naturally at bends and split toward multiple lower channels.
            if (ownLevel > neighborLevel)
                direction += new Vector3(offset.X, 0f, offset.Z);
            else if (neighborLevel > ownLevel)
                direction -= new Vector3(offset.X, 0f, offset.Z);
        }

        return direction.LengthSquared() > 0.001f
            ? direction.Normalized()
            : Vector3.Zero;
    }

    private bool IsSameLiquid(BlockState state, string liquidId)
    {
        if (state.IsAir()) return false;

        BlockResource other = BlockRegistry.Instance.GetBlock(state.BlockId);
        return other != null && other.IsLiquid && other.LiquidId == liquidId;
    }

    private bool CanFlowInto(BlockState target, string liquidId, int incomingLevel)
    {
        if (target.IsAir())
            return true;

        BlockResource targetResource = BlockRegistry.Instance.GetBlock(target.BlockId);
        if (targetResource == null || !targetResource.IsLiquid)
            return false;

        if (targetResource.LiquidId != liquidId)
            return false;

        // Never overwrite either kind of source.
        if (targetResource.IsLiquidSource || target.LiquidSource)
            return false;

        int currentLevel = target.LiquidLevel <= 0
            ? 16
            : Mathf.Clamp(target.LiquidLevel, 1, 16);

        return currentLevel < incomingLevel;
    }

    private void WriteLiquid(Vector3I pos, string liquidId, int level, bool falling, bool source)
    {
        // The liquid ID is the shared family key. Water's flowing block is
        // "water"; future liquid families can use their own registered IDs.
        string blockId = liquidId;

        var next = new BlockState
        {
            BlockId = blockId,
            BitMask = 0xFF,
            LiquidLevel = (byte)Mathf.Clamp(level, 1, 16),
            LiquidFalling = falling,
            LiquidSource = source
        };

        _world.SetBlockAtWorld(pos, next);
    }
}
