namespace IwaraDownloader.Forms
{
    partial class MainForm
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
            this.components = new System.ComponentModel.Container();
            
            // メインコンテナ
            this.mainSplitContainer = new SplitContainer();
            this.contentSplitContainer = new SplitContainer();

            // 上部ツールバー
            this.toolStrip = new ToolStrip();
            this.btnAddUser = new ToolStripButton();
            this.btnAddVideo = new ToolStripButton();
            this.toolStripSeparator1 = new ToolStripSeparator();
            this.btnCheckNow = new ToolStripButton();
            this.btnStartAll = new ToolStripButton();
            this.btnStopAll = new ToolStripButton();
            this.toolStripSeparator2 = new ToolStripSeparator();
            this.btnSettings = new ToolStripButton();
            this.toolStripSeparator3 = new ToolStripSeparator();
            this.btnSetup = new ToolStripButton();
            this.toolStripSeparator4 = new ToolStripSeparator();
            this.btnLogin = new ToolStripButton();
            this.lblLoginStatus = new ToolStripLabel();

            // URLテキストボックス
            this.txtUrl = new TextBox();
            this.btnPasteAndAdd = new Button();

            // 左: チャンネルツリー
            this.treeViewChannels = new TreeView();
            this.lblChannelHeader = new Label();
            this.panelChannelHeader = new Panel();

            // 右: 動画リスト
            this.listViewVideos = new ListView();
            this.colVideoTitle = new ColumnHeader();
            this.colVideoStatus = new ColumnHeader();
            this.colVideoProgress = new ColumnHeader();
            this.colVideoSize = new ColumnHeader();
            this.colVideoDate = new ColumnHeader();
            this.lblVideoHeader = new Label();
            this.panelVideoHeader = new Panel();

            // ステータスバー
            this.statusStrip = new StatusStrip();
            this.lblStatus = new ToolStripStatusLabel();
            this.lblDownloadCount = new ToolStripStatusLabel();
            this.progressBar = new ToolStripProgressBar();

            // タスクトレイアイコン
            this.notifyIcon = new NotifyIcon(this.components);
            this.contextMenuTray = new ContextMenuStrip(this.components);
            this.menuShow = new ToolStripMenuItem();
            this.menuSeparator = new ToolStripSeparator();
            this.menuExit = new ToolStripMenuItem();

            // コンテキストメニュー（チャンネル）
            this.contextMenuChannel = new ContextMenuStrip(this.components);
            this.menuChOpen = new ToolStripMenuItem();
            this.menuChCheckNow = new ToolStripMenuItem();
            this.menuChDownloadAll = new ToolStripMenuItem();
            this.menuChSeparator1 = new ToolStripSeparator();
            this.menuChSetSavePath = new ToolStripMenuItem();
            this.menuChSeparator2 = new ToolStripSeparator();
            this.menuChEnable = new ToolStripMenuItem();
            this.menuChDisable = new ToolStripMenuItem();
            this.menuChSeparator3 = new ToolStripSeparator();
            this.menuChDelete = new ToolStripMenuItem();

            // コンテキストメニュー（動画）
            this.contextMenuVideo = new ContextMenuStrip(this.components);
            this.menuVidDownload = new ToolStripMenuItem();
            this.menuVidCancel = new ToolStripMenuItem();
            this.menuVidRefreshInfo = new ToolStripMenuItem();
            this.menuVidSeparator1 = new ToolStripSeparator();
            this.menuVidPlay = new ToolStripMenuItem();
            this.menuVidOpenFolder = new ToolStripMenuItem();
            this.menuVidSeparator2 = new ToolStripSeparator();
            this.menuVidOpenPage = new ToolStripMenuItem();

            // ImageList
            this.imageListTree = new ImageList(this.components);

