namespace IwaraDownloader.Forms
{
    partial class ImportFromFolderWizard
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        #region Designer code

        private void InitializeComponent()
        {
            this.pnlHeader = new System.Windows.Forms.Panel();
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblStep = new System.Windows.Forms.Label();

            this.pnlBody = new System.Windows.Forms.Panel();

            // Step 1: フォルダ選択
            this.pnlStep1 = new System.Windows.Forms.Panel();
            this.lblStep1Title = new System.Windows.Forms.Label();
            this.lblStep1Desc = new System.Windows.Forms.Label();
            this.txtFolder = new System.Windows.Forms.TextBox();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.chkRecursive = new System.Windows.Forms.CheckBox();

            // Step 2: スキャン
            this.pnlStep2 = new System.Windows.Forms.Panel();
            this.lblStep2Title = new System.Windows.Forms.Label();
            this.lblScanStatus = new System.Windows.Forms.Label();
            this.progressScan = new System.Windows.Forms.ProgressBar();
            this.lblScanResult = new System.Windows.Forms.Label();

            // Step 3: 作者選択
            this.pnlStep3 = new System.Windows.Forms.Panel();
            this.lblStep3Title = new System.Windows.Forms.Label();
            this.lblStep3Desc = new System.Windows.Forms.Label();
            this.clbAuthors = new System.Windows.Forms.CheckedListBox();
            this.btnSelectAll = new System.Windows.Forms.Button();
            this.btnSelectNone = new System.Windows.Forms.Button();
            this.lblSingleVideos = new System.Windows.Forms.Label();

            // Step 4: 実行
            this.pnlStep4 = new System.Windows.Forms.Panel();
            this.lblStep4Title = new System.Windows.Forms.Label();
            this.lblImportStatus = new System.Windows.Forms.Label();
            this.progressImport = new System.Windows.Forms.ProgressBar();
            this.txtImportLog = new System.Windows.Forms.TextBox();

            // Step 5: 完了
            this.pnlStep5 = new System.Windows.Forms.Panel();
            this.lblStep5Title = new System.Windows.Forms.Label();
            this.lblSummary = new System.Windows.Forms.Label();
            this.lblDupNotice = new System.Windows.Forms.Label();

            this.pnlFooter = new System.Windows.Forms.Panel();
            this.btnHide = new System.Windows.Forms.Button();
            this.btnBack = new System.Windows.Forms.Button();
            this.btnNext = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();

            this.pnlHeader.SuspendLayout();
            this.pnlBody.SuspendLayout();
            this.pnlStep1.SuspendLayout();
            this.pnlStep2.SuspendLayout();
            this.pnlStep3.SuspendLayout();
            this.pnlStep4.SuspendLayout();
            this.pnlStep5.SuspendLayout();
            this.pnlFooter.SuspendLayout();
            this.SuspendLayout();

            // pnlHeader
            this.pnlHeader.BackColor = System.Drawing.Color.FromArgb(45, 55, 85);
            this.pnlHeader.Controls.Add(this.lblStep);
            this.pnlHeader.Controls.Add(this.lblTitle);
            this.pnlHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlHeader.Height = 64;
            this.pnlHeader.Name = "pnlHeader";

            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold);
            this.lblTitle.ForeColor = System.Drawing.Color.White;
            this.lblTitle.Location = new System.Drawing.Point(18, 10);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Text = "フォルダから取り込み";

            this.lblStep.AutoSize = true;
            this.lblStep.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblStep.ForeColor = System.Drawing.Color.FromArgb(180, 200, 230);
            this.lblStep.Location = new System.Drawing.Point(20, 40);
            this.lblStep.Name = "lblStep";
            this.lblStep.Text = "ステップ 1/5";

            // pnlBody
            this.pnlBody.BackColor = System.Drawing.Color.White;
            this.pnlBody.Controls.Add(this.pnlStep1);
            this.pnlBody.Controls.Add(this.pnlStep2);
            this.pnlBody.Controls.Add(this.pnlStep3);
            this.pnlBody.Controls.Add(this.pnlStep4);
            this.pnlBody.Controls.Add(this.pnlStep5);
            this.pnlBody.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlBody.Padding = new System.Windows.Forms.Padding(20);
            this.pnlBody.Name = "pnlBody";

            // pnlStep1 (フォルダ選択)
            this.pnlStep1.Controls.Add(this.chkRecursive);
            this.pnlStep1.Controls.Add(this.btnBrowse);
            this.pnlStep1.Controls.Add(this.txtFolder);
            this.pnlStep1.Controls.Add(this.lblStep1Desc);
            this.pnlStep1.Controls.Add(this.lblStep1Title);
            this.pnlStep1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlStep1.Name = "pnlStep1";

