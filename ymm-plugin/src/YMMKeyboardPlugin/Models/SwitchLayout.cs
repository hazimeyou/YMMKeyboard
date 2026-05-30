namespace YMMKeyboardPlugin.Models
{
    public static class SwitchLayout
    {
        private static readonly IReadOnlyDictionary<string, int> sortOrder;

        static SwitchLayout()
        {
            sortOrder = All
                .Select((item, index) => new { item.SwitchName, Index = index })
                .ToDictionary(item => item.SwitchName, item => item.Index, StringComparer.OrdinalIgnoreCase);
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
