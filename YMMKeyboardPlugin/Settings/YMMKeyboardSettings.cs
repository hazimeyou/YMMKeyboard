using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using YMMKeyboardPlugin.Mapping;
using YMMKeyboardPlugin.Models;
using YMMKeyboardPlugin.Views;
using YukkuriMovieMaker.Plugin;

namespace YMMKeyboardPlugin.Settings
{
    [DataContract]
    public class ButtonConfig
    {
        [DataMember] public string ActionName { get; set; } = MappingConverter.NoneActionName;
        [DataMember] public string Parameter { get; set; } = string.Empty;
    }

    [DataContract]
    public class YMMKeyboardSettings : SettingsBase<YMMKeyboardSettings>
    {
        private const string SettingsDirectoryName = "settings";
        private const string SettingsFileName = "YMMKeyboardSettings.json";
        private static readonly object saveLock = new();
        private static readonly JsonSerializerOptions jsonSerializerOptions = new()
        {
            WriteIndented = true,
        };

        public static YMMKeyboardSettings Current { get; private set; } = new();
        public static event Action<string>? ConnectionRequested;
        public static event Action<string>? DisconnectionRequested;
        public static event Action? SettingsLoaded;

        public override SettingsCategory Category => SettingsCategory.None;
        public override string Name => "キーボードプラグイン";
        public override bool HasSettingView => true;
        public override object? SettingView => new YMMKeyboardSettingsPanel(this);

        [DataMember] public string PortName { get; set; } = string.Empty;
        [DataMember] public List<string> StartupPortNames { get; set; } = new();
        [DataMember] public List<string> KnownDeviceUids { get; set; } = new();
        [DataMember] public Dictionary<string, ButtonConfig> UiButtonConfigs { get; set; } = new();
        [DataMember] public Dictionary<string, ButtonConfig> UiComboButtonConfigs { get; set; } = new();
        [DataMember] public Dictionary<string, Dictionary<string, ButtonConfig>> DeviceButtonConfigs { get; set; } = new();
        [DataMember] public Dictionary<string, Dictionary<string, ButtonConfig>> DeviceComboButtonConfigs { get; set; } = new();

        public YMMKeyboardSettings()
        {
            Current = this;
            LoadFromPluginDirectory();
        }

        public override void Initialize()
        {
            Current = this;
            LoadFromPluginDirectory();
            NormalizeSettings();
            SettingsLoaded?.Invoke();
        }

        public IReadOnlyList<string> GetKnownDeviceUids()
        {
            NormalizeSettings();
            return KnownDeviceUids.ToArray();
        }

        public IReadOnlyList<string> GetStartupPortNames()
        {
            NormalizeSettings();
            return StartupPortNames.ToArray();
        }

        public string GetSettingsFilePath()
        {
            return Path.Combine(GetSettingsDirectoryPath(), SettingsFileName);
        }

        public void RegisterKnownDeviceUid(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return;

            var added = false;
            if (!KnownDeviceUids.Contains(uid, StringComparer.OrdinalIgnoreCase))
            {
                KnownDeviceUids.Add(uid);
                added = true;
            }

            EnsureDeviceDefaults(uid);
            EnsureDeviceComboStore(uid);

            if (added)
                SaveToPluginDirectory();
        }

        public ButtonConfig GetUiButtonConfig(string switchName)
        {
            NormalizeSettings();
            EnsureUiDefault(switchName);
            return UiButtonConfigs[switchName];
        }

        public void SetUiButtonConfig(string switchName, ButtonConfig config)
        {
            EnsureUiDefault(switchName);
            UiButtonConfigs[switchName] = config;
            SaveToPluginDirectory();
        }

        public ButtonConfig GetUiComboButtonConfig(string combinationKey)
        {
            NormalizeSettings();
            return UiComboButtonConfigs.TryGetValue(combinationKey, out var config)
                ? config
                : new ButtonConfig();
        }

        public void SetUiComboButtonConfig(string combinationKey, ButtonConfig config)
        {
            if (string.IsNullOrWhiteSpace(combinationKey))
                return;

            UiComboButtonConfigs[combinationKey] = config;
            SaveToPluginDirectory();
        }

