using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace translateAI
{
    /// <summary>
    /// 托盘行为自检：translateAI.exe --tray-check
    /// 显示窗口 → 模拟点 X → 检查"窗口隐藏且进程未退出"，顺便确认热键注册成功。
    /// </summary>
    internal static class TrayCheck
    {
        public static int Run()
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "traycheck.log");
            try { File.Delete(logPath); } catch { }

            Form1 form = new Form1();

            Timer clickTimer = new Timer();
            clickTimer.Interval = 700;

            Timer verifyTimer = new Timer();
            verifyTimer.Interval = 1800;

            clickTimer.Tick += delegate
            {
                clickTimer.Stop();
                WriteLog(logPath, "step    : 触发 Close()（等价于点 X）");
                form.Close();
            };

            verifyTimer.Tick += delegate
            {
                verifyTimer.Stop();

                bool hidden = !form.Visible;
                bool alive = !form.IsDisposed;
                bool pass = hidden && alive;

                WriteLog(logPath,
                    "visible : " + form.Visible + Environment.NewLine +
                    "disposed: " + form.IsDisposed + Environment.NewLine +
                    "RESULT  : " + (pass ? "PASS" : "FAIL"));

                Application.Exit();
            };

            form.Shown += delegate
            {
                WriteLog(logPath, "step    : 窗口已显示");
                WriteLog(logPath, "hotkey  : registered = " + form.HotkeyRegistered);
                clickTimer.Start();
                verifyTimer.Start();
            };

            Application.Run(form);

            // 如果 Application.Run 提前返回，说明窗口真的被关掉了
            if (!File.Exists(logPath))
            {
                WriteLog(logPath, "RESULT  : FAIL - 窗口被真正关闭，Application.Run 提前返回");
                return 1;
            }

            string text = File.ReadAllText(logPath, Encoding.UTF8);
            return text.Contains("PASS") ? 0 : 1;
        }

        private static void WriteLog(string path, string line)
        {
            try { File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8); }
            catch { }
        }
    }
}
