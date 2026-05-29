global using System;
global using System.IO;
global using System.Diagnostics;
global using System.IO.Compression;
global using System.Text.Json;
global using System.ComponentModel;
global using System.Runtime.CompilerServices;
global using System.Linq;
global using System.Collections.Generic;
global using System.Windows.Input;
global using System.Windows;
global using System.Windows.Controls;
global using Microsoft.Win32;
global using System.Threading.Tasks;
global using System.Reflection;
global using System.Diagnostics.CodeAnalysis;
global using YukkuriMovieMaker.Commons;
global using YukkuriMovieMaker.Plugin;
using YMMKeyboardPlugin.Actions;
using YMMKeyboardPlugin.Key;
using YMMKeyboardPlugin.Logging;
using YMMKeyboardPlugin.Views;

namespace YMMKeyboardPlugin.Plugin
{
    public class MyToolPlugin : IToolPlugin
    {
        public string Name => "キーボードプラグイン";
        public Type ViewModelType => typeof(KeyboardAction);
        public Type ViewType => typeof(KeyboardView);

        private Keymacro? keymacro;

        public MyToolPlugin()
        {
            try
            {
                PluginLogger.ResetOnStartup();
                PluginLogger.Info("MyToolPlugin", "Startup log reset complete.");
                Debug.WriteLine("[MyToolPlugin] Constructor START");
                keymacro = new Keymacro();
                keymacro.Initialize();
                PluginLogger.Info("MyToolPlugin", "Constructor END");
                Debug.WriteLine("[MyToolPlugin] Constructor END");
            }
            catch (Exception ex)
            {
                PluginLogger.Error("MyToolPlugin", "Constructor Exception", ex);
                Debug.WriteLine($"[MyToolPlugin] Constructor Exception: {ex}");
                MessageBox.Show($"USB 接続または COM ポート設定を確認してください。\n{ex}", "キーボードプラグイン");
            }
        }

        /*
        // 旧実装では null 警告対策のため _keymacro を使用していた。
        */
    }
}