        public ButtonConfig GetDeviceButtonConfig(string uid, string switchName)
        {
            RegisterKnownDeviceUid(uid);
            EnsureDeviceDefaults(uid);
            return DeviceButtonConfigs[uid][switchName];
        }

        public void SetDeviceButtonConfig(string uid, string switchName, ButtonConfig config)
        {
            RegisterKnownDeviceUid(uid);
            DeviceButtonConfigs[uid][switchName] = config;
            SaveToPluginDirectory();
        }

        public ButtonConfig GetDeviceComboButtonConfig(string uid, string combinationKey)
        {
            RegisterKnownDeviceUid(uid);
            EnsureDeviceComboStore(uid);
            return DeviceComboButtonConfigs[uid].TryGetValue(combinationKey, out var config)
                ? config
                : new ButtonConfig();
        }

        public void SetDeviceComboButtonConfig(string uid, string combinationKey, ButtonConfig config)
        {
            if (string.IsNullOrWhiteSpace(combinationKey))
                return;

            RegisterKnownDeviceUid(uid);
            EnsureDeviceComboStore(uid);
            DeviceComboButtonConfigs[uid][combinationKey] = config;
            SaveToPluginDirectory();
        }

        public void UpdatePortName(string portName)
        {
            PortName = portName;
            SaveToPluginDirectory();
        }

        public void AddStartupPort(string portName)
        {
            if (string.IsNullOrWhiteSpace(portName))
                return;

            if (!StartupPortNames.Contains(portName, StringComparer.OrdinalIgnoreCase))
            {
                StartupPortNames.Add(portName);
                SaveToPluginDirectory();
            }
        }

