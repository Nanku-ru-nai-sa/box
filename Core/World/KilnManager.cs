using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

// World-owned kiln storage and processing. Processing is independent of UI visibility.
public partial class KilnManager : Node
{
    public static KilnManager Instance { get; private set; }

    private const string SaveFileName = "kilns.json";
    private readonly Dictionary<Vector3I, KilnState> _kilns = new();
    private readonly Dictionary<string, KilnRecipe> _recipes = new(StringComparer.OrdinalIgnoreCase);
    private string _loadedWorldId = "";
    private float _saveTimer;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public class KilnState
    {
        public InventorySlot Input { get; } = new();
        public InventorySlot Fuel { get; } = new();
        public InventorySlot Output { get; } = new();
        public float FuelRemaining;
        public float FuelTotal;
        public float Progress;
        public string ProcessingInputId = "";
    }

    private class KilnRecipe
    {
        public string InputId = "";
        public string OutputId = "";
        public int OutputCount = 1;
        public float Seconds = 10f;
    }

    [Serializable]
    public class SavedSlot
    {
        public string ItemId { get; set; } = "";
        public int Count { get; set; }
    }

    [Serializable]
    public class SavedKiln
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public SavedSlot Input { get; set; } = new();
        public SavedSlot Fuel { get; set; } = new();
        public SavedSlot Output { get; set; } = new();
        public float FuelRemaining { get; set; }
        public float FuelTotal { get; set; }
        public float Progress { get; set; }
        public string ProcessingInputId { get; set; } = "";
    }

    public override void _EnterTree()
    {
        if (Instance != null && Instance != this)
        {
            QueueFree();
            return;
        }
        Instance = this;
    }

    public override void _Ready()
    {
        LoadRecipes();
    }

    public override void _ExitTree()
    {
        SaveKilns();
        if (Instance == this) Instance = null;
    }

    public override void _Process(double delta)
    {
        EnsureWorldLoaded();
        if (string.IsNullOrEmpty(_loadedWorldId)) return;
        if (GetTree().Root.FindChild("ChunkManager", true, false) is not ChunkManager)
            return;

        foreach (KilnState kiln in _kilns.Values)
            ProcessKiln(kiln, (float)delta);

        // Avoid writing a save file every frame while a kiln is running.
        _saveTimer += (float)delta;
        if (_saveTimer >= 2f)
        {
            _saveTimer = 0f;
            SaveKilns();
        }
    }

    private void LoadRecipes()
    {
        _recipes.Clear();
        const string folder = "res://Core/Recipes/Kiln";
        using var dir = DirAccess.Open(folder);
        if (dir == null)
        {
            GD.PrintErr($"[KilnManager] Recipe folder missing: {folder}");
            return;
        }

        dir.ListDirBegin();
        string fileName = dir.GetNext();
        while (fileName != "")
        {
            if (!dir.CurrentIsDir() && fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                string path = $"{folder}/{fileName}";
                using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
                if (file != null)
                {
                    try
                    {
                        var data = Json.ParseString(file.GetAsText()).AsGodotDictionary();
                        var recipe = new KilnRecipe
                        {
                            InputId = data.ContainsKey("input") ? (string)data["input"] : "",
                            OutputId = data.ContainsKey("output") ? (string)data["output"] : "",
                            OutputCount = data.ContainsKey("count") ? Mathf.Max(1, (int)data["count"]) : 1,
                            Seconds = data.ContainsKey("seconds") ? Mathf.Max(0.1f, (float)data["seconds"]) : 10f
                        };
                        if (!string.IsNullOrWhiteSpace(recipe.InputId) &&
                            !string.IsNullOrWhiteSpace(recipe.OutputId))
                            _recipes[recipe.InputId] = recipe;
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"[KilnManager] Invalid recipe {path}: {e.Message}");
                    }
                }
            }
            fileName = dir.GetNext();
        }
        dir.ListDirEnd();
        GD.Print($"[KilnManager] Loaded {_recipes.Count} kiln recipes.");
    }

    private void EnsureWorldLoaded()
    {
        string worldId = SaveManager.Instance?.ActiveWorldId ?? "";
        if (worldId == _loadedWorldId) return;

        _kilns.Clear();
        _loadedWorldId = worldId;
        if (string.IsNullOrEmpty(worldId)) return;

        string path = GetSavePath();
        if (!FileAccess.FileExists(path)) return;

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) return;

        try
        {
            var saved = JsonSerializer.Deserialize<List<SavedKiln>>(file.GetAsText());
            if (saved == null) return;

            foreach (var data in saved)
            {
                var state = new KilnState
                {
                    FuelRemaining = Mathf.Max(0f, data.FuelRemaining),
                    FuelTotal = Mathf.Max(0f, data.FuelTotal),
                    Progress = Mathf.Max(0f, data.Progress),
                    ProcessingInputId = data.ProcessingInputId ?? ""
                };
                CopySavedSlot(data.Input, state.Input);
                CopySavedSlot(data.Fuel, state.Fuel);
                CopySavedSlot(data.Output, state.Output);
                _kilns[new Vector3I(data.X, data.Y, data.Z)] = state;
            }
            GD.Print($"[KilnManager] Loaded {_kilns.Count} kilns.");
        }
        catch (Exception e)
        {
            GD.PrintErr($"[KilnManager] Could not load kiln save: {e.Message}");
        }
    }

    private static void CopySavedSlot(SavedSlot source, InventorySlot target)
    {
        if (source == null || string.IsNullOrEmpty(source.ItemId) || source.Count <= 0)
            return;
        target.ItemId = source.ItemId;
        target.Count = source.Count;
    }

    private string GetSavePath()
    {
        return $"user://saves/worlds/{_loadedWorldId}/{SaveFileName}";
    }

    public KilnState GetKiln(Vector3I position)
    {
        EnsureWorldLoaded();
        if (string.IsNullOrEmpty(_loadedWorldId)) return null;
        if (!_kilns.TryGetValue(position, out KilnState state))
        {
            state = new KilnState();
            _kilns[position] = state;
            SaveKilns();
        }
        return state;
    }

    public List<InventorySlot> GetSlots(Vector3I position)
    {
        KilnState state = GetKiln(position);
        return state == null ? new List<InventorySlot>() : new List<InventorySlot> { state.Input, state.Fuel, state.Output };
    }

    public void NotifyChanged()
    {
        SaveKilns();
    }

    public List<InventorySlot> RemoveKiln(Vector3I position)
    {
        EnsureWorldLoaded();
        var contents = new List<InventorySlot>();
        if (_kilns.TryGetValue(position, out KilnState state))
        {
            contents.Add(CloneSlot(state.Input));
            contents.Add(CloneSlot(state.Fuel));
            contents.Add(CloneSlot(state.Output));
            _kilns.Remove(position);
            SaveKilns();
        }
        return contents;
    }

    private static InventorySlot CloneSlot(InventorySlot source)
    {
        return new InventorySlot { ItemId = source.ItemId, Count = source.Count };
    }

    public void SaveKilns()
    {
        EnsureWorldLoaded();
        if (string.IsNullOrEmpty(_loadedWorldId)) return;

        var saved = new List<SavedKiln>();
        foreach (var pair in _kilns)
        {
            var state = pair.Value;
            saved.Add(new SavedKiln
            {
                X = pair.Key.X, Y = pair.Key.Y, Z = pair.Key.Z,
                Input = new SavedSlot { ItemId = state.Input.ItemId, Count = state.Input.Count },
                Fuel = new SavedSlot { ItemId = state.Fuel.ItemId, Count = state.Fuel.Count },
                Output = new SavedSlot { ItemId = state.Output.ItemId, Count = state.Output.Count },
                FuelRemaining = state.FuelRemaining,
                FuelTotal = state.FuelTotal,
                Progress = state.Progress,
                ProcessingInputId = state.ProcessingInputId
            });
        }

        DirAccess.MakeDirRecursiveAbsolute($"user://saves/worlds/{_loadedWorldId}/");
        using var file = FileAccess.Open(GetSavePath(), FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PrintErr("[KilnManager] Could not write kiln save.");
            return;
        }
        file.StoreString(JsonSerializer.Serialize(saved, JsonOptions));
    }

    private bool ProcessKiln(KilnState kiln, float delta)
    {
        if (kiln.Input.IsEmpty)
        {
            kiln.Progress = 0f;
            kiln.ProcessingInputId = "";
            return false;
        }

        if (!_recipes.TryGetValue(kiln.Input.ItemId, out KilnRecipe recipe))
        {
            kiln.Progress = 0f;
            kiln.ProcessingInputId = "";
            return false;
        }

        if (kiln.ProcessingInputId != kiln.Input.ItemId)
        {
            kiln.Progress = 0f;
            kiln.ProcessingInputId = kiln.Input.ItemId;
        }

        if (!CanAcceptOutput(kiln.Output, recipe))
            return false;

        if (kiln.FuelRemaining <= 0f)
        {
            if (kiln.Fuel.IsEmpty) return false;
            ItemResource fuelItem = ItemRegistry.Instance?.GetItem(kiln.Fuel.ItemId);
            float burnSeconds = fuelItem?.FuelBurnSeconds ?? 0f;
            if (burnSeconds <= 0f) return false;

            kiln.Fuel.Count--;
            if (kiln.Fuel.Count <= 0) kiln.Fuel.Clear();
            kiln.FuelRemaining = burnSeconds;
            kiln.FuelTotal = burnSeconds;
        }

        // Consume fuel only while the kiln can actively make progress.
        float available = Mathf.Min(delta, kiln.FuelRemaining);
        float consumedTime = 0f;

        while (available > 0f && !kiln.Input.IsEmpty && CanAcceptOutput(kiln.Output, recipe))
        {
            float needed = Mathf.Max(0f, recipe.Seconds - kiln.Progress);
            float step = Mathf.Min(available, needed);

            kiln.Progress += step;
            available -= step;
            consumedTime += step;

            if (kiln.Progress >= recipe.Seconds)
            {
                kiln.Input.Count--;
                if (kiln.Input.Count <= 0) kiln.Input.Clear();

                if (kiln.Output.IsEmpty)
                {
                    kiln.Output.ItemId = recipe.OutputId;
                    kiln.Output.Count = recipe.OutputCount;
                }
                else
                {
                    kiln.Output.Count += recipe.OutputCount;
                }

                kiln.Progress = 0f;
            }
            else
            {
                break;
            }
        }

        kiln.FuelRemaining = Mathf.Max(0f, kiln.FuelRemaining - consumedTime);

        if (kiln.Input.IsEmpty)
        {
            kiln.Progress = 0f;
            kiln.ProcessingInputId = "";
        }

        return consumedTime > 0f;
    }

    private static bool CanAcceptOutput(InventorySlot output, KilnRecipe recipe)
    {
        if (output.IsEmpty) return true;
        if (output.ItemId != recipe.OutputId) return false;
        ItemResource item = ItemRegistry.Instance?.GetItem(recipe.OutputId);
        int max = item?.MaxStackSize ?? 64;
        return output.Count + recipe.OutputCount <= max;
    }

    public float GetProgress(Vector3I position)
    {
        KilnState state = GetKiln(position);
        if (state == null || state.Input.IsEmpty || !_recipes.TryGetValue(state.Input.ItemId, out KilnRecipe recipe))
            return 0f;
        return Mathf.Clamp(state.Progress / recipe.Seconds, 0f, 1f);
    }
}
