using System.Windows;
using YMMKeyboardPlugin.Actions;
using YMMKeyboardPlugin.Models;
using YMMKeyboardPlugin.Settings;

namespace YMMKeyboardPlugin.Mapping
{
    public static class MappingConverter
    {
        public const string NoneActionName = "None";
        public const string TestEventActionName = "TestEvent";
        public const string PlusSeekFrameActionName = "PlusSeekFrame";
        public const string MinusSeekFrameActionName = "MinusSeekFrame";

        public static IReadOnlyList<string> AvailableActions { get; } = new[]
        {
            NoneActionName,
            TestEventActionName,
            PlusSeekFrameActionName,
            MinusSeekFrameActionName,
        };

        public static void ExecuteUiSwitch(string switchName)
        {
            var config = YMMKeyboardSettings.Current.GetUiButtonConfig(switchName);
            ExecuteAction(config.ActionName, config.Parameter, switchName, "UI Keyboard");
        }

        public static void ExecuteUiCombination(IEnumerable<string> switchNames)
        {
            var combinationKey = SwitchLayout.NormalizeCombination(switchNames);
            if (string.IsNullOrWhiteSpace(combinationKey))
                return;

            var config = YMMKeyboardSettings.Current.GetUiComboButtonConfig(combinationKey);
            ExecuteAction(config.ActionName, config.Parameter, combinationKey, "UI Keyboard");
        }

        public static void ExecuteDeviceSwitch(string uid, string switchName)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return;

            var config = YMMKeyboardSettings.Current.GetDeviceButtonConfig(uid, switchName);
            ExecuteAction(config.ActionName, config.Parameter, switchName, uid);
        }

        public static void ExecuteAction(string actionName, string? parameter, string switchName, string sourceName)
        {
            var frameCount = ParseFrameCount(parameter);

            switch (actionName)
            {
                case TestEventActionName:
                    TestEvent.Execute($"{switchName} ({sourceName})", parameter);
                    break;
                case PlusSeekFrameActionName:
                    KeyboardAction.PlusSeekFrame(frameCount);
                    break;
                case MinusSeekFrameActionName:
                    KeyboardAction.MinusSeekFrame(frameCount);
                    break;
                case NoneActionName:
                case "":
                case null:
                    break;
                default:
                    MessageBox.Show($"未対応のアクションです: {actionName}", "キーボード設定");
                    break;
            }
        }

        private static int ParseFrameCount(string? parameter)
        {
            return int.TryParse(parameter, out var frames) && frames > 0 ? frames : 1;
        }

        public static void SW01() => ExecuteUiSwitch("SW01");
        public static void SW02() => ExecuteUiSwitch("SW02");
        public static void SW03() => ExecuteUiSwitch("SW03");
        public static void SW04() => ExecuteUiSwitch("SW04");
        public static void SW05() => ExecuteUiSwitch("SW05");
        public static void SW06() => ExecuteUiSwitch("SW06");
        public static void SW07() => ExecuteUiSwitch("SW07");
        public static void SW08() => ExecuteUiSwitch("SW08");
        public static void SW09() => ExecuteUiSwitch("SW09");
        public static void SW10() => ExecuteUiSwitch("SW10");
        public static void SW11() => ExecuteUiSwitch("SW11");
        public static void SW12() => ExecuteUiSwitch("SW12");
        public static void SW13() => ExecuteUiSwitch("SW13");
        public static void SW14() => ExecuteUiSwitch("SW14");
        public static void SW15() => ExecuteUiSwitch("SW15");
        public static void SW16() => ExecuteUiSwitch("SW16");
        public static void SW17() => ExecuteUiSwitch("SW17");
        public static void SW18() => ExecuteUiSwitch("SW18");
        public static void SW19() => ExecuteUiSwitch("SW19");
        public static void SW20() => ExecuteUiSwitch("SW20");
        public static void SW21() => ExecuteUiSwitch("SW21");
        public static void SW22() => ExecuteUiSwitch("SW22");
        public static void SW23() => ExecuteUiSwitch("SW23");
        public static void SW24() => ExecuteUiSwitch("SW24");
        public static void SW25() => ExecuteUiSwitch("SW25");
        public static void SW26() => ExecuteUiSwitch("SW26");
        public static void SW27() => ExecuteUiSwitch("SW27");
        public static void SW28() => ExecuteUiSwitch("SW28");
        public static void SW29() => ExecuteUiSwitch("SW29");
        public static void SW30() => ExecuteUiSwitch("SW30");
        public static void SW35() => ExecuteUiSwitch("SW35");
        public static void SW36() => ExecuteUiSwitch("SW36");
        public static void SW37() => ExecuteUiSwitch("SW37");
    }
}
