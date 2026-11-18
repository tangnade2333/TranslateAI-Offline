namespace translateAI
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.ComboBox cboFrom;
        private System.Windows.Forms.Button btnSwap;
        private System.Windows.Forms.ComboBox cboTo;
        private System.Windows.Forms.TextBox txtInput;
        private System.Windows.Forms.Button btnTranslate;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.TextBox txtOutput;
        private System.Windows.Forms.Button btnSettings;
        private System.Windows.Forms.Button btnLang;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.cboFrom = new System.Windows.Forms.ComboBox();
            this.btnSwap = new System.Windows.Forms.Button();
            this.cboTo = new System.Windows.Forms.ComboBox();
            this.txtInput = new System.Windows.Forms.TextBox();
            this.btnTranslate = new System.Windows.Forms.Button();
            this.lblStatus = new System.Windows.Forms.Label();
            this.txtOutput = new System.Windows.Forms.TextBox();
            this.btnSettings = new System.Windows.Forms.Button();
            this.btnLang = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // cboFrom
            // 
            this.cboFrom.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboFrom.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.cboFrom.Location = new System.Drawing.Point(12, 12);
            this.cboFrom.Name = "cboFrom";
            this.cboFrom.Size = new System.Drawing.Size(130, 25);
            this.cboFrom.TabIndex = 0;
            // 
            // btnSwap
            // 
            this.btnSwap.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.btnSwap.Location = new System.Drawing.Point(148, 11);
            this.btnSwap.Name = "btnSwap";
            this.btnSwap.Size = new System.Drawing.Size(44, 26);
            this.btnSwap.TabIndex = 1;
            this.btnSwap.Text = "⇄";
            this.btnSwap.UseVisualStyleBackColor = true;
            this.btnSwap.Click += new System.EventHandler(this.btnSwap_Click);
            // 
            // cboTo
            // 
            this.cboTo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboTo.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.cboTo.Location = new System.Drawing.Point(198, 12);
            this.cboTo.Name = "cboTo";
            this.cboTo.Size = new System.Drawing.Size(130, 25);
            this.cboTo.TabIndex = 2;
            // 
            // txtInput
            // 
            this.txtInput.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtInput.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.txtInput.Location = new System.Drawing.Point(12, 48);
            this.txtInput.Multiline = true;
            this.txtInput.Name = "txtInput";
            this.txtInput.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtInput.Size = new System.Drawing.Size(776, 174);
            this.txtInput.TabIndex = 3;
            this.txtInput.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtInput_KeyDown);
            // 
            // btnTranslate
            // 
            this.btnTranslate.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.btnTranslate.Location = new System.Drawing.Point(12, 228);
            this.btnTranslate.Name = "btnTranslate";
            this.btnTranslate.Size = new System.Drawing.Size(150, 34);
            this.btnTranslate.TabIndex = 4;
            this.btnTranslate.Text = "翻译 (Ctrl+Enter)";
            this.btnTranslate.UseVisualStyleBackColor = true;
            this.btnTranslate.Click += new System.EventHandler(this.btnTranslate_Click);
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblStatus.ForeColor = System.Drawing.Color.DimGray;
            this.lblStatus.Location = new System.Drawing.Point(168, 237);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(32, 17);
            this.lblStatus.TabIndex = 5;
            this.lblStatus.Text = "就绪";
            // 
            // txtOutput
            // 
            this.txtOutput.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtOutput.BackColor = System.Drawing.Color.White;
            this.txtOutput.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.txtOutput.Location = new System.Drawing.Point(12, 268);
            this.txtOutput.Multiline = true;
            this.txtOutput.Name = "txtOutput";
            this.txtOutput.ReadOnly = true;
            this.txtOutput.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtOutput.Size = new System.Drawing.Size(776, 240);
            this.txtOutput.TabIndex = 6;
            // 
            // btnSettings
            // 
            this.btnSettings.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSettings.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.btnSettings.Location = new System.Drawing.Point(700, 10);
            this.btnSettings.Name = "btnSettings";
            this.btnSettings.Size = new System.Drawing.Size(88, 28);
            this.btnSettings.TabIndex = 7;
            this.btnSettings.Text = "设置";
            this.btnSettings.UseVisualStyleBackColor = true;
            this.btnSettings.Click += new System.EventHandler(this.btnSettings_Click);
            // 
            // btnLang
            // 
            this.btnLang.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnLang.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.btnLang.Location = new System.Drawing.Point(604, 10);
            this.btnLang.Name = "btnLang";
            this.btnLang.Size = new System.Drawing.Size(88, 28);
            this.btnLang.TabIndex = 8;
            this.btnLang.Text = "English";
            this.btnLang.UseVisualStyleBackColor = true;
            this.btnLang.Click += new System.EventHandler(this.btnLang_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 520);
            this.Controls.Add(this.btnLang);
            this.Controls.Add(this.btnSettings);
            this.Controls.Add(this.txtOutput);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.btnTranslate);
            this.Controls.Add(this.txtInput);
            this.Controls.Add(this.cboTo);
            this.Controls.Add(this.btnSwap);
            this.Controls.Add(this.cboFrom);
            this.MinimumSize = new System.Drawing.Size(620, 420);
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "translateAI";
            this.ResumeLayout(false);
            this.PerformLayout();

        }
    }
}