            ((System.ComponentModel.ISupportInitialize)(this.mainSplitContainer)).BeginInit();
            this.mainSplitContainer.Panel1.SuspendLayout();
            this.mainSplitContainer.Panel2.SuspendLayout();
            this.mainSplitContainer.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.contentSplitContainer)).BeginInit();
            this.contentSplitContainer.Panel1.SuspendLayout();
            this.contentSplitContainer.Panel2.SuspendLayout();
            this.contentSplitContainer.SuspendLayout();
            this.toolStrip.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.contextMenuTray.SuspendLayout();
            this.contextMenuChannel.SuspendLayout();
            this.contextMenuVideo.SuspendLayout();
            this.SuspendLayout();

            // 
            // mainSplitContainer
            // 
            this.mainSplitContainer.Dock = DockStyle.Fill;
            this.mainSplitContainer.FixedPanel = FixedPanel.Panel1;
            this.mainSplitContainer.Location = new Point(0, 25);
            this.mainSplitContainer.Name = "mainSplitContainer";
            this.mainSplitContainer.Orientation = Orientation.Horizontal;
            this.mainSplitContainer.Panel1MinSize = 35;
            this.mainSplitContainer.Size = new Size(1000, 575);
            this.mainSplitContainer.SplitterDistance = 35;
            this.mainSplitContainer.IsSplitterFixed = true;
            this.mainSplitContainer.TabIndex = 0;
            // 
            // mainSplitContainer.Panel1 - URL入力
            // 
            this.mainSplitContainer.Panel1.Controls.Add(this.txtUrl);
            this.mainSplitContainer.Panel1.Controls.Add(this.btnPasteAndAdd);
            // 
            // mainSplitContainer.Panel2 - コンテンツ
            // 
            this.mainSplitContainer.Panel2.Controls.Add(this.contentSplitContainer);

            // 
            // contentSplitContainer - 左右分割
            // 
            this.contentSplitContainer.Dock = DockStyle.Fill;
            this.contentSplitContainer.Location = new Point(0, 0);
            this.contentSplitContainer.Name = "contentSplitContainer";
            this.contentSplitContainer.Size = new Size(1000, 536);
            this.contentSplitContainer.SplitterDistance = 250;
            this.contentSplitContainer.TabIndex = 0;
            // 
            // contentSplitContainer.Panel1 - チャンネルツリー
            // 
            this.contentSplitContainer.Panel1.Controls.Add(this.treeViewChannels);
            this.contentSplitContainer.Panel1.Controls.Add(this.panelChannelHeader);
            this.contentSplitContainer.Panel1MinSize = 150;
            // 
            // contentSplitContainer.Panel2 - 動画リスト
            // 
            this.contentSplitContainer.Panel2.Controls.Add(this.listViewVideos);
            this.contentSplitContainer.Panel2.Controls.Add(this.panelVideoHeader);

            // 
            // txtUrl
            // 
            this.txtUrl.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.txtUrl.Font = new Font("Yu Gothic UI", 10F);
            this.txtUrl.Location = new Point(8, 5);
            this.txtUrl.Name = "txtUrl";
            this.txtUrl.PlaceholderText = "URLを入力、またはリンクをペースト...";
            this.txtUrl.Size = new Size(870, 25);
            this.txtUrl.TabIndex = 0;
            this.txtUrl.KeyDown += new KeyEventHandler(this.txtUrl_KeyDown);

            // 
            // btnPasteAndAdd
            // 
            this.btnPasteAndAdd.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.btnPasteAndAdd.Location = new Point(885, 4);
            this.btnPasteAndAdd.Name = "btnPasteAndAdd";
            this.btnPasteAndAdd.Size = new Size(105, 27);
            this.btnPasteAndAdd.TabIndex = 1;
            this.btnPasteAndAdd.Text = "貼り付けて追加";
            this.btnPasteAndAdd.UseVisualStyleBackColor = true;
            this.btnPasteAndAdd.Click += new EventHandler(this.btnPasteAndAdd_Click);

            // 
            // panelChannelHeader
            // 
            this.panelChannelHeader.BackColor = Color.FromArgb(240, 240, 240);
            this.panelChannelHeader.BorderStyle = BorderStyle.FixedSingle;
            this.panelChannelHeader.Controls.Add(this.lblChannelHeader);
            this.panelChannelHeader.Dock = DockStyle.Top;
            this.panelChannelHeader.Location = new Point(0, 0);
            this.panelChannelHeader.Name = "panelChannelHeader";
            this.panelChannelHeader.Size = new Size(250, 25);
            this.panelChannelHeader.TabIndex = 1;

            // 
            // lblChannelHeader
            // 
            this.lblChannelHeader.AutoSize = true;
            this.lblChannelHeader.Font = new Font("Yu Gothic UI", 9F, FontStyle.Bold);
            this.lblChannelHeader.Location = new Point(5, 4);
            this.lblChannelHeader.Name = "lblChannelHeader";
            this.lblChannelHeader.Size = new Size(80, 15);
            this.lblChannelHeader.TabIndex = 0;
            this.lblChannelHeader.Text = "登録チャンネル";

            // 
            // treeViewChannels
            // 
            this.treeViewChannels.ContextMenuStrip = this.contextMenuChannel;
            this.treeViewChannels.Dock = DockStyle.Fill;
            this.treeViewChannels.Font = new Font("Yu Gothic UI", 9F);
            this.treeViewChannels.ImageList = this.imageListTree;
            this.treeViewChannels.Location = new Point(0, 25);
            this.treeViewChannels.Name = "treeViewChannels";
            this.treeViewChannels.Size = new Size(250, 511);
            this.treeViewChannels.TabIndex = 0;
            this.treeViewChannels.AfterSelect += new TreeViewEventHandler(this.treeViewChannels_AfterSelect);
            this.treeViewChannels.NodeMouseDoubleClick += new TreeNodeMouseClickEventHandler(this.treeViewChannels_NodeMouseDoubleClick);

            // 
            // panelVideoHeader
            // 
            this.panelVideoHeader.BackColor = Color.FromArgb(240, 240, 240);
            this.panelVideoHeader.BorderStyle = BorderStyle.FixedSingle;
            this.panelVideoHeader.Controls.Add(this.lblVideoHeader);
            this.panelVideoHeader.Dock = DockStyle.Top;
            this.panelVideoHeader.Location = new Point(0, 0);
            this.panelVideoHeader.Name = "panelVideoHeader";
            this.panelVideoHeader.Size = new Size(746, 25);
            this.panelVideoHeader.TabIndex = 1;

            // 
            // lblVideoHeader
            // 
            this.lblVideoHeader.AutoSize = true;
            this.lblVideoHeader.Font = new Font("Yu Gothic UI", 9F, FontStyle.Bold);
            this.lblVideoHeader.Location = new Point(5, 4);
            this.lblVideoHeader.Name = "lblVideoHeader";
            this.lblVideoHeader.Size = new Size(60, 15);
            this.lblVideoHeader.TabIndex = 0;
            this.lblVideoHeader.Text = "動画一覧";

            // 
            // listViewVideos
            // 
            this.listViewVideos.Columns.AddRange(new ColumnHeader[] {
                this.colVideoTitle,
                this.colVideoStatus,
                this.colVideoProgress,
                this.colVideoSize,
                this.colVideoDate
            });
            this.listViewVideos.ContextMenuStrip = this.contextMenuVideo;
            this.listViewVideos.Dock = DockStyle.Fill;
            this.listViewVideos.FullRowSelect = true;
            this.listViewVideos.GridLines = true;
            this.listViewVideos.Location = new Point(0, 25);
            this.listViewVideos.MultiSelect = true;
            this.listViewVideos.Name = "listViewVideos";
            this.listViewVideos.Size = new Size(746, 511);
            this.listViewVideos.TabIndex = 0;
            this.listViewVideos.UseCompatibleStateImageBehavior = false;
            this.listViewVideos.View = View.Details;
            this.listViewVideos.DoubleClick += new EventHandler(this.listViewVideos_DoubleClick);
            this.listViewVideos.KeyDown += new KeyEventHandler(this.listViewVideos_KeyDown);

            // 
            // colVideoTitle
            // 
            this.colVideoTitle.Text = "タイトル";
            this.colVideoTitle.Width = 350;

            // 
            // colVideoStatus
            // 
            this.colVideoStatus.Text = "状態";
            this.colVideoStatus.Width = 100;

            // 
            // colVideoProgress
            // 
            this.colVideoProgress.Text = "進捗";
            this.colVideoProgress.Width = 80;

            // 
            // colVideoSize
            // 
            this.colVideoSize.Text = "サイズ";
            this.colVideoSize.Width = 80;

            // 
            // colVideoDate
            // 
            this.colVideoDate.Text = "投稿日";
            this.colVideoDate.Width = 100;

            // 
            // toolStrip
            // 
            this.toolStrip.Items.AddRange(new ToolStripItem[] {
                this.btnAddUser,
                this.btnAddVideo,
                this.toolStripSeparator1,
                this.btnCheckNow,
                this.btnStartAll,
                this.btnStopAll,
                this.toolStripSeparator2,
                this.btnSettings,
                this.toolStripSeparator3,
                this.btnSetup,
                this.toolStripSeparator4,
                this.btnLogin,
                this.lblLoginStatus
            });
            this.toolStrip.Location = new Point(0, 0);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Size = new Size(1000, 25);
            this.toolStrip.TabIndex = 1;

            // 
            // btnAddUser
            // 
            this.btnAddUser.DisplayStyle = ToolStripItemDisplayStyle.Text;
            this.btnAddUser.Name = "btnAddUser";
            this.btnAddUser.Size = new Size(90, 22);
            this.btnAddUser.Text = "＋チャンネル";
            this.btnAddUser.Click += new EventHandler(this.btnAddUser_Click);

            // 
            // btnAddVideo
            // 
            this.btnAddVideo.DisplayStyle = ToolStripItemDisplayStyle.Text;
            this.btnAddVideo.Name = "btnAddVideo";
            this.btnAddVideo.Size = new Size(60, 22);
            this.btnAddVideo.Text = "＋動画";
            this.btnAddVideo.Click += new EventHandler(this.btnAddVideo_Click);

            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new Size(6, 25);

            // 
            // btnCheckNow
            // 
            this.btnCheckNow.DisplayStyle = ToolStripItemDisplayStyle.Text;
            this.btnCheckNow.Name = "btnCheckNow";
            this.btnCheckNow.Size = new Size(75, 22);
            this.btnCheckNow.Text = "今すぐ確認";
            this.btnCheckNow.Click += new EventHandler(this.btnCheckNow_Click);

            // 
            // btnStartAll
            // 
            this.btnStartAll.DisplayStyle = ToolStripItemDisplayStyle.Text;
            this.btnStartAll.Name = "btnStartAll";
            this.btnStartAll.Size = new Size(70, 22);
            this.btnStartAll.Text = "▶ DL開始";
            this.btnStartAll.Click += new EventHandler(this.btnStartAll_Click);

            // 
            // btnStopAll
            // 
            this.btnStopAll.DisplayStyle = ToolStripItemDisplayStyle.Text;
            this.btnStopAll.Name = "btnStopAll";
            this.btnStopAll.Size = new Size(60, 22);
            this.btnStopAll.Text = "■ 停止";
            this.btnStopAll.Click += new EventHandler(this.btnStopAll_Click);

            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new Size(6, 25);

            // 
            // btnSettings
            // 
            this.btnSettings.DisplayStyle = ToolStripItemDisplayStyle.Text;
            this.btnSettings.Name = "btnSettings";
            this.btnSettings.Size = new Size(35, 22);
            this.btnSettings.Text = "設定";
            this.btnSettings.Click += new EventHandler(this.btnSettings_Click);

            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new Size(6, 25);

            // 
            // btnSetup
            // 
            this.btnSetup.DisplayStyle = ToolStripItemDisplayStyle.Text;
            this.btnSetup.Name = "btnSetup";
            this.btnSetup.Size = new Size(100, 22);
            this.btnSetup.Text = "環境セットアップ";
            this.btnSetup.Click += new EventHandler(this.btnSetup_Click);

            // 
            // toolStripSeparator4
            // 
            this.toolStripSeparator4.Name = "toolStripSeparator4";
            this.toolStripSeparator4.Size = new Size(6, 25);

            // 
            // btnLogin
            // 
            this.btnLogin.DisplayStyle = ToolStripItemDisplayStyle.Text;
            this.btnLogin.Name = "btnLogin";
            this.btnLogin.Size = new Size(55, 22);
            this.btnLogin.Text = "ログイン";
            this.btnLogin.Click += new EventHandler(this.btnLogin_Click);

            // 
            // lblLoginStatus
            // 
            this.lblLoginStatus.Name = "lblLoginStatus";
            this.lblLoginStatus.Size = new Size(60, 22);
            this.lblLoginStatus.Text = "(未ログイン)";
            this.lblLoginStatus.ForeColor = Color.Gray;

            // 
            // statusStrip
            // 
            this.statusStrip.Items.AddRange(new ToolStripItem[] {
                this.lblStatus,
                this.lblDownloadCount,
                this.progressBar
            });
            this.statusStrip.Location = new Point(0, 600);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new Size(1000, 22);
            this.statusStrip.TabIndex = 2;

            // 
            // lblStatus
            // 
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new Size(45, 17);
            this.lblStatus.Text = "準備完了";
            this.lblStatus.Spring = true;
            this.lblStatus.TextAlign = ContentAlignment.MiddleLeft;

            // 
            // lblDownloadCount
            // 
            this.lblDownloadCount.Name = "lblDownloadCount";
            this.lblDownloadCount.Size = new Size(100, 17);
            this.lblDownloadCount.Text = "DL: 0 / 待機: 0";

            // 
            // progressBar
            // 
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new Size(100, 16);

            // 
            // notifyIcon
            // 
            this.notifyIcon.ContextMenuStrip = this.contextMenuTray;
            this.notifyIcon.Text = "IwaraDownloader";
            this.notifyIcon.Visible = true;
            this.notifyIcon.Icon = Properties.Resources.icon;
            this.notifyIcon.DoubleClick += new EventHandler(this.notifyIcon_DoubleClick);

            // 
            // contextMenuTray
            // 
            this.contextMenuTray.Items.AddRange(new ToolStripItem[] {
                this.menuShow,
                this.menuSeparator,
                this.menuExit
            });
            this.contextMenuTray.Name = "contextMenuTray";
            this.contextMenuTray.Size = new Size(100, 54);

            // 
            // menuShow
            // 
            this.menuShow.Name = "menuShow";
            this.menuShow.Size = new Size(99, 22);
            this.menuShow.Text = "表示";
            this.menuShow.Click += new EventHandler(this.menuShow_Click);

            // 
            // menuSeparator
            // 
            this.menuSeparator.Name = "menuSeparator";
            this.menuSeparator.Size = new Size(96, 6);

            // 
            // menuExit
            // 
            this.menuExit.Name = "menuExit";
            this.menuExit.Size = new Size(99, 22);
            this.menuExit.Text = "終了";
            this.menuExit.Click += new EventHandler(this.menuExit_Click);

            // 
            // contextMenuChannel
            // 
            this.contextMenuChannel.Items.AddRange(new ToolStripItem[] {
                this.menuChOpen,
                this.menuChCheckNow,
                this.menuChDownloadAll,
                this.menuChSeparator1,
                this.menuChSetSavePath,
                this.menuChSeparator2,
                this.menuChEnable,
                this.menuChDisable,
                this.menuChSeparator3,
                this.menuChDelete
            });
            this.contextMenuChannel.Name = "contextMenuChannel";
            this.contextMenuChannel.Size = new Size(180, 176);

            // 
            // menuChOpen
            // 
            this.menuChOpen.Name = "menuChOpen";
            this.menuChOpen.Size = new Size(179, 22);
            this.menuChOpen.Text = "ページを開く";
            this.menuChOpen.Click += new EventHandler(this.menuChOpen_Click);

            // 
            // menuChCheckNow
            // 
            this.menuChCheckNow.Name = "menuChCheckNow";
            this.menuChCheckNow.Size = new Size(179, 22);
            this.menuChCheckNow.Text = "今すぐ確認";
            this.menuChCheckNow.Click += new EventHandler(this.menuChCheckNow_Click);

            // 
            // menuChDownloadAll
            // 
            this.menuChDownloadAll.Name = "menuChDownloadAll";
            this.menuChDownloadAll.Size = new Size(179, 22);
            this.menuChDownloadAll.Text = "全てダウンロード";
            this.menuChDownloadAll.Click += new EventHandler(this.menuChDownloadAll_Click);

            // 
            // menuChSeparator1
            // 
            this.menuChSeparator1.Name = "menuChSeparator1";
            this.menuChSeparator1.Size = new Size(176, 6);

            // 
            // menuChSetSavePath
            // 
            this.menuChSetSavePath.Name = "menuChSetSavePath";
            this.menuChSetSavePath.Size = new Size(179, 22);
            this.menuChSetSavePath.Text = "保存先を変更...";
            this.menuChSetSavePath.Click += new EventHandler(this.menuChSetSavePath_Click);

            // 
            // menuChSeparator2
            // 
            this.menuChSeparator2.Name = "menuChSeparator2";
            this.menuChSeparator2.Size = new Size(176, 6);

            // 
            // menuChEnable
            // 
            this.menuChEnable.Name = "menuChEnable";
            this.menuChEnable.Size = new Size(179, 22);
            this.menuChEnable.Text = "有効にする";
            this.menuChEnable.Click += new EventHandler(this.menuChEnable_Click);

            // 
            // menuChDisable
            // 
            this.menuChDisable.Name = "menuChDisable";
            this.menuChDisable.Size = new Size(179, 22);
            this.menuChDisable.Text = "無効にする";
            this.menuChDisable.Click += new EventHandler(this.menuChDisable_Click);

            // 
            // menuChSeparator3
            // 
            this.menuChSeparator3.Name = "menuChSeparator3";
            this.menuChSeparator3.Size = new Size(176, 6);

            // 
            // menuChDelete
            // 
            this.menuChDelete.Name = "menuChDelete";
            this.menuChDelete.Size = new Size(179, 22);
            this.menuChDelete.Text = "削除";
            this.menuChDelete.Click += new EventHandler(this.menuChDelete_Click);

            // 
            // contextMenuVideo
            // 
            this.contextMenuVideo.Items.AddRange(new ToolStripItem[] {
                this.menuVidDownload,
                this.menuVidCancel,
                this.menuVidRefreshInfo,
                this.menuVidSeparator1,
                this.menuVidPlay,
                this.menuVidOpenFolder,
                this.menuVidSeparator2,
                this.menuVidOpenPage
            });
            this.contextMenuVideo.Name = "contextMenuVideo";
            this.contextMenuVideo.Size = new Size(160, 148);

            // 
            // menuVidDownload
            // 
            this.menuVidDownload.Name = "menuVidDownload";
            this.menuVidDownload.Size = new Size(159, 22);
            this.menuVidDownload.Text = "ダウンロード";
            this.menuVidDownload.Click += new EventHandler(this.menuVidDownload_Click);

            // 
            // menuVidCancel
            // 
            this.menuVidCancel.Name = "menuVidCancel";
            this.menuVidCancel.Size = new Size(159, 22);
            this.menuVidCancel.Text = "キャンセル";
            this.menuVidCancel.Click += new EventHandler(this.menuVidCancel_Click);

            // 
            // menuVidRefreshInfo
            // 
            this.menuVidRefreshInfo.Name = "menuVidRefreshInfo";
            this.menuVidRefreshInfo.Size = new Size(159, 22);
            this.menuVidRefreshInfo.Text = "情報再取得";
            this.menuVidRefreshInfo.Click += new EventHandler(this.menuVidRefreshInfo_Click);

            // 
            // menuVidSeparator1
            // 
            this.menuVidSeparator1.Name = "menuVidSeparator1";
            this.menuVidSeparator1.Size = new Size(156, 6);

            // 
            // menuVidPlay
            // 
            this.menuVidPlay.Name = "menuVidPlay";
            this.menuVidPlay.Size = new Size(159, 22);
            this.menuVidPlay.Text = "再生";
            this.menuVidPlay.Click += new EventHandler(this.menuVidPlay_Click);

            // 
            // menuVidOpenFolder
            // 
            this.menuVidOpenFolder.Name = "menuVidOpenFolder";
            this.menuVidOpenFolder.Size = new Size(159, 22);
            this.menuVidOpenFolder.Text = "フォルダを開く";
            this.menuVidOpenFolder.Click += new EventHandler(this.menuVidOpenFolder_Click);

            // 
            // menuVidSeparator2
            // 
            this.menuVidSeparator2.Name = "menuVidSeparator2";
            this.menuVidSeparator2.Size = new Size(156, 6);

            // 
            // menuVidOpenPage
            // 
            this.menuVidOpenPage.Name = "menuVidOpenPage";
            this.menuVidOpenPage.Size = new Size(159, 22);
            this.menuVidOpenPage.Text = "ページを開く";
            this.menuVidOpenPage.Click += new EventHandler(this.menuVidOpenPage_Click);

            // 
            // imageListTree
            // 
            this.imageListTree.ColorDepth = ColorDepth.Depth32Bit;
            this.imageListTree.ImageSize = new Size(16, 16);
            this.imageListTree.TransparentColor = Color.Transparent;

            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(1000, 622);
            this.Controls.Add(this.mainSplitContainer);
            this.Controls.Add(this.toolStrip);
            this.Controls.Add(this.statusStrip);
            this.Icon = Properties.Resources.icon;
            this.MinimumSize = new Size(800, 500);
            this.Name = "MainForm";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "IwaraDownloader";
            this.FormClosing += new FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new EventHandler(this.MainForm_Load);
            this.Resize += new EventHandler(this.MainForm_Resize);

            ((System.ComponentModel.ISupportInitialize)(this.mainSplitContainer)).EndInit();
            this.mainSplitContainer.Panel1.ResumeLayout(false);
            this.mainSplitContainer.Panel1.PerformLayout();
            this.mainSplitContainer.Panel2.ResumeLayout(false);
            this.mainSplitContainer.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.contentSplitContainer)).EndInit();
            this.contentSplitContainer.Panel1.ResumeLayout(false);
            this.contentSplitContainer.Panel2.ResumeLayout(false);
            this.contentSplitContainer.ResumeLayout(false);
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.contextMenuTray.ResumeLayout(false);
            this.contextMenuChannel.ResumeLayout(false);
            this.contextMenuVideo.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private SplitContainer mainSplitContainer;
        private SplitContainer contentSplitContainer;
        private TextBox txtUrl;
        private Button btnPasteAndAdd;
        private ToolStrip toolStrip;
        private ToolStripButton btnAddUser;
        private ToolStripButton btnAddVideo;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripButton btnCheckNow;
        private ToolStripButton btnStartAll;
        private ToolStripButton btnStopAll;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripButton btnSettings;
        private ToolStripSeparator toolStripSeparator3;
        private ToolStripButton btnSetup;
        private ToolStripSeparator toolStripSeparator4;
        private ToolStripButton btnLogin;
        private ToolStripLabel lblLoginStatus;
        private TreeView treeViewChannels;
        private Panel panelChannelHeader;
        private Label lblChannelHeader;
        private ListView listViewVideos;
        private Panel panelVideoHeader;
        private Label lblVideoHeader;
        private ColumnHeader colVideoTitle;
        private ColumnHeader colVideoStatus;
        private ColumnHeader colVideoProgress;
        private ColumnHeader colVideoSize;
        private ColumnHeader colVideoDate;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblStatus;
        private ToolStripStatusLabel lblDownloadCount;
        private ToolStripProgressBar progressBar;
        private NotifyIcon notifyIcon;
        private ContextMenuStrip contextMenuTray;
        private ToolStripMenuItem menuShow;
        private ToolStripSeparator menuSeparator;
        private ToolStripMenuItem menuExit;
        private ContextMenuStrip contextMenuChannel;
        private ToolStripMenuItem menuChOpen;
        private ToolStripMenuItem menuChCheckNow;
        private ToolStripMenuItem menuChDownloadAll;
        private ToolStripSeparator menuChSeparator1;
        private ToolStripMenuItem menuChSetSavePath;
        private ToolStripSeparator menuChSeparator2;
        private ToolStripMenuItem menuChEnable;
        private ToolStripMenuItem menuChDisable;
        private ToolStripSeparator menuChSeparator3;
        private ToolStripMenuItem menuChDelete;
        private ContextMenuStrip contextMenuVideo;
        private ToolStripMenuItem menuVidDownload;
        private ToolStripMenuItem menuVidCancel;
        private ToolStripMenuItem menuVidRefreshInfo;
        private ToolStripSeparator menuVidSeparator1;
        private ToolStripMenuItem menuVidPlay;
        private ToolStripMenuItem menuVidOpenFolder;
        private ToolStripSeparator menuVidSeparator2;
        private ToolStripMenuItem menuVidOpenPage;
        private ImageList imageListTree;
    }
}
