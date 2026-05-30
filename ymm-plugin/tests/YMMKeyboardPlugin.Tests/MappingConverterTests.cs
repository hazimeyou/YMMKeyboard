using YMMKeyboardPlugin.Mapping;

namespace YMMKeyboardPlugin.Tests;

public class MappingConverterTests
{
    [Theory]
    [InlineData(null, MappingConverter.NoneActionName)]
    [InlineData("", MappingConverter.NoneActionName)]
    [InlineData("  ", MappingConverter.NoneActionName)]
    [InlineData("None", MappingConverter.NoneActionName)]
    [InlineData("なし", MappingConverter.NoneActionName)]
    [InlineData("TestEvent", MappingConverter.TestEventActionName)]
    [InlineData("テストイベント", MappingConverter.TestEventActionName)]
    [InlineData("テスト本番削除", MappingConverter.TestEventActionName)]
    [InlineData("PlusSeekFrame", MappingConverter.PlusSeekFrameActionName)]
    [InlineData("シークバーを進める", MappingConverter.PlusSeekFrameActionName)]
    [InlineData("MinusSeekFrame", MappingConverter.MinusSeekFrameActionName)]
    [InlineData("シークバーを戻す", MappingConverter.MinusSeekFrameActionName)]
    [InlineData("LoadYmmtCatalog", MappingConverter.LoadYmmtCatalogActionName)]
    [InlineData("YMMT読み込み", MappingConverter.LoadYmmtCatalogActionName)]
    [InlineData("ymmt読み込み", MappingConverter.LoadYmmtCatalogActionName)]
    [InlineData("  CustomAction  ", "CustomAction")]
    public void NormalizeActionName_ConvertsAliases(string? input, string expected)
    {
        var actual = MappingConverter.NormalizeActionName(input);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AvailableActions_ContainsExpectedActions()
    {
        var actions = MappingConverter.AvailableActions;

        Assert.Contains(MappingConverter.NoneActionName, actions);
        Assert.DoesNotContain(MappingConverter.TestEventActionName, actions);
        Assert.Contains(MappingConverter.PlusSeekFrameActionName, actions);
        Assert.Contains(MappingConverter.MinusSeekFrameActionName, actions);
        Assert.Contains(MappingConverter.LoadYmmtCatalogActionName, actions);
        Assert.Equal(4, actions.Count);
    }
}
