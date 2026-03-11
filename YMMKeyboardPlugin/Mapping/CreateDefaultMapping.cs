using System;
using System.Collections.Generic;
using System.Diagnostics;
using YMMKeyboardPlugin.Models;

namespace YMMKeyboardPlugin.Mapping
{
    public static class CreateDefaultMapping
    {
        public static Dictionary<int, Action> Mapping(string uid)
        {
            Debug.WriteLine($"[Mapping] Create for UID={uid}");

            var map = new Dictionary<int, Action>();
            foreach (var item in SwitchLayout.All)
            {
                var switchName = item.SwitchName;
                map[item.SwitchId] = () => MappingConverter.ExecuteDeviceSwitch(uid, switchName);
            }

            return map;
        }

        /*
        // 旧実装: SW01-SW37 を個別に並べた固定マップ。
        // 現在は SwitchLayout と MappingConverter.ExecuteDeviceSwitch() に統合した。
        */
    }
}
