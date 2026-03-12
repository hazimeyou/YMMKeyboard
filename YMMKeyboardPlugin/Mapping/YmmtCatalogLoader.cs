using System.IO.Compression;
using System.Text.Json;
using YMMKeyboardPlugin.Models;

namespace YMMKeyboardPlugin.Mapping;

public static class YmmtCatalogLoader
{
    private const string CatalogEntryName = "catalog.json";

    public static bool TryLoad(string ymmtPath, out YmmtProjectSnapshot? snapshot, out string? error)
    {
        try
        {
            snapshot = Load(ymmtPath);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            snapshot = null;
            error = ex.Message;
            return false;
        }
    }

    public static YmmtProjectSnapshot Load(string ymmtPath)
    {
        if (string.IsNullOrWhiteSpace(ymmtPath))
            throw new ArgumentException("Path is empty.", nameof(ymmtPath));
        if (!File.Exists(ymmtPath))
            throw new FileNotFoundException("YMMT file was not found.", ymmtPath);

        using var archive = ZipFile.OpenRead(ymmtPath);
        var entry = archive.Entries.FirstOrDefault(e =>
            string.Equals(e.FullName, CatalogEntryName, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
            throw new InvalidDataException("catalog.json was not found in the archive.");

        using var stream = entry.Open();
        using var document = JsonDocument.Parse(stream);

        var root = document.RootElement;
        var templates = new List<YmmtTemplateSnapshot>();
        if (root.TryGetProperty("ItemTemplates", out var templateArray) && templateArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var template in templateArray.EnumerateArray())
            {
                templates.Add(ParseTemplate(template));
            }
        }

        return new YmmtProjectSnapshot
        {
            SourcePath = ymmtPath,
            Templates = templates,
        };
    }

    private static YmmtTemplateSnapshot ParseTemplate(JsonElement template)
    {
        var items = new List<YmmtItemSnapshot>();
        if (template.TryGetProperty("Items", out var itemArray) && itemArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in itemArray.EnumerateArray())
            {
                items.Add(ParseItem(item));
            }
        }

        return new YmmtTemplateSnapshot
        {
            Name = GetString(template, "Name") ?? string.Empty,
            SceneName = GetString(template, "SceneName") ?? string.Empty,
            Width = GetInt(template, "Width"),
            Height = GetInt(template, "Height"),
            Fps = GetInt(template, "FPS"),
            Items = items,
        };
    }

    private static YmmtItemSnapshot ParseItem(JsonElement item)
    {
        var filePath = GetString(item, "FilePath");
        return new YmmtItemSnapshot
        {
            ItemType = NormalizeTypeName(GetString(item, "$type")),
            FilePath = filePath,
            FileName = string.IsNullOrWhiteSpace(filePath) ? null : Path.GetFileName(filePath),
            Frame = GetInt(item, "Frame"),
            Length = GetInt(item, "Length"),
            Layer = GetInt(item, "Layer"),
            Group = GetInt(item, "Group"),
            X = GetAnimatedFirstValue(item, "X"),
            Y = GetAnimatedFirstValue(item, "Y"),
            Z = GetAnimatedFirstValue(item, "Z"),
            Opacity = GetAnimatedFirstValue(item, "Opacity"),
            Zoom = GetAnimatedFirstValue(item, "Zoom"),
            Rotation = GetAnimatedFirstValue(item, "Rotation"),
        };
    }

    private static string NormalizeTypeName(string? rawType)
    {
        if (string.IsNullOrWhiteSpace(rawType))
            return string.Empty;

        var shortType = rawType.Split(',')[0].Trim();
        const string prefix = "YukkuriMovieMaker.Project.Items.";
        return shortType.StartsWith(prefix, StringComparison.Ordinal)
            ? shortType[prefix.Length..]
            : shortType;
    }

    private static string? GetString(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var property))
            return null;
        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
    }

    private static int GetInt(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var property))
            return 0;
        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out var i32) => i32,
            JsonValueKind.Number when property.TryGetInt64(out var i64) => (int)i64,
            JsonValueKind.String when int.TryParse(property.GetString(), out var parsed) => parsed,
            _ => 0,
        };
    }

    private static double? GetAnimatedFirstValue(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var property))
            return null;

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var direct))
            return direct;

        if (property.ValueKind != JsonValueKind.Object)
            return null;

        if (!property.TryGetProperty("Values", out var values) || values.ValueKind != JsonValueKind.Array)
            return null;
        if (values.GetArrayLength() == 0)
            return null;

        var first = values[0];
        if (!first.TryGetProperty("Value", out var valueProperty))
            return null;

        return valueProperty.ValueKind switch
        {
            JsonValueKind.Number when valueProperty.TryGetDouble(out var numeric) => numeric,
            JsonValueKind.String when double.TryParse(valueProperty.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }
}
