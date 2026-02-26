using System;
using System.Collections.Generic;
using System.Diagnostics;
using YMMKeyboardPlugin.Key;
using YukkuriMovieMaker.UndoRedo;

namespace YMMKeyboardPlugin.Mapping
{
    public static class CreateDefaultMapping
    {

        public static Dictionary<int, Action> Mapping(
            string uid)
        {
            Debug.WriteLine($"[Mapping] Create for UID={uid}");

            var map = new Dictionary<int, Action>();

            map[01] = () =>
            {
                keyboardViewModel.InsertMp3();
            };
            map[02] = () =>
            {

            };

            map[36] = () =>
            {
                keyboardViewModel.purasu();
            };

            map[37] = () =>
            {
                keyboardViewModel.mainasu();
            };



            return map;
        }
    }
}
