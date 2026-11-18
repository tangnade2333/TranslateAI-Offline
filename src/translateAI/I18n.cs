using System;

namespace translateAI
{
    internal enum UiLang
    {
        Zh,
        En
    }

    /// <summary>
    /// 极简双语。字符串成对写在调用点：I18n.T("中文", "English")。
    /// 开源项目里这样改文案不用在资源文件之间跳来跳去。
    /// </summary>
    internal static class I18n
    {
        public static UiLang Current = UiLang.Zh;

        public static bool IsEn
        {
            get { return Current == UiLang.En; }
        }

        public static string Code
        {
            get { return IsEn ? "en" : "zh"; }
        }

        public static string T(string zh, string en)
        {
            return IsEn ? en : zh;
        }

        public static void Set(string code)
        {
            Current = string.Equals(code, "en", StringComparison.OrdinalIgnoreCase)
                ? UiLang.En
                : UiLang.Zh;
        }
    }
}
