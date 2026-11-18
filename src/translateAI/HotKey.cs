using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace translateAI
{
    /// <summary>热键的文本表示与 Win32 常量之间的转换。格式：Ctrl+Alt+P / Ctrl+Shift+F1。</summary>
    internal static class HotKey
    {
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const uint MOD_NOREPEAT = 0x4000;

        public static bool TryParse(string text, out uint mods, out uint vk)
        {
            mods = 0;
            vk = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            foreach (string raw in text.Split('+'))
            {
                string p = raw.Trim();
                if (p.Length == 0) return false;

                switch (p.ToLowerInvariant())
                {
                    case "ctrl":
                    case "control":
                        mods |= MOD_CONTROL;
                        break;
                    case "alt":
                        mods |= MOD_ALT;
                        break;
                    case "shift":
                        mods |= MOD_SHIFT;
                        break;
                    case "win":
                        mods |= MOD_WIN;
                        break;
                    default:
                        if (vk != 0) return false;
                        vk = ToVk(p);
                        if (vk == 0) return false;
                        break;
                }
            }

            // 必须至少带一个修饰键，否则会抢占普通按键
            return vk != 0 && mods != 0;
        }

        public static uint ToVk(string name)
        {
            if (name.Length == 1)
            {
                char c = char.ToUpperInvariant(name[0]);
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) return (uint)c;
                return 0;
            }

            if (name.Length >= 2 && (name[0] == 'F' || name[0] == 'f'))
            {
                int n;
                if (int.TryParse(name.Substring(1), out n) && n >= 1 && n <= 24)
                    return (uint)(0x70 + n - 1);
            }

            return 0;
        }

        public static bool IsSupportedKey(Keys key)
        {
            if (key >= Keys.A && key <= Keys.Z) return true;
            if (key >= Keys.D0 && key <= Keys.D9) return true;
            if (key >= Keys.F1 && key <= Keys.F24) return true;
            return false;
        }

        public static string Format(Keys key, bool ctrl, bool alt, bool shift)
        {
            List<string> parts = new List<string>();
            if (ctrl) parts.Add("Ctrl");
            if (alt) parts.Add("Alt");
            if (shift) parts.Add("Shift");

            string name;
            if (key >= Keys.A && key <= Keys.Z) name = key.ToString();
            else if (key >= Keys.D0 && key <= Keys.D9) name = ((char)('0' + (key - Keys.D0))).ToString();
            else name = key.ToString();

            parts.Add(name);
            return string.Join("+", parts);
        }
    }
}
