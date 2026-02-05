using System;
using System.Collections.Generic;
using System.Diagnostics;

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

            map[12] = () =>
            {
                Debug.WriteLine("[SW_6] Action START");

                Keymacro.ShowToast("SW_6 実行");

                var vm = Mp3InsertViewModel.Instance;
                if (vm == null)
                {
                    Debug.WriteLine("[SW_6] Mp3InsertViewModel.Instance is NULL");
                    return;
                }

                Debug.WriteLine("[SW_6] Call InsertMp3()");
                vm.InsertMp3();
            };


            return map;
        }
    }
}
