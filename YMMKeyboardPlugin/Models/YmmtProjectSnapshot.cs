namespace YMMKeyboardPlugin.Models;

public sealed class YmmtProjectSnapshot
{
    public required string SourcePath { get; init; }
    public required IReadOnlyList<YmmtTemplateSnapshot> Templates { get; init; }
}

public sealed class YmmtTemplateSnapshot
{
    public required string Name { get; init; }
    public required string SceneName { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int Fps { get; init; }
    public required IReadOnlyList<YmmtItemSnapshot> Items { get; init; }
}

public sealed class YmmtItemSnapshot
{
    public required string ItemType { get; init; }
    public string? FilePath { get; init; }
    public string? FileName { get; init; }
    public int Frame { get; init; }
    public int Length { get; init; }
    public int Layer { get; init; }
    public int Group { get; init; }
    public double? X { get; init; }
    public double? Y { get; init; }
    public double? Z { get; init; }
    public double? Opacity { get; init; }
    public double? Zoom { get; init; }
    public double? Rotation { get; init; }
}
