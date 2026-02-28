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
                MappingConverter.SW01();
            };
            map[02] = () =>
            {
                MappingConverter.SW02();
            };
            map[03] = () =>
            {
                MappingConverter.SW03();
            };
            map[04] = () =>
            {
                MappingConverter.SW04();
            };
            map[05] = () =>
            {
                MappingConverter.SW05();
            };
            map[06] = () =>
            {
                MappingConverter.SW06();
            };
            map[08] = () =>
            {
                MappingConverter.SW07();
            };
            map[09] = () =>
            {
                MappingConverter.SW08();
            };
            map[10] = () =>
            {
                MappingConverter.SW09();
            };
            map[11] = () =>
            {
                MappingConverter.SW10();
            };
            map[12] = () =>
            {
                MappingConverter.SW11();
            };
            map[13] = () =>
            {
                MappingConverter.SW12();
            };
            map[15] = () =>
            {
                MappingConverter.SW13();
            };
            map[16] = () =>
            {
                MappingConverter.SW14();
            };
            map[17] = () =>
            {
                MappingConverter.SW15();
            };

            map[18] = () =>
            {
                MappingConverter.SW16();
            };
            map[19] = () =>
            {
                MappingConverter.SW17();
            };
            map[20] = () =>
            {
                MappingConverter.SW18();
            };
            map[22] = () =>
            {
                MappingConverter.SW19();
            };
            map[23] = () =>
            {
                MappingConverter.SW20();
            };
            map[24] = () =>
            {
                MappingConverter.SW21();
            };
            map[25] = () =>
            {
                MappingConverter.SW22();
            };
            map[26] = () =>
            {
                MappingConverter.SW23();
            };
            map[27] = () =>
            {
                MappingConverter.SW24();
            };
            map[29] = () =>
            {
                MappingConverter.SW25();
            };
            map[30] = () =>
            {
                MappingConverter.SW26();
            };
            map[31] = () =>
            {
                MappingConverter.SW27();
            };
            map[32] = () =>
            {
                MappingConverter.SW28();
            };
            map[33] = () =>
            {
                MappingConverter.SW29();
            };
            map[34] = () =>
            {
                MappingConverter.SW30();
            };
            map[35] = () =>
            {
                MappingConverter.SW35();
            };
            map[36] = () =>
            {
                MappingConverter.SW37();
            };
            map[37] = () =>
            {
                MappingConverter.SW36();
            };
            return map;
        }
    }
}
