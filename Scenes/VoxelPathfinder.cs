using Godot;
using System;
using System.Collections.Generic;

// ============================================================
// VOXEL PATHFINDER
// ============================================================
//
// Uses the mob's automatically generated collision dimensions.
//
// The Mob supplies:
//   - collision width
//   - collision depth
//   - collision height
//
// The pathfinder then checks whether the mob's footprint can
// actually occupy each voxel position.
//
// ============================================================

public static class VoxelPathfinder
{
    public static Func<Vector3I, bool> IsSolidBlock = _ => false;

    private const int MaxNodesToSearch = 4000;
    private const int MaxFallCheck = 3;

    private const float Clearance = 0.04f;

    private class PathNode
    {
        public Vector3I Position;
        public PathNode Parent;
        public float GCost;
        public float HCost;
        public float FCost => GCost + HCost;
    }

    // =========================================================
    // FIND PATH
    // =========================================================

    public static List<Vector3> FindPath(
        Vector3 startWorld,
        Vector3 endWorld,
        int maxRange = 24,
        float mobWidth = 1f,
        float mobDepth = 1f,
        float mobHeight = 2f)
    {
        if (IsSolidBlock == null)
        {
            GD.PushWarning(
                "VoxelPathfinder.IsSolidBlock was never set."
            );

            return null;
        }

        mobWidth =
            Mathf.Max(mobWidth, 0.1f);

        mobDepth =
            Mathf.Max(mobDepth, 0.1f);

        mobHeight =
            Mathf.Max(mobHeight, 0.1f);

        Vector3I start =
            DropToGround(
                WorldToBlock(startWorld),
                mobWidth,
                mobDepth,
                mobHeight
            );

        Vector3I end =
            DropToGround(
                WorldToBlock(endWorld),
                mobWidth,
                mobDepth,
                mobHeight
            );

        if (
            start.DistanceSquaredTo(end) >
            (float)maxRange * maxRange)
        {
            return null;
        }

        var open =
            new List<PathNode>();

        var closed =
            new HashSet<Vector3I>();

        var nodeLookup =
            new Dictionary<Vector3I, PathNode>();

        var startNode =
            new PathNode
            {
                Position = start,
                GCost = 0f,
                HCost = Heuristic(start, end)
            };

        open.Add(startNode);
        nodeLookup[start] = startNode;

        int searched = 0;

        while (
            open.Count > 0 &&
            searched < MaxNodesToSearch)
        {
            searched++;

            open.Sort(
                (a, b) =>
                    a.FCost.CompareTo(b.FCost)
            );

            PathNode current =
                open[0];

            open.RemoveAt(0);

            if (current.Position == end)
            {
                return BuildPath(current);
            }

            closed.Add(current.Position);

            foreach (
                Vector3I neighbor
                in GetWalkableNeighbors(
                    current.Position,
                    mobWidth,
                    mobDepth,
                    mobHeight))
            {
                if (closed.Contains(neighbor))
                    continue;

                float tentativeG =
                    current.GCost +
                    current.Position.DistanceTo(
                        neighbor
                    );

                if (
                    !nodeLookup.TryGetValue(
                        neighbor,
                        out PathNode neighborNode))
                {
                    neighborNode =
                        new PathNode
                        {
                            Position = neighbor
                        };

                    nodeLookup[neighbor] =
                        neighborNode;

                    open.Add(neighborNode);
                }
                else if (
                    tentativeG >=
                    neighborNode.GCost)
                {
                    continue;
                }

                neighborNode.Parent =
                    current;

                neighborNode.GCost =
                    tentativeG;

                neighborNode.HCost =
                    Heuristic(
                        neighbor,
                        end
                    );
            }
        }

        return null;
    }

    // =========================================================
    // NEIGHBORS
    // =========================================================

    private static IEnumerable<Vector3I>
        GetWalkableNeighbors(
            Vector3I pos,
            float mobWidth,
            float mobDepth,
            float mobHeight)
    {
        Vector3I[] dirs =
        {
            new Vector3I(1, 0, 0),
            new Vector3I(-1, 0, 0),

            new Vector3I(0, 0, 1),
            new Vector3I(0, 0, -1),

            new Vector3I(1, 0, 1),
            new Vector3I(1, 0, -1),

            new Vector3I(-1, 0, 1),
            new Vector3I(-1, 0, -1)
        };

        foreach (Vector3I dir in dirs)
        {
            Vector3I flat =
                pos + dir;

            // -------------------------------------------------
            // SAME LEVEL
            // -------------------------------------------------

            if (
                IsStandable(
                    flat,
                    mobWidth,
                    mobDepth,
                    mobHeight))
            {
                yield return flat;
                continue;
            }

            // -------------------------------------------------
            // ONE BLOCK UP
            // -------------------------------------------------

            Vector3I up =
                flat + Vector3I.Up;

            if (
                IsStandable(
                    up,
                    mobWidth,
                    mobDepth,
                    mobHeight) &&
                CanStepUp(
                    pos,
                    up,
                    mobWidth,
                    mobDepth,
                    mobHeight))
            {
                yield return up;
                continue;
            }

            // -------------------------------------------------
            // FALL DOWN
            // -------------------------------------------------

            for (
                int fall = 1;
                fall <= MaxFallCheck;
                fall++)
            {
                Vector3I down =
                    flat +
                    Vector3I.Down * fall;

                if (
                    IsStandable(
                        down,
                        mobWidth,
                        mobDepth,
                        mobHeight))
                {
                    yield return down;
                    break;
                }

                if (IsSolidBlock(down))
                    break;
            }
        }
    }

    // =========================================================
    // STANDABLE
    // =========================================================

