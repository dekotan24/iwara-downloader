namespace IwaraDownloader.Forms
{
    partial class SettingsForm
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
            this.tabControl = new TabControl();
            this.tabGeneral = new TabPage();
            this.tabAccount = new TabPage();
            this.tabBackup = new TabPage();

            // 一般設定
            this.grpDownload = new GroupBox();
            this.lblDownloadFolder = new Label();
            this.txtDownloadFolder = new TextBox();
            this.btnBrowseFolder = new Button();
            this.lblQuality = new Label();
            this.cmbQuality = new ComboBox();
            this.lblConcurrent = new Label();
            this.numConcurrent = new NumericUpDown();
            this.lblRetry = new Label();
            this.numRetry = new NumericUpDown();

            this.grpAutoCheck = new GroupBox();
            this.chkAutoCheck = new CheckBox();
            this.lblCheckInterval = new Label();
            this.cmbCheckInterval = new ComboBox();

            this.grpNotification = new GroupBox();
            this.chkToast = new CheckBox();
            this.chkStartMinimized = new CheckBox();

            // アカウント設定
            this.grpAccount = new GroupBox();
            this.lblUsername = new Label();
            this.txtUsername = new TextBox();
            this.lblPassword = new Label();
            this.txtPassword = new TextBox();
            this.lblAccountNote = new Label();

            // バックアップ
            this.grpExport = new GroupBox();
            this.btnExportSettings = new Button();
            this.btnExportSubscriptions = new Button();
            this.grpImport = new GroupBox();
            this.btnImportSettings = new Button();
            this.btnImportSubscriptions = new Button();

            // ボタン
            this.btnOk = new Button();
            this.btnCancel = new Button();
            this.btnApply = new Button();

            this.tabControl.SuspendLayout();
            this.tabGeneral.SuspendLayout();
            this.tabAccount.SuspendLayout();
            this.tabBackup.SuspendLayout();
            this.grpDownload.SuspendLayout();
            this.grpAutoCheck.SuspendLayout();
            this.grpNotification.SuspendLayout();
            this.grpAccount.SuspendLayout();
            this.grpExport.SuspendLayout();
            this.grpImport.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numConcurrent)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numRetry)).BeginInit();
            this.SuspendLayout();

            // 
            // tabControl
            // 
            this.tabControl.Controls.Add(this.tabGeneral);
            this.tabControl.Controls.Add(this.tabAccount);
            this.tabControl.Controls.Add(this.tabBackup);
            this.tabControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.tabControl.Location = new Point(12, 12);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new Size(460, 380);
            this.tabControl.TabIndex = 0;

            // 
            // tabGeneral
            // 
            this.tabGeneral.Controls.Add(this.grpDownload);
            this.tabGeneral.Controls.Add(this.grpAutoCheck);
            this.tabGeneral.Controls.Add(this.grpNotification);
            this.tabGeneral.Location = new Point(4, 24);
            this.tabGeneral.Name = "tabGeneral";
            this.tabGeneral.Padding = new Padding(3);
            this.tabGeneral.Size = new Size(452, 352);
            this.tabGeneral.TabIndex = 0;
            this.tabGeneral.Text = "一般";
            this.tabGeneral.UseVisualStyleBackColor = true;

            // 
            // grpDownload
            // 
            this.grpDownload.Controls.Add(this.lblDownloadFolder);
            this.grpDownload.Controls.Add(this.txtDownloadFolder);
            this.grpDownload.Controls.Add(this.btnBrowseFolder);
            this.grpDownload.Controls.Add(this.lblQuality);
            this.grpDownload.Controls.Add(this.cmbQuality);
            this.grpDownload.Controls.Add(this.lblConcurrent);
            this.grpDownload.Controls.Add(this.numConcurrent);
            this.grpDownload.Controls.Add(this.lblRetry);
            this.grpDownload.Controls.Add(this.numRetry);
            this.grpDownload.Location = new Point(6, 6);
            this.grpDownload.Name = "grpDownload";
            this.grpDownload.Size = new Size(440, 145);
            this.grpDownload.TabIndex = 0;
            this.grpDownload.TabStop = false;
            this.grpDownload.Text = "ダウンロード設定";

            // 
            // lblDownloadFolder
            // 
            this.lblDownloadFolder.AutoSize = true;
            this.lblDownloadFolder.Location = new Point(10, 25);
            this.lblDownloadFolder.Name = "lblDownloadFolder";
            this.lblDownloadFolder.Size = new Size(80, 15);
            this.lblDownloadFolder.TabIndex = 0;
            this.lblDownloadFolder.Text = "保存先フォルダ:";

            // 
            // txtDownloadFolder
            // 
            this.txtDownloadFolder.Location = new Point(100, 22);
            this.txtDownloadFolder.Name = "txtDownloadFolder";
            this.txtDownloadFolder.Size = new Size(260, 23);
            this.txtDownloadFolder.TabIndex = 1;

            // 
            // btnBrowseFolder
            // 
            this.btnBrowseFolder.Location = new Point(366, 21);
            this.btnBrowseFolder.Name = "btnBrowseFolder";
            this.btnBrowseFolder.Size = new Size(60, 25);
            this.btnBrowseFolder.TabIndex = 2;
            this.btnBrowseFolder.Text = "参照...";
            this.btnBrowseFolder.UseVisualStyleBackColor = true;
            this.btnBrowseFolder.Click += new EventHandler(this.btnBrowseFolder_Click);

            // 
            // lblQuality
            // 
            this.lblQuality.AutoSize = true;
            this.lblQuality.Location = new Point(10, 55);
            this.lblQuality.Name = "lblQuality";
            this.lblQuality.Size = new Size(70, 15);
            this.lblQuality.TabIndex = 3;
            this.lblQuality.Text = "デフォルト画質:";

            // 
            // cmbQuality
            // 
            this.cmbQuality.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbQuality.FormattingEnabled = true;
            this.cmbQuality.Items.AddRange(new object[] {
                "Source (最高画質)",
                "1080p",
                "720p",
                "540p",
                "360p"
            });
            this.cmbQuality.Location = new Point(100, 52);
            this.cmbQuality.Name = "cmbQuality";
            this.cmbQuality.Size = new Size(150, 23);
            this.cmbQuality.TabIndex = 4;

            // 
            // lblConcurrent
            // 
            this.lblConcurrent.AutoSize = true;
            this.lblConcurrent.Location = new Point(10, 85);
            this.lblConcurrent.Name = "lblConcurrent";
            this.lblConcurrent.Size = new Size(80, 15);
            this.lblConcurrent.TabIndex = 5;
            this.lblConcurrent.Text = "同時DL数:";

            // 
            // numConcurrent
            // 
            this.numConcurrent.Location = new Point(100, 82);
            this.numConcurrent.Maximum = new decimal(new int[] { 3, 0, 0, 0 });
            this.numConcurrent.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numConcurrent.Name = "numConcurrent";
            this.numConcurrent.Size = new Size(60, 23);
            this.numConcurrent.TabIndex = 6;
            this.numConcurrent.Value = new decimal(new int[] { 2, 0, 0, 0 });

            // 
            // lblRetry
            // 
            this.lblRetry.AutoSize = true;
            this.lblRetry.Location = new Point(10, 115);
            this.lblRetry.Name = "lblRetry";
            this.lblRetry.Size = new Size(80, 15);
            this.lblRetry.TabIndex = 7;
            this.lblRetry.Text = "リトライ回数:";

            // 
            // numRetry
            // 
            this.numRetry.Location = new Point(100, 112);
            this.numRetry.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
            this.numRetry.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numRetry.Name = "numRetry";
            this.numRetry.Size = new Size(60, 23);
            this.numRetry.TabIndex = 8;
            this.numRetry.Value = new decimal(new int[] { 3, 0, 0, 0 });

            // 
            // grpAutoCheck
            // 
            this.grpAutoCheck.Controls.Add(this.chkAutoCheck);
            this.grpAutoCheck.Controls.Add(this.lblCheckInterval);
            this.grpAutoCheck.Controls.Add(this.cmbCheckInterval);
            this.grpAutoCheck.Controls.Add(this.chkAutoDownload);
            this.grpAutoCheck.Location = new Point(6, 157);
            this.grpAutoCheck.Name = "grpAutoCheck";
            this.grpAutoCheck.Size = new Size(440, 85);
            this.grpAutoCheck.TabIndex = 1;
            this.grpAutoCheck.TabStop = false;
            this.grpAutoCheck.Text = "自動チェック";

            // 
            // chkAutoCheck
            // 
            this.chkAutoCheck.AutoSize = true;
            this.chkAutoCheck.Location = new Point(10, 25);
            this.chkAutoCheck.Name = "chkAutoCheck";
            this.chkAutoCheck.Size = new Size(130, 19);
            this.chkAutoCheck.TabIndex = 0;
            this.chkAutoCheck.Text = "新着を自動でチェック";
            this.chkAutoCheck.UseVisualStyleBackColor = true;

            // 
            // lblCheckInterval
            // 
            this.lblCheckInterval.AutoSize = true;
            this.lblCheckInterval.Location = new Point(180, 26);
            this.lblCheckInterval.Name = "lblCheckInterval";
            this.lblCheckInterval.Size = new Size(50, 15);
            this.lblCheckInterval.TabIndex = 1;
            this.lblCheckInterval.Text = "間隔:";

            // 
            // cmbCheckInterval
            // 
            this.cmbCheckInterval.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbCheckInterval.FormattingEnabled = true;
            this.cmbCheckInterval.Items.AddRange(new object[] {
                "30分",
                "1時間",
                "2時間",
                "6時間",
                "12時間",
                "1日"
            });
            this.cmbCheckInterval.Location = new Point(230, 23);
            this.cmbCheckInterval.Name = "cmbCheckInterval";
            this.cmbCheckInterval.Size = new Size(100, 23);
            this.cmbCheckInterval.TabIndex = 2;

            // 
            // chkAutoDownload
            // 
            this.chkAutoDownload = new CheckBox();
            this.chkAutoDownload.AutoSize = true;
            this.chkAutoDownload.Location = new Point(10, 55);
            this.chkAutoDownload.Name = "chkAutoDownload";
            this.chkAutoDownload.Size = new Size(200, 19);
            this.chkAutoDownload.TabIndex = 3;
            this.chkAutoDownload.Text = "新着検出時に自動でDL開始";
            this.chkAutoDownload.UseVisualStyleBackColor = true;

            // 
            // grpNotification
            // 
            this.grpNotification.Controls.Add(this.chkToast);
            this.grpNotification.Controls.Add(this.chkStartMinimized);
            this.grpNotification.Controls.Add(this.chkMinimizeToTray);
            this.grpNotification.Location = new Point(6, 248);
            this.grpNotification.Name = "grpNotification";
            this.grpNotification.Size = new Size(440, 100);
            this.grpNotification.TabIndex = 2;
            this.grpNotification.TabStop = false;
            this.grpNotification.Text = "通知・起動";

            // 
            // chkToast
            // 
            this.chkToast.AutoSize = true;
            this.chkToast.Location = new Point(10, 25);
            this.chkToast.Name = "chkToast";
            this.chkToast.Size = new Size(130, 19);
            this.chkToast.TabIndex = 0;
            this.chkToast.Text = "トースト通知を有効化";
            this.chkToast.UseVisualStyleBackColor = true;

            // 
            // chkStartMinimized
            // 
            this.chkStartMinimized.AutoSize = true;
            this.chkStartMinimized.Location = new Point(10, 50);
            this.chkStartMinimized.Name = "chkStartMinimized";
            this.chkStartMinimized.Size = new Size(130, 19);
            this.chkStartMinimized.TabIndex = 1;
            this.chkStartMinimized.Text = "起動時に最小化";
            this.chkStartMinimized.UseVisualStyleBackColor = true;

            // 
            // chkMinimizeToTray
            // 
            this.chkMinimizeToTray = new CheckBox();
            this.chkMinimizeToTray.AutoSize = true;
            this.chkMinimizeToTray.Location = new Point(10, 75);
            this.chkMinimizeToTray.Name = "chkMinimizeToTray";
            this.chkMinimizeToTray.Size = new Size(200, 19);
            this.chkMinimizeToTray.TabIndex = 2;
            this.chkMinimizeToTray.Text = "閉じるボタンでトレイに最小化";
            this.chkMinimizeToTray.UseVisualStyleBackColor = true;

            // 
            // tabAccount
            // 
            this.tabAccount.Controls.Add(this.grpAccount);
            this.tabAccount.Location = new Point(4, 24);
            this.tabAccount.Name = "tabAccount";
            this.tabAccount.Padding = new Padding(3);
            this.tabAccount.Size = new Size(452, 352);
            this.tabAccount.TabIndex = 1;
            this.tabAccount.Text = "アカウント";
            this.tabAccount.UseVisualStyleBackColor = true;

            // 
            // grpAccount
            // 
            this.grpAccount.Controls.Add(this.lblUsername);
            this.grpAccount.Controls.Add(this.txtUsername);
            this.grpAccount.Controls.Add(this.lblPassword);
            this.grpAccount.Controls.Add(this.txtPassword);
            this.grpAccount.Controls.Add(this.lblAccountNote);
            this.grpAccount.Location = new Point(6, 6);
            this.grpAccount.Name = "grpAccount";
            this.grpAccount.Size = new Size(440, 140);
            this.grpAccount.TabIndex = 0;
            this.grpAccount.TabStop = false;
            this.grpAccount.Text = "iwaraアカウント";

            // 
            // lblUsername
            // 
            this.lblUsername.AutoSize = true;
            this.lblUsername.Location = new Point(10, 30);
            this.lblUsername.Name = "lblUsername";
            this.lblUsername.Size = new Size(60, 15);
            this.lblUsername.TabIndex = 0;
            this.lblUsername.Text = "ユーザー名:";

            // 
            // txtUsername
            // 
            this.txtUsername.Location = new Point(80, 27);
            this.txtUsername.Name = "txtUsername";
            this.txtUsername.Size = new Size(200, 23);
            this.txtUsername.TabIndex = 1;

            // 
            // lblPassword
            // 
            this.lblPassword.AutoSize = true;
            this.lblPassword.Location = new Point(10, 60);
            this.lblPassword.Name = "lblPassword";
            this.lblPassword.Size = new Size(60, 15);
            this.lblPassword.TabIndex = 2;
            this.lblPassword.Text = "パスワード:";

            // 
            // txtPassword
            // 
            this.txtPassword.Location = new Point(80, 57);
            this.txtPassword.Name = "txtPassword";
            this.txtPassword.PasswordChar = '●';
            this.txtPassword.Size = new Size(200, 23);
            this.txtPassword.TabIndex = 3;

            // 
            // lblAccountNote
            // 
            this.lblAccountNote.AutoSize = true;
            this.lblAccountNote.ForeColor = Color.Gray;
            this.lblAccountNote.Location = new Point(10, 95);
            this.lblAccountNote.Name = "lblAccountNote";
            this.lblAccountNote.Size = new Size(350, 30);
            this.lblAccountNote.TabIndex = 4;
            this.lblAccountNote.Text = "※ R-18コンテンツやプライベート動画をダウンロードするには\r\n　 iwaraアカウントでのログインが必要です。";

            // 
            // tabBackup
            // 
            this.tabBackup.Controls.Add(this.grpExport);
            this.tabBackup.Controls.Add(this.grpImport);
            this.tabBackup.Location = new Point(4, 24);
            this.tabBackup.Name = "tabBackup";
            this.tabBackup.Padding = new Padding(3);
            this.tabBackup.Size = new Size(452, 352);
            this.tabBackup.TabIndex = 2;
            this.tabBackup.Text = "バックアップ";
            this.tabBackup.UseVisualStyleBackColor = true;

            // 
            // grpExport
            // 
            this.grpExport.Controls.Add(this.btnExportSettings);
            this.grpExport.Controls.Add(this.btnExportSubscriptions);
            this.grpExport.Location = new Point(6, 6);
            this.grpExport.Name = "grpExport";
            this.grpExport.Size = new Size(440, 70);
            this.grpExport.TabIndex = 0;
            this.grpExport.TabStop = false;
            this.grpExport.Text = "エクスポート";

            // 
            // btnExportSettings
            // 
            this.btnExportSettings.Location = new Point(10, 30);
            this.btnExportSettings.Name = "btnExportSettings";
            this.btnExportSettings.Size = new Size(130, 27);
            this.btnExportSettings.TabIndex = 0;
            this.btnExportSettings.Text = "設定をエクスポート";
            this.btnExportSettings.UseVisualStyleBackColor = true;
            this.btnExportSettings.Click += new EventHandler(this.btnExportSettings_Click);

            // 
            // btnExportSubscriptions
            // 
            this.btnExportSubscriptions.Location = new Point(150, 30);
            this.btnExportSubscriptions.Name = "btnExportSubscriptions";
            this.btnExportSubscriptions.Size = new Size(160, 27);
            this.btnExportSubscriptions.TabIndex = 1;
            this.btnExportSubscriptions.Text = "購読リストをエクスポート";
            this.btnExportSubscriptions.UseVisualStyleBackColor = true;
            this.btnExportSubscriptions.Click += new EventHandler(this.btnExportSubscriptions_Click);

            // 
            // grpImport
            // 
            this.grpImport.Controls.Add(this.btnImportSettings);
            this.grpImport.Controls.Add(this.btnImportSubscriptions);
            this.grpImport.Location = new Point(6, 82);
            this.grpImport.Name = "grpImport";
            this.grpImport.Size = new Size(440, 70);
            this.grpImport.TabIndex = 1;
            this.grpImport.TabStop = false;
            this.grpImport.Text = "インポート";

            // 
            // btnImportSettings
            // 
            this.btnImportSettings.Location = new Point(10, 30);
            this.btnImportSettings.Name = "btnImportSettings";
            this.btnImportSettings.Size = new Size(130, 27);
            this.btnImportSettings.TabIndex = 0;
            this.btnImportSettings.Text = "設定をインポート";
            this.btnImportSettings.UseVisualStyleBackColor = true;
            this.btnImportSettings.Click += new EventHandler(this.btnImportSettings_Click);

            // 
            // btnImportSubscriptions
            // 
            this.btnImportSubscriptions.Location = new Point(150, 30);
            this.btnImportSubscriptions.Name = "btnImportSubscriptions";
            this.btnImportSubscriptions.Size = new Size(160, 27);
            this.btnImportSubscriptions.TabIndex = 1;
            this.btnImportSubscriptions.Text = "購読リストをインポート";
            this.btnImportSubscriptions.UseVisualStyleBackColor = true;
            this.btnImportSubscriptions.Click += new EventHandler(this.btnImportSubscriptions_Click);

            // 
            // btnOk
            // 
            this.btnOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.btnOk.Location = new Point(236, 400);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new Size(75, 27);
            this.btnOk.TabIndex = 1;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new EventHandler(this.btnOk_Click);

            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.btnCancel.DialogResult = DialogResult.Cancel;
            this.btnCancel.Location = new Point(317, 400);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new Size(75, 27);
            this.btnCancel.TabIndex = 2;
            this.btnCancel.Text = "キャンセル";
            this.btnCancel.UseVisualStyleBackColor = true;

            // 
            // btnApply
            // 
            this.btnApply.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.btnApply.Location = new Point(398, 400);
            this.btnApply.Name = "btnApply";
            this.btnApply.Size = new Size(75, 27);
            this.btnApply.TabIndex = 3;
            this.btnApply.Text = "適用";
            this.btnApply.UseVisualStyleBackColor = true;
            this.btnApply.Click += new EventHandler(this.btnApply_Click);

            // 
            // SettingsForm
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new Size(484, 441);
            this.Controls.Add(this.tabControl);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnApply);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SettingsForm";
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "設定";
            this.Load += new EventHandler(this.SettingsForm_Load);

            this.tabControl.ResumeLayout(false);
            this.tabGeneral.ResumeLayout(false);
            this.tabAccount.ResumeLayout(false);
            this.tabBackup.ResumeLayout(false);
            this.grpDownload.ResumeLayout(false);
            this.grpDownload.PerformLayout();
            this.grpAutoCheck.ResumeLayout(false);
            this.grpAutoCheck.PerformLayout();
            this.grpNotification.ResumeLayout(false);
            this.grpNotification.PerformLayout();
            this.grpAccount.ResumeLayout(false);
            this.grpAccount.PerformLayout();
            this.grpExport.ResumeLayout(false);
            this.grpImport.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.numConcurrent)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numRetry)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        private TabControl tabControl;
        private TabPage tabGeneral;
        private TabPage tabAccount;
        private TabPage tabBackup;
        private GroupBox grpDownload;
        private Label lblDownloadFolder;
        private TextBox txtDownloadFolder;
        private Button btnBrowseFolder;
        private Label lblQuality;
        private ComboBox cmbQuality;
        private Label lblConcurrent;
        private NumericUpDown numConcurrent;
        private Label lblRetry;
        private NumericUpDown numRetry;
        private GroupBox grpAutoCheck;
        private CheckBox chkAutoCheck;
        private Label lblCheckInterval;
        private ComboBox cmbCheckInterval;
        private CheckBox chkAutoDownload;
        private GroupBox grpNotification;
        private CheckBox chkToast;
        private CheckBox chkStartMinimized;
        private CheckBox chkMinimizeToTray;
        private GroupBox grpAccount;
        private Label lblUsername;
        private TextBox txtUsername;
        private Label lblPassword;
        private TextBox txtPassword;
        private Label lblAccountNote;
        private GroupBox grpExport;
        private Button btnExportSettings;
        private Button btnExportSubscriptions;
        private GroupBox grpImport;
        private Button btnImportSettings;
        private Button btnImportSubscriptions;
        private Button btnOk;
        private Button btnCancel;
        private Button btnApply;
    }
}
