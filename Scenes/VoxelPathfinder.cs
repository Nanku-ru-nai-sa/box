using Godot;
using System;
using System.Collections.Generic;

// Block-grid A* pathfinder for mobs. Works directly on your world's block data
// instead of a baked NavigationMesh, so it doesn't need re-baking every time
// a block is placed or mined.
//
// SETUP (do this once, e.g. in your world/game root's _Ready(), or wherever
// you already hold a reference to the ChunkManager node — it has no static
// Instance, so grab it however your other scripts normally do):
//
//     var chunkManager = GetNode<ChunkManager>("path/to/ChunkManager");
//     VoxelPathfinder.IsSolidBlock = pos =>
//     {
//         var block = chunkManager.GetBlockAtWorld(pos);
//         // Water isn't "air" (IsAir() is false for it) but mobs can't
//         // currently swim, so it's treated as passable/non-solid rather
//         // than as ground — they'll wade through shallow water rather
//         // than being blocked by it, though they won't try to path
//         // across deep water on purpose either since there's no floor.
//         return !block.IsAir() && block.BlockId != "water";
//     };
public static class VoxelPathfinder
{
    public static Func<Vector3I, bool> IsSolidBlock = _ => false;

    private const int MaxNodesToSearch = 4000;
    private const int MaxFallCheck = 3; // how many blocks a mob is willing to drop in one step

    private class PathNode
    {
        public Vector3I Position;
        public PathNode Parent;
        public float GCost;
        public float HCost;
        public float FCost => GCost + HCost;
    }

    // Returns a list of world-space waypoints (block centers) from start to end,
    // or null if no path was found within maxRange blocks.
    public static List<Vector3> FindPath(Vector3 startWorld, Vector3 endWorld, int maxRange = 24)
    {
        if (IsSolidBlock == null)
        {
            GD.PushWarning("VoxelPathfinder.IsSolidBlock was never set — see setup comment at top of VoxelPathfinder.cs");
            return null;
        }

        Vector3I start = DropToGround(WorldToBlock(startWorld));
        Vector3I end = DropToGround(WorldToBlock(endWorld));

        if (start.DistanceSquaredTo(end) > (float)maxRange * maxRange)
            return null;

        var open = new List<PathNode>();
        var closed = new HashSet<Vector3I>();
        var nodeLookup = new Dictionary<Vector3I, PathNode>();

        var startNode = new PathNode { Position = start, GCost = 0f, HCost = Heuristic(start, end) };
        open.Add(startNode);
        nodeLookup[start] = startNode;

        int searched = 0;

        while (open.Count > 0 && searched < MaxNodesToSearch)
        {
            searched++;
            open.Sort((a, b) => a.FCost.CompareTo(b.FCost));
            PathNode current = open[0];
            open.RemoveAt(0);

            if (current.Position == end)
                return BuildPath(current);

            closed.Add(current.Position);

            foreach (Vector3I neighborPos in GetWalkableNeighbors(current.Position))
            {
                if (closed.Contains(neighborPos)) continue;

                float tentativeG = current.GCost + current.Position.DistanceTo(neighborPos);

                if (!nodeLookup.TryGetValue(neighborPos, out PathNode neighborNode))
                {
                    neighborNode = new PathNode { Position = neighborPos };
                    nodeLookup[neighborPos] = neighborNode;
                    open.Add(neighborNode);
                }
                else if (tentativeG >= neighborNode.GCost)
                {
                    continue; // not a better path to this node
                }

                neighborNode.Parent = current;
                neighborNode.GCost = tentativeG;
                neighborNode.HCost = Heuristic(neighborPos, end);
            }
        }

        return null; // ran out of search budget or range without reaching the target
    }

    private static IEnumerable<Vector3I> GetWalkableNeighbors(Vector3I pos)
    {
        Vector3I[] flatDirs =
        {
            new Vector3I(1, 0, 0), new Vector3I(-1, 0, 0),
            new Vector3I(0, 0, 1), new Vector3I(0, 0, -1),
            new Vector3I(1, 0, 1), new Vector3I(1, 0, -1),
            new Vector3I(-1, 0, 1), new Vector3I(-1, 0, -1),
        };

        foreach (var dir in flatDirs)
        {
            Vector3I flat = pos + dir;

            // same-level step
            if (IsStandable(flat))
            {
                yield return flat;
                continue;
            }

            // step up one block (only if there's headroom above the current tile to climb into)
            Vector3I up = flat + Vector3I.Up;
            if (IsStandable(up) && !IsSolidBlock(pos + Vector3I.Up))
            {
                yield return up;
                continue;
            }

            // step/fall down, up to MaxFallCheck blocks
            for (int fall = 1; fall <= MaxFallCheck; fall++)
            {
                Vector3I down = flat + Vector3I.Down * fall;
                if (IsStandable(down))
                {
                    yield return down;
                    break;
                }
                if (IsSolidBlock(down)) break; // hit solid ground too early / wall in the way
            }
        }
    }

    // "Standable" = solid floor beneath, and 2 blocks of clear space (feet + head) at this position.
    private static bool IsStandable(Vector3I feetPos)
    {
        bool floorSolid = IsSolidBlock(feetPos + Vector3I.Down);
        bool feetClear = !IsSolidBlock(feetPos);
        bool headClear = !IsSolidBlock(feetPos + Vector3I.Up);
        return floorSolid && feetClear && headClear;
    }

    private static Vector3I DropToGround(Vector3I pos)
    {
        for (int i = 0; i < 8; i++)
        {
            if (IsStandable(pos)) return pos;
            if (!IsSolidBlock(pos + Vector3I.Down))
                pos += Vector3I.Down;
            else
                break;
        }
        return pos;
    }

    private static float Heuristic(Vector3I a, Vector3I b) => a.DistanceTo(b);

    private static Vector3I WorldToBlock(Vector3 world) =>
        new Vector3I(Mathf.FloorToInt(world.X), Mathf.FloorToInt(world.Y), Mathf.FloorToInt(world.Z));

    private static List<Vector3> BuildPath(PathNode endNode)
    {
        var result = new List<Vector3>();
        PathNode node = endNode;
        while (node != null)
        {
            result.Insert(0, new Vector3(node.Position.X + 0.5f, node.Position.Y, node.Position.Z + 0.5f));
            node = node.Parent;
        }
        return result;
    }
}