            this.lblStep1Title.AutoSize = true;
            this.lblStep1Title.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.lblStep1Title.Location = new System.Drawing.Point(10, 10);
            this.lblStep1Title.Name = "lblStep1Title";
            this.lblStep1Title.Text = "取り込み元フォルダを選択";

            this.lblStep1Desc.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.lblStep1Desc.Location = new System.Drawing.Point(10, 45);
            this.lblStep1Desc.Name = "lblStep1Desc";
            this.lblStep1Desc.Size = new System.Drawing.Size(670, 120);
            this.lblStep1Desc.Text =
                "選択したフォルダ内の mp4/m4v ファイルをスキャンし、iwara のカスタムタグから\r\n" +
                "videoId を取得して DB に取り込みます。\r\n\r\n" +
                "※ タグの無いファイルはスキップされます (旧バージョン / 手動コピー等)\r\n" +
                "※ iwara にログイン済みである必要があります";

            this.txtFolder.Font = new System.Drawing.Font("Consolas", 9F);
            this.txtFolder.Location = new System.Drawing.Point(10, 175);
            this.txtFolder.Name = "txtFolder";
            this.txtFolder.Size = new System.Drawing.Size(580, 23);
            this.txtFolder.PlaceholderText = "D:\\Iwara\\BackupDrive";

