using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

// Central place the CreativeMenu (and anything else that wants "every item
// in the game") pulls its list from. There is no hand-maintained item
// registry — every .png dropped into Assets/Textures/Items/ is treated as
// an item and shows up automatically. This means testing a brand-new item
// is just "add the texture" with nothing else to wire up.
//
// Categories are optional and separate from the item list itself: an item
// with no category tag still shows up (under "All" and "Misc"), it's just
// not sorted into a named tab yet. This lets you build/test items today and
// go back and categorize them later without anything ever being hidden.
public static class ItemCatalog
{
    private const string ItemsFolder    = "res://Assets/Textures/Items/";
    private const string CategoriesFile = "res://Core/Items/categories.json";

    public const string AllCategory     = "All";
    public const string DefaultCategory = "Misc";

    private static List<string> _allItemIds;
    private static Dictionary<string, string> _categories;

    // Every item id currently known (filename minus ".png"), sorted A-Z.
    public static List<string> GetAllItemIds()
    {
        if (_allItemIds != null) return _allItemIds;

        _allItemIds = new List<string>();
        using var dir = DirAccess.Open(ItemsFolder);
        if (dir == null)
        {
            GD.PrintErr($"ItemCatalog: couldn't open {ItemsFolder}");
            return _allItemIds;
        }

        dir.ListDirBegin();
        string fileName = dir.GetNext();
        while (fileName != "")
        {
            if (!dir.CurrentIsDir() && fileName.EndsWith(".png"))
            {
                string id = fileName.Substring(0, fileName.Length - 4); // strip ".png"
                _allItemIds.Add(id);
            }
            fileName = dir.GetNext();
        }
        dir.ListDirEnd();

        _allItemIds.Sort(StringComparer.OrdinalIgnoreCase);
        return _allItemIds;
    }

    // Optional res://Core/Items/categories.json — a flat
    // { "itemId": "CategoryName", ... } map. If the file is missing
    // entirely this just returns an empty map and every item falls back
    // to DefaultCategory — nothing errors out.
    public static Dictionary<string, string> GetCategories()
    {
        if (_categories != null) return _categories;
        _categories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!FileAccess.FileExists(CategoriesFile)) return _categories;

        using var f = FileAccess.Open(CategoriesFile, FileAccess.ModeFlags.Read);
        string text = f.GetAsText();
        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(text);
            if (parsed != null)
                _categories = new Dictionary<string, string>(parsed, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception e)
        {
            GD.PrintErr($"ItemCatalog: failed to parse {CategoriesFile}: {e.Message}");
        }
        return _categories;
    }

    public static string GetCategory(string itemId)
    {
        var cats = GetCategories();
        return cats.TryGetValue(itemId, out var cat) ? cat : DefaultCategory;
    }

    // Distinct category names currently in use, sorted, for building tabs.
    // "Misc" is included automatically whenever at least one known item
    // has no tag, even if categories.json exists and covers everything else.
    public static List<string> GetAllCategoryNames()
    {
        var cats = GetCategories();
        var set  = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in cats.Values) set.Add(v);

        foreach (var id in GetAllItemIds())
        {
            if (!cats.ContainsKey(id)) { set.Add(DefaultCategory); break; }
        }
        return new List<string>(set);
    }

    // Clears the cached item/category lists so the next GetAllItemIds() /
    // GetCategories() call re-scans disk. Not called automatically anywhere
    // yet — handy to hook up to a debug key later if you want to add items
    // or edit categories.json without restarting the game.
    public static void Refresh()
    {
        _allItemIds  = null;
        _categories  = null;
    }
}