using Godot;
using System;
using System.Collections.Generic;

// RecipeManager — autoload singleton (add to Project > Autoloads as "RecipeManager")
// Handles recipe definitions, grid matching, and learned recipe persistence.
// Recipes are loaded from individual JSON files in res://Recipes/ — one file per recipe.

public partial class RecipeManager : Node
{
    public static RecipeManager Instance { get; private set; }

    // ── Recipe definition ─────────────────────────────────────────────────────

    public class Recipe
    {
        public string   Id          { get; set; } // unique id e.g. "planks_from_log"
        public string   ResultId    { get; set; } // output item id
        public int      ResultCount { get; set; } = 1;
        public bool     Shaped      { get; set; } = true;

        // For shaped recipes: Pattern[row, col] = item id, or "" for empty.
        public string[,] Pattern    { get; set; }

        // For shapeless recipes: just a list of ingredients
        public List<string> Ingredients { get; set; } = new();
    }

    private List<Recipe>      _allRecipes     = new();
    private HashSet<string>   _learnedIds     = new(); // recipe ids the player has crafted
    private const string      SavePath        = "user://saves/world1/learned_recipes.json";
    private const string      RecipeFolder    = "res://Core/Recipes";

    public event Action OnLearnedChanged;

    public override void _Ready()
    {
        Instance = this;
        RegisterAllRecipes();
        LoadLearned();
    }

    // ── Recipe registry — loads every .json file in RecipeFolder ────────────────

    private void RegisterAllRecipes()
    {
        _allRecipes.Clear();

        using var dir = DirAccess.Open(RecipeFolder);
        if (dir == null)
        {
            GD.PrintErr($"RecipeManager: folder not found — {RecipeFolder}. Create it and add recipe .json files.");
            return;
        }

        dir.ListDirBegin();
        string fileName = dir.GetNext();
        while (fileName != "")
        {
            if (!dir.CurrentIsDir() && fileName.EndsWith(".json"))
                LoadRecipeFile($"{RecipeFolder}/{fileName}");
            fileName = dir.GetNext();
        }
        dir.ListDirEnd();

        GD.Print($"RecipeManager: loaded {_allRecipes.Count} recipes from {RecipeFolder}");
    }

    private void LoadRecipeFile(string path)
    {
        if (!FileAccess.FileExists(path)) return;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        string text = file.GetAsText();

        Godot.Collections.Dictionary data;
        try
        {
            data = Json.ParseString(text).AsGodotDictionary();
        }
        catch (Exception e)
        {
            GD.PrintErr($"RecipeManager: bad JSON in {path} — {e.Message}");
            return;
        }

        try
        {
            var recipe = ParseRecipe(data, path);
            if (recipe != null) _allRecipes.Add(recipe);
        }
        catch (Exception e)
        {
            GD.PrintErr($"RecipeManager: failed to parse {path} — {e.Message}");
        }
    }

    private Recipe ParseRecipe(Godot.Collections.Dictionary data, string path)
    {
        if (!data.ContainsKey("id") || !data.ContainsKey("result"))
        {
            GD.PrintErr($"RecipeManager: '{path}' is missing 'id' or 'result'.");
            return null;
        }

        var recipe = new Recipe
        {
            Id          = (string)data["id"],
            ResultId    = (string)data["result"],
            ResultCount = data.ContainsKey("count")  ? (int)data["count"]  : 1,
            Shaped      = data.ContainsKey("shaped") ? (bool)data["shaped"] : true
        };

        if (recipe.Shaped)
        {
            if (!data.ContainsKey("pattern"))
            {
                GD.PrintErr($"RecipeManager: shaped recipe '{recipe.Id}' is missing 'pattern' ({path}).");
                return null;
            }

            var patternArr = data["pattern"].AsGodotArray();
            int rows = patternArr.Count;
            int cols = ((string)patternArr[0]).Length;

            // "key" maps single characters used in the pattern to item ids.
            // Space (or "_") always means empty, no key entry needed for it.
            var keyMap = new Dictionary<char, string>();
            if (data.ContainsKey("key"))
            {
                var keyDict = data["key"].AsGodotDictionary();
                foreach (var k in keyDict.Keys)
                    keyMap[((string)k)[0]] = (string)keyDict[k];
            }

            var pattern = new string[rows, cols];
            for (int r = 0; r < rows; r++)
            {
                string rowStr = (string)patternArr[r];
                for (int c = 0; c < cols; c++)
                {
                    char ch = c < rowStr.Length ? rowStr[c] : ' ';
                    if (ch == ' ' || ch == '_')
                        pattern[r, c] = " ";
                    else if (keyMap.TryGetValue(ch, out string itemId))
                        pattern[r, c] = itemId;
                    else
                    {
                        GD.PrintErr($"RecipeManager: recipe '{recipe.Id}' uses key '{ch}' with no entry in \"key\" ({path}).");
                        pattern[r, c] = " ";
                    }
                }
            }
            recipe.Pattern = pattern;
        }
        else
        {
            if (!data.ContainsKey("ingredients"))
            {
                GD.PrintErr($"RecipeManager: shapeless recipe '{recipe.Id}' is missing 'ingredients' ({path}).");
                return null;
            }
            recipe.Ingredients = new List<string>();
            foreach (var i in data["ingredients"].AsGodotArray())
                recipe.Ingredients.Add((string)i);
        }

        return recipe;
    }

