namespace YMMKeyboardPlugin.Models
{
public static class SwitchLayout
{
    private static readonly IReadOnlyDictionary<string, int> sortOrder;
    private static readonly IReadOnlyDictionary<(int Row, int Col), int> matrixSwitchIds;

    static SwitchLayout()
    {
        sortOrder = All
            .Select((item, index) => new { item.SwitchName, Index = index })
            .ToDictionary(item => item.SwitchName, item => item.Index, StringComparer.OrdinalIgnoreCase);

        // Keep this aligned with firmware/src/RP2040TinyUsb/src/main.c matrix mapping.
        matrixSwitchIds = new Dictionary<(int Row, int Col), int>
        {
            [(0, 0)] = 1,
            [(0, 1)] = 2,
            [(0, 2)] = 3,
            [(0, 3)] = 4,
            [(0, 4)] = 5,
            [(0, 5)] = 6,
            [(1, 0)] = 8,
            [(1, 1)] = 9,
            [(1, 2)] = 10,
            [(1, 3)] = 11,
            [(1, 4)] = 12,
            [(1, 5)] = 13,
            [(2, 0)] = 15,
            [(2, 1)] = 16,
            [(2, 2)] = 17,
            [(2, 3)] = 18,
            [(2, 4)] = 19,
            [(2, 5)] = 20,
            [(3, 0)] = 22,
            [(3, 1)] = 23,
            [(3, 2)] = 24,
            [(3, 3)] = 25,
            [(3, 4)] = 26,
            [(3, 5)] = 27,
            [(4, 0)] = 29,
            [(4, 1)] = 30,
            [(4, 2)] = 31,
            [(4, 3)] = 32,
            [(4, 4)] = 33,
            [(4, 5)] = 34,
            [(5, 0)] = 35,
        };
    }

        public static IReadOnlyList<(int SwitchId, string SwitchName)> All { get; } = new[]
        {
            (1, "SW01"),
            (2, "SW02"),
            (3, "SW03"),
            (4, "SW04"),
            (5, "SW05"),
            (6, "SW06"),
            (8, "SW07"),
            (9, "SW08"),
            (10, "SW09"),
            (11, "SW10"),
            (12, "SW11"),
            (13, "SW12"),
            (15, "SW13"),
            (16, "SW14"),
            (17, "SW15"),
            (18, "SW16"),
            (19, "SW17"),
            (20, "SW18"),
            (22, "SW19"),
            (23, "SW20"),
            (24, "SW21"),
            (25, "SW22"),
            (26, "SW23"),
            (27, "SW24"),
            (29, "SW25"),
            (30, "SW26"),
            (31, "SW27"),
            (32, "SW28"),
            (33, "SW29"),
            (34, "SW30"),
            (35, "SW35"),
            (36, "SW37"),
            (37, "SW36")
        };

    public static bool TryGetSwitchName(int switchId, out string switchName)
    {
        foreach (var item in All)
        {
                if (item.SwitchId == switchId)
                {
                    switchName = item.SwitchName;
                    return true;
                }
            }

        switchName = string.Empty;
        return false;
    }

    public static bool TryGetMatrixSwitchName(int row, int col, out string switchName)
    {
        if (!matrixSwitchIds.TryGetValue((row, col), out var switchId))
        {
            switchName = string.Empty;
            return false;
        }

        return TryGetSwitchName(switchId, out switchName);
    }

    public static string NormalizeCombination(IEnumerable<string> switchNames)
    {
            return string.Join("+", switchNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(GetSortIndex)
                .ThenBy(name => name, StringComparer.OrdinalIgnoreCase));
        }

        public static string FormatCombination(IEnumerable<string> switchNames)
        {
            return string.Join(" + ", switchNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(GetSortIndex)
                .ThenBy(name => name, StringComparer.OrdinalIgnoreCase));
        }

        private static int GetSortIndex(string switchName)
        {
            return sortOrder.TryGetValue(switchName, out var index)
                ? index
                : int.MaxValue;
        }
    }
}
