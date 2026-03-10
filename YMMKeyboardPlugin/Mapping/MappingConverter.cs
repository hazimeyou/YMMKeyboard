using YMMKeyboardPlugin.Settings;

namespace YMMKeyboardPlugin.Mapping
{
    public static class MappingConverter
    {
        public const string NoneActionName = "None";
        public const string TestEventActionName = "TestEvent";

        public static IReadOnlyList<string> AvailableActions { get; } = new[]
        {
            NoneActionName,
            TestEventActionName,
        };

        public static void ExecuteSwitch(string uid, string switchName)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return;

            var config = YMMKeyboardSettings.Current.GetButtonConfig(uid, switchName);
            ExecuteAction(config.ActionName, config.Parameter, switchName, uid);
        }

        public static void ExecuteAction(string actionName, string? parameter, string switchName, string uid)
        {
            switch (actionName)
            {
                case TestEventActionName:
                    TestEvent.Execute($"{switchName} ({uid})", parameter);
                    break;
                case NoneActionName:
                case "":
                case null:
                    break;
                default:
                    MessageBox.Show($"未対応のアクションです: {actionName}", "キーボード割り当て");
                    break;
            }
        }

        private static string GetManualUid()
        {
            return YMMKeyboardSettings.Current.GetManualTargetUid();
        }

        public static void SW01() => ExecuteSwitch(GetManualUid(), "SW01");
        public static void SW02() => ExecuteSwitch(GetManualUid(), "SW02");
        public static void SW03() => ExecuteSwitch(GetManualUid(), "SW03");
        public static void SW04() => ExecuteSwitch(GetManualUid(), "SW04");
        public static void SW05() => ExecuteSwitch(GetManualUid(), "SW05");
        public static void SW06() => ExecuteSwitch(GetManualUid(), "SW06");
        public static void SW07() => ExecuteSwitch(GetManualUid(), "SW07");
        public static void SW08() => ExecuteSwitch(GetManualUid(), "SW08");
        public static void SW09() => ExecuteSwitch(GetManualUid(), "SW09");
        public static void SW10() => ExecuteSwitch(GetManualUid(), "SW10");
        public static void SW11() => ExecuteSwitch(GetManualUid(), "SW11");
        public static void SW12() => ExecuteSwitch(GetManualUid(), "SW12");
        public static void SW13() => ExecuteSwitch(GetManualUid(), "SW13");
        public static void SW14() => ExecuteSwitch(GetManualUid(), "SW14");
        public static void SW15() => ExecuteSwitch(GetManualUid(), "SW15");
        public static void SW16() => ExecuteSwitch(GetManualUid(), "SW16");
        public static void SW17() => ExecuteSwitch(GetManualUid(), "SW17");
        public static void SW18() => ExecuteSwitch(GetManualUid(), "SW18");
        public static void SW19() => ExecuteSwitch(GetManualUid(), "SW19");
        public static void SW20() => ExecuteSwitch(GetManualUid(), "SW20");
        public static void SW21() => ExecuteSwitch(GetManualUid(), "SW21");
        public static void SW22() => ExecuteSwitch(GetManualUid(), "SW22");
        public static void SW23() => ExecuteSwitch(GetManualUid(), "SW23");
        public static void SW24() => ExecuteSwitch(GetManualUid(), "SW24");
        public static void SW25() => ExecuteSwitch(GetManualUid(), "SW25");
        public static void SW26() => ExecuteSwitch(GetManualUid(), "SW26");
        public static void SW27() => ExecuteSwitch(GetManualUid(), "SW27");
        public static void SW28() => ExecuteSwitch(GetManualUid(), "SW28");
        public static void SW29() => ExecuteSwitch(GetManualUid(), "SW29");
        public static void SW30() => ExecuteSwitch(GetManualUid(), "SW30");
        public static void SW35() => ExecuteSwitch(GetManualUid(), "SW35");
        public static void SW36() => ExecuteSwitch(GetManualUid(), "SW36");
        public static void SW37() => ExecuteSwitch(GetManualUid(), "SW37");
    }
}