            this.btnBrowse.Location = new System.Drawing.Point(595, 174);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(80, 25);
            this.btnBrowse.Text = "参照...";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);

            this.chkRecursive.AutoSize = true;
            this.chkRecursive.Checked = true;
            this.chkRecursive.Location = new System.Drawing.Point(10, 210);
            this.chkRecursive.Name = "chkRecursive";
            this.chkRecursive.Text = "サブフォルダも再帰的にスキャン";

            // pnlStep2 (スキャン)
            this.pnlStep2.Controls.Add(this.lblScanResult);
            this.pnlStep2.Controls.Add(this.progressScan);
            this.pnlStep2.Controls.Add(this.lblScanStatus);
            this.pnlStep2.Controls.Add(this.lblStep2Title);
            this.pnlStep2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlStep2.Name = "pnlStep2";
            this.pnlStep2.Visible = false;

            this.lblStep2Title.AutoSize = true;
            this.lblStep2Title.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.lblStep2Title.Location = new System.Drawing.Point(10, 10);
            this.lblStep2Title.Name = "lblStep2Title";
            this.lblStep2Title.Text = "スキャン中";

            this.lblScanStatus.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblScanStatus.Location = new System.Drawing.Point(10, 50);
            this.lblScanStatus.Name = "lblScanStatus";
            this.lblScanStatus.Size = new System.Drawing.Size(670, 20);
            this.lblScanStatus.Text = "準備中...";

            this.progressScan.Location = new System.Drawing.Point(10, 75);
            this.progressScan.Name = "progressScan";
            this.progressScan.Size = new System.Drawing.Size(670, 18);
            this.progressScan.Style = System.Windows.Forms.ProgressBarStyle.Marquee;

            this.lblScanResult.Font = new System.Drawing.Font("Consolas", 9F);
            this.lblScanResult.Location = new System.Drawing.Point(10, 110);
            this.lblScanResult.Name = "lblScanResult";
            this.lblScanResult.Size = new System.Drawing.Size(670, 200);
            this.lblScanResult.Text = "";

            // pnlStep3 (作者選択)
            this.pnlStep3.Controls.Add(this.lblSingleVideos);
            this.pnlStep3.Controls.Add(this.btnSelectNone);
            this.pnlStep3.Controls.Add(this.btnSelectAll);
            this.pnlStep3.Controls.Add(this.clbAuthors);
            this.pnlStep3.Controls.Add(this.lblStep3Desc);
            this.pnlStep3.Controls.Add(this.lblStep3Title);
            this.pnlStep3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlStep3.Name = "pnlStep3";
            this.pnlStep3.Visible = false;

            this.lblStep3Title.AutoSize = true;
            this.lblStep3Title.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.lblStep3Title.Location = new System.Drawing.Point(10, 10);
            this.lblStep3Title.Name = "lblStep3Title";
            this.lblStep3Title.Text = "チャンネルとして登録する作者を選択";

            this.lblStep3Desc.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblStep3Desc.ForeColor = System.Drawing.Color.DimGray;
            this.lblStep3Desc.Location = new System.Drawing.Point(10, 45);
            this.lblStep3Desc.Name = "lblStep3Desc";
            this.lblStep3Desc.Size = new System.Drawing.Size(670, 40);
            this.lblStep3Desc.Text =
                "チェックを入れた作者がチャンネル (購読) として追加されます。\r\n" +
                "チェックを外した作者の動画は『単発動画』として取り込まれます。";

            this.clbAuthors.CheckOnClick = true;
            this.clbAuthors.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.clbAuthors.Location = new System.Drawing.Point(10, 90);
            this.clbAuthors.Name = "clbAuthors";
            this.clbAuthors.Size = new System.Drawing.Size(670, 200);
            this.clbAuthors.IntegralHeight = false;

            this.btnSelectAll.Location = new System.Drawing.Point(10, 295);
            this.btnSelectAll.Name = "btnSelectAll";
            this.btnSelectAll.Size = new System.Drawing.Size(80, 25);
            this.btnSelectAll.Text = "全選択";
            this.btnSelectAll.UseVisualStyleBackColor = true;
            this.btnSelectAll.Click += new System.EventHandler(this.btnSelectAll_Click);

            this.btnSelectNone.Location = new System.Drawing.Point(95, 295);
            this.btnSelectNone.Name = "btnSelectNone";
            this.btnSelectNone.Size = new System.Drawing.Size(80, 25);
            this.btnSelectNone.Text = "全解除";
            this.btnSelectNone.UseVisualStyleBackColor = true;
            this.btnSelectNone.Click += new System.EventHandler(this.btnSelectNone_Click);

            this.lblSingleVideos.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblSingleVideos.ForeColor = System.Drawing.Color.DimGray;
            this.lblSingleVideos.Location = new System.Drawing.Point(190, 299);
            this.lblSingleVideos.Name = "lblSingleVideos";
            this.lblSingleVideos.Size = new System.Drawing.Size(490, 20);
            this.lblSingleVideos.Text = "";

            // pnlStep4 (実行)
            this.pnlStep4.Controls.Add(this.txtImportLog);
            this.pnlStep4.Controls.Add(this.progressImport);
            this.pnlStep4.Controls.Add(this.lblImportStatus);
            this.pnlStep4.Controls.Add(this.lblStep4Title);
            this.pnlStep4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlStep4.Name = "pnlStep4";
            this.pnlStep4.Visible = false;

            this.lblStep4Title.AutoSize = true;
            this.lblStep4Title.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.lblStep4Title.Location = new System.Drawing.Point(10, 10);
            this.lblStep4Title.Name = "lblStep4Title";
            this.lblStep4Title.Text = "取り込み実行中";

            this.lblImportStatus.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblImportStatus.Location = new System.Drawing.Point(10, 50);
            this.lblImportStatus.Name = "lblImportStatus";
            this.lblImportStatus.Size = new System.Drawing.Size(670, 20);
            this.lblImportStatus.Text = "準備中...";

            this.progressImport.Location = new System.Drawing.Point(10, 75);
            this.progressImport.Name = "progressImport";
            this.progressImport.Size = new System.Drawing.Size(670, 18);
            this.progressImport.Style = System.Windows.Forms.ProgressBarStyle.Continuous;

            this.txtImportLog.BackColor = System.Drawing.Color.FromArgb(30, 30, 35);
            this.txtImportLog.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtImportLog.Font = new System.Drawing.Font("Consolas", 8.5F);
            this.txtImportLog.ForeColor = System.Drawing.Color.FromArgb(200, 220, 200);
            this.txtImportLog.Location = new System.Drawing.Point(10, 105);
            this.txtImportLog.Multiline = true;
            this.txtImportLog.Name = "txtImportLog";
            this.txtImportLog.ReadOnly = true;
            this.txtImportLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtImportLog.Size = new System.Drawing.Size(670, 215);
            this.txtImportLog.WordWrap = false;

            // pnlStep5 (完了)
            this.pnlStep5.Controls.Add(this.lblDupNotice);
            this.pnlStep5.Controls.Add(this.lblSummary);
            this.pnlStep5.Controls.Add(this.lblStep5Title);
            this.pnlStep5.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlStep5.Name = "pnlStep5";
            this.pnlStep5.Visible = false;

            this.lblStep5Title.AutoSize = true;
            this.lblStep5Title.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold);
            this.lblStep5Title.ForeColor = System.Drawing.Color.FromArgb(40, 130, 60);
            this.lblStep5Title.Location = new System.Drawing.Point(10, 20);
            this.lblStep5Title.Name = "lblStep5Title";
            this.lblStep5Title.Text = "取り込み完了!";

            this.lblSummary.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.lblSummary.Location = new System.Drawing.Point(10, 65);
            this.lblSummary.Name = "lblSummary";
            this.lblSummary.Size = new System.Drawing.Size(670, 160);
            this.lblSummary.Text = "";

            this.lblDupNotice.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.lblDupNotice.ForeColor = System.Drawing.Color.FromArgb(200, 80, 40);
            this.lblDupNotice.Location = new System.Drawing.Point(10, 230);
            this.lblDupNotice.Name = "lblDupNotice";
            this.lblDupNotice.Size = new System.Drawing.Size(670, 60);
            this.lblDupNotice.Text = "";

            // pnlFooter
            this.pnlFooter.BackColor = System.Drawing.Color.FromArgb(240, 240, 245);
            this.pnlFooter.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlFooter.Height = 60;
            this.pnlFooter.Name = "pnlFooter";

            // FlowLayoutPanel で右寄せレイアウト (DPI/AutoScale でも重ならない)
            var flowButtons = new System.Windows.Forms.FlowLayoutPanel
            {
                FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft,
                Dock = System.Windows.Forms.DockStyle.Fill,
                Padding = new System.Windows.Forms.Padding(10),
                WrapContents = false,
            };

            this.btnCancel.Location = new System.Drawing.Point(0, 0);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(95, 30);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(5, 5, 5, 5);
            this.btnCancel.Text = "キャンセル";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);

            this.btnNext.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
            this.btnNext.Location = new System.Drawing.Point(0, 0);
            this.btnNext.Name = "btnNext";
            this.btnNext.Size = new System.Drawing.Size(130, 30);
            this.btnNext.Margin = new System.Windows.Forms.Padding(5, 5, 5, 5);
            this.btnNext.Text = "次へ >";
            this.btnNext.UseVisualStyleBackColor = true;
            this.btnNext.Click += new System.EventHandler(this.btnNext_Click);

            this.btnBack.Location = new System.Drawing.Point(0, 0);
            this.btnBack.Name = "btnBack";
            this.btnBack.Size = new System.Drawing.Size(95, 30);
            this.btnBack.Margin = new System.Windows.Forms.Padding(5, 5, 5, 5);
            this.btnBack.Text = "< 戻る";
            this.btnBack.UseVisualStyleBackColor = true;
            this.btnBack.Click += new System.EventHandler(this.btnBack_Click);

            this.btnHide.Location = new System.Drawing.Point(0, 0);
            this.btnHide.Name = "btnHide";
            this.btnHide.Size = new System.Drawing.Size(150, 30);
            this.btnHide.Margin = new System.Windows.Forms.Padding(5, 5, 5, 5);
            this.btnHide.Text = "バックグラウンドで実行";
            this.btnHide.UseVisualStyleBackColor = true;
            this.btnHide.Visible = false;
            this.btnHide.Click += new System.EventHandler(this.btnHide_Click);

            // 右から左の順に Add (FlowDirection.RightToLeft なので最初に Add したものが右端)
            flowButtons.Controls.Add(this.btnCancel);
            flowButtons.Controls.Add(this.btnNext);
            flowButtons.Controls.Add(this.btnBack);
            flowButtons.Controls.Add(this.btnHide);
            this.pnlFooter.Controls.Add(flowButtons);

            // ImportFromFolderWizard
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(720, 540);
            this.Controls.Add(this.pnlBody);
            this.Controls.Add(this.pnlFooter);
            this.Controls.Add(this.pnlHeader);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ImportFromFolderWizard";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "フォルダから取り込み";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ImportFromFolderWizard_FormClosing);

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
            this.pnlStep5.ResumeLayout(false);
            this.pnlStep5.PerformLayout();
            this.pnlFooter.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Panel pnlHeader;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblStep;
        private System.Windows.Forms.Panel pnlBody;
        private System.Windows.Forms.Panel pnlStep1;
        private System.Windows.Forms.Label lblStep1Title;
        private System.Windows.Forms.Label lblStep1Desc;
        private System.Windows.Forms.TextBox txtFolder;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.CheckBox chkRecursive;
        private System.Windows.Forms.Panel pnlStep2;
        private System.Windows.Forms.Label lblStep2Title;
        private System.Windows.Forms.Label lblScanStatus;
        private System.Windows.Forms.ProgressBar progressScan;
        private System.Windows.Forms.Label lblScanResult;
        private System.Windows.Forms.Panel pnlStep3;
        private System.Windows.Forms.Label lblStep3Title;
        private System.Windows.Forms.Label lblStep3Desc;
        private System.Windows.Forms.CheckedListBox clbAuthors;
        private System.Windows.Forms.Button btnSelectAll;
        private System.Windows.Forms.Button btnSelectNone;
        private System.Windows.Forms.Label lblSingleVideos;
        private System.Windows.Forms.Panel pnlStep4;
        private System.Windows.Forms.Label lblStep4Title;
        private System.Windows.Forms.Label lblImportStatus;
        private System.Windows.Forms.ProgressBar progressImport;
        private System.Windows.Forms.TextBox txtImportLog;
        private System.Windows.Forms.Panel pnlStep5;
        private System.Windows.Forms.Label lblStep5Title;
        private System.Windows.Forms.Label lblSummary;
        private System.Windows.Forms.Label lblDupNotice;
        private System.Windows.Forms.Panel pnlFooter;
        private System.Windows.Forms.Button btnHide;
        private System.Windows.Forms.Button btnBack;
        private System.Windows.Forms.Button btnNext;
        private System.Windows.Forms.Button btnCancel;
    }
}
