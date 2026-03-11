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
using YMMKeyboardPlugin.Key;

namespace YMMKeyboardPlugin
{
    public class MyToolPlugin : IToolPlugin
    {
        public string Name => "キーボードプラグイン";
        public Type ViewModelType => typeof(KeyboardAction);
        public Type ViewType => typeof(KeyboardView);

        private Keymacro _keymacro;

        public MyToolPlugin()
        {
            try
            {
                Debug.WriteLine("[MyToolPlugin] Constructor START");
                _keymacro = new Keymacro();
                _keymacro.Initialize();
                Debug.WriteLine("[MyToolPlugin] Constructor END");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MyToolPlugin] Constructor Exception: {ex}");
                MessageBox.Show($"多分USB刺さってないかCOMポート間違ってる\n{ex}");
            }
        }
    }
}
