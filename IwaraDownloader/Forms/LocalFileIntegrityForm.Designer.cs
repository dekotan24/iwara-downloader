namespace IwaraDownloader.Forms
{
    partial class LocalFileIntegrityForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _scanCts?.Cancel();
                _scanCts?.Dispose();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.lblDescription = new Label();
            this.dgvIssues = new DataGridView();
            this.colTitle = new DataGridViewTextBoxColumn();
            this.colVideoId = new DataGridViewTextBoxColumn();
            this.colStatus = new DataGridViewTextBoxColumn();
            this.colDbPath = new DataGridViewTextBoxColumn();
            this.colReason = new DataGridViewTextBoxColumn();
            this.panelFooter = new Panel();
            this.btnScan = new Button();
            this.btnMap = new Button();
            this.btnRedownload = new Button();
            this.btnClose = new Button();
            this.lblStatus = new Label();
            ((System.ComponentModel.ISupportInitialize)(this.dgvIssues)).BeginInit();
            this.panelFooter.SuspendLayout();
            this.SuspendLayout();

            // lblDescription
            this.lblDescription.Dock = DockStyle.Top;
            this.lblDescription.Height = 48;
            this.lblDescription.Padding = new Padding(10, 8, 10, 4);
            this.lblDescription.Name = "lblDescription";
            this.lblDescription.Text = "DBが保持するローカルファイルの不整合を検出します。対象を選択して、ローカルファイルの再紐付けまたは再ダウンロードを実行できます。";

            // dgvIssues
            this.dgvIssues.AllowUserToAddRows = false;
            this.dgvIssues.AllowUserToDeleteRows = false;
            this.dgvIssues.AllowUserToResizeRows = false;
            this.dgvIssues.AutoGenerateColumns = false;
            this.dgvIssues.BackgroundColor = SystemColors.Window;
            this.dgvIssues.BorderStyle = BorderStyle.FixedSingle;
            this.dgvIssues.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvIssues.Columns.AddRange(new DataGridViewColumn[] {
                this.colTitle, this.colVideoId, this.colStatus, this.colDbPath, this.colReason});
            this.dgvIssues.Dock = DockStyle.Fill;
            this.dgvIssues.MultiSelect = true;
            this.dgvIssues.Name = "dgvIssues";
            this.dgvIssues.ReadOnly = true;
            this.dgvIssues.RowHeadersVisible = false;
            this.dgvIssues.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this.dgvIssues.SelectionChanged += new EventHandler(this.dgvIssues_SelectionChanged);

            // colTitle
            this.colTitle.HeaderText = "タイトル";
            this.colTitle.Name = "colTitle";
            this.colTitle.ReadOnly = true;
            this.colTitle.Width = 300;

            // colVideoId
            this.colVideoId.HeaderText = "Video ID";
            this.colVideoId.Name = "colVideoId";
            this.colVideoId.ReadOnly = true;
            this.colVideoId.Width = 130;

            // colStatus
            this.colStatus.HeaderText = "状態";
            this.colStatus.Name = "colStatus";
            this.colStatus.ReadOnly = true;
            this.colStatus.Width = 90;

            // colDbPath
            this.colDbPath.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            this.colDbPath.HeaderText = "DBの保存先";
            this.colDbPath.MinimumWidth = 240;
            this.colDbPath.Name = "colDbPath";
            this.colDbPath.ReadOnly = true;

            // colReason
            this.colReason.HeaderText = "不整合理由";
            this.colReason.Name = "colReason";
            this.colReason.ReadOnly = true;
            this.colReason.Width = 180;

            // panelFooter
            this.panelFooter.Controls.Add(this.btnClose);
            this.panelFooter.Controls.Add(this.btnRedownload);
            this.panelFooter.Controls.Add(this.btnMap);
            this.panelFooter.Controls.Add(this.btnScan);
            this.panelFooter.Controls.Add(this.lblStatus);
            this.panelFooter.Dock = DockStyle.Bottom;
            this.panelFooter.Height = 54;
            this.panelFooter.Name = "panelFooter";
            this.panelFooter.Padding = new Padding(8, 8, 8, 8);

            // lblStatus
            this.lblStatus.AutoEllipsis = true;
            this.lblStatus.Dock = DockStyle.Fill;
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Text = "スキャン待機中";
            this.lblStatus.TextAlign = ContentAlignment.MiddleLeft;

            // btnScan
            this.btnScan.Dock = DockStyle.Right;
            this.btnScan.Margin = new Padding(4, 0, 0, 0);
            this.btnScan.Name = "btnScan";
            this.btnScan.Size = new Size(100, 38);
            this.btnScan.TabIndex = 0;
            this.btnScan.Text = "再スキャン";
            this.btnScan.UseVisualStyleBackColor = true;
            this.btnScan.Click += new EventHandler(this.btnScan_Click);

            // btnMap
            this.btnMap.Dock = DockStyle.Right;
            this.btnMap.Margin = new Padding(4, 0, 0, 0);
            this.btnMap.Name = "btnMap";
            this.btnMap.Size = new Size(150, 38);
            this.btnMap.TabIndex = 1;
            this.btnMap.Text = "ローカルを再紐付け...";
            this.btnMap.UseVisualStyleBackColor = true;
            this.btnMap.Click += new EventHandler(this.btnMap_Click);

            // btnRedownload
            this.btnRedownload.Dock = DockStyle.Right;
            this.btnRedownload.Margin = new Padding(4, 0, 0, 0);
            this.btnRedownload.Name = "btnRedownload";
            this.btnRedownload.Size = new Size(150, 38);
            this.btnRedownload.TabIndex = 2;
            this.btnRedownload.Text = "再ダウンロード";
            this.btnRedownload.UseVisualStyleBackColor = true;
            this.btnRedownload.Click += new EventHandler(this.btnRedownload_Click);

            // btnClose
            this.btnClose.DialogResult = DialogResult.Cancel;
            this.btnClose.Dock = DockStyle.Right;
            this.btnClose.Margin = new Padding(4, 0, 0, 0);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new Size(90, 38);
            this.btnClose.TabIndex = 3;
            this.btnClose.Text = "閉じる";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new EventHandler(this.btnClose_Click);

            // LocalFileIntegrityForm
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.CancelButton = this.btnClose;
            this.ClientSize = new Size(1180, 620);
            this.Controls.Add(this.dgvIssues);
            this.Controls.Add(this.lblDescription);
            this.Controls.Add(this.panelFooter);
            this.MinimizeBox = false;
            this.MinimumSize = new Size(800, 420);
            this.Name = "LocalFileIntegrityForm";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "ローカルファイル整合性チェック";
            this.Load += new EventHandler(this.LocalFileIntegrityForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dgvIssues)).EndInit();
            this.panelFooter.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        private Label lblDescription;
        private DataGridView dgvIssues;
        private DataGridViewTextBoxColumn colTitle;
        private DataGridViewTextBoxColumn colVideoId;
        private DataGridViewTextBoxColumn colStatus;
        private DataGridViewTextBoxColumn colDbPath;
        private DataGridViewTextBoxColumn colReason;
        private Panel panelFooter;
        private Button btnScan;
        private Button btnMap;
        private Button btnRedownload;
        private Button btnClose;
        private Label lblStatus;
    }
}
