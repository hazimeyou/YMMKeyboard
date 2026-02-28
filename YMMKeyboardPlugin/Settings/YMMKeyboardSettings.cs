using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using YukkuriMovieMaker.Plugin;

namespace YMMKeyboardPlugin.Settings
{
    [DataContract]
    public class ButtonConfig
    {
        [DataMember] public string ActionName { get; set; } = "None";
        [DataMember] public string Parameter { get; set; } = "";
    }

    [DataContract]
    public class YMMKeyboardSettings : SettingsBase<YMMKeyboardSettings>
    {
        public override SettingsCategory Category => SettingsCategory.None;
        public override string Name => "キーボードプラグイン";
        public override bool HasSettingView => true;
        public override object? SettingView => new YMMKeyboardSettingsView();
        [DataMember]
        public Dictionary<string, ButtonConfig> ButtonConfigs { get; set; } = new Dictionary<string, ButtonConfig>();
        public override void Initialize()
        {
            
        }
    }
}
