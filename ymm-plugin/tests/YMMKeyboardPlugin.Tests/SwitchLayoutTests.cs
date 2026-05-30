using YMMKeyboardPlugin.Models;

namespace YMMKeyboardPlugin.Tests;

public class SwitchLayoutTests
{
    [Theory]
    [InlineData(1, "SW01")]
    [InlineData(36, "SW37")]
    [InlineData(37, "SW36")]
    public void TryGetSwitchName_ReturnsMappedName(int switchId, string expected)
    {
        var result = SwitchLayout.TryGetSwitchName(switchId, out var switchName);

        Assert.True(result);
        Assert.Equal(expected, switchName);
    }

    [Fact]
    public void TryGetSwitchName_ReturnsFalseForUnknownId()
    {
        var result = SwitchLayout.TryGetSwitchName(999, out var switchName);

        Assert.False(result);
        Assert.Equal(string.Empty, switchName);
    }

    [Fact]
    public void NormalizeCombination_RemovesDuplicatesAndSortsByLayoutOrder()
    {
        var normalized = SwitchLayout.NormalizeCombination(new[] { "SW03", "SW01", "sw03", "SW02" });

        Assert.Equal("SW01+SW02+SW03", normalized);
    }

    [Fact]
    public void NormalizeCombination_PutsUnknownSwitchesAfterKnownOnes()
    {
        var normalized = SwitchLayout.NormalizeCombination(new[] { "ZZZ", "SW02", "AAA", "SW01" });

        Assert.Equal("SW01+SW02+AAA+ZZZ", normalized);
    }

    [Fact]
    public void FormatCombination_UsesReadableSeparator()
    {
        var formatted = SwitchLayout.FormatCombination(new[] { "SW02", "SW01", "SW02" });

        Assert.Equal("SW01 + SW02", formatted);
    }
}
