using System;
using System.Collections.Generic;
using System.Diagnostics;
using YukkuriMovieMaker.UndoRedo;

namespace YMMKeyboardPlugin
{
    public static class CreateDefaultMapping
    {

        public static Dictionary<int, Action> Mapping(
            string uid,
            Mp3InsertViewModel mp3Vm)
        {
            Debug.WriteLine($"[Mapping] Create for UID={uid}");

            var map = new Dictionary<int, Action>();

            map[01] = () =>
            {
                Debug.WriteLine("[SW_01] Action START");

                Keymacro.ShowToast("SW-01 実行");

                var vm = Mp3InsertViewModel.Instance;
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
                Mp3InsertViewModel.purasu();
            };

            map[37] = () =>
            {
                Mp3InsertViewModel.mainasu();
            };



            return map;
        }
    }
}