    // ── Matching ──────────────────────────────────────────────────────────────

    public Recipe FindMatch(string[,] grid, int gridSize)
    {
        foreach (var recipe in _allRecipes)
        {
            if (recipe.Shaped)
            {
                if (MatchShaped(recipe, grid, gridSize)) return recipe;
            }
            else
            {
                if (MatchShapeless(recipe, grid, gridSize)) return recipe;
            }
        }
        return null;
    }

    private bool MatchShaped(Recipe recipe, string[,] grid, int gridSize)
    {
        int patRows = recipe.Pattern.GetLength(0);
        int patCols = recipe.Pattern.GetLength(1);

        if (patRows > gridSize || patCols > gridSize) return false;

        for (int rowOff = 0; rowOff <= gridSize - patRows; rowOff++)
        {
            for (int colOff = 0; colOff <= gridSize - patCols; colOff++)
            {
                if (ShapedMatchAt(recipe, grid, gridSize, rowOff, colOff))
                    return true;
            }
        }
        return false;
    }

    private bool ShapedMatchAt(Recipe recipe, string[,] grid, int gridSize, int rowOff, int colOff)
    {
        int patRows = recipe.Pattern.GetLength(0);
        int patCols = recipe.Pattern.GetLength(1);

        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                string gridItem = grid[r, c] ?? "";
                string patItem  = "";

                int pr = r - rowOff;
                int pc = c - colOff;
                if (pr >= 0 && pr < patRows && pc >= 0 && pc < patCols)
                    patItem = recipe.Pattern[pr, pc] ?? " ";
                if (patItem == " ") patItem = "";

                if (gridItem != patItem) return false;
            }
        }
        return true;
    }

    private bool MatchShapeless(Recipe recipe, string[,] grid, int gridSize)
    {
        var gridItems = new List<string>();
        for (int r = 0; r < gridSize; r++)
            for (int c = 0; c < gridSize; c++)
                if (!string.IsNullOrEmpty(grid[r, c]))
                    gridItems.Add(grid[r, c]);

        if (gridItems.Count != recipe.Ingredients.Count) return false;

        var remaining = new List<string>(recipe.Ingredients);
        foreach (var item in gridItems)
        {
            if (!remaining.Remove(item)) return false;
        }
        return remaining.Count == 0;
    }

    // ── Learned recipes ───────────────────────────────────────────────────────

    public bool IsLearned(string recipeId) => _learnedIds.Contains(recipeId);

    public void LearnRecipe(string recipeId)
    {
        if (_learnedIds.Add(recipeId))
        {
            SaveLearned();
            OnLearnedChanged?.Invoke();
            GD.Print($"Learned recipe: {recipeId}");
        }
    }

    public IEnumerable<Recipe> GetLearnedRecipes()
    {
        foreach (var r in _allRecipes)
            if (_learnedIds.Contains(r.Id))
                yield return r;
    }

    public List<Recipe> GetAllRecipes() => _allRecipes;

    private void SaveLearned()
    {
        var dir = DirAccess.Open("user://");
        if (!dir.DirExists("saves/world1"))
            dir.MakeDirRecursive("saves/world1");

        var arr = new Godot.Collections.Array();
        foreach (var id in _learnedIds) arr.Add(id);

        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        file.StoreString(Json.Stringify(arr));
    }

    private void LoadLearned()
    {
        if (!FileAccess.FileExists(SavePath)) return;
        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        var parsed = Json.ParseString(file.GetAsText()).AsGodotArray();
        foreach (var id in parsed)
            _learnedIds.Add((string)id);
    }

    public List<string> GetIngredientDisplay(Recipe recipe)
    {
        var result = new List<string>();
        if (recipe.Shaped)
        {
            var seen = new Dictionary<string, int>();
            for (int r = 0; r < recipe.Pattern.GetLength(0); r++)
                for (int c = 0; c < recipe.Pattern.GetLength(1); c++)
                {
                    string item = recipe.Pattern[r, c];
                    if (!string.IsNullOrEmpty(item) && item != " ")
                        seen[item] = seen.TryGetValue(item, out int n) ? n + 1 : 1;
                }
            foreach (var kvp in seen)
                result.Add(kvp.Value > 1 ? $"{kvp.Key} x{kvp.Value}" : kvp.Key);
        }
        else
        {
            var seen = new Dictionary<string, int>();
            foreach (var item in recipe.Ingredients)
                seen[item] = seen.TryGetValue(item, out int n) ? n + 1 : 1;
            foreach (var kvp in seen)
                result.Add(kvp.Value > 1 ? $"{kvp.Key} x{kvp.Value}" : kvp.Key);
        }
        return result;
    }
}