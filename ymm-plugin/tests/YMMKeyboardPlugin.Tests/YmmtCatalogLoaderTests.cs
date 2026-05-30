using System.IO.Compression;
using System.Text;
using YMMKeyboardPlugin.Mapping;

namespace YMMKeyboardPlugin.Tests;

public class YmmtCatalogLoaderTests
{
    [Fact]
    public void Load_ParsesCustomPlacementValuesFromCatalog()
    {
        var catalogJson = """
        {
          "ItemTemplates": [
            {
              "Name": "TemplateA",
              "SceneName": "SceneA",
              "Width": 1920,
              "Height": 1080,
              "FPS": "60",
              "Items": [
                {
                  "$type": "YukkuriMovieMaker.Project.Items.ShapeItem, YukkuriMovieMaker",
                  "Frame": "12",
                  "Length": 90,
                  "Layer": 3,
                  "Group": 2,
                  "X": { "Values": [ { "Value": 120.5 } ] },
                  "Y": { "Values": [ { "Value": "-40.25" } ] },
                  "Z": 5,
                  "Opacity": { "Values": [ { "Value": 0.75 } ] },
                  "Zoom": { "Values": [ { "Value": "1.2" } ] },
                  "Rotation": { "Values": [ { "Value": 33 } ] }
                }
              ]
            }
          ]
        }
        """;

        var ymmtPath = CreateYmmtWithCatalog(catalogJson);

        try
        {
            var snapshot = YmmtCatalogLoader.Load(ymmtPath);

            Assert.Single(snapshot.Templates);
            var template = snapshot.Templates[0];
            Assert.Equal("TemplateA", template.Name);
            Assert.Equal("SceneA", template.SceneName);
            Assert.Equal(1920, template.Width);
            Assert.Equal(1080, template.Height);
            Assert.Equal(60, template.Fps);

            Assert.Single(template.Items);
            var item = template.Items[0];
            Assert.Equal("ShapeItem", item.ItemType);
            Assert.Equal(12, item.Frame);
            Assert.Equal(90, item.Length);
            Assert.Equal(3, item.Layer);
            Assert.Equal(2, item.Group);
            Assert.Equal(120.5, item.X);
            Assert.Equal(-40.25, item.Y);
            Assert.Equal(5, item.Z);
            Assert.Equal(0.75, item.Opacity);
            Assert.Equal(1.2, item.Zoom);
            Assert.Equal(33, item.Rotation);
        }
        finally
        {
            if (File.Exists(ymmtPath))
                File.Delete(ymmtPath);
        }
    }

    [Fact]
    public void TryLoad_ReturnsFalseWhenCatalogMissing()
    {
        var ymmtPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ymmt");
        using (var archive = ZipFile.Open(ymmtPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("dummy.txt");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write("dummy");
        }

        try
        {
            var ok = YmmtCatalogLoader.TryLoad(ymmtPath, out var snapshot, out var error);

            Assert.False(ok);
            Assert.Null(snapshot);
            Assert.False(string.IsNullOrWhiteSpace(error));
        }
        finally
        {
            if (File.Exists(ymmtPath))
                File.Delete(ymmtPath);
        }
    }

    private static string CreateYmmtWithCatalog(string catalogJson)
    {
        var ymmtPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ymmt");

        using var archive = ZipFile.Open(ymmtPath, ZipArchiveMode.Create);
        var entry = archive.CreateEntry("catalog.json");
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(catalogJson);

        return ymmtPath;
    }
}
