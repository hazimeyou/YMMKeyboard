using System.Reflection;
using System.Text;
using System.Windows;
using YMMKeyboardPlugin.Mapping;
using YMMKeyboardPlugin.Models;

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

            var message = BuildSummary(snapshot);
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

    private static string BuildSummary(YmmtProjectSnapshot snapshot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("YMMT load succeeded");
        sb.AppendLine($"Source: {snapshot.SourcePath}");
        sb.AppendLine($"Templates: {snapshot.Templates.Count}");

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

            foreach (var item in template.Items.Take(5))
            {
                sb.AppendLine(
                    $"- {item.ItemType} L{item.Layer} F{item.Frame} Len{item.Length} XY=({item.X ?? 0},{item.Y ?? 0}) File={item.FileName ?? "-"}");
            }
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
}
