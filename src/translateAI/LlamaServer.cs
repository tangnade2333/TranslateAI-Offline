using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace translateAI
{
    /// <summary>
    /// 把子进程绑进一个 Job 对象，并设置 KILL_ON_JOB_CLOSE。
    /// 这样只要本进程消失（正常退出、崩溃、被任务管理器强杀），llama-server 必定跟着死，
    /// 不会留下吃 1GB 内存的孤儿进程。
    /// </summary>
    internal static class JobObject
    {
        private const int JobObjectExtendedLimitInformation = 9;
        private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string lpName);

        [DllImport("kernel32.dll")]
        private static extern bool SetInformationJobObject(
            IntPtr hJob, int infoClass, IntPtr lpInfo, uint cbInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        private static IntPtr _job = IntPtr.Zero;
        private static readonly object _lock = new object();

        private static IntPtr EnsureJob()
        {
            lock (_lock)
            {
                if (_job != IntPtr.Zero) return _job;

                IntPtr job = CreateJobObject(IntPtr.Zero, null);
                if (job == IntPtr.Zero) return IntPtr.Zero;

                JOBOBJECT_EXTENDED_LIMIT_INFORMATION info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
                info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

                int size = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                IntPtr ptr = Marshal.AllocHGlobal(size);

                try
                {
                    Marshal.StructureToPtr(info, ptr, false);
                    if (!SetInformationJobObject(job, JobObjectExtendedLimitInformation, ptr, (uint)size))
                        return IntPtr.Zero;
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }

                // 故意不关闭句柄：句柄随本进程结束而关闭，那一刻子进程会被系统杀掉
                _job = job;
                return _job;
            }
        }

        /// <summary>把进程放进 Job。失败不抛异常（比如宿主本身已在不可嵌套的 Job 里）。</summary>
        public static bool Attach(IntPtr processHandle)
        {
            try
            {
                IntPtr job = EnsureJob();
                if (job == IntPtr.Zero) return false;
                return AssignProcessToJobObject(job, processHandle);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// 从 exe 所在目录向上找 runtime\llama-server.exe，找到的那一层就是"包根目录"。
    /// 开发期（src\translateAI\bin\Release）和发布期（exe 在包根目录）共用同一套代码。
    /// </summary>
    internal sealed class AppPaths
    {
        public string Root { get; private set; }
        public string RuntimeDir { get; private set; }

        public static AppPaths Discover()
        {
            DirectoryInfo dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

            for (int depth = 0; depth < 10 && dir != null; depth++)
            {
                string cpu = Path.Combine(dir.FullName, "runtime");
                string vulkan = Path.Combine(dir.FullName, "runtime-vulkan");

                bool hasCpu = File.Exists(Path.Combine(cpu, "llama-server.exe"));
                bool hasVulkan = File.Exists(Path.Combine(vulkan, "llama-server.exe"));

                if (hasCpu || hasVulkan)
                {
                    // 优先 Vulkan 构建：没有显卡时它自己会退回 CPU（已实测）
                    return new AppPaths
                    {
                        Root = dir.FullName,
                        RuntimeDir = hasVulkan ? vulkan : cpu
                    };
                }
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "找不到 runtime\\llama-server.exe，已从 exe 所在目录向上查找 10 层。");
        }
    }

    internal sealed class AppConfig
    {
        public string Model = "";
        public int Port = 8787;
        public int Ctx = 2048;
        public int Threads = 0;
        public string Hotkey = "Ctrl+P";
        public string Lang = "zh";

        public static AppConfig Load(string root)
        {
            AppConfig cfg = new AppConfig();
            string path = Path.Combine(root, "config.json");
            if (!File.Exists(path)) return cfg;

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                JavaScriptSerializer ser = new JavaScriptSerializer();
                Dictionary<string, object> map = ser.Deserialize<Dictionary<string, object>>(json);
                if (map == null) return cfg;

                object v;
                if (map.TryGetValue("model", out v) && v is string) cfg.Model = (string)v;
                if (map.TryGetValue("port", out v) && v != null) cfg.Port = Convert.ToInt32(v);
                if (map.TryGetValue("ctx", out v) && v != null) cfg.Ctx = Convert.ToInt32(v);
                if (map.TryGetValue("threads", out v) && v != null) cfg.Threads = Convert.ToInt32(v);
                if (map.TryGetValue("hotkey", out v) && v is string && ((string)v).Trim().Length > 0)
                    cfg.Hotkey = (string)v;
                if (map.TryGetValue("lang", out v) && v is string && ((string)v).Trim().Length > 0)
                    cfg.Lang = (string)v;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("config.json 解析失败 / invalid config.json：" + ex.Message, ex);
            }
            return cfg;
        }

        public void Save(string root)
        {
            Dictionary<string, object> map = new Dictionary<string, object>();
            map["model"] = Model ?? "";
            map["port"] = Port;
            map["ctx"] = Ctx;
            map["threads"] = Threads;
            map["hotkey"] = Hotkey ?? "Ctrl+P";
            map["lang"] = Lang ?? "zh";

            string json = new JavaScriptSerializer().Serialize(map);
            File.WriteAllText(Path.Combine(root, "config.json"), json, new UTF8Encoding(false));
        }

        public string ResolveModel(string root)
        {
            if (!string.IsNullOrWhiteSpace(Model))
            {
                string p = Path.Combine(root, Model.Replace('/', '\\'));
                if (File.Exists(p)) return p;

                string p2 = Path.Combine(Path.Combine(root, "models"), Model);
                if (File.Exists(p2)) return p2;
            }

            string dir = Path.Combine(root, "models");
            if (Directory.Exists(dir))
            {
                string first = Directory.GetFiles(dir, "*.gguf")
                                        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                                        .FirstOrDefault();
                if (first != null) return first;
            }

            throw new FileNotFoundException("models\\ 里没有 .gguf 模型文件。");
        }
    }

    /// <summary>
    /// llama-server 进程的生命周期管理：启动 → 等 /health 就绪 → 释放时关闭。
    /// 如果端口上已经有健康的实例（比如手动起的），就直接复用，不重复拉起。
    /// </summary>
    internal sealed class LlamaServer : IDisposable
    {
        private Process _proc;
        private readonly Queue<string> _tail = new Queue<string>();

        public int Port { get; private set; }
        public bool Owned { get; private set; }

        public string Tail
        {
            get
            {
                lock (_tail) { return string.Join(Environment.NewLine, _tail.ToArray()); }
            }
        }

        public static async Task<LlamaServer> StartAsync(
            AppPaths paths, AppConfig cfg, Action<string> progress, CancellationToken ct)
        {
            LlamaServer server = new LlamaServer();
            server.Port = cfg.Port;

            using (HttpClient probe = new HttpClient())
            {
                probe.Timeout = TimeSpan.FromSeconds(2);
                if (await IsHealthy(probe, cfg.Port))
                {
                    server.Owned = false;
                    if (progress != null) progress("复用已运行的引擎 127.0.0.1:" + cfg.Port);
                    return server;
                }
            }

            string model = cfg.ResolveModel(paths.Root);
            string exe = Path.Combine(paths.RuntimeDir, "llama-server.exe");

            if (progress != null)
                progress("加载 " + Path.GetFileName(model) + " ...");

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = exe;
            psi.Arguments = BuildArguments(model, cfg);
            psi.WorkingDirectory = paths.Root;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;

            Process proc = new Process();
            proc.StartInfo = psi;
            proc.OutputDataReceived += (s, e) => server.PushTail(e.Data);
            proc.ErrorDataReceived += (s, e) => server.PushTail(e.Data);

            proc.Start();
            JobObject.Attach(proc.Handle);
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            server._proc = proc;
            server.Owned = true;

            using (HttpClient http = new HttpClient())
            {
                http.Timeout = TimeSpan.FromSeconds(2);
                Stopwatch sw = Stopwatch.StartNew();

                while (sw.Elapsed < TimeSpan.FromSeconds(90))
                {
                    ct.ThrowIfCancellationRequested();
                    if (proc.HasExited)
                        throw new InvalidOperationException(
                            "引擎异常退出（code " + proc.ExitCode + "）。\n" + server.Tail);

                    if (await IsHealthy(http, cfg.Port)) return server;
                    await Task.Delay(300, ct);
                }
            }

            server.Dispose();
            throw new TimeoutException("引擎 90 秒内没有就绪。\n" + server.Tail);
        }

        private static string BuildArguments(string model, AppConfig cfg)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("--model \"").Append(model).Append("\"");
            sb.Append(" --host 127.0.0.1");
            sb.Append(" --port ").Append(cfg.Port);
            sb.Append(" --ctx-size ").Append(cfg.Ctx);
            sb.Append(" --no-webui");
            sb.Append(" -ngl 99");
            if (cfg.Threads > 0) sb.Append(" -t ").Append(cfg.Threads);
            return sb.ToString();
        }

        private static async Task<bool> IsHealthy(HttpClient http, int port)
        {
            try
            {
                using (HttpResponseMessage r = await http.GetAsync("http://127.0.0.1:" + port + "/health"))
                    return r.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private void PushTail(string line)
        {
            if (line == null) return;
            lock (_tail)
            {
                _tail.Enqueue(line);
                while (_tail.Count > 40) _tail.Dequeue();
            }
        }

        public void Dispose()
        {
            if (_proc == null) return;

            try
            {
                if (Owned && !_proc.HasExited)
                {
                    _proc.Kill();
                    _proc.WaitForExit(5000);
                }
            }
            catch { }

            try { _proc.Dispose(); } catch { }
            _proc = null;
        }
    }
}
