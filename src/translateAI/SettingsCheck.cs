using System;
using System.IO;
using System.Text;

namespace translateAI
{
    /// <summary>
    /// 设置面板自检：translateAI.exe --settings-check
    /// 确认能扫到模型、读出现有热键、窗口构造不崩，并验证配置读写往返。
    /// </summary>
    internal static class SettingsCheck
    {
        public static int Run()
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settingscheck.log");
            try { File.Delete(logPath); } catch { }

            int code = 1;

            try
            {
                AppPaths paths = AppPaths.Discover();
                AppConfig cfg = AppConfig.Load(paths.Root);
                I18n.Set(cfg.Lang);

                using (SettingsForm f = new SettingsForm(paths.Root, cfg))
                {
                    int count = f.ModelCount;
                    string hot = f.HotkeyText;

                    Write(logPath, "root   : " + paths.Root);
                    Write(logPath, "lang   : " + I18n.Code);
                    Write(logPath, "title  : " + f.Text);
                    Write(logPath, "models : " + count);
                    Write(logPath, "hotkey : " + hot);

                    bool pass = count > 0 && !string.IsNullOrWhiteSpace(hot);

                    // 顺便验证配置读写往返（会把 config.json 升级成含 hotkey/lang 的新格式）
                    cfg.Save(paths.Root);
                    AppConfig reloaded = AppConfig.Load(paths.Root);
                    bool roundTrip = string.Equals(reloaded.Hotkey, cfg.Hotkey) &&
                                     string.Equals(reloaded.Model, cfg.Model) &&
                                     string.Equals(reloaded.Lang, cfg.Lang);

                    Write(logPath, "save   : roundtrip=" + roundTrip + " hotkey=" + reloaded.Hotkey);
                    Write(logPath, "RESULT : " + ((pass && roundTrip) ? "PASS" : "FAIL"));
                    code = (pass && roundTrip) ? 0 : 1;
                }
            }
            catch (Exception ex)
            {
                Write(logPath, "RESULT : FAIL " + ex.Message);
            }

            return code;
        }

        private static void Write(string path, string line)
        {
            try { File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8); }
            catch { }
        }
    }
}
