using Godot;
using System;

public static class MobDefinitionLoader
{
    public static MobDefinition Load(string path)
    {
        if (!Godot.FileAccess.FileExists(path))
        {
            GD.PrintErr($"[MobDefinition] File not found: {path}");
            return null;
        }

        try
        {
            using Godot.FileAccess file =
                Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);

            string json = file.GetAsText();

            var options = new System.Text.Json.JsonSerializerOptions
{
    IncludeFields = true,
    PropertyNameCaseInsensitive = true
};

MobDefinition definition =
    System.Text.Json.JsonSerializer.Deserialize<MobDefinition>(json, options);

            if (definition == null)
            {
                GD.PrintErr($"[MobDefinition] Failed to deserialize: {path}");
                return null;
            }

            return definition;
        }
        catch (Exception e)
        {
            GD.PrintErr($"[MobDefinition] Error loading {path}: {e.Message}");
            return null;
        }
    }
}