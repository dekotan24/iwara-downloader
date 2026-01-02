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

            this.btnRefresh = new Button();
            this.btnExportCsv = new Button();
            this.btnClose = new Button();

            this.grpOverview.SuspendLayout();
            this.tableOverview.SuspendLayout();
            this.tabControl.SuspendLayout();
            this.tabChannels.SuspendLayout();
            this.tabDaily.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvChannelStats)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvDailyStats)).BeginInit();
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
            this.tabControl.Location = new Point(12, 178);
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
            ((System.ComponentModel.ISupportInitialize)(this.dgvChannelStats)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvDailyStats)).EndInit();
            this.ResumeLayout(false);
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
        private Button btnRefresh;
        private Button btnExportCsv;
        private Button btnClose;
    }
}
