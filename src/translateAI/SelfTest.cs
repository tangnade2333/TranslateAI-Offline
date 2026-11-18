using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace translateAI
{
    /// <summary>
    /// 无界面自检：translateAI.exe --selftest
    /// 启动引擎 → 按两个方向各翻一句 + 一个多行样例 → 边跑边写 selftest.log。
    /// </summary>
    internal static class SelfTest
    {
        private sealed class Case
        {
            public string Name;
            public string From;
            public string To;
            public string Text;
        }

        private static readonly Case[] Cases = new Case[]
        {
            new Case { Name = "auto -> zh", From = "auto", To = "zh",
                       Text = "The quick brown fox jumps over the lazy dog." },
            new Case { Name = "zh -> en",   From = "zh",   To = "en",
                       Text = "这个功能在旧版本里是坏的，升级之后才恢复正常。" },
            new Case { Name = "多行保换行", From = "auto", To = "zh",
                       Text = "The device ships with a 12-month warranty.\n\nPlease keep the receipt for claims." }
        };

        public static int Run()
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest.log");
            int exitCode = 1;
            StreamWriter log = null;

            try
            {
                log = new StreamWriter(logPath, false, new UTF8Encoding(false));
                log.AutoFlush = true;

                log.WriteLine("start   : " + DateTime.Now.ToString("HH:mm:ss"));
                log.WriteLine("base    : " + AppDomain.CurrentDomain.BaseDirectory);

                Stopwatch total = Stopwatch.StartNew();

                AppPaths paths = AppPaths.Discover();
                AppConfig cfg = AppConfig.Load(paths.Root);

                log.WriteLine("root    : " + paths.Root);
                log.WriteLine("runtime : " + paths.RuntimeDir);
                log.WriteLine("port    : " + cfg.Port);
                log.WriteLine("ctx     : " + cfg.Ctx);

                using (HttpClient http = new HttpClient())
                {
                    http.Timeout = TimeSpan.FromMinutes(5);

                    LlamaServer server = LlamaServer.StartAsync(
                        paths, cfg, m => log.WriteLine("  " + m), CancellationToken.None)
                        .GetAwaiter().GetResult();

                    using (server)
                    {
                        log.WriteLine("server  : ready in " + total.ElapsedMilliseconds + " ms, owned=" + server.Owned);

                        int pass = 0;
                        foreach (Case c in Cases)
                        {
                            Stopwatch sw = Stopwatch.StartNew();
                            string result = Translator.TranslateTextAsync(
                                http, server.Port, c.Text, c.From, c.To, null, CancellationToken.None)
                                .GetAwaiter().GetResult();
                            sw.Stop();

                            bool ok = !string.IsNullOrWhiteSpace(result);
                            if (ok) pass++;

                            int lineCount = result.Split(new string[] { "\r\n" }, StringSplitOptions.None).Length;

                            log.WriteLine("");
                            log.WriteLine("[" + c.Name + "] " + sw.ElapsedMilliseconds + " ms  " + (ok ? "OK" : "EMPTY")
                                          + "  lines=" + lineCount);
                            log.WriteLine("  in  : " + c.Text.Replace("\n", " / "));
                            log.WriteLine("  out : " + result.Replace("\r\n", " / "));
                        }

                        exitCode = (pass == Cases.Length) ? 0 : 1;
                        log.WriteLine("");
                        log.WriteLine(exitCode == 0
                            ? "RESULT  : PASS (" + pass + "/" + Cases.Length + ")"
                            : "RESULT  : FAIL (" + pass + "/" + Cases.Length + ")");
                    }
                }
            }
            catch (Exception ex)
            {
                exitCode = 1;
                if (log != null)
                {
                    log.WriteLine("RESULT  : FAIL");
                    log.WriteLine(ex.ToString());
                }
            }
            finally
            {
                if (log != null) { try { log.Dispose(); } catch { } }
            }

            return exitCode;
        }
    }
}