        public void RemoveStartupPort(string portName)
        {
            var existing = StartupPortNames.FirstOrDefault(name => string.Equals(name, portName, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                StartupPortNames.Remove(existing);
                SaveToPluginDirectory();
            }
        }

        public void RequestConnection()
        {
            if (!string.IsNullOrWhiteSpace(PortName))
                ConnectionRequested?.Invoke(PortName);
        }

        public void RequestDisconnection()
        {
            if (!string.IsNullOrWhiteSpace(PortName))
                DisconnectionRequested?.Invoke(PortName);
        }

        private void LoadFromPluginDirectory()
        {
            try
            {
                var path = GetSettingsFilePath();
                if (!File.Exists(path))
                {
                    NormalizeSettings();
                    return;
                }

                var json = File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<PersistedSettings>(json);
                if (data is null)
                    return;

                PortName = data.PortName ?? string.Empty;
                StartupPortNames = data.StartupPortNames ?? new();
                KnownDeviceUids = data.KnownDeviceUids ?? new();
                UiButtonConfigs = data.UiButtonConfigs ?? new();
                UiComboButtonConfigs = data.UiComboButtonConfigs ?? new();
                DeviceButtonConfigs = data.DeviceButtonConfigs ?? new();
                DeviceComboButtonConfigs = data.DeviceComboButtonConfigs ?? new();
                NormalizeSettings();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[YMMKeyboardSettings] Load failed: {ex}");
            }
        }

        private void SaveToPluginDirectory()
        {
            try
            {
                NormalizeSettings();
                PersistedSettings data;
                string settingsFilePath;
                string tempFilePath;

                lock (saveLock)
                {
                    var directory = GetSettingsDirectoryPath();
                    Directory.CreateDirectory(directory);
                    settingsFilePath = GetSettingsFilePath();
                    tempFilePath = settingsFilePath + ".tmp";

                    data = new PersistedSettings
                    {
                        PortName = PortName,
                        StartupPortNames = StartupPortNames.ToList(),
                        KnownDeviceUids = KnownDeviceUids.ToList(),
                        UiButtonConfigs = CloneConfigs(UiButtonConfigs),
                        UiComboButtonConfigs = CloneConfigs(UiComboButtonConfigs),
                        DeviceButtonConfigs = CloneNestedConfigs(DeviceButtonConfigs),
                        DeviceComboButtonConfigs = CloneNestedConfigs(DeviceComboButtonConfigs),
                    };

                    var json = JsonSerializer.Serialize(data, jsonSerializerOptions);
                    File.WriteAllText(tempFilePath, json);

                    if (File.Exists(settingsFilePath))
                        File.Copy(tempFilePath, settingsFilePath, overwrite: true);
                    else
                        File.Move(tempFilePath, settingsFilePath);

                    if (File.Exists(tempFilePath))
                        File.Delete(tempFilePath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[YMMKeyboardSettings] Save failed: {ex}");
            }
        }

        private string GetSettingsDirectoryPath()
        {
            var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                ?? AppContext.BaseDirectory;
            return Path.Combine(assemblyDirectory, SettingsDirectoryName);
        }

        private void NormalizeSettings()
        {
            StartupPortNames = StartupPortNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            KnownDeviceUids = KnownDeviceUids
                .Where(uid => !string.IsNullOrWhiteSpace(uid))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            UiComboButtonConfigs = UiComboButtonConfigs
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

            NormalizeActionNames(UiButtonConfigs);
            NormalizeActionNames(UiComboButtonConfigs);

            foreach (var item in SwitchLayout.All)
                EnsureUiDefault(item.SwitchName);

            foreach (var uid in KnownDeviceUids)
            {
                EnsureDeviceDefaults(uid);
                EnsureDeviceComboStore(uid);
                NormalizeActionNames(DeviceButtonConfigs[uid]);
                NormalizeActionNames(DeviceComboButtonConfigs[uid]);
            }
        }

        private static void NormalizeActionNames(Dictionary<string, ButtonConfig> configs)
        {
            foreach (var pair in configs)
            {
                pair.Value.ActionName = MappingConverter.NormalizeActionName(pair.Value.ActionName);
            }
        }

        private void EnsureUiDefault(string switchName)
        {
            if (!UiButtonConfigs.ContainsKey(switchName))
                UiButtonConfigs[switchName] = new ButtonConfig();
        }

        private void EnsureDeviceDefaults(string uid)
        {
            if (!DeviceButtonConfigs.TryGetValue(uid, out var configs))
            {
                configs = new Dictionary<string, ButtonConfig>(StringComparer.OrdinalIgnoreCase);
                DeviceButtonConfigs[uid] = configs;
            }

            foreach (var item in SwitchLayout.All)
            {
                if (!configs.ContainsKey(item.SwitchName))
                    configs[item.SwitchName] = new ButtonConfig();
            }
        }

        private void EnsureDeviceComboStore(string uid)
        {
            if (!DeviceComboButtonConfigs.ContainsKey(uid))
                DeviceComboButtonConfigs[uid] = new Dictionary<string, ButtonConfig>(StringComparer.OrdinalIgnoreCase);
        }

        private static Dictionary<string, ButtonConfig> CloneConfigs(Dictionary<string, ButtonConfig> source)
        {
            return source.ToDictionary(
                pair => pair.Key,
                pair => new ButtonConfig
                {
                    ActionName = pair.Value.ActionName,
                    Parameter = pair.Value.Parameter,
                },
                StringComparer.OrdinalIgnoreCase);
        }

        private static Dictionary<string, Dictionary<string, ButtonConfig>> CloneNestedConfigs(
            Dictionary<string, Dictionary<string, ButtonConfig>> source)
        {
            return source.ToDictionary(
                pair => pair.Key,
                pair => CloneConfigs(pair.Value),
                StringComparer.OrdinalIgnoreCase);
        }

        private class PersistedSettings
        {
            public string? PortName { get; set; }
            public List<string>? StartupPortNames { get; set; }
            public List<string>? KnownDeviceUids { get; set; }
            public Dictionary<string, ButtonConfig>? UiButtonConfigs { get; set; }
            public Dictionary<string, ButtonConfig>? UiComboButtonConfigs { get; set; }
            public Dictionary<string, Dictionary<string, ButtonConfig>>? DeviceButtonConfigs { get; set; }
            public Dictionary<string, Dictionary<string, ButtonConfig>>? DeviceComboButtonConfigs { get; set; }
        }
    }
}
