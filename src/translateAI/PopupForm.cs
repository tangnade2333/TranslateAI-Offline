using System;
using System.Drawing;
using System.Windows.Forms;

namespace translateAI
{
    /// <summary>
    /// 划词翻译的悬浮窗：无边框、置顶、贴鼠标弹出，Esc 或点击别处即关闭。
    /// </summary>
    internal sealed class PopupForm : Form
    {
        private const int Pad = 12;
        private const int MaxWidth = 520;

        private readonly Label _text;
        private Timer _busyTimer;
        private Timer _autoCloseTimer;
        private string _busyPrefix = "翻译中";
        private int _dots;
        private bool _sizeLocked;

        public PopupForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.White;
            DoubleBuffered = true;
            KeyPreview = true;

            _text = new Label();
            _text.AutoSize = true;
            _text.MaximumSize = new Size(MaxWidth - Pad * 2, 0);
            _text.Font = new Font("Microsoft YaHei UI", 10.5F);
            _text.ForeColor = Color.FromArgb(30, 30, 30);
            _text.BackColor = Color.White;
            _text.Location = new Point(Pad, Pad);
            Controls.Add(_text);
        }

        /// <summary>
        /// Show() 时不抢焦点：内部就是 SW_SHOWNOACTIVATE。
        /// 注意这里没有设 WS_EX_NOACTIVATE，所以之后 Activate() 依然有效。
        /// </summary>
        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        // ---------- 忙碌态（带动画点） ----------

        public void ShowBusyNoActivate(Point anchor, string prefix)
        {
            _busyPrefix = prefix;
            _dots = 1;
            _sizeLocked = false;

            SetBusyText(prefix + ".");

            PlaceAt(anchor);

            if (!Visible) Show();

            if (_busyTimer == null)
            {
                _busyTimer = new Timer();
                _busyTimer.Interval = 300;
                _busyTimer.Tick += OnBusyTick;
            }
            _busyTimer.Start();

            ArmAutoClose();
        }

        /// <summary>兜底：窗口始终没拿到焦点时，Esc 和失焦关闭都不会触发，20 秒后自动关。</summary>
        private void ArmAutoClose()
        {
            if (_autoCloseTimer == null)
            {
                _autoCloseTimer = new Timer();
                _autoCloseTimer.Interval = 20000;
                _autoCloseTimer.Tick += delegate { Close(); };
            }
            _autoCloseTimer.Stop();
            _autoCloseTimer.Start();
        }

        public void UpdateBusyPrefix(string prefix)
        {
            _busyPrefix = prefix;
        }

        private void OnBusyTick(object sender, EventArgs e)
        {
            if (!Visible) return;
            _dots = _dots % 3 + 1;
            SetBusyText(_busyPrefix + new string('.', _dots));
        }

        private void SetBusyText(string text)
        {
            if (!_sizeLocked)
            {
                // 用最长形态（三个点）定尺寸，动画过程中窗口不会一跳一跳
                _text.MaximumSize = new Size(MaxWidth - Pad * 2, 0);
                _text.Text = _busyPrefix + "...";
                Size pref = _text.PreferredSize;
                ClientSize = new Size(Math.Max(120, pref.Width + Pad * 2), pref.Height + Pad * 2);
                _sizeLocked = true;
            }
            _text.Text = text;
        }

        private void StopBusy()
        {
            if (_busyTimer != null) _busyTimer.Stop();
        }

        /// <summary>显示最终结果，并激活窗口（这样 Esc / 点外面能关）。</summary>
        public void ShowResult(string text, Point anchor)
        {
            StopBusy();
            _sizeLocked = false;
            SetText(text);
            PlaceAt(anchor);

            if (!Visible) Show();
            Activate();
            BringToFront();

            ArmAutoClose();
        }

        /// <summary>取词完成后调用：把焦点拿过来，让 Esc 生效。</summary>
        public void TakeFocus()
        {
            if (!Visible) Show();
            Activate();
            BringToFront();
        }

        // ---------- 布局 ----------

        public void SetText(string text)
        {
            _text.MaximumSize = new Size(MaxWidth - Pad * 2, 0);
            _text.Text = text ?? "";

            Size pref = _text.PreferredSize;
            int w = Math.Min(MaxWidth, pref.Width + Pad * 2);
            int h = pref.Height + Pad * 2;

            ClientSize = new Size(Math.Max(80, w), Math.Max(40, h));
        }

        public void ShowAt(Point anchor)
        {
            PlaceAt(anchor);
            if (!Visible) Show();
            Activate();
            BringToFront();
        }

        private void PlaceAt(Point anchor)
        {
            Rectangle wa = Screen.FromPoint(anchor).WorkingArea;

            int x = anchor.X + 16;
            int y = anchor.Y + 20;

            if (x + Width > wa.Right) x = wa.Right - Width - 8;
            if (y + Height > wa.Bottom) y = anchor.Y - Height - 12;
            if (x < wa.Left) x = wa.Left + 8;
            if (y < wa.Top) y = wa.Top + 8;

            Location = new Point(x, y);
        }

        // ---------- 交互 ----------

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            using (Pen border = new Pen(Color.FromArgb(200, 200, 200)))
                e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);

            using (Pen accent = new Pen(Color.FromArgb(45, 137, 239)))
                e.Graphics.DrawLine(accent, 1, 1, Width - 2, 1);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_busyTimer != null)
            {
                _busyTimer.Stop();
                _busyTimer.Dispose();
                _busyTimer = null;
            }

            if (_autoCloseTimer != null)
            {
                _autoCloseTimer.Stop();
                _autoCloseTimer.Dispose();
                _autoCloseTimer = null;
            }

            base.OnFormClosed(e);
        }
    }
}