    private static bool IsStandable(
        Vector3I feetPos,
        float mobWidth,
        float mobDepth,
        float mobHeight)
    {
        if (!HasSolidFloor(
                feetPos,
                mobWidth,
                mobDepth))
        {
            return false;
        }

        if (!HasClearance(
                feetPos,
                mobWidth,
                mobDepth,
                mobHeight))
        {
            return false;
        }

        return true;
    }

    // =========================================================
    // FLOOR
    // =========================================================

    private static bool HasSolidFloor(
        Vector3I feetPos,
        float mobWidth,
        float mobDepth)
    {
        GetFootprintRange(
            mobWidth,
            out int minX,
            out int maxX
        );

        GetFootprintRange(
            mobDepth,
            out int minZ,
            out int maxZ
        );

        for (
            int x = minX;
            x <= maxX;
            x++)
        {
            for (
                int z = minZ;
                z <= maxZ;
                z++)
            {
                Vector3I floor =
                    new Vector3I(
                        feetPos.X + x,
                        feetPos.Y - 1,
                        feetPos.Z + z
                    );

                if (!IsSolidBlock(floor))
                    return false;
            }
        }

        return true;
    }

    // =========================================================
    // CLEARANCE
    // =========================================================

    private static bool HasClearance(
        Vector3I feetPos,
        float mobWidth,
        float mobDepth,
        float mobHeight)
    {
        GetFootprintRange(
            mobWidth,
            out int minX,
            out int maxX
        );

        GetFootprintRange(
            mobDepth,
            out int minZ,
            out int maxZ
        );

        int heightBlocks =
            Mathf.Max(
                1,
                Mathf.CeilToInt(
                    mobHeight - Clearance
                )
            );

        for (
            int y = 0;
            y < heightBlocks;
            y++)
        {
            for (
                int x = minX;
                x <= maxX;
                x++)
            {
                for (
                    int z = minZ;
                    z <= maxZ;
                    z++)
                {
                    Vector3I block =
                        new Vector3I(
                            feetPos.X + x,
                            feetPos.Y + y,
                            feetPos.Z + z
                        );

                    if (IsSolidBlock(block))
                        return false;
                }
            }
        }

        return true;
    }

    // =========================================================
    // STEP UP
    // =========================================================

    private static bool CanStepUp(
        Vector3I from,
        Vector3I to,
        float mobWidth,
        float mobDepth,
        float mobHeight)
    {
        Vector3I direction =
            to - from;

        direction.Y = 0;

        GetFootprintRange(
            mobWidth,
            out int minX,
            out int maxX
        );

        GetFootprintRange(
            mobDepth,
            out int minZ,
            out int maxZ
        );

        int heightBlocks =
            Mathf.Max(
                1,
                Mathf.CeilToInt(
                    mobHeight - Clearance
                )
            );

        // Check the destination volume.
        for (
            int y = 1;
            y <= heightBlocks;
            y++)
        {
            for (
                int x = minX;
                x <= maxX;
                x++)
            {
                for (
                    int z = minZ;
                    z <= maxZ;
                    z++)
                {
                    Vector3I block =
                        new Vector3I(
                            from.X +
                            direction.X +
                            x,

                            from.Y + y,

                            from.Z +
                            direction.Z +
                            z
                        );

                    if (IsSolidBlock(block))
                        return false;
                }
            }
        }

        return true;
    }

    // =========================================================
    // FOOTPRINT RANGE
    // =========================================================
    //
    // For a 1-block-wide mob:
    //     1 cell
    //
    // For a 2-block-wide mob:
    //     2 cells
    //
    // We intentionally round upward only when the body actually
    // requires another full voxel of clearance.
    //
    // =========================================================

    private static void GetFootprintRange(
        float size,
        out int min,
        out int max)
    {
        if (size <= 1.0f + Clearance)
        {
            min = 0;
            max = 0;
            return;
        }

        int cells =
            Mathf.CeilToInt(
                size - Clearance
            );

        cells =
            Mathf.Max(
                1,
                cells
            );

        int left =
            cells / 2;

        int right =
            cells - left - 1;

        min = -left;
        max = right;
    }

    // =========================================================
    // DROP TO GROUND
    // =========================================================

    private static Vector3I DropToGround(
        Vector3I pos,
        float mobWidth,
        float mobDepth,
        float mobHeight)
    {
        for (int i = 0; i < 8; i++)
        {
            if (
                IsStandable(
                    pos,
                    mobWidth,
                    mobDepth,
                    mobHeight))
            {
                return pos;
            }

            if (
                !IsSolidBlock(
                    pos + Vector3I.Down))
            {
                pos += Vector3I.Down;
            }
            else
            {
                break;
            }
        }

        return pos;
    }

    // =========================================================
    // HEURISTIC
    // =========================================================

    private static float Heuristic(
        Vector3I a,
        Vector3I b)
    {
        return a.DistanceTo(b);
    }

    // =========================================================
    // WORLD → BLOCK
    // =========================================================

    private static Vector3I WorldToBlock(
        Vector3 world)
    {
        return new Vector3I(
            Mathf.FloorToInt(world.X),
            Mathf.FloorToInt(world.Y),
            Mathf.FloorToInt(world.Z)
        );
    }

    // =========================================================
    // BUILD PATH
    // =========================================================

    private static List<Vector3> BuildPath(
        PathNode endNode)
    {
        var result =
            new List<Vector3>();

        PathNode node =
            endNode;

        while (node != null)
        {
            result.Insert(
                0,
                new Vector3(
                    node.Position.X + 0.5f,
                    node.Position.Y,
                    node.Position.Z + 0.5f
                )
            );

            node =
                node.Parent;
        }

        return result;
    }
}