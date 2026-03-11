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
                Debug.WriteLine("[MyToolPlugin] Constructor START");
                keymacro = new Keymacro();
                keymacro.Initialize();
                Debug.WriteLine("[MyToolPlugin] Constructor END");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MyToolPlugin] Constructor Exception: {ex}");
                MessageBox.Show($"多分USB刺さってないかCOMポート間違ってる\n{ex}");
            }
        }

        /*
        // 旧実装: null 非許容の _keymacro を使っていた。
        // private Keymacro _keymacro;
        */
    }
}
