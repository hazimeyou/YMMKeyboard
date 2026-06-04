using System.Windows;
using YMMKeyboardPlugin.Actions;
using YMMKeyboardPlugin.Diagnostics;
using YMMKeyboardPlugin.Key;
using YMMKeyboardPlugin.Logging;
using YMMKeyboardPlugin.Models;
using YMMKeyboardPlugin.Settings;

namespace YMMKeyboardPlugin.Mapping
{
    public static class MappingConverter
    {
        public const string NoneActionName = "なし";
        public const string TestEventActionName = "テストイベント";
        public const string PlusSeekFrameActionName = "シークバーを進める";
        public const string MinusSeekFrameActionName = "シークバーを戻す";
        public const string LoadYmmtCatalogActionName = "YMMT読み込み";
        public const string KeyAActionName = "A";
        public const string SpaceActionName = "Space";
        private static readonly HashSet<string> warnedUnknownActions = new(StringComparer.Ordinal);

        public static IReadOnlyList<string> AvailableActions { get; } = BuildAvailableActions();

        private static IReadOnlyList<string> BuildAvailableActions()
        {
            var actions = new List<string>
            {
                NoneActionName,
                PlusSeekFrameActionName,
                MinusSeekFrameActionName,
                LoadYmmtCatalogActionName,
                KeyAActionName,
                SpaceActionName,
            };

            return actions;
        }

        public static void ExecuteUiSwitch(string switchName)
        {
            var config = YMMKeyboardSettings.Current.GetUiButtonConfig(switchName);
            ExecuteAction(config.ActionName, config.Parameter, switchName, "UIキーボード");
        }

        public static void ExecuteUiCombination(IEnumerable<string> switchNames)
        {
            var combinationKey = SwitchLayout.NormalizeCombination(switchNames);
            if (string.IsNullOrWhiteSpace(combinationKey))
                return;

            var config = YMMKeyboardSettings.Current.GetUiComboButtonConfig(combinationKey);
            ExecuteAction(config.ActionName, config.Parameter, combinationKey, "UIキーボード");
        }

        public static void ExecuteDeviceSwitch(string uid, string switchName, KeyEvent? input = null)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return;

            var config = YMMKeyboardSettings.Current.GetDeviceButtonConfig(uid, switchName);
            if (input is not null)
                InputDiagnostics.RecordInputMapped(input, config.ActionName, $"device-switch:{uid}:{switchName}");

            ExecuteAction(config.ActionName, config.Parameter, switchName, uid, input);
        }

        public static void ExecuteAction(string actionName, string? parameter, string switchName, string sourceName, KeyEvent? input = null)
        {
            actionName = NormalizeActionName(actionName);
            var frameCount = ParseFrameCount(parameter);

            if (input is not null)
            {
                InputDiagnostics.RecordMacroResolved(input, actionName, 1, string.IsNullOrWhiteSpace(actionName) ? "noop" : "resolved");
            }

            switch (actionName)
            {
                case TestEventActionName:
                    if (input is not null)
                        InputDiagnostics.RecordDispatchPrepared(input, "message-box", nameof(TestEvent.Execute), $"switch={switchName}; parameter={parameter ?? "(null)"}");
                    ExecuteAndRecordDispatch(
                        input,
                        "message-box",
                        nameof(TestEvent.Execute),
                        $"switch={switchName}; parameter={parameter ?? "(null)"}",
                        () => TestEvent.Execute($"{switchName} ({sourceName})", parameter));
                    break;
                case PlusSeekFrameActionName:
                    if (input is not null)
                        InputDiagnostics.RecordDispatchPrepared(input, "seek-frame", nameof(KeyboardAction.PlusSeekFrame), $"frameCount={frameCount}");
                    ExecuteAndRecordDispatch(
                        input,
                        "seek-frame",
                        nameof(KeyboardAction.PlusSeekFrame),
                        $"frameCount={frameCount}",
                        () => KeyboardAction.PlusSeekFrame(frameCount));
                    break;
                case MinusSeekFrameActionName:
                    if (input is not null)
                        InputDiagnostics.RecordDispatchPrepared(input, "seek-frame", nameof(KeyboardAction.MinusSeekFrame), $"frameCount={frameCount}");
                    ExecuteAndRecordDispatch(
                        input,
                        "seek-frame",
                        nameof(KeyboardAction.MinusSeekFrame),
                        $"frameCount={frameCount}",
                        () => KeyboardAction.MinusSeekFrame(frameCount));
                    break;
                case LoadYmmtCatalogActionName:
                    if (input is not null)
                        InputDiagnostics.RecordDispatchPrepared(input, "catalog-load", nameof(LoadYmmtCatalogAction.Execute), $"switch={switchName}; parameter={parameter ?? "(null)"}");
                    ExecuteAndRecordDispatch(
                        input,
                        "catalog-load",
                        nameof(LoadYmmtCatalogAction.Execute),
                        $"switch={switchName}; parameter={parameter ?? "(null)"}",
                        () => LoadYmmtCatalogAction.Execute($"{switchName} ({sourceName})", parameter));
                    break;
                case KeyAActionName:
                    if (input is not null)
                        InputDiagnostics.RecordDispatchPrepared(input, "key-input", nameof(WindowsInputSender.SendKeyTap), "vk=41");
                    ExecuteAndRecordDispatch(
                        input,
                        "key-input",
                        nameof(WindowsInputSender.SendKeyTap),
                        "vk=41",
                        () => WindowsInputSender.SendKeyTap(0x41));
                    break;
                case SpaceActionName:
                    if (input is not null)
                        InputDiagnostics.RecordDispatchPrepared(input, "key-input", nameof(WindowsInputSender.SendKeyTap), "vk=20");
                    ExecuteAndRecordDispatch(
                        input,
                        "key-input",
                        nameof(WindowsInputSender.SendKeyTap),
                        "vk=20",
                        () => WindowsInputSender.SendKeyTap(0x20));
                    break;
                case NoneActionName:
                case "":
                case null:
                    if (input is not null)
                        InputDiagnostics.RecordDispatchSkipped(input, "none", "noop", $"switch={switchName}", "no action configured");
                    break;
                default:
                    if (warnedUnknownActions.Add(actionName))
                        PluginLogger.Warn("MappingConverter", $"Unsupported action was ignored: {actionName}");
                    if (input is not null)
                        InputDiagnostics.RecordDispatchSkipped(input, "unsupported", actionName, $"switch={switchName}; parameter={parameter ?? "(null)"}", "unsupported action");
                    break;
            }
        }

