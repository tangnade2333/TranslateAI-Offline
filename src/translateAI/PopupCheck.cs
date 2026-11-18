using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace translateAI
{
    /// <summary>
    /// 悬浮窗自检：translateAI.exe --popup-check
    /// 弹一个真实悬浮窗，检查尺寸、置顶、位置后自动关闭。
    /// </summary>
    internal static class PopupCheck
    {
        public static int Run()
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "popupcheck.log");
            try { File.Delete(logPath); } catch { }

            PopupForm popup = new PopupForm();

            string sample = "这是一段用于验证悬浮窗尺寸与换行的示例译文，长度大约两到三行，" +
                            "用来确认自动宽高、置顶和贴边逻辑是否正常工作。";

            popup.SetText(sample);
            popup.ShowAt(new Point(200, 200));

            Timer timer = new Timer();
            timer.Interval = 800;
            timer.Tick += delegate
            {
                timer.Stop();

                Write(logPath, "visible : " + popup.Visible);
                Write(logPath, "topmost : " + popup.TopMost);
                Write(logPath, "size    : " + popup.Width + " x " + popup.Height);
                Write(logPath, "location: " + popup.Location.X + "," + popup.Location.Y);

                popup.Close();
                Application.Exit();
            };
            timer.Start();

            Application.Run();

            string text = File.Exists(logPath) ? File.ReadAllText(logPath, Encoding.UTF8) : "";
            return (text.Contains("visible : True") && text.Contains("topmost : True")) ? 0 : 1;
        }

        private static void Write(string path, string line)
        {
            try { File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8); }
            catch { }
        }
    }
}
