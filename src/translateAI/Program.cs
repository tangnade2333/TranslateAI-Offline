using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace translateAI
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                if (args.Length > 0)
                {
                    switch (args[0].ToLowerInvariant())
                    {
                        case "--selftest": return SelfTest.Run();
                        case "--tray-check": return TrayCheck.Run();
                        case "--popup-check": return PopupCheck.Run();
                        case "--settings-check": return SettingsCheck.Run();
                    }
                }

                // 单实例：第二次启动只提示，不再开一个托盘图标
                bool created;
                using (Mutex mutex = new Mutex(true, @"Local\translateAI-single-instance", out created))
                {
                    if (!created)
                    {
                        MessageBox.Show("translateAI 已经在运行，请查看系统托盘。\r\ntranslateAI is already running (see tray).",
                            "translateAI", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return 0;
                    }

                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new Form1());
                }

                return 0;
            }
            catch (Exception ex)
            {
                // 启动阶段就崩的话，至少留下痕迹
                try
                {
                    string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                    File.AppendAllText(p, DateTime.Now.ToString("s") + " " + ex + Environment.NewLine, Encoding.UTF8);
                }
                catch { }

                MessageBox.Show(ex.ToString(), "translateAI 启动失败 / failed to start",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }
}
