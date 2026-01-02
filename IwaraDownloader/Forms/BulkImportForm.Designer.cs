namespace IwaraDownloader.Forms
{
    partial class BulkImportForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.lblDescription = new Label();
            this.txtUrls = new TextBox();
            this.btnPaste = new Button();
            this.btnLoadFile = new Button();
            this.btnClear = new Button();
            this.lblStats = new Label();
            this.progressBar = new ProgressBar();
            this.btnImport = new Button();
            this.btnCancel = new Button();
            this.lblHelp = new Label();
            this.SuspendLayout();

            // 
            // lblDescription
            // 
            this.lblDescription.AutoSize = true;
            this.lblDescription.Location = new Point(12, 12);
            this.lblDescription.Name = "lblDescription";
            this.lblDescription.Size = new Size(350, 15);
            this.lblDescription.Text = "iwara.tv の動画URLを入力してください（1行に1URL、または連続入力可）";

            // 
            // txtUrls
            // 
            this.txtUrls.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.txtUrls.Font = new Font("Consolas", 9F);
            this.txtUrls.Location = new Point(12, 35);
            this.txtUrls.Multiline = true;
            this.txtUrls.Name = "txtUrls";
            this.txtUrls.ScrollBars = ScrollBars.Both;
            this.txtUrls.Size = new Size(460, 280);
            this.txtUrls.TabIndex = 0;
            this.txtUrls.WordWrap = false;
            this.txtUrls.PlaceholderText = "https://www.iwara.tv/video/xxxxxx\nhttps://www.iwara.tv/video/yyyyyy\n\n# コメント行は無視されます\n# VideoIdのみでもOK: abcd1234";
            this.txtUrls.TextChanged += new EventHandler(this.txtUrls_TextChanged);

            // 
            // btnPaste
            // 
            this.btnPaste.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            this.btnPaste.Location = new Point(12, 325);
            this.btnPaste.Name = "btnPaste";
            this.btnPaste.Size = new Size(100, 27);
            this.btnPaste.TabIndex = 1;
            this.btnPaste.Text = "クリップボードから";
            this.btnPaste.UseVisualStyleBackColor = true;
            this.btnPaste.Click += new EventHandler(this.btnPaste_Click);

            // 
            // btnLoadFile
            // 
            this.btnLoadFile.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            this.btnLoadFile.Location = new Point(118, 325);
            this.btnLoadFile.Name = "btnLoadFile";
            this.btnLoadFile.Size = new Size(100, 27);
            this.btnLoadFile.TabIndex = 2;
            this.btnLoadFile.Text = "ファイルから";
            this.btnLoadFile.UseVisualStyleBackColor = true;
            this.btnLoadFile.Click += new EventHandler(this.btnLoadFile_Click);

            // 
            // btnClear
            // 
            this.btnClear.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            this.btnClear.Location = new Point(224, 325);
            this.btnClear.Name = "btnClear";
            this.btnClear.Size = new Size(75, 27);
            this.btnClear.TabIndex = 3;
            this.btnClear.Text = "クリア";
            this.btnClear.UseVisualStyleBackColor = true;
            this.btnClear.Click += new EventHandler(this.btnClear_Click);

            // 
            // lblStats
            // 
            this.lblStats.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.lblStats.Location = new Point(320, 330);
            this.lblStats.Name = "lblStats";
            this.lblStats.Size = new Size(150, 15);
            this.lblStats.TabIndex = 4;
            this.lblStats.Text = "検出されたURL: 0件";
            this.lblStats.TextAlign = ContentAlignment.MiddleRight;

            // 
            // lblHelp
            // 
            this.lblHelp.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            this.lblHelp.AutoSize = true;
            this.lblHelp.ForeColor = Color.Gray;
            this.lblHelp.Location = new Point(12, 360);
            this.lblHelp.Name = "lblHelp";
            this.lblHelp.Size = new Size(400, 30);
            this.lblHelp.Text = "※ 対応形式: https://www.iwara.tv/video/xxxxx または VideoIdのみ\n※ 重複URLは自動でスキップされます";

            // 
            // progressBar
            // 
            this.progressBar.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.progressBar.Location = new Point(12, 400);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new Size(460, 20);
            this.progressBar.TabIndex = 5;
            this.progressBar.Visible = false;

            // 
            // btnImport
            // 
            this.btnImport.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.btnImport.Location = new Point(316, 430);
            this.btnImport.Name = "btnImport";
            this.btnImport.Size = new Size(75, 27);
            this.btnImport.TabIndex = 6;
            this.btnImport.Text = "インポート";
            this.btnImport.UseVisualStyleBackColor = true;
            this.btnImport.Click += new EventHandler(this.btnImport_Click);

            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.btnCancel.DialogResult = DialogResult.Cancel;
            this.btnCancel.Location = new Point(397, 430);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new Size(75, 27);
            this.btnCancel.TabIndex = 7;
            this.btnCancel.Text = "キャンセル";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new EventHandler(this.btnCancel_Click);

            // 
            // BulkImportForm
            // 
            this.AcceptButton = this.btnImport;
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new Size(484, 470);
            this.Controls.Add(this.lblDescription);
            this.Controls.Add(this.txtUrls);
            this.Controls.Add(this.btnPaste);
            this.Controls.Add(this.btnLoadFile);
            this.Controls.Add(this.btnClear);
            this.Controls.Add(this.lblStats);
            this.Controls.Add(this.lblHelp);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.btnImport);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimizeBox = false;
            this.MinimumSize = new Size(400, 400);
            this.Name = "BulkImportForm";
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "URL一括インポート";
            this.Load += new EventHandler(this.BulkImportForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private Label lblDescription;
        private TextBox txtUrls;
        private Button btnPaste;
        private Button btnLoadFile;
        private Button btnClear;
        private Label lblStats;
        private Label lblHelp;
        private ProgressBar progressBar;
        private Button btnImport;
        private Button btnCancel;
    }
}
