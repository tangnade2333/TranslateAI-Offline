using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace translateAI
{
    public partial class Form1 : Form
    {
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 0x4D31;

        private const byte VK_CONTROL_BYTE = 0x11;
        private const byte VK_C_BYTE = 0x43;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetClipboardSequenceNumber();

        private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        private LlamaServer _server;

        private NotifyIcon _notify;
        private ToolStripMenuItem _miShow;
        private ToolStripMenuItem _miClipboard;
        private ToolStripMenuItem _miExit;

        private PopupForm _popup;
        private bool _exiting;
        private bool _balloonShown;
        private bool _hotkeyOk;

        private string _root;
        private AppConfig _cfg;
        private string _hotkeyText = "Ctrl+P";

        private System.Windows.Forms.Timer _statusTimer;
        private string _statusPrefix = "翻译中";
        private int _statusDots;

        internal bool HotkeyRegistered
        {
            get { return _hotkeyOk; }
        }

        public Form1()
        {
            InitializeComponent();

            LoadConfig();
            I18n.Set(_cfg != null ? _cfg.Lang : "zh");

            RebuildLanguageCombos();

            cboFrom.SelectedIndex = 0;                 // 自动检测
            cboTo.SelectedIndex = IndexOfCode("zh");   // 中文

            txtInput.Text = "The quick brown fox jumps over the lazy dog.";

            SetupTray();
            ApplyLanguage();
        }

        // ---------- 配置 ----------

        private void LoadConfig()
        {
            try
            {
                AppPaths paths = AppPaths.Discover();
                _root = paths.Root;
                _cfg = AppConfig.Load(_root);

                if (!string.IsNullOrWhiteSpace(_cfg.Hotkey)) _hotkeyText = _cfg.Hotkey;
            }
            catch
            {
                _cfg = new AppConfig();
                _hotkeyText = "Ctrl+P";
            }
        }

        // ---------- 多语言 ----------

        private void ApplyLanguage()
        {
            bool en = I18n.IsEn;

            Text = "translateAI";
            btnTranslate.Text = I18n.T("翻译 (Ctrl+Enter)", "Translate (Ctrl+Enter)");
            btnSettings.Text = I18n.T("设置", "Settings");
            btnLang.Text = en ? "中文" : "English";

            RebuildLanguageCombos();

            if (_miShow != null) _miShow.Text = I18n.T("显示窗口", "Show window");
            if (_miClipboard != null) _miClipboard.Text = I18n.T("翻译剪贴板", "Translate clipboard");
            if (_miExit != null) _miExit.Text = I18n.T("退出", "Exit");

            if (_notify != null)
            {
                _notify.Text = en
                    ? "translateAI (" + _hotkeyText + " selection translate)"
                    : "translateAI（" + _hotkeyText + " 划词翻译）";
            }

            if (_statusTimer == null || !_statusTimer.Enabled)
                UpdateDirectionHint();
        }

        private void RebuildLanguageCombos()
        {
            string fromCode = Languages.Auto;
            string toCode = "zh";

            Lang fromLang = cboFrom.SelectedItem as Lang;
            Lang toLang = cboTo.SelectedItem as Lang;
            if (fromLang != null) fromCode = fromLang.Code;
            if (toLang != null) toCode = toLang.Code;

            cboFrom.BeginUpdate();
            cboTo.BeginUpdate();

            cboFrom.Items.Clear();
            cboTo.Items.Clear();

            foreach (Lang l in Languages.All)
            {
                if (l.Code != Languages.Auto) cboTo.Items.Add(l);
                cboFrom.Items.Add(l);
            }

            cboFrom.SelectedIndex = Math.Max(0, IndexOfCodeIn(cboFrom, fromCode));
            cboTo.SelectedIndex = Math.Max(0, IndexOfCodeIn(cboTo, toCode));

            cboFrom.EndUpdate();
            cboTo.EndUpdate();
        }

        private static int IndexOfCodeIn(ComboBox box, string code)
        {
            for (int i = 0; i < box.Items.Count; i++)
            {
                Lang l = box.Items[i] as Lang;
                if (l != null && l.Code == code) return i;
            }
            return -1;
        }

        private void btnLang_Click(object sender, EventArgs e)
        {
            I18n.Set(I18n.IsEn ? "zh" : "en");
            ApplyLanguage();

            if (_cfg != null && _root != null)
            {
                _cfg.Lang = I18n.Code;
                try { _cfg.Save(_root); }
                catch { }
            }
        }

        // ---------- 状态栏的动画点 ----------

        private void StartStatus(string prefix)
        {
            _statusPrefix = prefix;
            _statusDots = 1;

            if (_statusTimer == null)
            {
                _statusTimer = new System.Windows.Forms.Timer();
                _statusTimer.Interval = 300;
                _statusTimer.Tick += delegate
                {
                    _statusDots = _statusDots % 3 + 1;
                    lblStatus.Text = _statusPrefix + new string('.', _statusDots);
                };
            }

            lblStatus.Text = prefix + ".";
            _statusTimer.Start();
        }

        private void UpdateStatusPrefix(string prefix)
        {
            _statusPrefix = prefix;
        }

        private void StopStatus(string text)
        {
            if (_statusTimer != null) _statusTimer.Stop();
            lblStatus.Text = text;
        }

        // ---------- 全局热键 ----------

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RegisterCurrentHotkey();
        }

        private void RegisterCurrentHotkey()
        {
            uint mods, vk;

            if (!HotKey.TryParse(_hotkeyText, out mods, out vk))
            {
                _hotkeyText = "Ctrl+P";
                if (!HotKey.TryParse(_hotkeyText, out mods, out vk))
                {
                    lblStatus.Text = I18n.T("热键配置无效", "Invalid hotkey");
                    return;
                }
            }

            _hotkeyOk = RegisterHotKey(Handle, HOTKEY_ID, mods | HotKey.MOD_NOREPEAT, vk);

            if (!_hotkeyOk)
            {
                lblStatus.Text = I18n.T(
                    _hotkeyText + " 注册失败（可能被其他程序占用）",
                    _hotkeyText + " failed to register (already in use?)");
            }
        }

        /// <summary>改热键：先注销旧的，注册失败就回滚，不能把用户搞成没热键。</summary>
        private void ApplyHotkey(string text)
        {
            uint mods, vk;
            if (!HotKey.TryParse(text, out mods, out vk))
            {
                lblStatus.Text = I18n.T("热键格式无效：" + text, "Invalid hotkey format: " + text);
                return;
            }

            if (_hotkeyOk)
            {
                UnregisterHotKey(Handle, HOTKEY_ID);
                _hotkeyOk = false;
            }

            if (RegisterHotKey(Handle, HOTKEY_ID, mods | HotKey.MOD_NOREPEAT, vk))
            {
                _hotkeyOk = true;
                _hotkeyText = text;

                if (_notify != null)
                {
                    _notify.Text = I18n.IsEn
                        ? "translateAI (" + text + " selection translate)"
                        : "translateAI（" + text + " 划词翻译）";
                }

                lblStatus.Text = I18n.T("热键已改为 " + text, "Hotkey changed to " + text);
            }
            else
            {
                uint m2, v2;
                if (HotKey.TryParse(_hotkeyText, out m2, out v2))
                    _hotkeyOk = RegisterHotKey(Handle, HOTKEY_ID, m2 | HotKey.MOD_NOREPEAT, v2);

                lblStatus.Text = I18n.T(
                    text + " 被其他程序占用，仍使用 " + _hotkeyText,
                    text + " is taken, still using " + _hotkeyText);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                BeginInvoke(new Action(OnHotkeyPressed));
                return;
            }
            base.WndProc(ref m);
        }

        private void OnHotkeyPressed()
        {
            Point anchor = Cursor.Position;

            // 必须先取词再弹窗：只要我们的窗口出现，键盘焦点就离开原程序了，
            // 这时再发 Ctrl+C 会复制到我们自己身上（剪贴板为空）。
            string selected = CaptureSelection();

            PopupForm popup = NewPopup(anchor);

            if (string.IsNullOrEmpty(selected))
            {
                popup.ShowResult(I18n.T("没取到选中文字", "No selected text"), anchor);
                return;
            }

            popup.ShowBusyNoActivate(anchor, I18n.T("翻译中", "Translating"));
            RunTranslation(popup, selected, anchor);
        }

        private PopupForm NewPopup(Point anchor)
        {
            PopupForm popup = new PopupForm();
            _popup = popup;
            popup.FormClosed += delegate { if (_popup == popup) _popup = null; };
            return popup;
        }

        private async void RunTranslation(PopupForm popup, string text, Point anchor)
        {
            Lang from = cboFrom.SelectedItem as Lang;
            Lang to = cboTo.SelectedItem as Lang;

            if (from == null || to == null)
            {
                popup.ShowResult(I18n.T("语言未选择", "Language not selected"), anchor);
                return;
            }

            try
            {
                popup.TakeFocus();

                if (_server == null)
                {
                    popup.UpdateBusyPrefix(I18n.T("引擎启动中", "Starting engine"));
                    await EnsureServerAsync();
                }

                if (popup.IsDisposed) return;
                popup.UpdateBusyPrefix(I18n.T("翻译中", "Translating"));

                string result = await Translator.TranslateTextAsync(
                    _http, _server.Port, text, from.Code, to.Code,
                    (i, n) => popup.UpdateBusyPrefix(I18n.T("翻译中 ", "Translating ") + i + "/" + n),
                    CancellationToken.None);

                if (popup.IsDisposed) return;
                popup.ShowResult(result, anchor);
            }
            catch (Exception ex)
            {
                if (!popup.IsDisposed)
                    popup.ShowResult(I18n.T("翻译失败：", "Translation failed: ") + ex.Message, anchor);
            }
        }

        private async Task EnsureServerAsync()
        {
            if (_server != null) return;

            AppPaths paths = AppPaths.Discover();
            AppConfig cfg = AppConfig.Load(paths.Root);

            _server = await LlamaServer.StartAsync(paths, cfg, m => { }, CancellationToken.None);
        }

        /// <summary>
        /// 取词：发 Ctrl+C，用剪贴板序列号判断是否复制成功，只读一次剪贴板。
        /// 不能轮询 Clipboard.ContainsText()——别的程序占着剪贴板时每次会阻塞近 200ms。
        /// </summary>
        private string CaptureSelection()
        {
            Diag("capture start fg=" + GetForegroundWindow());
            Stopwatch sw = Stopwatch.StartNew();

            uint before = GetClipboardSequenceNumber();

            SendCtrlC();

            string captured = null;

            for (int i = 0; i < 50; i++)
            {
                Thread.Sleep(20);

                uint now = GetClipboardSequenceNumber();
                if (now == before) continue;

                // 变了，稍等一下让写入方释放剪贴板，只读一次
                Thread.Sleep(30);

                try
                {
                    if (Clipboard.ContainsText()) captured = Clipboard.GetText();
                }
                catch { }

                if (!string.IsNullOrEmpty(captured)) break;

                before = now;
            }

            Diag("capture done len=" + (captured == null ? -1 : captured.Length) + " ms=" + sw.ElapsedMilliseconds);

            if (captured == null) return null;
            return captured.Trim();
        }

        private static void Diag(string msg)
        {
            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "capture.log"),
                    DateTime.Now.ToString("HH:mm:ss.fff") + " " + msg + Environment.NewLine);
            }
            catch { }
        }

        private static void SendCtrlC()
        {
            keybd_event(VK_CONTROL_BYTE, 0, 0, UIntPtr.Zero);
            keybd_event(VK_C_BYTE, 0, 0, UIntPtr.Zero);
            keybd_event(VK_C_BYTE, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL_BYTE, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        // ---------- 托盘 ----------

        private static Icon BuildTrayIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);

                    using (SolidBrush bg = new SolidBrush(Color.FromArgb(45, 137, 239)))
                        g.FillEllipse(bg, 0, 0, 31, 31);

                    using (Font f = new Font("Microsoft YaHei UI", 17F, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (SolidBrush fg = new SolidBrush(Color.White))
                    using (StringFormat sf = new StringFormat())
                    {
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
                        g.DrawString("译", f, fg, new RectangleF(0, 1, 32, 32), sf);
                    }
                }

                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        private void SetupTray()
        {
            _notify = new NotifyIcon();
            _notify.Icon = BuildTrayIcon();
            _notify.Text = "translateAI";
            _notify.Visible = true;
            _notify.DoubleClick += delegate { ShowMain(); };

            ContextMenuStrip menu = new ContextMenuStrip();

            _miShow = new ToolStripMenuItem(I18n.T("显示窗口", "Show window"));
            _miShow.Click += delegate { ShowMain(); };

            _miClipboard = new ToolStripMenuItem(I18n.T("翻译剪贴板", "Translate clipboard"));
            _miClipboard.Click += delegate { TranslateClipboard(); };

            _miExit = new ToolStripMenuItem(I18n.T("退出", "Exit"));
            _miExit.Click += delegate { ExitApp(); };

            menu.Items.Add(_miShow);
            menu.Items.Add(_miClipboard);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_miExit);

            _notify.ContextMenuStrip = menu;
        }

        private void ShowMain()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
        }

        private void HideToTray()
        {
            // 注意：不要在这里改 ShowInTaskbar —— 该属性会重建窗口句柄，
            // 在 FormClosing 期间重建句柄会让关闭流程直接走完。
            Hide();

            if (!_balloonShown)
            {
                _balloonShown = true;
                _notify.ShowBalloonTip(2000, "translateAI",
                    I18n.T("已最小化到托盘。选中文字后按 " + _hotkeyText + " 直接翻译。",
                           "Minimized to tray. Select text and press " + _hotkeyText + " to translate."),
                    ToolTipIcon.Info);
            }
        }

        private void ExitApp()
        {
            _exiting = true;
            Close();
        }

        private void TranslateClipboard()
        {
            string text = "";
            try
            {
                if (Clipboard.ContainsText()) text = Clipboard.GetText().Trim();
            }
            catch { }

            if (text.Length == 0)
            {
                lblStatus.Text = I18n.T("剪贴板里没有文本", "Clipboard has no text");
                return;
            }

            Point anchor = Cursor.Position;
            PopupForm popup = NewPopup(anchor);
            popup.ShowBusyNoActivate(anchor, I18n.T("翻译中", "Translating"));
            RunTranslation(popup, text, anchor);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 点 X 只是藏到托盘；只有托盘"退出"才真正结束
            if (!_exiting && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }
            base.OnFormClosing(e);
        }

        // ---------- 设置 ----------

        private void btnSettings_Click(object sender, EventArgs e)
        {
            if (_root == null)
            {
                MessageBox.Show(this, I18n.T("找不到包根目录（runtime\\llama-server.exe）。",
                                             "Package root not found (runtime\\llama-server.exe)."),
                                "translateAI");
                return;
            }

            AppConfig cfg = AppConfig.Load(_root);

            using (SettingsForm dlg = new SettingsForm(_root, cfg))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                string newModel = dlg.SelectedModel;
                string newHotkey = dlg.SelectedHotkey;

                bool modelChanged = !string.IsNullOrEmpty(newModel) &&
                    !string.Equals(Path.GetFileName(cfg.Model ?? ""), newModel,
                                   StringComparison.OrdinalIgnoreCase);

                bool hotkeyChanged = !string.IsNullOrEmpty(newHotkey) &&
                    !string.Equals(cfg.Hotkey, newHotkey, StringComparison.OrdinalIgnoreCase);

                if (!string.IsNullOrEmpty(newModel)) cfg.Model = "models\\" + newModel;
                if (!string.IsNullOrEmpty(newHotkey)) cfg.Hotkey = newHotkey;
                cfg.Lang = I18n.Code;

                try
                {
                    cfg.Save(_root);
                    _cfg = cfg;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, I18n.T("保存配置失败：" + ex.Message, "Failed to save config: " + ex.Message),
                                    "translateAI");
                }

                if (modelChanged && _server != null)
                {
                    _server.Dispose();
                    _server = null;
                    lblStatus.Text = I18n.T("模型已切换，下次翻译会重新加载",
                                            "Model changed; engine will reload on next translation");
                }

                if (hotkeyChanged) ApplyHotkey(newHotkey);
            }
        }

        // ---------- 主窗口翻译 ----------

        private int IndexOfCode(string code)
        {
            return Math.Max(0, IndexOfCodeIn(cboTo, code));
        }

        private void UpdateDirectionHint()
        {
            Lang from = cboFrom.SelectedItem as Lang;
            Lang to = cboTo.SelectedItem as Lang;
            if (from == null || to == null) return;
            lblStatus.Text = from + " → " + to;
        }

        private void btnSwap_Click(object sender, EventArgs e)
        {
            Lang from = cboFrom.SelectedItem as Lang;
            Lang to = cboTo.SelectedItem as Lang;
            if (from == null || to == null) return;

            if (from.Code == Languages.Auto)
            {
                cboFrom.SelectedItem = to;
                cboTo.SelectedItem = Languages.Find(to.Code == "zh" ? "en" : "zh");
            }
            else
            {
                cboFrom.SelectedItem = to;
                cboTo.SelectedItem = from;
            }

            UpdateDirectionHint();
        }

        private async void btnTranslate_Click(object sender, EventArgs e)
        {
            string text = txtInput.Text.Trim();
            if (text.Length == 0) return;

            Lang from = cboFrom.SelectedItem as Lang;
            Lang to = cboTo.SelectedItem as Lang;
            if (from == null || to == null) return;

            btnTranslate.Enabled = false;
            txtOutput.Text = "";

            try
            {
                if (_server == null)
                {
                    StartStatus(I18n.T("准备引擎", "Preparing engine"));
                    await EnsureServerAsync();
                }

                StartStatus(I18n.T("翻译中", "Translating"));
                Stopwatch sw = Stopwatch.StartNew();

                string result = await Translator.TranslateTextAsync(
                    _http, _server.Port, text, from.Code, to.Code,
                    (i, n) => UpdateStatusPrefix(I18n.T("翻译中 ", "Translating ") + i + "/" + n),
                    CancellationToken.None);

                sw.Stop();

                txtOutput.Text = result;
                StopStatus(from + " → " + to + " · " + sw.ElapsedMilliseconds + " ms");
            }
            catch (Exception ex)
            {
                StopStatus(I18n.T("失败", "Failed"));
                txtOutput.Text = ex.Message;
            }
            finally
            {
                btnTranslate.Enabled = true;
            }
        }

        private void txtInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                btnTranslate.PerformClick();
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try
            {
                if (_hotkeyOk)
                {
                    UnregisterHotKey(Handle, HOTKEY_ID);
                    _hotkeyOk = false;
                }
            }
            catch { }

            if (_statusTimer != null)
            {
                _statusTimer.Stop();
                _statusTimer.Dispose();
                _statusTimer = null;
            }

            if (_notify != null)
            {
                _notify.Visible = false;
                _notify.Dispose();
                _notify = null;
            }

            if (_server != null)
            {
                _server.Dispose();
                _server = null;
            }

            base.OnFormClosed(e);
        }
    }
}
