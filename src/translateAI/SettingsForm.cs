using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace translateAI
{
    /// <summary>设置面板：选模型、录热键。</summary>
    internal sealed class SettingsForm : Form
    {
        private readonly ComboBox _models;
        private readonly TextBox _hotkey;
        private readonly Label _hint;

        public string SelectedModel { get; private set; }
        public string SelectedHotkey { get; private set; }

        internal int ModelCount { get { return _models.Items.Count; } }
        internal string HotkeyText { get { return _hotkey.Text; } }

        public SettingsForm(string root, AppConfig cfg)
        {
            Text = I18n.T("设置", "Settings");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(470, 200);
            Font = new Font("Microsoft YaHei UI", 9F);

            Label lblModel = new Label();
            lblModel.Text = I18n.T("翻译模型", "Model");
            lblModel.Location = new Point(16, 22);
            lblModel.AutoSize = true;

            _models = new ComboBox();
            _models.DropDownStyle = ComboBoxStyle.DropDownList;
            _models.Location = new Point(96, 18);
            _models.Size = new Size(350, 25);

            LoadModels(root, cfg);

            Label lblHotkey = new Label();
            lblHotkey.Text = I18n.T("划词热键", "Hotkey");
            lblHotkey.Location = new Point(16, 68);
            lblHotkey.AutoSize = true;

            _hotkey = new TextBox();
            _hotkey.Location = new Point(96, 64);
            _hotkey.Size = new Size(180, 25);
            _hotkey.ReadOnly = true;
            _hotkey.Text = string.IsNullOrWhiteSpace(cfg.Hotkey) ? "Ctrl+P" : cfg.Hotkey;
            _hotkey.KeyDown += OnHotkeyKeyDown;

            _hint = new Label();
            _hint.Text = I18n.T("点进输入框，直接按组合键", "Click the box and press a combo");
            _hint.Location = new Point(284, 68);
            _hint.AutoSize = true;
            _hint.ForeColor = Color.DimGray;

            Label tip = new Label();
            tip.Text = I18n.T("模型切换后，下一次翻译会重新加载引擎（约 6 秒）。",
                              "After switching models, the engine reloads on next translation (~6s).");
            tip.Location = new Point(16, 108);
            tip.AutoSize = true;
            tip.ForeColor = Color.DimGray;

            Button ok = new Button();
            ok.Text = I18n.T("确定", "OK");
            ok.Location = new Point(276, 148);
            ok.Size = new Size(80, 30);
            ok.Click += delegate
            {
                Commit();
                DialogResult = DialogResult.OK;
                Close();
            };

            Button cancel = new Button();
            cancel.Text = I18n.T("取消", "Cancel");
            cancel.Location = new Point(366, 148);
            cancel.Size = new Size(80, 30);
            cancel.DialogResult = DialogResult.Cancel;

            Controls.Add(lblModel);
            Controls.Add(_models);
            Controls.Add(lblHotkey);
            Controls.Add(_hotkey);
            Controls.Add(_hint);
            Controls.Add(tip);
            Controls.Add(ok);
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        private void LoadModels(string root, AppConfig cfg)
        {
            string dir = Path.Combine(root, "models");
            string current = Path.GetFileName(cfg.Model ?? "");

            if (!Directory.Exists(dir)) return;

            foreach (string file in Directory.GetFiles(dir, "*.gguf"))
            {
                string name = Path.GetFileName(file);
                _models.Items.Add(name);

                if (string.Equals(name, current, StringComparison.OrdinalIgnoreCase))
                    _models.SelectedItem = name;
            }

            if (_models.SelectedIndex < 0 && _models.Items.Count > 0)
                _models.SelectedIndex = 0;
        }

        private void OnHotkeyKeyDown(object sender, KeyEventArgs e)
        {
            e.SuppressKeyPress = true;
            e.Handled = true;

            // 单纯按修饰键时不动，等用户按主键
            if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey ||
                e.KeyCode == Keys.Menu || e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin)
                return;

            if (!HotKey.IsSupportedKey(e.KeyCode))
            {
                _hint.Text = I18n.T("只支持字母、数字、F1–F24", "Letters, digits and F1-F24 only");
                _hint.ForeColor = Color.Firebrick;
                return;
            }

            if (!e.Control && !e.Alt && !e.Shift)
            {
                _hint.Text = I18n.T("至少要带 Ctrl / Alt / Shift", "Needs Ctrl / Alt / Shift");
                _hint.ForeColor = Color.Firebrick;
                return;
            }

            _hotkey.Text = HotKey.Format(e.KeyCode, e.Control, e.Alt, e.Shift);
            _hint.Text = I18n.T("点进输入框，直接按组合键", "Click the box and press a combo");
            _hint.ForeColor = Color.DimGray;
        }

        private void Commit()
        {
            SelectedModel = _models.SelectedItem as string;
            SelectedHotkey = _hotkey.Text.Trim();
        }
    }
}
