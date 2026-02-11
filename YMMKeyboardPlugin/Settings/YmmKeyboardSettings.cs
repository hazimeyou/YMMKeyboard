using System;
using System.Collections.Generic;
using System.Text;

namespace YMMKeyboardPlugin.Settings
{
    public class YMMKeyboardPluginSettings : SettingsBase<YMMKeyboardPluginSettings>
    {
        public override SettingsCategory Category => SettingsCategory.None;
        public override string Name => "マクロキーボード";

        public override bool HasSettingView => true;
        public override object? SettingView => new YmmKeyboardSettingsView();


        public override void Initialize()
        {
        }
    }
}
