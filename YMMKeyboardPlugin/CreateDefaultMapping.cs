using System;
using System.Collections.Generic;
using System.Diagnostics;
using YMMKeyboardPlugin.MainView;
using YukkuriMovieMaker.UndoRedo;
using YMMKeyboardPlugin.TimelineTool;
namespace YMMKeyboardPlugin
{
    public static class CreateDefaultMapping
    {

        public static Dictionary<int, Action> Mapping(
            string uid,
            TimelineImport mp3Vm)
        {
            Debug.WriteLine($"[Mapping] Create for UID={uid}");

            var map = new Dictionary<int, Action>();

            map[01] = () =>
            {
                Debug.WriteLine("[SW_01] Action START");

                Keymacro.ShowToast("SW-01 実行");

                var vm = TimelineImport.Instance;
                if (vm == null)
                {
                    Debug.WriteLine("[SW_01] Mp3InsertViewModel.Instance is NULL");
                    return;
                }

                Debug.WriteLine("[SW_01] Call InsertMp3()");
                vm.InsertMp3();
            };
            map[02] = () =>
            {

            };

            map[36] = () =>
            {
                TimelineSeek.purasu();
            };

            map[37] = () =>
            {
                TimelineSeek.mainasu();
            };



            return map;
        }
    }
}
