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
namespace YMMKeyboardPlugin
{
    public class MyToolPlugin : IToolPlugin
    {
        public string Name => "キーボードプラグイン";
        public Type ViewModelType => typeof(Mp3InsertViewModel);
        public Type ViewType => typeof(Mp3InsertView);

        private Keymacro _keymacro;

        public MyToolPlugin()
        {
            // ★ YMM起動時に必ず呼ばれる
            _keymacro = new Keymacro();
            _keymacro.Initialize(); // ← ここであなたのコードが動く
        }
    }
}