        private static void ExecuteAndRecordDispatch(KeyEvent? input, string dispatchType, string target, string payloadSummary, Action action)
        {
            try
            {
                action();
                if (input is not null)
                    InputDiagnostics.RecordDispatchExecuted(input, dispatchType, target, payloadSummary, "completed");
            }
            catch (Exception ex)
            {
                PluginLogger.Error("MappingConverter", $"Dispatch execution failed. target={target}; payload={payloadSummary}", ex);
                if (input is not null)
                    InputDiagnostics.RecordDispatchFailed(input, dispatchType, target, payloadSummary, "exception", ex);
            }
        }

        private static void ExecuteAndRecordDispatch(KeyEvent? input, string dispatchType, string target, string payloadSummary, Func<bool> action)
        {
            try
            {
                var success = action();
                if (input is null)
                    return;

                if (success)
                {
                    InputDiagnostics.RecordDispatchExecuted(input, dispatchType, target, payloadSummary, "completed");
                }
                else
                {
                    var detail = target == nameof(WindowsInputSender.SendKeyTap)
                        ? WindowsInputSender.LastFailureDetail ?? $"{target} returned false."
                        : $"{target} returned false.";
                    InputDiagnostics.RecordDispatchFailed(input, dispatchType, target, payloadSummary, "returned-false", new InvalidOperationException(detail));
                }
            }
            catch (Exception ex)
            {
                PluginLogger.Error("MappingConverter", $"Dispatch execution failed. target={target}; payload={payloadSummary}", ex);
                if (input is not null)
                    InputDiagnostics.RecordDispatchFailed(input, dispatchType, target, payloadSummary, "exception", ex);
            }
        }

        private static int ParseFrameCount(string? parameter)
        {
            return int.TryParse(parameter, out var frames) && frames > 0 ? frames : 1;
        }

        public static string NormalizeActionName(string? actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
                return NoneActionName;

            return actionName.Trim() switch
            {
                "なし" => NoneActionName,
                "None" => NoneActionName,
                "テストイベント" => TestEventActionName,
                "TestEvent" => TestEventActionName,
                "テスト本番削除" => TestEventActionName, // 旧設定との互換
                "シークバーを進める" => PlusSeekFrameActionName,
                "PlusSeekFrame" => PlusSeekFrameActionName,
                "シークバーを戻す" => MinusSeekFrameActionName,
                "MinusSeekFrame" => MinusSeekFrameActionName,
                "YMMT読み込み" => LoadYmmtCatalogActionName,
                "ymmt読み込み" => LoadYmmtCatalogActionName,
                "LoadYmmtCatalog" => LoadYmmtCatalogActionName,
                "A" => KeyAActionName,
                "Space" => SpaceActionName,
                _ => actionName.Trim(),
            };
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
