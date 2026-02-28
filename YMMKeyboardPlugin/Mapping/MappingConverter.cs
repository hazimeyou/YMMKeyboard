using System;
using System.Collections.Generic;
using System.Text;
using YMMKeyboardPlugin.Settings;

namespace YMMKeyboardPlugin.Mapping
{
     public class MappingConverter
    {
        public static void SW01() => Execute("SW01");
        public static void SW02()
        {
            MessageBox.Show("SW02");
        }
        public static void SW03()
        {
            MessageBox.Show("SW03");
        }
        public static void SW04()
        {
            MessageBox.Show("SW04");
        }
        public static void SW05()
        {
            MessageBox.Show("SW05");
        }
        public static void SW06()
        {
            MessageBox.Show("SW06");
        }
        public static void SW07()
        {
            MessageBox.Show("SW07");
        }
        public static void SW08()
        {
            MessageBox.Show("SW08");
        }
        public static void SW09()
        {
            MessageBox.Show("SW09");
        }
        public static void SW10()
        {
            MessageBox.Show("SW10");
        }
        public static void SW11()
        {
            MessageBox.Show("SW11");
        }
        public static void SW12()
        {
            MessageBox.Show("SW12");
        }
        public static void SW13()
        {
            MessageBox.Show("SW13");
        }
        public static void SW14()
        {
            MessageBox.Show("SW14");
        }
        public static void SW15()
        {
            MessageBox.Show("SW15");
        }
        public static void SW16()
        {
            MessageBox.Show("SW16");
        }
        public static void SW17()
        {
            MessageBox.Show("SW17");
        }
        public static void SW18()
        {
            MessageBox.Show("SW18");
        }
        public static void SW19()
        {
            MessageBox.Show("SW19");
        }
        public static void SW20()
        {
            MessageBox.Show("SW20");
        }
        public static void SW21()
        {
            MessageBox.Show("SW21");
        }
        public static void SW22()
        {
            MessageBox.Show("SW22");
        }
        public static void SW23()
        {
            MessageBox.Show("SW23");
        }
        public static void SW24()
        {
            MessageBox.Show("SW24");
        }
        public static void SW25()
        {
            MessageBox.Show("SW25");
        }
        public static void SW26()
        {
            MessageBox.Show("SW26");
        }
        public static void SW27()
        {
            MessageBox.Show("SW27");
        }
        public static void SW28()
        {
            MessageBox.Show("SW28");
        }
        public static void SW29()
        {
            MessageBox.Show("SW29");
        }
        public static void SW30()
        {
            MessageBox.Show("SW30");
        }
        public static void SW35()
        {
            MessageBox.Show("SW35");
        }
        public static void SW36()
        {
            MessageBox.Show("SW36");
        }
        public static void SW37()
        {
            MessageBox.Show("SW37");
        }
        private static void Execute(string swName)
        {
            // 1. 設定を取得
            var config = YMMKeyboardSettings.Default.GetConfig(swName);

            // 2. アクション名に応じて分岐
            switch (config.ActionName)
            {
                case "InsertMp3":
                    // オプション（Parameter）に入っているパスを使う
                    keyboardViewModel.InsertMp3(config.Parameter);
                    break;

                case "Copy":
                    // コピー処理など
                    break;

                default:
                    MessageBox.Show($"{swName} には何も割り当てられていません");
                    break;
            }
        }
    }
}
