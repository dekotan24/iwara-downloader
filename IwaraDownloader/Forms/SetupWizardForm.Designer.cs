namespace IwaraDownloader.Forms
{
    partial class SetupWizardForm
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
            this.pnlHeader = new System.Windows.Forms.Panel();
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblStep = new System.Windows.Forms.Label();
            this.pnlBody = new System.Windows.Forms.Panel();

            // Step 1: Welcome
            this.pnlStep1 = new System.Windows.Forms.Panel();
            this.lblWelcome = new System.Windows.Forms.Label();
            this.lblWelcomeDesc = new System.Windows.Forms.Label();

            // Step 2: Python choice
            this.pnlStep2 = new System.Windows.Forms.Panel();
            this.lblStep2Title = new System.Windows.Forms.Label();
            this.rbAutoDownload = new System.Windows.Forms.RadioButton();
            this.lblAutoDesc = new System.Windows.Forms.Label();
            this.rbExistingPython = new System.Windows.Forms.RadioButton();
            this.lblExistingDesc = new System.Windows.Forms.Label();
            this.txtPythonPath = new System.Windows.Forms.TextBox();
            this.btnBrowse = new System.Windows.Forms.Button();

            // Step 3: Progress
            this.pnlStep3 = new System.Windows.Forms.Panel();
            this.lblStep3Title = new System.Windows.Forms.Label();
            this.lblProgressMsg = new System.Windows.Forms.Label();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.txtLog = new System.Windows.Forms.TextBox();

            // Step 4: Complete
            this.pnlStep4 = new System.Windows.Forms.Panel();
            this.lblComplete = new System.Windows.Forms.Label();
            this.lblCompleteDesc = new System.Windows.Forms.Label();

            // Footer
            this.pnlFooter = new System.Windows.Forms.Panel();
            this.btnBack = new System.Windows.Forms.Button();
            this.btnNext = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();

            this.pnlHeader.SuspendLayout();
            this.pnlBody.SuspendLayout();
            this.pnlStep1.SuspendLayout();
            this.pnlStep2.SuspendLayout();
            this.pnlStep3.SuspendLayout();
            this.pnlStep4.SuspendLayout();
            this.pnlFooter.SuspendLayout();
            this.SuspendLayout();

            // pnlHeader
            this.pnlHeader.BackColor = System.Drawing.Color.FromArgb(45, 55, 85);
            this.pnlHeader.Controls.Add(this.lblStep);
            this.pnlHeader.Controls.Add(this.lblTitle);
            this.pnlHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlHeader.Height = 64;
            this.pnlHeader.Name = "pnlHeader";

            // lblTitle
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold);
            this.lblTitle.ForeColor = System.Drawing.Color.White;
            this.lblTitle.Location = new System.Drawing.Point(18, 10);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Text = "iwara-downloader セットアップ";

            // lblStep
            this.lblStep.AutoSize = true;
            this.lblStep.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblStep.ForeColor = System.Drawing.Color.FromArgb(180, 200, 230);
            this.lblStep.Location = new System.Drawing.Point(20, 40);
            this.lblStep.Name = "lblStep";
            this.lblStep.Text = "ステップ 1/4: ようこそ";

            // pnlBody
            this.pnlBody.BackColor = System.Drawing.Color.White;
            this.pnlBody.Controls.Add(this.pnlStep1);
            this.pnlBody.Controls.Add(this.pnlStep2);
            this.pnlBody.Controls.Add(this.pnlStep3);
            this.pnlBody.Controls.Add(this.pnlStep4);
            this.pnlBody.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlBody.Name = "pnlBody";
            this.pnlBody.Padding = new System.Windows.Forms.Padding(20);

            // pnlStep1 (Welcome)
            this.pnlStep1.Controls.Add(this.lblWelcomeDesc);
            this.pnlStep1.Controls.Add(this.lblWelcome);
            this.pnlStep1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlStep1.Name = "pnlStep1";

            this.lblWelcome.AutoSize = true;
            this.lblWelcome.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.lblWelcome.Location = new System.Drawing.Point(10, 20);
            this.lblWelcome.Name = "lblWelcome";
            this.lblWelcome.Text = "iwara-downloader をご利用いただきありがとうございます。";

            this.lblWelcomeDesc.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.lblWelcomeDesc.Location = new System.Drawing.Point(10, 60);
            this.lblWelcomeDesc.Name = "lblWelcomeDesc";
            this.lblWelcomeDesc.Size = new System.Drawing.Size(520, 200);
            this.lblWelcomeDesc.Text =
                "このウィザードでは、動作に必要な以下を自動的にセットアップします:\r\n" +
                "\r\n" +
                "  ・ Python 3.10.11 (Embeddable版) ※自動DLまたは既存パス指定\r\n" +
                "  ・ pip (パッケージマネージャ)\r\n" +
                "  ・ cloudscraper (Cloudflare対策)\r\n" +
                "  ・ yt-dlp (動画ダウンローダ)\r\n" +
                "\r\n" +
                "ネットワーク接続が必要です。完了まで数分かかる場合があります。\r\n" +
                "「次へ」を押して開始してください。";

            // pnlStep2 (Python choice)
            this.pnlStep2.Controls.Add(this.btnBrowse);
            this.pnlStep2.Controls.Add(this.txtPythonPath);
            this.pnlStep2.Controls.Add(this.lblExistingDesc);
            this.pnlStep2.Controls.Add(this.rbExistingPython);
            this.pnlStep2.Controls.Add(this.lblAutoDesc);
            this.pnlStep2.Controls.Add(this.rbAutoDownload);
            this.pnlStep2.Controls.Add(this.lblStep2Title);
            this.pnlStep2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlStep2.Name = "pnlStep2";
            this.pnlStep2.Visible = false;

            this.lblStep2Title.AutoSize = true;
            this.lblStep2Title.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.lblStep2Title.Location = new System.Drawing.Point(10, 10);
            this.lblStep2Title.Name = "lblStep2Title";
            this.lblStep2Title.Text = "Python の取得方法を選択";

            this.rbAutoDownload.AutoSize = true;
            this.rbAutoDownload.Checked = true;
            this.rbAutoDownload.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.rbAutoDownload.Location = new System.Drawing.Point(15, 55);
            this.rbAutoDownload.Name = "rbAutoDownload";
            this.rbAutoDownload.Text = "自動ダウンロード (推奨)";

            this.lblAutoDesc.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblAutoDesc.ForeColor = System.Drawing.Color.DimGray;
            this.lblAutoDesc.Location = new System.Drawing.Point(38, 80);
            this.lblAutoDesc.Name = "lblAutoDesc";
            this.lblAutoDesc.Size = new System.Drawing.Size(520, 36);
            this.lblAutoDesc.Text =
                "python.org から Python 3.10.11 (Embeddable amd64) を自動でダウンロードし、\r\n" +
                "アプリ専用フォルダ (Python310/) に展開します。システムのPythonには触れません。";

            this.rbExistingPython.AutoSize = true;
            this.rbExistingPython.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.rbExistingPython.Location = new System.Drawing.Point(15, 135);
            this.rbExistingPython.Name = "rbExistingPython";
            this.rbExistingPython.Text = "既存のPythonを使用";
            this.rbExistingPython.CheckedChanged += new System.EventHandler(this.rbExistingPython_CheckedChanged);

            this.lblExistingDesc.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblExistingDesc.ForeColor = System.Drawing.Color.DimGray;
            this.lblExistingDesc.Location = new System.Drawing.Point(38, 160);
            this.lblExistingDesc.Name = "lblExistingDesc";
            this.lblExistingDesc.Size = new System.Drawing.Size(520, 18);
            this.lblExistingDesc.Text = "python.exe へのパスを指定してください。";

            this.txtPythonPath.Enabled = false;
            this.txtPythonPath.Font = new System.Drawing.Font("Consolas", 9F);
            this.txtPythonPath.Location = new System.Drawing.Point(38, 185);
            this.txtPythonPath.Name = "txtPythonPath";
            this.txtPythonPath.Size = new System.Drawing.Size(430, 23);
            this.txtPythonPath.PlaceholderText = "C:\\Python310\\python.exe";

            this.btnBrowse.Enabled = false;
            this.btnBrowse.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnBrowse.Location = new System.Drawing.Point(475, 184);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(70, 25);
            this.btnBrowse.Text = "参照...";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);

            // pnlStep3 (Progress)
            this.pnlStep3.Controls.Add(this.txtLog);
            this.pnlStep3.Controls.Add(this.progressBar);
            this.pnlStep3.Controls.Add(this.lblProgressMsg);
            this.pnlStep3.Controls.Add(this.lblStep3Title);
            this.pnlStep3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlStep3.Name = "pnlStep3";
            this.pnlStep3.Visible = false;

            this.lblStep3Title.AutoSize = true;
            this.lblStep3Title.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.lblStep3Title.Location = new System.Drawing.Point(10, 10);
            this.lblStep3Title.Name = "lblStep3Title";
            this.lblStep3Title.Text = "セットアップ実行中";

            this.lblProgressMsg.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblProgressMsg.Location = new System.Drawing.Point(10, 50);
            this.lblProgressMsg.Name = "lblProgressMsg";
            this.lblProgressMsg.Size = new System.Drawing.Size(550, 20);
            this.lblProgressMsg.Text = "準備中...";

            this.progressBar.Location = new System.Drawing.Point(10, 75);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(550, 18);
            this.progressBar.Style = System.Windows.Forms.ProgressBarStyle.Continuous;

            this.txtLog.BackColor = System.Drawing.Color.FromArgb(30, 30, 35);
            this.txtLog.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtLog.Font = new System.Drawing.Font("Consolas", 8.5F);
            this.txtLog.ForeColor = System.Drawing.Color.FromArgb(200, 220, 200);
            this.txtLog.Location = new System.Drawing.Point(10, 105);
            this.txtLog.Multiline = true;
            this.txtLog.Name = "txtLog";
            this.txtLog.ReadOnly = true;
            this.txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtLog.Size = new System.Drawing.Size(550, 200);
            this.txtLog.WordWrap = false;

            // pnlStep4 (Complete)
            this.pnlStep4.Controls.Add(this.lblCompleteDesc);
            this.pnlStep4.Controls.Add(this.lblComplete);
            this.pnlStep4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlStep4.Name = "pnlStep4";
            this.pnlStep4.Visible = false;

            this.lblComplete.AutoSize = true;
            this.lblComplete.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold);
            this.lblComplete.ForeColor = System.Drawing.Color.FromArgb(40, 130, 60);
            this.lblComplete.Location = new System.Drawing.Point(10, 30);
            this.lblComplete.Name = "lblComplete";
            this.lblComplete.Text = "セットアップ完了!";

            this.lblCompleteDesc.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.lblCompleteDesc.Location = new System.Drawing.Point(10, 80);
            this.lblCompleteDesc.Name = "lblCompleteDesc";
            this.lblCompleteDesc.Size = new System.Drawing.Size(550, 120);
            this.lblCompleteDesc.Text =
                "iwara-downloader を使用する準備が整いました。\r\n" +
                "\r\n" +
                "「完了」を押してウィザードを閉じてください。\r\n" +
                "続いて画面上部の「ログイン」から iwara にログインしてください。";

            // pnlFooter
            this.pnlFooter.BackColor = System.Drawing.Color.FromArgb(240, 240, 245);
            this.pnlFooter.Controls.Add(this.btnCancel);
            this.pnlFooter.Controls.Add(this.btnNext);
            this.pnlFooter.Controls.Add(this.btnBack);
            this.pnlFooter.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlFooter.Height = 56;
            this.pnlFooter.Name = "pnlFooter";

            this.btnBack.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this.btnBack.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.btnBack.Location = new System.Drawing.Point(305, 15);
            this.btnBack.Name = "btnBack";
            this.btnBack.Size = new System.Drawing.Size(90, 28);
            this.btnBack.Text = "< 戻る";
            this.btnBack.UseVisualStyleBackColor = true;
            this.btnBack.Click += new System.EventHandler(this.btnBack_Click);

            this.btnNext.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this.btnNext.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
            this.btnNext.Location = new System.Drawing.Point(400, 15);
            this.btnNext.Name = "btnNext";
            this.btnNext.Size = new System.Drawing.Size(90, 28);
            this.btnNext.Text = "次へ >";
            this.btnNext.UseVisualStyleBackColor = true;
            this.btnNext.Click += new System.EventHandler(this.btnNext_Click);

            this.btnCancel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this.btnCancel.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.btnCancel.Location = new System.Drawing.Point(495, 15);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(90, 28);
            this.btnCancel.Text = "キャンセル";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);

            // SetupWizardForm
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(600, 460);
            this.Controls.Add(this.pnlBody);
            this.Controls.Add(this.pnlFooter);
            this.Controls.Add(this.pnlHeader);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SetupWizardForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "iwara-downloader セットアップ";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.SetupWizardForm_FormClosing);

            this.pnlHeader.ResumeLayout(false);
            this.pnlHeader.PerformLayout();
            this.pnlBody.ResumeLayout(false);
            this.pnlStep1.ResumeLayout(false);
            this.pnlStep1.PerformLayout();
            this.pnlStep2.ResumeLayout(false);
            this.pnlStep2.PerformLayout();
            this.pnlStep3.ResumeLayout(false);
            this.pnlStep3.PerformLayout();
            this.pnlStep4.ResumeLayout(false);
            this.pnlStep4.PerformLayout();
            this.pnlFooter.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Panel pnlHeader;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblStep;
        private System.Windows.Forms.Panel pnlBody;
        private System.Windows.Forms.Panel pnlStep1;
        private System.Windows.Forms.Label lblWelcome;
        private System.Windows.Forms.Label lblWelcomeDesc;
        private System.Windows.Forms.Panel pnlStep2;
        private System.Windows.Forms.Label lblStep2Title;
        private System.Windows.Forms.RadioButton rbAutoDownload;
        private System.Windows.Forms.Label lblAutoDesc;
        private System.Windows.Forms.RadioButton rbExistingPython;
        private System.Windows.Forms.Label lblExistingDesc;
        private System.Windows.Forms.TextBox txtPythonPath;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.Panel pnlStep3;
        private System.Windows.Forms.Label lblStep3Title;
        private System.Windows.Forms.Label lblProgressMsg;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.TextBox txtLog;
        private System.Windows.Forms.Panel pnlStep4;
        private System.Windows.Forms.Label lblComplete;
        private System.Windows.Forms.Label lblCompleteDesc;
        private System.Windows.Forms.Panel pnlFooter;
        private System.Windows.Forms.Button btnBack;
        private System.Windows.Forms.Button btnNext;
        private System.Windows.Forms.Button btnCancel;
    }
}
