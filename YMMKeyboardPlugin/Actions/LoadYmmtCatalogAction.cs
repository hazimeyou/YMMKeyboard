using System.Reflection;
using System.Text;
using System.Windows;
using YMMKeyboardPlugin.Mapping;
using YMMKeyboardPlugin.Models;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.Project.Items;

namespace YMMKeyboardPlugin.Actions;

public static class LoadYmmtCatalogAction
{
    public static void Execute(string switchName, string? parameter)
    {
        try
        {
            var rawPath = parameter?.Trim();
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                MessageBox.Show(
                    "Set .ymmt file path in parameter.\nExample: C:\\Users\\<user>\\Downloads\\test.ymmt",
                    "LoadYmmtCatalog");
                return;
            }

            var path = NormalizePath(rawPath);
            if (!YmmtCatalogLoader.TryLoad(path, out var snapshot, out var error) || snapshot is null)
            {
                MessageBox.Show($"Load failed.\nPath: {path}\nError: {error}", "LoadYmmtCatalog");
                return;
            }

            string savedPath;
            try
            {
                savedPath = SaveToPluginFolder(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Loaded but save failed.\nPath: {path}\nError: {ex.Message}", "LoadYmmtCatalog");
                return;
            }

            var importResult = ImportToTimeline(snapshot);

            var message = BuildSummary(snapshot, importResult);
            message += $"\n\nSavedTo: {savedPath}";
            MessageBox.Show(message, "LoadYmmtCatalog");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unexpected error: {ex.Message}", "LoadYmmtCatalog");
        }
    }

    private static string NormalizePath(string rawPath)
    {
        var path = rawPath.Trim().Trim('"');
        path = Environment.ExpandEnvironmentVariables(path);
        return Path.GetFullPath(path);
    }

    private static ImportResult ImportToTimeline(YmmtProjectSnapshot snapshot)
    {
        var timeline = KeyboardAction.TimelineInstance;
        if (timeline is null)
            return new ImportResult(0, 0, "Timeline is not available.");

        var template = snapshot.Templates.FirstOrDefault();
        if (template is null)
            return new ImportResult(0, 0, "No template found in .ymmt.");

        var imported = 0;
        var skipped = 0;
        var notes = new List<string>();
        var baseFrame = timeline.CurrentFrame;

        foreach (var src in template.Items)
        {
            if (!TryCreateTimelineItem(src, baseFrame, out var item, out var reason) || item is null)
            {
                skipped++;
                if (!string.IsNullOrWhiteSpace(reason) && notes.Count < 5)
                    notes.Add(reason);
                continue;
            }

            var frame = GetPropertyInt(item, "Frame");
            var layer = GetPropertyInt(item, "Layer");
            if (Application.Current is not null)
                Application.Current.Dispatcher.Invoke(() => timeline.TryAddItems(new IItem[] { item }, frame, layer));
            else
                timeline.TryAddItems(new IItem[] { item }, frame, layer);
            imported++;
        }

        var noteText = notes.Count == 0 ? string.Empty : string.Join("\n", notes);
        return new ImportResult(imported, skipped, noteText);
    }

    private static bool TryCreateTimelineItem(YmmtItemSnapshot src, int baseFrame, out IItem? item, out string reason)
    {
        item = null;
        reason = string.Empty;

        try
        {
            if (string.Equals(src.ItemType, "AudioItem", StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(src.FilePath) || !File.Exists(src.FilePath))
                {
                    reason = $"Skip AudioItem: file not found ({src.FileName ?? "-"})";
                    return false;
                }

                var audio = new AudioItem(src.FilePath);
                SetIfWritable(audio, "Frame", baseFrame + Math.Max(0, src.Frame));
                SetIfWritable(audio, "Layer", Math.Max(0, src.Layer));
                SetIfWritable(audio, "Length", Math.Max(1, src.Length));
                item = audio;
                return true;
            }

            var itemsAssembly = typeof(AudioItem).Assembly;
            var type = itemsAssembly.GetType($"YukkuriMovieMaker.Project.Items.{src.ItemType}");
            if (type is null)
            {
                reason = $"Skip {src.ItemType}: unknown type";
                return false;
            }

            var instance = Activator.CreateInstance(type);
            if (instance is not IItem timelineItem)
            {
                reason = $"Skip {src.ItemType}: cannot create item";
                return false;
            }

            SetIfWritable(instance, "Frame", baseFrame + Math.Max(0, src.Frame));
            SetIfWritable(instance, "Layer", Math.Max(0, src.Layer));
            SetIfWritable(instance, "Length", Math.Max(1, src.Length));
            item = timelineItem;
            return true;
        }
        catch (Exception ex)
        {
            reason = $"Skip {src.ItemType}: {ex.Message}";
            return false;
        }
    }

    private static int GetPropertyInt(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property is null)
            return 0;

        var value = property.GetValue(instance);
        return value switch
        {
            int i => i,
            long l => l > int.MaxValue ? int.MaxValue : l < int.MinValue ? int.MinValue : (int)l,
            _ => 0,
        };
    }

    private static void SetIfWritable(object instance, string propertyName, object value)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property?.CanWrite != true)
            return;

        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var converted = Convert.ChangeType(value, targetType);
        property.SetValue(instance, converted);
    }

    private static string BuildSummary(YmmtProjectSnapshot snapshot, ImportResult importResult)
    {
        var sb = new StringBuilder();
        sb.AppendLine("YMMT load succeeded");
        sb.AppendLine($"Source: {snapshot.SourcePath}");
        sb.AppendLine($"Templates: {snapshot.Templates.Count}");
        sb.AppendLine($"Imported: {importResult.Imported}");
        sb.AppendLine($"Skipped: {importResult.Skipped}");
        if (!string.IsNullOrWhiteSpace(importResult.Note))
            sb.AppendLine($"Notes:\n{importResult.Note}");

        for (var i = 0; i < snapshot.Templates.Count; i++)
        {
            var template = snapshot.Templates[i];
            sb.AppendLine();
            sb.AppendLine($"[{i}] {template.Name} / Scene={template.SceneName} / {template.Width}x{template.Height} / FPS={template.Fps}");
            sb.AppendLine($"Items: {template.Items.Count}");

            var groups = template.Items
                .GroupBy(item => item.ItemType)
                .OrderByDescending(group => group.Count())
                .Select(group => $"{group.Key}:{group.Count()}");
            sb.AppendLine($"Types: {string.Join(", ", groups)}");
        }

        return sb.ToString();
    }

    private static string SaveToPluginFolder(string sourceYmmtPath)
    {
        var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? AppContext.BaseDirectory;
        var targetDirectory = Path.Combine(assemblyDirectory, "projects");
        Directory.CreateDirectory(targetDirectory);

        var fileName = Path.GetFileName(sourceYmmtPath);
        var destinationPath = Path.Combine(targetDirectory, fileName);

        if (!File.Exists(destinationPath))
        {
            File.Copy(sourceYmmtPath, destinationPath, overwrite: false);
            return destinationPath;
        }

        if (string.Equals(
                Path.GetFullPath(sourceYmmtPath),
                Path.GetFullPath(destinationPath),
                StringComparison.OrdinalIgnoreCase))
        {
            return destinationPath;
        }

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var versionedPath = Path.Combine(targetDirectory, $"{baseName}_{timestamp}{extension}");
        File.Copy(sourceYmmtPath, versionedPath, overwrite: false);
        return versionedPath;
    }

    private readonly record struct ImportResult(int Imported, int Skipped, string Note);
}
