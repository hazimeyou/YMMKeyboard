using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Shapes;

namespace YMMKeyboardPlugin.Settings
{
    public partial class YMMKeyboardSettingsView : UserControl
    {
        public YMMKeyboardSettingsView()
        {

        }
        // ボタン設定を変更する時の例（簡略化しています）
        private void SetButtonAction(string swName, string action, string path = "")
        {
            var settings = YMMKeyboardSettings.Default;
            settings.ButtonConfigs[swName] = new ButtonConfig
            {
                ActionName = action,
                Parameter = path
            };
            settings.Save(); // 設定を保存
        }
        public void File()
        {
            // 初期選択値
            var path = @"";

            // ファイル選択ダイアログ
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "テキストファイル (*.txt)|*.txt|すべてのファイル (*.*)|*.*",
                Multiselect = false,
                Title = "ファイルを選択してください"
            };
            // 初期選択値があれば設定
            if (!string.IsNullOrEmpty(path))
            {
                var info = new FileInfo(path);
                if (Directory.Exists(info.DirectoryName))
                {
                    dlg.InitialDirectory = info.DirectoryName;
                    dlg.FileName = info.Name;
                }
            }
            // ファイル選択ダイアログ表示
            if (dlg.ShowDialog() == true)
            {
                // 選択値で更新
                path = dlg.FileName;
            }
            path = dlg.FileName;
                }
            }
    }

