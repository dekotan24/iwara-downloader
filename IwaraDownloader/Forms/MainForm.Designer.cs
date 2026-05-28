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
            this.btnClipMonitor = new ToolStripButton();
            this.btnViewMode = new ToolStripButton();
            this.btnNsfwFilter = new ToolStripDropDownButton();
            this.menuNsfwAll = new ToolStripMenuItem();
            this.menuNsfwSfw = new ToolStripMenuItem();
            this.menuNsfwNsfw = new ToolStripMenuItem();
            this.toolStripSeparator6 = new ToolStripSeparator();
            this.btnSettings = new ToolStripButton();
            this.toolStripSeparator3 = new ToolStripSeparator();
            this.btnSetup = new ToolStripButton();
            this.toolStripSeparator4 = new ToolStripSeparator();
            this.btnLogin = new ToolStripButton();
            this.lblLoginStatus = new ToolStripLabel();
            this.toolStripSeparator5 = new ToolStripSeparator();
            this.btnHelp = new ToolStripDropDownButton();
            this.menuHelpAbout = new ToolStripMenuItem();
            this.menuHelpOpenLogs = new ToolStripMenuItem();
            this.menuHelpGitHub = new ToolStripMenuItem();
            this.menuHelpSeparator1 = new ToolStripSeparator();
            this.menuToolsBulkImport = new ToolStripMenuItem();
            this.menuToolsSearchImport = new ToolStripMenuItem();
            this.menuToolsDuplicateCheck = new ToolStripMenuItem();
            this.menuToolsStatistics = new ToolStripMenuItem();

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
            this.colVideoSource = new ColumnHeader();
            this.colVideoStatus = new ColumnHeader();
            this.colVideoProgress = new ColumnHeader();
            this.colVideoSize = new ColumnHeader();
            this.colVideoDate = new ColumnHeader();
            this.lblVideoHeader = new Label();
            this.panelVideoHeader = new Panel();
            this.txtVideoFilter = new TextBox();
            this.btnClearFilter = new Button();
            this.panelVideoFilter = new Panel();

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

            // コンテキストメニュー(チャンネル)
            this.contextMenuChannel = new ContextMenuStrip(this.components);
            this.menuChOpen = new ToolStripMenuItem();
            this.menuChCheckNow = new ToolStripMenuItem();
            this.menuChDownloadAll = new ToolStripMenuItem();
            this.menuChSeparator1 = new ToolStripSeparator();
            this.menuChSetSavePath = new ToolStripMenuItem();
            this.menuChExternalDL = new ToolStripMenuItem();
            this.menuChExternalDLInherit = new ToolStripMenuItem();
            this.menuChExternalDLOn = new ToolStripMenuItem();
            this.menuChExternalDLOff = new ToolStripMenuItem();
            this.menuChSeparator2 = new ToolStripSeparator();
            this.menuChEnable = new ToolStripMenuItem();
            this.menuChDisable = new ToolStripMenuItem();
            this.menuChSeparator3 = new ToolStripSeparator();
            this.menuChDelete = new ToolStripMenuItem();

            // コンテキストメニュー(動画) - 空の容器のみ宣言。
            // 項目は Opening イベントで毎回動的に組み立てる (BuildVideoContextMenu)
            this.contextMenuVideo = new ContextMenuStrip(this.components);

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
            this.contentSplitContainer.Panel2.Controls.Add(this.panelVideoFilter);
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
            // panelVideoFilter
            // 
            this.panelVideoFilter.BackColor = Color.FromArgb(250, 250, 250);
            this.panelVideoFilter.BorderStyle = BorderStyle.None;
            this.panelVideoFilter.Controls.Add(this.btnClearFilter);
            this.panelVideoFilter.Controls.Add(this.txtVideoFilter);
            this.panelVideoFilter.Dock = DockStyle.Top;
            this.panelVideoFilter.Location = new Point(0, 25);
            this.panelVideoFilter.Name = "panelVideoFilter";
            this.panelVideoFilter.Size = new Size(746, 28);
            this.panelVideoFilter.TabIndex = 2;

            // 
            // txtVideoFilter
            // 
            this.txtVideoFilter.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.txtVideoFilter.Font = new Font("Yu Gothic UI", 9F);
            this.txtVideoFilter.Location = new Point(3, 3);
            this.txtVideoFilter.Name = "txtVideoFilter";
            this.txtVideoFilter.PlaceholderText = "🔍 フィルター(タイトルで絞り込み)...";
            this.txtVideoFilter.Size = new Size(630, 23);
            this.txtVideoFilter.TabIndex = 0;
            this.txtVideoFilter.TextChanged += new EventHandler(this.txtVideoFilter_TextChanged);
            this.txtVideoFilter.KeyDown += new KeyEventHandler(this.txtVideoFilter_KeyDown);

            // 
            // btnClearFilter
            // 
            this.btnClearFilter.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.btnClearFilter.FlatStyle = FlatStyle.Flat;
            this.btnClearFilter.Font = new Font("Yu Gothic UI", 8F);
            this.btnClearFilter.Location = new Point(638, 2);
            this.btnClearFilter.Name = "btnClearFilter";
            this.btnClearFilter.Size = new Size(50, 23);
            this.btnClearFilter.TabIndex = 1;
            this.btnClearFilter.Text = "クリア";
            this.btnClearFilter.UseVisualStyleBackColor = true;
            this.btnClearFilter.Click += new EventHandler(this.btnClearFilter_Click);

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
                this.colVideoSource,
                this.colVideoStatus,
                this.colVideoProgress,
                this.colVideoSize,
                this.colVideoDate
            });
            this.listViewVideos.ContextMenuStrip = this.contextMenuVideo;
            this.listViewVideos.Dock = DockStyle.Fill;
            this.listViewVideos.FullRowSelect = true;
            this.listViewVideos.GridLines = true;
            this.listViewVideos.Location = new Point(0, 53);
            this.listViewVideos.MultiSelect = true;
            this.listViewVideos.Name = "listViewVideos";
            this.listViewVideos.ShowItemToolTips = true;
            this.listViewVideos.Size = new Size(746, 483);
            this.listViewVideos.TabIndex = 0;
            this.listViewVideos.UseCompatibleStateImageBehavior = false;
            this.listViewVideos.View = View.Details;
            this.listViewVideos.VirtualMode = true;
            this.listViewVideos.VirtualListSize = 0;
            this.listViewVideos.RetrieveVirtualItem += new RetrieveVirtualItemEventHandler(this.listViewVideos_RetrieveVirtualItem);
            this.listViewVideos.SearchForVirtualItem += new SearchForVirtualItemEventHandler(this.listViewVideos_SearchForVirtualItem);
            this.listViewVideos.CacheVirtualItems += new CacheVirtualItemsEventHandler(this.listViewVideos_CacheVirtualItems);
            this.listViewVideos.ColumnClick += new ColumnClickEventHandler(this.listViewVideos_ColumnClick);
            this.listViewVideos.MouseDoubleClick += new MouseEventHandler(this.listViewVideos_MouseDoubleClick);
            this.listViewVideos.KeyDown += new KeyEventHandler(this.listViewVideos_KeyDown);
            this.listViewVideos.MouseDown += new MouseEventHandler(this.listViewVideos_MouseDown);
            this.listViewVideos.SelectedIndexChanged += new EventHandler(this.listViewVideos_SelectedIndexChanged);

            //
            // colVideoTitle
            //
            this.colVideoTitle.Text = "タイトル";
            this.colVideoTitle.Width = 350;

            //
            // colVideoSource
            //
            this.colVideoSource.Text = "ソース";
            this.colVideoSource.Width = 70;

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
            this.colVideoDate.Text = "追加日時";
            this.colVideoDate.Width = 130;

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
                this.btnClipMonitor,
                this.btnViewMode,
                this.btnNsfwFilter,
                this.toolStripSeparator6,
                this.btnSettings,
                this.toolStripSeparator3,
                this.btnSetup,
                this.toolStripSeparator4,
                this.btnLogin,
                this.lblLoginStatus,
                this.toolStripSeparator5,
                this.btnHelp
            });
            this.toolStrip.Location = new Point(0, 0);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Size = new Size(1000, 25);
            this.toolStrip.TabIndex = 1;

            // 
            // btnAddUser
            // 
            this.btnAddUser.AutoToolTip = false;
            this.btnAddUser.DisplayStyle = ToolStripItemDisplayStyle.Text;
            this.btnAddUser.Name = "btnAddUser";
            this.btnAddUser.Size = new Size(90, 22);
            this.btnAddUser.Text = "＋チャンネル";
            this.btnAddUser.Click += new EventHandler(this.btnAddUser_Click);

            // 
            // btnAddVideo
            // 
            this.btnAddVideo.AutoToolTip = false;
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
            this.btnCheckNow.AutoToolTip = false;
            this.btnCheckNow.DisplayStyle = ToolStripItemDisplayStyle.Text;
            this.btnCheckNow.Name = "btnCheckNow";
            this.btnCheckNow.Size = new Size(90, 22);
            this.btnCheckNow.Text = "今すぐ確認 (F5)";
            this.btnCheckNow.Click += new EventHandler(this.btnCheckNow_Click);

            // 
            // btnStartAll
            // 
            this.btnStartAll.AutoToolTip = false;
            this.btnStartAll.DisplayStyle = ToolStripItemDisplayStyle.Text;
            this.btnStartAll.Name = "btnStartAll";
            this.btnStartAll.Size = new Size(70, 22);
            this.btnStartAll.Text = "▶ DL開始";
            this.btnStartAll.Click += new EventHandler(this.btnStartAll_Click);

            // 
            // btnStopAll
            // 
            this.btnStopAll.AutoToolTip = false;
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
            // btnClipMonitor (クリップボード監視 ON/OFF)
            //
            this.btnClipMonitor.AutoToolTip = true;
            this.btnClipMonitor.DisplayStyle = ToolStripItemDisplayStyle.Text;
            this.btnClipMonitor.Name = "btnClipMonitor";
            this.btnClipMonitor.Size = new Size(100, 22);
            this.btnClipMonitor.Text = "📋監視: OFF";
            this.btnClipMonitor.ToolTipText = "クリップボード監視: iwara URL を自動でキュー追加";
            this.btnClipMonitor.CheckOnClick = true;
            this.btnClipMonitor.CheckedChanged += new EventHandler(this.btnClipMonitor_CheckedChanged);

            //
            // btnViewMode (表示モード切替)
            //
            this.btnViewMode.AutoToolTip = true;
            this.btnViewMode.DisplayStyle = ToolStripItemDisplayStyle.Text;
            this.btnViewMode.Name = "btnViewMode";
            this.btnViewMode.Size = new Size(70, 22);
            this.btnViewMode.Text = "📋詳細";
            this.btnViewMode.ToolTipText = "表示モード切替 (詳細 / サムネ)";
            this.btnViewMode.CheckOnClick = true;
            this.btnViewMode.CheckedChanged += new EventHandler(this.btnViewMode_CheckedChanged);

            //
            // btnNsfwFilter (NSFWフィルタ ドロップダウン)
            //
            this.btnNsfwFilter.DisplayStyle = ToolStripItemDisplayStyle.Text;
            this.btnNsfwFilter.Name = "btnNsfwFilter";
            this.btnNsfwFilter.Size = new Size(60, 22);
            this.btnNsfwFilter.Text = "🔞全部";
            this.btnNsfwFilter.ToolTipText = "NSFW フィルタ";
            this.btnNsfwFilter.DropDownItems.AddRange(new ToolStripItem[] {
                this.menuNsfwAll, this.menuNsfwSfw, this.menuNsfwNsfw
            });

            //
            // menuNsfwAll / menuNsfwSfw / menuNsfwNsfw
            //
            this.menuNsfwAll.Name = "menuNsfwAll";
            this.menuNsfwAll.Text = "全部表示";
            this.menuNsfwAll.Click += new EventHandler(this.menuNsfwAll_Click);

            this.menuNsfwSfw.Name = "menuNsfwSfw";
            this.menuNsfwSfw.Text = "SFW のみ (general)";
            this.menuNsfwSfw.Click += new EventHandler(this.menuNsfwSfw_Click);

            this.menuNsfwNsfw.Name = "menuNsfwNsfw";
            this.menuNsfwNsfw.Text = "NSFW のみ (ecchi)";
            this.menuNsfwNsfw.Click += new EventHandler(this.menuNsfwNsfw_Click);

            //
            // toolStripSeparator6
            //
            this.toolStripSeparator6.Name = "toolStripSeparator6";
            this.toolStripSeparator6.Size = new Size(6, 25);

            //
            // btnSettings
            //
            this.btnSettings.AutoToolTip = false;
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
            this.btnSetup.AutoToolTip = false;
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
            this.btnLogin.AutoToolTip = false;
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
            // toolStripSeparator5
            // 
            this.toolStripSeparator5.Name = "toolStripSeparator5";
            this.toolStripSeparator5.Size = new Size(6, 25);

            // 
            // btnHelp
            // 
            this.btnHelp.AutoToolTip = false;
            this.btnHelp.DisplayStyle = ToolStripItemDisplayStyle.Text;
            this.btnHelp.DropDownItems.AddRange(new ToolStripItem[] {
                this.menuHelpAbout,
                this.menuHelpOpenLogs,
                this.menuHelpGitHub,
                this.menuHelpSeparator1,
                this.menuToolsBulkImport,
                this.menuToolsSearchImport,
                this.menuToolsDuplicateCheck,
                this.menuToolsStatistics
            });
            this.btnHelp.Name = "btnHelp";
            this.btnHelp.Size = new Size(55, 22);
            this.btnHelp.Text = "ヘルプ";

            // 
            // menuHelpAbout
            // 
            this.menuHelpAbout.Name = "menuHelpAbout";
            this.menuHelpAbout.Size = new Size(180, 22);
            this.menuHelpAbout.Text = "バージョン情報...";
            this.menuHelpAbout.Click += new EventHandler(this.menuHelpAbout_Click);

            // 
            // menuHelpOpenLogs
            // 
            this.menuHelpOpenLogs.Name = "menuHelpOpenLogs";
            this.menuHelpOpenLogs.Size = new Size(180, 22);
            this.menuHelpOpenLogs.Text = "ログフォルダを開く";
            this.menuHelpOpenLogs.Click += new EventHandler(this.menuHelpOpenLogs_Click);

            // 
            // menuHelpGitHub
            // 
            this.menuHelpGitHub.Name = "menuHelpGitHub";
            this.menuHelpGitHub.Size = new Size(180, 22);
            this.menuHelpGitHub.Text = "GitHubページ";
            this.menuHelpGitHub.Click += new EventHandler(this.menuHelpGitHub_Click);

            // 
            // menuHelpSeparator1
            // 
            this.menuHelpSeparator1.Name = "menuHelpSeparator1";
            this.menuHelpSeparator1.Size = new Size(177, 6);

            //
            // menuToolsBulkImport
            //
            this.menuToolsBulkImport.Name = "menuToolsBulkImport";
            this.menuToolsBulkImport.Size = new Size(180, 22);
            this.menuToolsBulkImport.Text = "URL一括インポート...";
            this.menuToolsBulkImport.Click += new EventHandler(this.menuToolsBulkImport_Click);

            //
            // menuToolsSearchImport
            //
            this.menuToolsSearchImport.Name = "menuToolsSearchImport";
            this.menuToolsSearchImport.Size = new Size(180, 22);
            this.menuToolsSearchImport.Text = "iwara 検索インポート...";
            this.menuToolsSearchImport.Click += new EventHandler(this.menuToolsSearchImport_Click);

            //
            // menuToolsDuplicateCheck
            //
            this.menuToolsDuplicateCheck.Name = "menuToolsDuplicateCheck";
            this.menuToolsDuplicateCheck.Size = new Size(180, 22);
            this.menuToolsDuplicateCheck.Text = "重複チェック...";
            this.menuToolsDuplicateCheck.Click += new EventHandler(this.menuToolsDuplicateCheck_Click);

            // 
            // menuToolsStatistics
            // 
            this.menuToolsStatistics.Name = "menuToolsStatistics";
            this.menuToolsStatistics.Size = new Size(180, 22);
            this.menuToolsStatistics.Text = "統計ダッシュボード...";
            this.menuToolsStatistics.Click += new EventHandler(this.menuToolsStatistics_Click);

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
                this.menuChExternalDL,
                this.menuChSeparator2,
                this.menuChEnable,
                this.menuChDisable,
                this.menuChSeparator3,
                this.menuChDelete
            });
            this.contextMenuChannel.Name = "contextMenuChannel";
            this.contextMenuChannel.Size = new Size(180, 176);
            this.contextMenuChannel.Opening += new System.ComponentModel.CancelEventHandler(this.contextMenuChannel_Opening);

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
            // menuChExternalDL (iwara外動画DL設定 - サブメニュー)
            //
            this.menuChExternalDL.Name = "menuChExternalDL";
            this.menuChExternalDL.Size = new Size(179, 22);
            this.menuChExternalDL.Text = "iwara外動画のDL";
            this.menuChExternalDL.DropDownItems.AddRange(new ToolStripItem[] {
                this.menuChExternalDLInherit,
                this.menuChExternalDLOn,
                this.menuChExternalDLOff
            });

            this.menuChExternalDLInherit.Name = "menuChExternalDLInherit";
            this.menuChExternalDLInherit.Size = new Size(200, 22);
            this.menuChExternalDLInherit.Text = "デフォルト設定に従う";
            this.menuChExternalDLInherit.Click += new EventHandler(this.menuChExternalDLInherit_Click);

            this.menuChExternalDLOn.Name = "menuChExternalDLOn";
            this.menuChExternalDLOn.Size = new Size(200, 22);
            this.menuChExternalDLOn.Text = "DLする";
            this.menuChExternalDLOn.Click += new EventHandler(this.menuChExternalDLOn_Click);

            this.menuChExternalDLOff.Name = "menuChExternalDLOff";
            this.menuChExternalDLOff.Size = new Size(200, 22);
            this.menuChExternalDLOff.Text = "DLしない";
            this.menuChExternalDLOff.Click += new EventHandler(this.menuChExternalDLOff_Click);

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
            // contextMenuVideo (動画一覧の右クリックメニュー)
            //   項目は Designer で固定定義 → Opening では Visible トグルのみ
            //   動的 Items.AddRange だと AutoClose / Click イベントが壊れるため
            //
            this.menuVidDownload = new ToolStripMenuItem();
            this.menuVidCancel = new ToolStripMenuItem();
            this.menuVidRetryFailed = new ToolStripMenuItem();
            this.menuVidReDownload = new ToolStripMenuItem();
            this.menuVidRefreshInfo = new ToolStripMenuItem();
            this.menuVidCheckFileExists = new ToolStripMenuItem();
            this.menuVidSep1 = new ToolStripSeparator();
            this.menuVidPlay = new ToolStripMenuItem();
            this.menuVidOpenFolder = new ToolStripMenuItem();
            this.menuVidSep2 = new ToolStripSeparator();
            this.menuVidOpenPage = new ToolStripMenuItem();
            this.menuVidOpenAuthor = new ToolStripMenuItem();
            this.menuVidCopyUrl = new ToolStripMenuItem();
            this.menuVidCopyTitle = new ToolStripMenuItem();
            this.menuVidSep3 = new ToolStripSeparator();
            this.menuVidDetails = new ToolStripMenuItem();
            this.menuVidSep4 = new ToolStripSeparator();
            this.menuVidDelete = new ToolStripMenuItem();

            this.contextMenuVideo.Name = "contextMenuVideo";
            this.contextMenuVideo.Items.AddRange(new ToolStripItem[]
            {
                this.menuVidDownload, this.menuVidCancel, this.menuVidRetryFailed,
                this.menuVidReDownload, this.menuVidRefreshInfo, this.menuVidCheckFileExists,
                this.menuVidSep1,
                this.menuVidPlay, this.menuVidOpenFolder,
                this.menuVidSep2,
                this.menuVidOpenPage, this.menuVidOpenAuthor, this.menuVidCopyUrl, this.menuVidCopyTitle,
                this.menuVidSep3,
                this.menuVidDetails,
                this.menuVidSep4,
                this.menuVidDelete,
            });
            this.contextMenuVideo.Opening += new System.ComponentModel.CancelEventHandler(this.OnVideoContextMenuOpening);

            this.menuVidDownload.Text = "ダウンロード";
            this.menuVidDownload.ShortcutKeyDisplayString = "Ctrl+D";
            this.menuVidDownload.Click += new EventHandler(this.menuVidDownload_Click);
            this.menuVidCancel.Text = "キャンセル";
            this.menuVidCancel.Click += new EventHandler(this.menuVidCancel_Click);
            this.menuVidRetryFailed.Text = "失敗を再試行";
            this.menuVidRetryFailed.Click += new EventHandler(this.menuVidRetryFailed_Click);
            this.menuVidReDownload.Text = "再ダウンロード...";
            this.menuVidReDownload.Click += new EventHandler(this.menuVidReDownload_Click);
            this.menuVidRefreshInfo.Text = "情報再取得";
            this.menuVidRefreshInfo.Click += new EventHandler(this.menuVidRefreshInfo_Click);
            this.menuVidCheckFileExists.Text = "ファイル存在チェック";
            this.menuVidCheckFileExists.Click += new EventHandler(this.menuVidCheckFileExists_Click);
            this.menuVidPlay.Text = "再生";
            this.menuVidPlay.Click += new EventHandler(this.menuVidPlay_Click);
            this.menuVidOpenFolder.Text = "フォルダを開く";
            this.menuVidOpenFolder.Click += new EventHandler(this.menuVidOpenFolder_Click);
            this.menuVidOpenPage.Text = "ページを開く";
            this.menuVidOpenPage.Click += new EventHandler(this.menuVidOpenPage_Click);
            this.menuVidOpenAuthor.Text = "投稿者のページを開く";
            this.menuVidOpenAuthor.Click += new EventHandler(this.menuVidOpenAuthor_Click);
            this.menuVidCopyUrl.Text = "URLをコピー";
            this.menuVidCopyUrl.Click += new EventHandler(this.menuVidCopyUrl_Click);
            this.menuVidCopyTitle.Text = "タイトルをコピー";
            this.menuVidCopyTitle.Click += new EventHandler(this.menuVidCopyTitle_Click);
            this.menuVidDetails.Text = "詳細情報...";
            this.menuVidDetails.Click += new EventHandler(this.menuVidDetails_Click);
            this.menuVidDelete.Text = "削除";
            this.menuVidDelete.ShortcutKeyDisplayString = "Delete";
            this.menuVidDelete.Click += new EventHandler(this.menuVidDelete_Click);

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
            this.KeyPreview = true;
            this.MinimumSize = new Size(800, 500);
            this.Name = "MainForm";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "IwaraDownloader";
            this.FormClosing += new FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new EventHandler(this.MainForm_Load);
            this.KeyDown += new KeyEventHandler(this.MainForm_KeyDown);
            this.Resize += new EventHandler(this.MainForm_Resize);
            this.Shown += new EventHandler(this.MainForm_Shown);

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
        private ToolStripButton btnClipMonitor;
        private ToolStripButton btnViewMode;
        private ToolStripDropDownButton btnNsfwFilter;
        private ToolStripMenuItem menuNsfwAll;
        private ToolStripMenuItem menuNsfwSfw;
        private ToolStripMenuItem menuNsfwNsfw;
        private ToolStripSeparator toolStripSeparator6;
        private ToolStripButton btnSettings;
        private ToolStripSeparator toolStripSeparator3;
        private ToolStripButton btnSetup;
        private ToolStripSeparator toolStripSeparator4;
        private ToolStripButton btnLogin;
        private ToolStripLabel lblLoginStatus;
        private ToolStripSeparator toolStripSeparator5;
        private ToolStripDropDownButton btnHelp;
        private ToolStripMenuItem menuHelpAbout;
        private ToolStripMenuItem menuHelpOpenLogs;
        private ToolStripMenuItem menuHelpGitHub;
        private ToolStripSeparator menuHelpSeparator1;
        private ToolStripMenuItem menuToolsBulkImport;
        private ToolStripMenuItem menuToolsSearchImport;
        private ToolStripMenuItem menuToolsDuplicateCheck;
        private ToolStripMenuItem menuToolsStatistics;
        private TreeView treeViewChannels;
        private Panel panelChannelHeader;
        private Label lblChannelHeader;
        private ListView listViewVideos;
        private Panel panelVideoHeader;
        private Label lblVideoHeader;
        private ColumnHeader colVideoTitle;
        private ColumnHeader colVideoSource;
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
        private ToolStripMenuItem menuChExternalDL;
        private ToolStripMenuItem menuChExternalDLInherit;
        private ToolStripMenuItem menuChExternalDLOn;
        private ToolStripMenuItem menuChExternalDLOff;
        private ToolStripSeparator menuChSeparator2;
        private ToolStripMenuItem menuChEnable;
        private ToolStripMenuItem menuChDisable;
        private ToolStripSeparator menuChSeparator3;
        private ToolStripMenuItem menuChDelete;
        private ContextMenuStrip contextMenuVideo;
        private ToolStripMenuItem menuVidDownload, menuVidCancel, menuVidRetryFailed, menuVidReDownload;
        private ToolStripMenuItem menuVidRefreshInfo, menuVidCheckFileExists;
        private ToolStripSeparator menuVidSep1;
        private ToolStripMenuItem menuVidPlay, menuVidOpenFolder;
        private ToolStripSeparator menuVidSep2;
        private ToolStripMenuItem menuVidOpenPage, menuVidOpenAuthor, menuVidCopyUrl, menuVidCopyTitle;
        private ToolStripSeparator menuVidSep3;
        private ToolStripMenuItem menuVidDetails;
        private ToolStripSeparator menuVidSep4;
        private ToolStripMenuItem menuVidDelete;
        private ImageList imageListTree;
        private Panel panelVideoFilter;
        private TextBox txtVideoFilter;
        private Button btnClearFilter;
    }
}
