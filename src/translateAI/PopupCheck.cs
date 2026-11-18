using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace translateAI
{
    /// <summary>
    /// 悬浮窗自检：translateAI.exe --popup-check
    /// 检查短文本/长文本的尺寸、置顶、是否出现滚动条、文本是否可选中。
    /// </summary>
    internal static class PopupCheck
    {
        private const string ShortSample =
            "这是一段用于验证悬浮窗尺寸与换行的示例译文，长度大约两到三行，" +
            "用来确认自动宽高、置顶和贴边逻辑是否正常工作。";

        private static readonly string LongSample = BuildLongSample();

        private static string BuildLongSample()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 1; i <= 40; i++)
            {
                sb.Append("第 ").Append(i)
                  .Append(" 段：这一段用来测试超过最大高度时是否出现滚动条，以及内容是否仍然可以选中、可以复制。")
                  .Append("\r\n");
            }
            return sb.ToString();
        }

        public static int Run()
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "popupcheck.log");
            try { File.Delete(logPath); } catch { }

            PopupForm popup = new PopupForm();
            popup.SetText(ShortSample);
            popup.ShowAt(new Point(200, 200));

            Timer timer = new Timer();
            timer.Interval = 600;
            timer.Tick += delegate
            {
                timer.Stop();

                Write(logPath, "visible   : " + popup.Visible);
                Write(logPath, "topmost   : " + popup.TopMost);
                Write(logPath, "selectable: " + popup.TextIsSelectable);
                Write(logPath, "short     : " + popup.Width + " x " + popup.Height + "  scroll=" + popup.HasScrollBar);

                // 换成超长文本，验证封顶与滚动条
                popup.SetText(LongSample);

                Timer t2 = new Timer();
                t2.Interval = 400;
                t2.Tick += delegate
                {
                    t2.Stop();
                    Write(logPath, "long      : " + popup.Width + " x " + popup.Height + "  scroll=" + popup.HasScrollBar);
                    Write(logPath, "RESULT    : " + ((popup.Height <= 420 && popup.HasScrollBar) ? "PASS" : "FAIL"));
                    popup.Close();
                    Application.Exit();
                };
                t2.Start();
            };
            timer.Start();

            Application.Run();

            string text = File.Exists(logPath) ? File.ReadAllText(logPath, Encoding.UTF8) : "";
            return text.Contains("RESULT    : PASS") ? 0 : 1;
        }

        private static void Write(string path, string line)
        {
            try { File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8); }
            catch { }
        }
    }
}
