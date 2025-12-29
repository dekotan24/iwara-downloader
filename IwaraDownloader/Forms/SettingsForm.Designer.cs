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
            this.tabAdvanced = new TabPage();
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
            this.grpPython = new GroupBox();
            this.lblPythonPath = new Label();
            this.txtPythonPath = new TextBox();
            this.btnBrowsePython = new Button();
            this.lblPythonNote = new Label();
            
            this.grpAccount = new GroupBox();
            this.lblEmail = new Label();
            this.txtEmail = new TextBox();
            this.lblPassword = new Label();
            this.txtPassword = new TextBox();
            this.lblLoginStatus = new Label();
            this.btnReLogin = new Button();
            this.lblAccountNote = new Label();

            // 詳細設定（レート制限）
            this.grpRateLimit = new GroupBox();
            this.lblApiDelay = new Label();
            this.numApiDelay = new NumericUpDown();
            this.lblApiDelayUnit = new Label();
            this.lblDownloadDelay = new Label();
            this.numDownloadDelay = new NumericUpDown();
            this.lblDownloadDelayUnit = new Label();
            this.lblChannelDelay = new Label();
            this.numChannelDelay = new NumericUpDown();
            this.lblChannelDelayUnit = new Label();
            this.lblPageDelay = new Label();
            this.numPageDelay = new NumericUpDown();
            this.lblPageDelayUnit = new Label();

            this.grpErrorHandling = new GroupBox();
            this.lblRateLimitBase = new Label();
            this.numRateLimitBase = new NumericUpDown();
            this.lblRateLimitBaseUnit = new Label();
            this.lblRateLimitMax = new Label();
            this.numRateLimitMax = new NumericUpDown();
            this.lblRateLimitMaxUnit = new Label();
            this.chkExponentialBackoff = new CheckBox();
            this.lblAdvancedNote = new Label();

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
            this.tabAdvanced.SuspendLayout();
            this.tabBackup.SuspendLayout();
            this.grpDownload.SuspendLayout();
            this.grpAutoCheck.SuspendLayout();
            this.grpNotification.SuspendLayout();
            this.grpPython.SuspendLayout();
            this.grpAccount.SuspendLayout();
            this.grpRateLimit.SuspendLayout();
            this.grpErrorHandling.SuspendLayout();
            this.grpExport.SuspendLayout();
            this.grpImport.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numConcurrent)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numRetry)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numApiDelay)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numDownloadDelay)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numChannelDelay)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numPageDelay)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numRateLimitBase)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numRateLimitMax)).BeginInit();
            this.SuspendLayout();

            // 
            // tabControl
            // 
            this.tabControl.Controls.Add(this.tabGeneral);
            this.tabControl.Controls.Add(this.tabAccount);
            this.tabControl.Controls.Add(this.tabAdvanced);
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
            this.tabAccount.Controls.Add(this.grpPython);
            this.tabAccount.Controls.Add(this.grpAccount);
            this.tabAccount.Location = new Point(4, 24);
            this.tabAccount.Name = "tabAccount";
            this.tabAccount.Padding = new Padding(3);
            this.tabAccount.Size = new Size(452, 352);
            this.tabAccount.TabIndex = 1;
            this.tabAccount.Text = "アカウント";
            this.tabAccount.UseVisualStyleBackColor = true;

            // 
            // grpPython
            // 
            this.grpPython.Controls.Add(this.lblPythonPath);
            this.grpPython.Controls.Add(this.txtPythonPath);
            this.grpPython.Controls.Add(this.btnBrowsePython);
            this.grpPython.Controls.Add(this.lblPythonNote);
            this.grpPython.Location = new Point(6, 6);
            this.grpPython.Name = "grpPython";
            this.grpPython.Size = new Size(440, 90);
            this.grpPython.TabIndex = 0;
            this.grpPython.TabStop = false;
            this.grpPython.Text = "Python環境";

            // lblPythonPath
            this.lblPythonPath.AutoSize = true;
            this.lblPythonPath.Location = new Point(10, 30);
            this.lblPythonPath.Name = "lblPythonPath";
            this.lblPythonPath.Size = new Size(80, 15);
            this.lblPythonPath.Text = "Pythonパス:";

            // txtPythonPath
            this.txtPythonPath.Location = new Point(100, 27);
            this.txtPythonPath.Name = "txtPythonPath";
            this.txtPythonPath.Size = new Size(260, 23);
            this.txtPythonPath.TabIndex = 1;

            // btnBrowsePython
            this.btnBrowsePython.Location = new Point(366, 26);
            this.btnBrowsePython.Name = "btnBrowsePython";
            this.btnBrowsePython.Size = new Size(60, 25);
            this.btnBrowsePython.TabIndex = 2;
            this.btnBrowsePython.Text = "参照...";
            this.btnBrowsePython.UseVisualStyleBackColor = true;
            this.btnBrowsePython.Click += new EventHandler(this.btnBrowsePython_Click);

            // lblPythonNote
            this.lblPythonNote.AutoSize = true;
            this.lblPythonNote.ForeColor = Color.Gray;
            this.lblPythonNote.Location = new Point(10, 60);
            this.lblPythonNote.Name = "lblPythonNote";
            this.lblPythonNote.Size = new Size(400, 15);
            this.lblPythonNote.Text = "※ 初回セットアップ済みの場合、パス変更のみ行います（ライブラリの再インストールは不要）";

            // 
            // grpAccount
            // 
            this.grpAccount.Controls.Add(this.lblEmail);
            this.grpAccount.Controls.Add(this.txtEmail);
            this.grpAccount.Controls.Add(this.lblPassword);
            this.grpAccount.Controls.Add(this.txtPassword);
            this.grpAccount.Controls.Add(this.lblLoginStatus);
            this.grpAccount.Controls.Add(this.btnReLogin);
            this.grpAccount.Controls.Add(this.lblAccountNote);
            this.grpAccount.Location = new Point(6, 102);
            this.grpAccount.Name = "grpAccount";
            this.grpAccount.Size = new Size(440, 165);
            this.grpAccount.TabIndex = 1;
            this.grpAccount.TabStop = false;
            this.grpAccount.Text = "iwaraアカウント";

            // 
            // lblEmail
            // 
            this.lblEmail.AutoSize = true;
            this.lblEmail.Location = new Point(10, 30);
            this.lblEmail.Name = "lblEmail";
            this.lblEmail.Size = new Size(80, 15);
            this.lblEmail.TabIndex = 0;
            this.lblEmail.Text = "メールアドレス:";

            // 
            // txtEmail
            // 
            this.txtEmail.Location = new Point(100, 27);
            this.txtEmail.Name = "txtEmail";
            this.txtEmail.Size = new Size(200, 23);
            this.txtEmail.TabIndex = 1;

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
            this.txtPassword.Location = new Point(100, 57);
            this.txtPassword.Name = "txtPassword";
            this.txtPassword.PasswordChar = '●';
            this.txtPassword.Size = new Size(200, 23);
            this.txtPassword.TabIndex = 3;

            // 
            // lblLoginStatus
            // 
            this.lblLoginStatus.AutoSize = true;
            this.lblLoginStatus.Location = new Point(310, 30);
            this.lblLoginStatus.Name = "lblLoginStatus";
            this.lblLoginStatus.Size = new Size(100, 15);
            this.lblLoginStatus.TabIndex = 4;
            this.lblLoginStatus.Text = "(未ログイン)";
            this.lblLoginStatus.ForeColor = Color.Gray;

            // 
            // btnReLogin
            // 
            this.btnReLogin.Location = new Point(310, 55);
            this.btnReLogin.Name = "btnReLogin";
            this.btnReLogin.Size = new Size(115, 27);
            this.btnReLogin.TabIndex = 5;
            this.btnReLogin.Text = "再ログイン";
            this.btnReLogin.UseVisualStyleBackColor = true;
            this.btnReLogin.Click += new EventHandler(this.btnReLogin_Click);

            // 
            // lblAccountNote
            // 
            this.lblAccountNote.AutoSize = true;
            this.lblAccountNote.ForeColor = Color.Gray;
            this.lblAccountNote.Location = new Point(10, 95);
            this.lblAccountNote.Name = "lblAccountNote";
            this.lblAccountNote.Size = new Size(400, 60);
            this.lblAccountNote.TabIndex = 6;
            this.lblAccountNote.Text = "※ R-18コンテンツやプライベート動画をダウンロードするには\r\n　 iwaraアカウントでのログインが必要です。\r\n※ メールアドレス/パスワード変更後は「再ログイン」を押してください。";

            // 
            // tabAdvanced
            // 
            this.tabAdvanced.Controls.Add(this.grpRateLimit);
            this.tabAdvanced.Controls.Add(this.grpErrorHandling);
            this.tabAdvanced.Controls.Add(this.lblAdvancedNote);
            this.tabAdvanced.Location = new Point(4, 24);
            this.tabAdvanced.Name = "tabAdvanced";
            this.tabAdvanced.Padding = new Padding(3);
            this.tabAdvanced.Size = new Size(452, 352);
            this.tabAdvanced.TabIndex = 2;
            this.tabAdvanced.Text = "詳細設定";
            this.tabAdvanced.UseVisualStyleBackColor = true;

            // 
            // grpRateLimit
            // 
            this.grpRateLimit.Controls.Add(this.lblApiDelay);
            this.grpRateLimit.Controls.Add(this.numApiDelay);
            this.grpRateLimit.Controls.Add(this.lblApiDelayUnit);
            this.grpRateLimit.Controls.Add(this.lblDownloadDelay);
            this.grpRateLimit.Controls.Add(this.numDownloadDelay);
            this.grpRateLimit.Controls.Add(this.lblDownloadDelayUnit);
            this.grpRateLimit.Controls.Add(this.lblChannelDelay);
            this.grpRateLimit.Controls.Add(this.numChannelDelay);
            this.grpRateLimit.Controls.Add(this.lblChannelDelayUnit);
            this.grpRateLimit.Controls.Add(this.lblPageDelay);
            this.grpRateLimit.Controls.Add(this.numPageDelay);
            this.grpRateLimit.Controls.Add(this.lblPageDelayUnit);
            this.grpRateLimit.Location = new Point(6, 6);
            this.grpRateLimit.Name = "grpRateLimit";
            this.grpRateLimit.Size = new Size(440, 140);
            this.grpRateLimit.TabIndex = 0;
            this.grpRateLimit.TabStop = false;
            this.grpRateLimit.Text = "レート制限設定";

            // lblApiDelay
            this.lblApiDelay.AutoSize = true;
            this.lblApiDelay.Location = new Point(10, 25);
            this.lblApiDelay.Name = "lblApiDelay";
            this.lblApiDelay.Size = new Size(120, 15);
            this.lblApiDelay.Text = "APIリクエスト間隔:";

            // numApiDelay
            this.numApiDelay.Location = new Point(140, 22);
            this.numApiDelay.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            this.numApiDelay.Minimum = new decimal(new int[] { 500, 0, 0, 0 });
            this.numApiDelay.Name = "numApiDelay";
            this.numApiDelay.Size = new Size(80, 23);
            this.numApiDelay.Value = new decimal(new int[] { 1000, 0, 0, 0 });

            // lblApiDelayUnit
            this.lblApiDelayUnit.AutoSize = true;
            this.lblApiDelayUnit.Location = new Point(225, 25);
            this.lblApiDelayUnit.Text = "ミリ秒";

            // lblDownloadDelay
            this.lblDownloadDelay.AutoSize = true;
            this.lblDownloadDelay.Location = new Point(10, 55);
            this.lblDownloadDelay.Name = "lblDownloadDelay";
            this.lblDownloadDelay.Size = new Size(120, 15);
            this.lblDownloadDelay.Text = "ダウンロード間隔:";

            // numDownloadDelay
            this.numDownloadDelay.Location = new Point(140, 52);
            this.numDownloadDelay.Maximum = new decimal(new int[] { 60000, 0, 0, 0 });
            this.numDownloadDelay.Minimum = new decimal(new int[] { 1000, 0, 0, 0 });
            this.numDownloadDelay.Name = "numDownloadDelay";
            this.numDownloadDelay.Size = new Size(80, 23);
            this.numDownloadDelay.Value = new decimal(new int[] { 3000, 0, 0, 0 });

            // lblDownloadDelayUnit
            this.lblDownloadDelayUnit.AutoSize = true;
            this.lblDownloadDelayUnit.Location = new Point(225, 55);
            this.lblDownloadDelayUnit.Text = "ミリ秒";

            // lblChannelDelay
            this.lblChannelDelay.AutoSize = true;
            this.lblChannelDelay.Location = new Point(10, 85);
            this.lblChannelDelay.Name = "lblChannelDelay";
            this.lblChannelDelay.Size = new Size(120, 15);
            this.lblChannelDelay.Text = "チャンネル巡回間隔:";

            // numChannelDelay
            this.numChannelDelay.Location = new Point(140, 82);
            this.numChannelDelay.Maximum = new decimal(new int[] { 120000, 0, 0, 0 });
            this.numChannelDelay.Minimum = new decimal(new int[] { 1000, 0, 0, 0 });
            this.numChannelDelay.Name = "numChannelDelay";
            this.numChannelDelay.Size = new Size(80, 23);
            this.numChannelDelay.Value = new decimal(new int[] { 5000, 0, 0, 0 });

            // lblChannelDelayUnit
            this.lblChannelDelayUnit.AutoSize = true;
            this.lblChannelDelayUnit.Location = new Point(225, 85);
            this.lblChannelDelayUnit.Text = "ミリ秒";

            // lblPageDelay
            this.lblPageDelay.AutoSize = true;
            this.lblPageDelay.Location = new Point(10, 115);
            this.lblPageDelay.Name = "lblPageDelay";
            this.lblPageDelay.Size = new Size(120, 15);
            this.lblPageDelay.Text = "ページ取得間隔:";

            // numPageDelay
            this.numPageDelay.Location = new Point(140, 112);
            this.numPageDelay.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            this.numPageDelay.Minimum = new decimal(new int[] { 100, 0, 0, 0 });
            this.numPageDelay.Name = "numPageDelay";
            this.numPageDelay.Size = new Size(80, 23);
            this.numPageDelay.Value = new decimal(new int[] { 500, 0, 0, 0 });

            // lblPageDelayUnit
            this.lblPageDelayUnit.AutoSize = true;
            this.lblPageDelayUnit.Location = new Point(225, 115);
            this.lblPageDelayUnit.Text = "ミリ秒";

            // 
            // grpErrorHandling
            // 
            this.grpErrorHandling.Controls.Add(this.lblRateLimitBase);
            this.grpErrorHandling.Controls.Add(this.numRateLimitBase);
            this.grpErrorHandling.Controls.Add(this.lblRateLimitBaseUnit);
            this.grpErrorHandling.Controls.Add(this.lblRateLimitMax);
            this.grpErrorHandling.Controls.Add(this.numRateLimitMax);
            this.grpErrorHandling.Controls.Add(this.lblRateLimitMaxUnit);
            this.grpErrorHandling.Controls.Add(this.chkExponentialBackoff);
            this.grpErrorHandling.Location = new Point(6, 152);
            this.grpErrorHandling.Name = "grpErrorHandling";
            this.grpErrorHandling.Size = new Size(440, 115);
            this.grpErrorHandling.TabIndex = 1;
            this.grpErrorHandling.TabStop = false;
            this.grpErrorHandling.Text = "エラー時の動作 (429/403)";

            // lblRateLimitBase
            this.lblRateLimitBase.AutoSize = true;
            this.lblRateLimitBase.Location = new Point(10, 25);
            this.lblRateLimitBase.Name = "lblRateLimitBase";
            this.lblRateLimitBase.Size = new Size(120, 15);
            this.lblRateLimitBase.Text = "基本待機時間:";

            // numRateLimitBase
            this.numRateLimitBase.Location = new Point(140, 22);
            this.numRateLimitBase.Maximum = new decimal(new int[] { 300000, 0, 0, 0 });
            this.numRateLimitBase.Minimum = new decimal(new int[] { 5000, 0, 0, 0 });
            this.numRateLimitBase.Increment = new decimal(new int[] { 5000, 0, 0, 0 });
            this.numRateLimitBase.Name = "numRateLimitBase";
            this.numRateLimitBase.Size = new Size(80, 23);
            this.numRateLimitBase.Value = new decimal(new int[] { 30000, 0, 0, 0 });

            // lblRateLimitBaseUnit
            this.lblRateLimitBaseUnit.AutoSize = true;
            this.lblRateLimitBaseUnit.Location = new Point(225, 25);
            this.lblRateLimitBaseUnit.Text = "ミリ秒";

            // lblRateLimitMax
            this.lblRateLimitMax.AutoSize = true;
            this.lblRateLimitMax.Location = new Point(10, 55);
            this.lblRateLimitMax.Name = "lblRateLimitMax";
            this.lblRateLimitMax.Size = new Size(120, 15);
            this.lblRateLimitMax.Text = "最大待機時間:";

            // numRateLimitMax
            this.numRateLimitMax.Location = new Point(140, 52);
            this.numRateLimitMax.Maximum = new decimal(new int[] { 600000, 0, 0, 0 });
            this.numRateLimitMax.Minimum = new decimal(new int[] { 30000, 0, 0, 0 });
            this.numRateLimitMax.Increment = new decimal(new int[] { 30000, 0, 0, 0 });
            this.numRateLimitMax.Name = "numRateLimitMax";
            this.numRateLimitMax.Size = new Size(80, 23);
            this.numRateLimitMax.Value = new decimal(new int[] { 300000, 0, 0, 0 });

            // lblRateLimitMaxUnit
            this.lblRateLimitMaxUnit.AutoSize = true;
            this.lblRateLimitMaxUnit.Location = new Point(225, 55);
            this.lblRateLimitMaxUnit.Text = "ミリ秒";

            // chkExponentialBackoff
            this.chkExponentialBackoff.AutoSize = true;
            this.chkExponentialBackoff.Checked = true;
            this.chkExponentialBackoff.CheckState = CheckState.Checked;
            this.chkExponentialBackoff.Location = new Point(10, 85);
            this.chkExponentialBackoff.Name = "chkExponentialBackoff";
            this.chkExponentialBackoff.Size = new Size(320, 19);
            this.chkExponentialBackoff.Text = "エクスポネンシャルバックオフを有効にする（連続エラー時に待機時間を増加）";
            this.chkExponentialBackoff.UseVisualStyleBackColor = true;

            // lblAdvancedNote
            this.lblAdvancedNote.AutoSize = true;
            this.lblAdvancedNote.ForeColor = Color.Gray;
            this.lblAdvancedNote.Location = new Point(10, 280);
            this.lblAdvancedNote.Name = "lblAdvancedNote";
            this.lblAdvancedNote.Size = new Size(420, 60);
            this.lblAdvancedNote.Text = "※ レート制限設定はサーバー負荷を軽減し、403/429エラーを防ぎます。\r\n※ 値が小さすぎるとアクセス制限される可能性があります。\r\n※ 大きすぎるとダウンロードに時間がかかります。";

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
            this.Icon = Properties.Resources.icon;
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
            this.tabAdvanced.ResumeLayout(false);
            this.tabAdvanced.PerformLayout();
            this.tabBackup.ResumeLayout(false);
            this.grpDownload.ResumeLayout(false);
            this.grpDownload.PerformLayout();
            this.grpAutoCheck.ResumeLayout(false);
            this.grpAutoCheck.PerformLayout();
            this.grpNotification.ResumeLayout(false);
            this.grpNotification.PerformLayout();
            this.grpPython.ResumeLayout(false);
            this.grpPython.PerformLayout();
            this.grpAccount.ResumeLayout(false);
            this.grpAccount.PerformLayout();
            this.grpRateLimit.ResumeLayout(false);
            this.grpRateLimit.PerformLayout();
            this.grpErrorHandling.ResumeLayout(false);
            this.grpErrorHandling.PerformLayout();
            this.grpExport.ResumeLayout(false);
            this.grpImport.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.numConcurrent)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numRetry)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numApiDelay)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numDownloadDelay)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numChannelDelay)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numPageDelay)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numRateLimitBase)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numRateLimitMax)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        private TabControl tabControl;
        private TabPage tabGeneral;
        private TabPage tabAccount;
        private TabPage tabAdvanced;
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
        private GroupBox grpPython;
        private Label lblPythonPath;
        private TextBox txtPythonPath;
        private Button btnBrowsePython;
        private Label lblPythonNote;
        private GroupBox grpAccount;
        private Label lblEmail;
        private TextBox txtEmail;
        private Label lblPassword;
        private TextBox txtPassword;
        private Label lblLoginStatus;
        private Button btnReLogin;
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

        // 詳細設定（レート制限）
        private GroupBox grpRateLimit;
        private Label lblApiDelay;
        private NumericUpDown numApiDelay;
        private Label lblApiDelayUnit;
        private Label lblDownloadDelay;
        private NumericUpDown numDownloadDelay;
        private Label lblDownloadDelayUnit;
        private Label lblChannelDelay;
        private NumericUpDown numChannelDelay;
        private Label lblChannelDelayUnit;
        private Label lblPageDelay;
        private NumericUpDown numPageDelay;
        private Label lblPageDelayUnit;
        private GroupBox grpErrorHandling;
        private Label lblRateLimitBase;
        private NumericUpDown numRateLimitBase;
        private Label lblRateLimitBaseUnit;
        private Label lblRateLimitMax;
        private NumericUpDown numRateLimitMax;
        private Label lblRateLimitMaxUnit;
        private CheckBox chkExponentialBackoff;
        private Label lblAdvancedNote;
    }
}
