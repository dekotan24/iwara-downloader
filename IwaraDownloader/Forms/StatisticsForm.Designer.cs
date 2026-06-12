namespace IwaraDownloader.Forms
{
    partial class StatisticsForm
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
            this.grpOverview = new GroupBox();
            this.tableOverview = new TableLayoutPanel();
            this.lblTotalVideosLabel = new Label();
            this.lblTotalVideos = new Label();
            this.lblCompletedLabel = new Label();
            this.lblCompletedVideos = new Label();
            this.lblFailedLabel = new Label();
            this.lblFailedVideos = new Label();
            this.lblPendingLabel = new Label();
            this.lblPendingVideos = new Label();
            this.lblTotalSizeLabel = new Label();
            this.lblTotalSize = new Label();
            this.lblChannelsLabel = new Label();
            this.lblTotalChannels = new Label();
            this.lblActiveChannelsLabel = new Label();
            this.lblActiveChannels = new Label();
            this.lblSuccessRateLabel = new Label();
            this.lblSuccessRate = new Label();
            this.progressSuccess = new ProgressBar();

            this.tabControl = new TabControl();
            this.tabChannels = new TabPage();
            this.tabDaily = new TabPage();
            this.dgvChannelStats = new DataGridView();
            this.dgvDailyStats = new DataGridView();

            // 追加タブ: 失敗分析
            this.tabFailure = new TabPage();
            this.tlpFailure = new TableLayoutPanel();
            this.lblErrorTitle = new Label();
            this.dgvErrorStats = new DataGridView();
            this.lblRetryTitle = new Label();
            this.dgvRetryStats = new DataGridView();
            // 追加タブ: 月別推移
            this.tabMonthly = new TabPage();
            this.dgvMonthlyStats = new DataGridView();
            // 追加タブ: サイズ・長さ
            this.tabDistribution = new TabPage();
            this.tlpDistribution = new TableLayoutPanel();
            this.lblSizeTitle = new Label();
            this.dgvSizeStats = new DataGridView();
            this.lblDurationTitle = new Label();
            this.dgvDurationStats = new DataGridView();
            // 追加タブ: 内容
            this.tabContent = new TabPage();
            this.tlpContent = new TableLayoutPanel();
            this.lblTagTitle = new Label();
            this.dgvTagStats = new DataGridView();
            this.lblRatingTitle = new Label();
            this.dgvRatingStats = new DataGridView();
            this.lblSiteTitle = new Label();
            this.dgvSiteStats = new DataGridView();
            // 追加タブ: 投稿者
            this.tabAuthors = new TabPage();
            this.dgvAuthorStats = new DataGridView();

            this.btnRefresh = new Button();
            this.btnExportCsv = new Button();
            this.btnClose = new Button();

            this.grpOverview.SuspendLayout();
            this.tableOverview.SuspendLayout();
            this.tabControl.SuspendLayout();
            this.tabChannels.SuspendLayout();
            this.tabDaily.SuspendLayout();
            this.tabFailure.SuspendLayout();
            this.tlpFailure.SuspendLayout();
            this.tabMonthly.SuspendLayout();
            this.tabDistribution.SuspendLayout();
            this.tlpDistribution.SuspendLayout();
            this.tabContent.SuspendLayout();
            this.tlpContent.SuspendLayout();
            this.tabAuthors.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvChannelStats)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvDailyStats)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvErrorStats)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvRetryStats)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvMonthlyStats)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvSizeStats)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvDurationStats)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvTagStats)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvRatingStats)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvSiteStats)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvAuthorStats)).BeginInit();
            this.SuspendLayout();

            // 
            // grpOverview
            // 
            this.grpOverview.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.grpOverview.Controls.Add(this.tableOverview);
            this.grpOverview.Controls.Add(this.progressSuccess);
            this.grpOverview.Location = new Point(12, 12);
            this.grpOverview.Name = "grpOverview";
            this.grpOverview.Size = new Size(560, 160);
            this.grpOverview.TabIndex = 0;
            this.grpOverview.TabStop = false;
            this.grpOverview.Text = "概要";

            // 
            // tableOverview
            // 
            this.tableOverview.ColumnCount = 4;
            this.tableOverview.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            this.tableOverview.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            this.tableOverview.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            this.tableOverview.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            this.tableOverview.Controls.Add(this.lblTotalVideosLabel, 0, 0);
            this.tableOverview.Controls.Add(this.lblTotalVideos, 0, 1);
            this.tableOverview.Controls.Add(this.lblCompletedLabel, 1, 0);
            this.tableOverview.Controls.Add(this.lblCompletedVideos, 1, 1);
            this.tableOverview.Controls.Add(this.lblFailedLabel, 2, 0);
            this.tableOverview.Controls.Add(this.lblFailedVideos, 2, 1);
            this.tableOverview.Controls.Add(this.lblPendingLabel, 3, 0);
            this.tableOverview.Controls.Add(this.lblPendingVideos, 3, 1);
            this.tableOverview.Controls.Add(this.lblTotalSizeLabel, 0, 2);
            this.tableOverview.Controls.Add(this.lblTotalSize, 0, 3);
            this.tableOverview.Controls.Add(this.lblChannelsLabel, 1, 2);
            this.tableOverview.Controls.Add(this.lblTotalChannels, 1, 3);
            this.tableOverview.Controls.Add(this.lblActiveChannelsLabel, 2, 2);
            this.tableOverview.Controls.Add(this.lblActiveChannels, 2, 3);
            this.tableOverview.Controls.Add(this.lblSuccessRateLabel, 3, 2);
            this.tableOverview.Controls.Add(this.lblSuccessRate, 3, 3);
            this.tableOverview.Location = new Point(10, 22);
            this.tableOverview.Name = "tableOverview";
            this.tableOverview.RowCount = 4;
            this.tableOverview.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            this.tableOverview.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            this.tableOverview.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            this.tableOverview.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            this.tableOverview.Size = new Size(540, 100);
            this.tableOverview.TabIndex = 0;

            // Labels - Row 0 (Headers)
            this.lblTotalVideosLabel.AutoSize = true;
            this.lblTotalVideosLabel.ForeColor = Color.Gray;
            this.lblTotalVideosLabel.Text = "総動画数";
            this.lblTotalVideosLabel.TextAlign = ContentAlignment.MiddleCenter;
            this.lblTotalVideosLabel.Dock = DockStyle.Fill;

            this.lblCompletedLabel.AutoSize = true;
            this.lblCompletedLabel.ForeColor = Color.Gray;
            this.lblCompletedLabel.Text = "完了";
            this.lblCompletedLabel.TextAlign = ContentAlignment.MiddleCenter;
            this.lblCompletedLabel.Dock = DockStyle.Fill;

            this.lblFailedLabel.AutoSize = true;
            this.lblFailedLabel.ForeColor = Color.Gray;
            this.lblFailedLabel.Text = "失敗";
            this.lblFailedLabel.TextAlign = ContentAlignment.MiddleCenter;
            this.lblFailedLabel.Dock = DockStyle.Fill;

            this.lblPendingLabel.AutoSize = true;
            this.lblPendingLabel.ForeColor = Color.Gray;
            this.lblPendingLabel.Text = "待機中";
            this.lblPendingLabel.TextAlign = ContentAlignment.MiddleCenter;
            this.lblPendingLabel.Dock = DockStyle.Fill;

            // Labels - Row 1 (Values)
            this.lblTotalVideos.AutoSize = true;
            this.lblTotalVideos.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            this.lblTotalVideos.Text = "0";
            this.lblTotalVideos.TextAlign = ContentAlignment.MiddleCenter;
            this.lblTotalVideos.Dock = DockStyle.Fill;

            this.lblCompletedVideos.AutoSize = true;
            this.lblCompletedVideos.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            this.lblCompletedVideos.ForeColor = Color.Green;
            this.lblCompletedVideos.Text = "0";
            this.lblCompletedVideos.TextAlign = ContentAlignment.MiddleCenter;
            this.lblCompletedVideos.Dock = DockStyle.Fill;

            this.lblFailedVideos.AutoSize = true;
            this.lblFailedVideos.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            this.lblFailedVideos.ForeColor = Color.Red;
            this.lblFailedVideos.Text = "0";
            this.lblFailedVideos.TextAlign = ContentAlignment.MiddleCenter;
            this.lblFailedVideos.Dock = DockStyle.Fill;

            this.lblPendingVideos.AutoSize = true;
            this.lblPendingVideos.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            this.lblPendingVideos.ForeColor = Color.Orange;
            this.lblPendingVideos.Text = "0";
            this.lblPendingVideos.TextAlign = ContentAlignment.MiddleCenter;
            this.lblPendingVideos.Dock = DockStyle.Fill;

            // Labels - Row 2 (Headers)
            this.lblTotalSizeLabel.AutoSize = true;
            this.lblTotalSizeLabel.ForeColor = Color.Gray;
            this.lblTotalSizeLabel.Text = "総サイズ";
            this.lblTotalSizeLabel.TextAlign = ContentAlignment.MiddleCenter;
            this.lblTotalSizeLabel.Dock = DockStyle.Fill;

            this.lblChannelsLabel.AutoSize = true;
            this.lblChannelsLabel.ForeColor = Color.Gray;
            this.lblChannelsLabel.Text = "チャンネル数";
            this.lblChannelsLabel.TextAlign = ContentAlignment.MiddleCenter;
            this.lblChannelsLabel.Dock = DockStyle.Fill;

            this.lblActiveChannelsLabel.AutoSize = true;
            this.lblActiveChannelsLabel.ForeColor = Color.Gray;
            this.lblActiveChannelsLabel.Text = "有効チャンネル";
            this.lblActiveChannelsLabel.TextAlign = ContentAlignment.MiddleCenter;
            this.lblActiveChannelsLabel.Dock = DockStyle.Fill;

            this.lblSuccessRateLabel.AutoSize = true;
            this.lblSuccessRateLabel.ForeColor = Color.Gray;
            this.lblSuccessRateLabel.Text = "成功率";
            this.lblSuccessRateLabel.TextAlign = ContentAlignment.MiddleCenter;
            this.lblSuccessRateLabel.Dock = DockStyle.Fill;

            // Labels - Row 3 (Values)
            this.lblTotalSize.AutoSize = true;
            this.lblTotalSize.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            this.lblTotalSize.Text = "0 B";
            this.lblTotalSize.TextAlign = ContentAlignment.MiddleCenter;
            this.lblTotalSize.Dock = DockStyle.Fill;

            this.lblTotalChannels.AutoSize = true;
            this.lblTotalChannels.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            this.lblTotalChannels.Text = "0";
            this.lblTotalChannels.TextAlign = ContentAlignment.MiddleCenter;
            this.lblTotalChannels.Dock = DockStyle.Fill;

            this.lblActiveChannels.AutoSize = true;
            this.lblActiveChannels.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            this.lblActiveChannels.Text = "0";
            this.lblActiveChannels.TextAlign = ContentAlignment.MiddleCenter;
            this.lblActiveChannels.Dock = DockStyle.Fill;

            this.lblSuccessRate.AutoSize = true;
            this.lblSuccessRate.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            this.lblSuccessRate.Text = "-";
            this.lblSuccessRate.TextAlign = ContentAlignment.MiddleCenter;
            this.lblSuccessRate.Dock = DockStyle.Fill;

            // 
            // progressSuccess
            // 
            this.progressSuccess.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.progressSuccess.Location = new Point(10, 130);
            this.progressSuccess.Name = "progressSuccess";
            this.progressSuccess.Size = new Size(540, 20);
            this.progressSuccess.Style = ProgressBarStyle.Continuous;
            this.progressSuccess.TabIndex = 1;

            // 
            // tabControl
            // 
            this.tabControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.tabControl.Controls.Add(this.tabChannels);
            this.tabControl.Controls.Add(this.tabDaily);
            this.tabControl.Controls.Add(this.tabFailure);
            this.tabControl.Controls.Add(this.tabMonthly);
            this.tabControl.Controls.Add(this.tabDistribution);
            this.tabControl.Controls.Add(this.tabContent);
            this.tabControl.Controls.Add(this.tabAuthors);
            this.tabControl.Location = new Point(12, 178);
            this.tabControl.Multiline = true;
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new Size(560, 270);
            this.tabControl.TabIndex = 1;

            // 
            // tabChannels
            // 
            this.tabChannels.Controls.Add(this.dgvChannelStats);
            this.tabChannels.Location = new Point(4, 24);
            this.tabChannels.Name = "tabChannels";
            this.tabChannels.Padding = new Padding(3);
            this.tabChannels.Size = new Size(552, 242);
            this.tabChannels.TabIndex = 0;
            this.tabChannels.Text = "チャンネル別";
            this.tabChannels.UseVisualStyleBackColor = true;

            // 
            // dgvChannelStats
            // 
            this.dgvChannelStats.AllowUserToAddRows = false;
            this.dgvChannelStats.AllowUserToDeleteRows = false;
            this.dgvChannelStats.AllowUserToResizeRows = false;
            this.dgvChannelStats.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvChannelStats.BackgroundColor = SystemColors.Window;
            this.dgvChannelStats.BorderStyle = BorderStyle.None;
            this.dgvChannelStats.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvChannelStats.Dock = DockStyle.Fill;
            this.dgvChannelStats.Location = new Point(3, 3);
            this.dgvChannelStats.Name = "dgvChannelStats";
            this.dgvChannelStats.ReadOnly = true;
            this.dgvChannelStats.RowHeadersVisible = false;
            this.dgvChannelStats.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this.dgvChannelStats.Size = new Size(546, 236);
            this.dgvChannelStats.TabIndex = 0;

            // 
            // tabDaily
            // 
            this.tabDaily.Controls.Add(this.dgvDailyStats);
            this.tabDaily.Location = new Point(4, 24);
            this.tabDaily.Name = "tabDaily";
            this.tabDaily.Padding = new Padding(3);
            this.tabDaily.Size = new Size(552, 242);
            this.tabDaily.TabIndex = 1;
            this.tabDaily.Text = "日別推移";
            this.tabDaily.UseVisualStyleBackColor = true;

            // 
            // dgvDailyStats
            // 
            this.dgvDailyStats.AllowUserToAddRows = false;
            this.dgvDailyStats.AllowUserToDeleteRows = false;
            this.dgvDailyStats.AllowUserToResizeRows = false;
            this.dgvDailyStats.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvDailyStats.BackgroundColor = SystemColors.Window;
            this.dgvDailyStats.BorderStyle = BorderStyle.None;
            this.dgvDailyStats.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvDailyStats.Dock = DockStyle.Fill;
            this.dgvDailyStats.Location = new Point(3, 3);
            this.dgvDailyStats.Name = "dgvDailyStats";
            this.dgvDailyStats.ReadOnly = true;
            this.dgvDailyStats.RowHeadersVisible = false;
            this.dgvDailyStats.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this.dgvDailyStats.Size = new Size(546, 236);
            this.dgvDailyStats.TabIndex = 0;

            // ============================================================
            // tabFailure (失敗分析) : エラー別 + リトライ別
            // ============================================================
            this.tabFailure.Controls.Add(this.tlpFailure);
            this.tabFailure.Location = new Point(4, 24);
            this.tabFailure.Name = "tabFailure";
            this.tabFailure.Padding = new Padding(3);
            this.tabFailure.Size = new Size(552, 242);
            this.tabFailure.TabIndex = 2;
            this.tabFailure.Text = "失敗分析";
            this.tabFailure.UseVisualStyleBackColor = true;

            this.tlpFailure.ColumnCount = 1;
            this.tlpFailure.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.tlpFailure.Controls.Add(this.lblErrorTitle, 0, 0);
            this.tlpFailure.Controls.Add(this.dgvErrorStats, 0, 1);
            this.tlpFailure.Controls.Add(this.lblRetryTitle, 0, 2);
            this.tlpFailure.Controls.Add(this.dgvRetryStats, 0, 3);
            this.tlpFailure.Dock = DockStyle.Fill;
            this.tlpFailure.Location = new Point(3, 3);
            this.tlpFailure.Name = "tlpFailure";
            this.tlpFailure.RowCount = 4;
            this.tlpFailure.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            this.tlpFailure.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));
            this.tlpFailure.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            this.tlpFailure.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));
            this.tlpFailure.Size = new Size(546, 236);
            this.tlpFailure.TabIndex = 0;

            this.lblErrorTitle.AutoSize = false;
            this.lblErrorTitle.Dock = DockStyle.Fill;
            this.lblErrorTitle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            this.lblErrorTitle.Text = "エラー種別ごとの失敗数";
            this.lblErrorTitle.TextAlign = ContentAlignment.MiddleLeft;

            this.lblRetryTitle.AutoSize = false;
            this.lblRetryTitle.Dock = DockStyle.Fill;
            this.lblRetryTitle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            this.lblRetryTitle.Text = "リトライ回数別の失敗数";
            this.lblRetryTitle.TextAlign = ContentAlignment.MiddleLeft;

            this.ConfigureStatGrid(this.dgvErrorStats, "dgvErrorStats");
            this.ConfigureStatGrid(this.dgvRetryStats, "dgvRetryStats");

            // ============================================================
            // tabMonthly (月別推移)
            // ============================================================
            this.tabMonthly.Controls.Add(this.dgvMonthlyStats);
            this.tabMonthly.Location = new Point(4, 24);
            this.tabMonthly.Name = "tabMonthly";
            this.tabMonthly.Padding = new Padding(3);
            this.tabMonthly.Size = new Size(552, 242);
            this.tabMonthly.TabIndex = 3;
            this.tabMonthly.Text = "月別推移";
            this.tabMonthly.UseVisualStyleBackColor = true;
            this.ConfigureStatGrid(this.dgvMonthlyStats, "dgvMonthlyStats");

            // ============================================================
            // tabDistribution (サイズ・長さ) : サイズ分布 + 動画長分布
            // ============================================================
            this.tabDistribution.Controls.Add(this.tlpDistribution);
            this.tabDistribution.Location = new Point(4, 24);
            this.tabDistribution.Name = "tabDistribution";
            this.tabDistribution.Padding = new Padding(3);
            this.tabDistribution.Size = new Size(552, 242);
            this.tabDistribution.TabIndex = 4;
            this.tabDistribution.Text = "サイズ・長さ";
            this.tabDistribution.UseVisualStyleBackColor = true;

            this.tlpDistribution.ColumnCount = 1;
            this.tlpDistribution.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.tlpDistribution.Controls.Add(this.lblSizeTitle, 0, 0);
            this.tlpDistribution.Controls.Add(this.dgvSizeStats, 0, 1);
            this.tlpDistribution.Controls.Add(this.lblDurationTitle, 0, 2);
            this.tlpDistribution.Controls.Add(this.dgvDurationStats, 0, 3);
            this.tlpDistribution.Dock = DockStyle.Fill;
            this.tlpDistribution.Location = new Point(3, 3);
            this.tlpDistribution.Name = "tlpDistribution";
            this.tlpDistribution.RowCount = 4;
            this.tlpDistribution.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            this.tlpDistribution.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            this.tlpDistribution.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            this.tlpDistribution.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            this.tlpDistribution.Size = new Size(546, 236);
            this.tlpDistribution.TabIndex = 0;

            this.lblSizeTitle.AutoSize = false;
            this.lblSizeTitle.Dock = DockStyle.Fill;
            this.lblSizeTitle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            this.lblSizeTitle.Text = "ファイルサイズ分布";
            this.lblSizeTitle.TextAlign = ContentAlignment.MiddleLeft;

            this.lblDurationTitle.AutoSize = false;
            this.lblDurationTitle.Dock = DockStyle.Fill;
            this.lblDurationTitle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            this.lblDurationTitle.Text = "再生時間分布";
            this.lblDurationTitle.TextAlign = ContentAlignment.MiddleLeft;

            this.ConfigureStatGrid(this.dgvSizeStats, "dgvSizeStats");
            this.ConfigureStatGrid(this.dgvDurationStats, "dgvDurationStats");

            // ============================================================
            // tabContent (内容) : タグランキング + Rating + サイト
            // ============================================================
            this.tabContent.Controls.Add(this.tlpContent);
            this.tabContent.Location = new Point(4, 24);
            this.tabContent.Name = "tabContent";
            this.tabContent.Padding = new Padding(3);
            this.tabContent.Size = new Size(552, 242);
            this.tabContent.TabIndex = 5;
            this.tabContent.Text = "内容";
            this.tabContent.UseVisualStyleBackColor = true;

            this.tlpContent.ColumnCount = 1;
            this.tlpContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.tlpContent.Controls.Add(this.lblTagTitle, 0, 0);
            this.tlpContent.Controls.Add(this.dgvTagStats, 0, 1);
            this.tlpContent.Controls.Add(this.lblRatingTitle, 0, 2);
            this.tlpContent.Controls.Add(this.dgvRatingStats, 0, 3);
            this.tlpContent.Controls.Add(this.lblSiteTitle, 0, 4);
            this.tlpContent.Controls.Add(this.dgvSiteStats, 0, 5);
            this.tlpContent.Dock = DockStyle.Fill;
            this.tlpContent.Location = new Point(3, 3);
            this.tlpContent.Name = "tlpContent";
            this.tlpContent.RowCount = 6;
            this.tlpContent.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            this.tlpContent.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            this.tlpContent.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            this.tlpContent.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
            this.tlpContent.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            this.tlpContent.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
            this.tlpContent.Size = new Size(546, 236);
            this.tlpContent.TabIndex = 0;

            this.lblTagTitle.AutoSize = false;
            this.lblTagTitle.Dock = DockStyle.Fill;
            this.lblTagTitle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            this.lblTagTitle.Text = "タグ別ランキング(上位50)";
            this.lblTagTitle.TextAlign = ContentAlignment.MiddleLeft;

            this.lblRatingTitle.AutoSize = false;
            this.lblRatingTitle.Dock = DockStyle.Fill;
            this.lblRatingTitle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            this.lblRatingTitle.Text = "Rating別";
            this.lblRatingTitle.TextAlign = ContentAlignment.MiddleLeft;

            this.lblSiteTitle.AutoSize = false;
            this.lblSiteTitle.Dock = DockStyle.Fill;
            this.lblSiteTitle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            this.lblSiteTitle.Text = "サイト別";
            this.lblSiteTitle.TextAlign = ContentAlignment.MiddleLeft;

            this.ConfigureStatGrid(this.dgvTagStats, "dgvTagStats");
            this.ConfigureStatGrid(this.dgvRatingStats, "dgvRatingStats");
            this.ConfigureStatGrid(this.dgvSiteStats, "dgvSiteStats");

            // ============================================================
            // tabAuthors (投稿者ランキング)
            // ============================================================
            this.tabAuthors.Controls.Add(this.dgvAuthorStats);
            this.tabAuthors.Location = new Point(4, 24);
            this.tabAuthors.Name = "tabAuthors";
            this.tabAuthors.Padding = new Padding(3);
            this.tabAuthors.Size = new Size(552, 242);
            this.tabAuthors.TabIndex = 6;
            this.tabAuthors.Text = "投稿者";
            this.tabAuthors.UseVisualStyleBackColor = true;
            this.ConfigureStatGrid(this.dgvAuthorStats, "dgvAuthorStats");

            //
            // btnRefresh
            //
            this.btnRefresh.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            this.btnRefresh.Location = new Point(12, 458);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new Size(75, 27);
            this.btnRefresh.TabIndex = 2;
            this.btnRefresh.Text = "更新";
            this.btnRefresh.UseVisualStyleBackColor = true;
            this.btnRefresh.Click += new EventHandler(this.btnRefresh_Click);

            // 
            // btnExportCsv
            // 
            this.btnExportCsv.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            this.btnExportCsv.Location = new Point(93, 458);
            this.btnExportCsv.Name = "btnExportCsv";
            this.btnExportCsv.Size = new Size(100, 27);
            this.btnExportCsv.TabIndex = 3;
            this.btnExportCsv.Text = "CSVエクスポート";
            this.btnExportCsv.UseVisualStyleBackColor = true;
            this.btnExportCsv.Click += new EventHandler(this.btnExportCsv_Click);

            // 
            // btnClose
            // 
            this.btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.btnClose.DialogResult = DialogResult.Cancel;
            this.btnClose.Location = new Point(497, 458);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new Size(75, 27);
            this.btnClose.TabIndex = 4;
            this.btnClose.Text = "閉じる";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new EventHandler(this.btnClose_Click);

            // 
            // StatisticsForm
            // 
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.CancelButton = this.btnClose;
            this.ClientSize = new Size(584, 497);
            this.Controls.Add(this.grpOverview);
            this.Controls.Add(this.tabControl);
            this.Controls.Add(this.btnRefresh);
            this.Controls.Add(this.btnExportCsv);
            this.Controls.Add(this.btnClose);
            this.MinimizeBox = false;
            this.MinimumSize = new Size(500, 450);
            this.Name = "StatisticsForm";
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "統計ダッシュボード";
            this.Load += new EventHandler(this.StatisticsForm_Load);
            this.grpOverview.ResumeLayout(false);
            this.tableOverview.ResumeLayout(false);
            this.tableOverview.PerformLayout();
            this.tabControl.ResumeLayout(false);
            this.tabChannels.ResumeLayout(false);
            this.tabDaily.ResumeLayout(false);
            this.tabFailure.ResumeLayout(false);
            this.tlpFailure.ResumeLayout(false);
            this.tabMonthly.ResumeLayout(false);
            this.tabDistribution.ResumeLayout(false);
            this.tlpDistribution.ResumeLayout(false);
            this.tabContent.ResumeLayout(false);
            this.tlpContent.ResumeLayout(false);
            this.tabAuthors.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvChannelStats)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvDailyStats)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvErrorStats)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvRetryStats)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvMonthlyStats)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvSizeStats)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvDurationStats)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvTagStats)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvRatingStats)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvSiteStats)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvAuthorStats)).EndInit();
            this.ResumeLayout(false);
        }

        /// <summary>
        /// 統計用 DataGridView の共通プロパティを設定する
        /// </summary>
        private void ConfigureStatGrid(DataGridView dgv, string name)
        {
            dgv.AllowUserToAddRows = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.AllowUserToResizeRows = false;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgv.BackgroundColor = SystemColors.Window;
            dgv.BorderStyle = BorderStyle.None;
            dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgv.Dock = DockStyle.Fill;
            dgv.Name = name;
            dgv.ReadOnly = true;
            dgv.RowHeadersVisible = false;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.TabIndex = 0;
        }

        #endregion

        private GroupBox grpOverview;
        private TableLayoutPanel tableOverview;
        private Label lblTotalVideosLabel;
        private Label lblTotalVideos;
        private Label lblCompletedLabel;
        private Label lblCompletedVideos;
        private Label lblFailedLabel;
        private Label lblFailedVideos;
        private Label lblPendingLabel;
        private Label lblPendingVideos;
        private Label lblTotalSizeLabel;
        private Label lblTotalSize;
        private Label lblChannelsLabel;
        private Label lblTotalChannels;
        private Label lblActiveChannelsLabel;
        private Label lblActiveChannels;
        private Label lblSuccessRateLabel;
        private Label lblSuccessRate;
        private ProgressBar progressSuccess;
        private TabControl tabControl;
        private TabPage tabChannels;
        private TabPage tabDaily;
        private DataGridView dgvChannelStats;
        private DataGridView dgvDailyStats;
        private TabPage tabFailure;
        private TableLayoutPanel tlpFailure;
        private Label lblErrorTitle;
        private DataGridView dgvErrorStats;
        private Label lblRetryTitle;
        private DataGridView dgvRetryStats;
        private TabPage tabMonthly;
        private DataGridView dgvMonthlyStats;
        private TabPage tabDistribution;
        private TableLayoutPanel tlpDistribution;
        private Label lblSizeTitle;
        private DataGridView dgvSizeStats;
        private Label lblDurationTitle;
        private DataGridView dgvDurationStats;
        private TabPage tabContent;
        private TableLayoutPanel tlpContent;
        private Label lblTagTitle;
        private DataGridView dgvTagStats;
        private Label lblRatingTitle;
        private DataGridView dgvRatingStats;
        private Label lblSiteTitle;
        private DataGridView dgvSiteStats;
        private TabPage tabAuthors;
        private DataGridView dgvAuthorStats;
        private Button btnRefresh;
        private Button btnExportCsv;
        private Button btnClose;
    }
}
