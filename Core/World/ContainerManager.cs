
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// ============================================================
// CONTAINER MANAGER
// Handles crate storage, connected crates, and world saving.
// Each crate has 4 storage slots (a 2x2 grid).
// ============================================================

public partial class ContainerManager : Node
{
    public static ContainerManager Instance { get; private set; }

    public const string CrateBlockId = "crate";
    public const int SlotsPerCrate = 4;

    private readonly Dictionary<Vector3I, List<InventorySlot>> _containers = new();

    private static readonly Vector3I[] Directions =
    {
        new Vector3I( 1,  0,  0),
        new Vector3I(-1,  0,  0),
        new Vector3I( 0,  1,  0),
        new Vector3I( 0, -1,  0),
        new Vector3I( 0,  0,  1),
        new Vector3I( 0,  0, -1)
    };

    private ChunkManager _chunkManager;
    private bool _loaded;

    public override void _EnterTree()
    {
        if (Instance != null && Instance != this)
        {
            GD.PrintErr("[ContainerManager] Duplicate manager detected.");
            QueueFree();
            return;
        }

        Instance = this;
    }

    public override void _Ready()
    {
        _chunkManager = GetTree().Root.FindChild(
            "ChunkManager", true, false
        ) as ChunkManager;

        // The active world may not have been selected yet.
        // Call LoadForActiveWorld after the world is ready.
        if (SaveManager.Instance != null &&
            !string.IsNullOrEmpty(SaveManager.Instance.ActiveWorldId))
        {
            LoadForActiveWorld();
        }
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    // --------------------------------------------------------
    // WORLD SETUP
    // --------------------------------------------------------

    public void LoadForActiveWorld()
    {
        _containers.Clear();
        _loaded = true;

        if (SaveManager.Instance == null)
        {
            GD.PrintErr("[ContainerManager] SaveManager is missing.");
            return;
        }

        List<SavedContainerData> saved =
            SaveManager.Instance.LoadWorldContainers();

        foreach (SavedContainerData data in saved)
        {
            if (data == null)
                continue;

            Vector3I position = new Vector3I(data.X, data.Y, data.Z);

            var slots = new List<InventorySlot>();

            if (data.Slots != null)
            {
                foreach (SavedContainerSlotData savedSlot in data.Slots)
                {
                    if (savedSlot == null)
                    {
                        slots.Add(new InventorySlot());
                        continue;
                    }

                    slots.Add(new InventorySlot
                    {
                        ItemId = savedSlot.ItemId ?? "",
                        Count = savedSlot.Count,
                        CurrentDurability = savedSlot.CurrentDurability,
                        CustomName = savedSlot.CustomName ?? ""
                    });
                }
            }

            while (slots.Count < SlotsPerCrate)
                slots.Add(new InventorySlot());

            // Preserve existing records if a malformed save contains
            // more than one record for the same position.
            _containers[position] = slots;
        }

        GD.Print($"[ContainerManager] Loaded {_containers.Count} crate records.");
    }

    // --------------------------------------------------------
    // CRATE REGISTRATION
    // Call this when a crate is placed or opened.
    // --------------------------------------------------------

    public bool RegisterCrate(Vector3I worldPosition)
    {
        if (!IsCrateBlock(worldPosition))
            return false;

        EnsureContainerRecord(worldPosition);
        return true;
    }

    private void EnsureContainerRecord(Vector3I position)
    {
        if (_containers.ContainsKey(position))
            return;

        var slots = new List<InventorySlot>();

        for (int i = 0; i < SlotsPerCrate; i++)
            slots.Add(new InventorySlot());

        _containers[position] = slots;
    }

    private bool IsCrateBlock(Vector3I position)
    {
        if (_chunkManager == null)
        {
            _chunkManager = GetTree().Root.FindChild(
                "ChunkManager", true, false
            ) as ChunkManager;
        }

        if (_chunkManager == null)
            return false;

        BlockState block = _chunkManager.GetBlockAtWorld(position);
        return block.BlockId == CrateBlockId && !block.IsAir();
    }

    // --------------------------------------------------------
    // CONNECTED CRATES
    // Crates connect through faces, including up and down.
    // Diagonal crates do not connect.
    // --------------------------------------------------------

    public List<Vector3I> GetConnectedCratePositions(Vector3I startPosition)
    {
        var result = new List<Vector3I>();
        var visited = new HashSet<Vector3I>();
        var queue = new Queue<Vector3I>();

        if (!IsCrateBlock(startPosition))
            return result;

        queue.Enqueue(startPosition);
        visited.Add(startPosition);

        while (queue.Count > 0)
        {
            Vector3I current = queue.Dequeue();

            if (!IsCrateBlock(current))
                continue;

            EnsureContainerRecord(current);
            result.Add(current);

            foreach (Vector3I direction in Directions)
            {
                Vector3I neighbor = current + direction;

                if (visited.Contains(neighbor))
                    continue;

                if (!IsCrateBlock(neighbor))
                    continue;

                visited.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }

        return result;
    }

    // Returns the combined slots for a connected crate group.
    // Slot order follows the connected crate positions returned above.
    // The returned InventorySlot objects are the live storage slots.
    public List<InventorySlot> GetConnectedSlots(Vector3I startPosition)
    {
        var combined = new List<InventorySlot>();

        foreach (Vector3I position in GetConnectedCratePositions(startPosition))
        {
            if (_containers.TryGetValue(position, out List<InventorySlot> slots))
                combined.AddRange(slots);
        }

        return combined;
    }

    public int GetConnectedSlotCount(Vector3I startPosition)
    {
        return GetConnectedSlots(startPosition).Count;
    }

    // --------------------------------------------------------
    // STORAGE ACCESS
    // --------------------------------------------------------

    public List<InventorySlot> GetCrateSlots(Vector3I worldPosition)
    {
        if (!RegisterCrate(worldPosition))
            return new List<InventorySlot>();

        return _containers[worldPosition];
    }

    public bool IsContainerRegistered(Vector3I worldPosition)
    {
        return _containers.ContainsKey(worldPosition);
    }

    // --------------------------------------------------------
    // BREAKING A CRATE
    // Call this before removing the crate block.
    // Returns the items that were inside so the caller can drop them.
    // The crate item itself should be dropped by the caller.
    // --------------------------------------------------------

    public List<InventorySlot> RemoveCrate(Vector3I worldPosition)
    {
        var contents = new List<InventorySlot>();

        if (_containers.TryGetValue(worldPosition, out List<InventorySlot> slots))
        {
            foreach (InventorySlot slot in slots)
            {
                if (slot == null || slot.IsEmpty)
                    continue;

                contents.Add(new InventorySlot
                {
                    ItemId = slot.ItemId,
                    Count = slot.Count,
                    CurrentDurability = slot.CurrentDurability,
                    CustomName = slot.CustomName
                });
            }
        }

        _containers.Remove(worldPosition);
        SaveContainers();

        return contents;
    }

    // --------------------------------------------------------
    // SAVING
    // Uses the SaveManager methods already in your project.
    // --------------------------------------------------------

    public void SaveContainers()
    {
        if (!_loaded)
            return;

        if (SaveManager.Instance == null)
        {
            GD.PrintErr("[ContainerManager] Cannot save: SaveManager is missing.");
            return;
        }

        var saved = new List<SavedContainerData>();

        foreach (var entry in _containers)
        {
            var data = new SavedContainerData
            {
                X = entry.Key.X,
                Y = entry.Key.Y,
                Z = entry.Key.Z,
                Slots = new List<SavedContainerSlotData>()
            };

            foreach (InventorySlot slot in entry.Value)
            {
                data.Slots.Add(new SavedContainerSlotData
                {
                    ItemId = slot?.ItemId ?? "",
                    Count = slot?.Count ?? 0,
                    CurrentDurability = slot?.CurrentDurability ?? 0,
                    CustomName = slot?.CustomName ?? ""
                });
            }

            saved.Add(data);
        }

        SaveManager.Instance.SaveWorldContainers(saved);
    }
}
