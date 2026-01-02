namespace IwaraDownloader.Forms
{
    partial class DuplicateCheckForm
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
            this.dgvDuplicates = new DataGridView();
            this.lblDetails = new Label();
            this.lstDetails = new ListBox();
            this.btnScan = new Button();
            this.btnRemoveDuplicates = new Button();
            this.btnRemoveSelected = new Button();
            this.btnClose = new Button();
            this.lblStatus = new Label();
            this.splitContainer = new SplitContainer();

            ((System.ComponentModel.ISupportInitialize)(this.dgvDuplicates)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.SuspendLayout();

            // 
            // lblDescription
            // 
            this.lblDescription.AutoSize = true;
            this.lblDescription.Location = new Point(12, 12);
            this.lblDescription.Name = "lblDescription";
            this.lblDescription.Size = new Size(400, 15);
            this.lblDescription.Text = "複数のチャンネルに存在する同一動画（VideoId）を検出します。";

            // 
            // splitContainer
            // 
            this.splitContainer.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.splitContainer.Location = new Point(12, 35);
            this.splitContainer.Name = "splitContainer";
            this.splitContainer.Orientation = Orientation.Horizontal;
            this.splitContainer.Size = new Size(560, 370);
            this.splitContainer.SplitterDistance = 240;
            this.splitContainer.TabIndex = 1;

            // 
            // splitContainer.Panel1
            // 
            this.splitContainer.Panel1.Controls.Add(this.dgvDuplicates);

            // 
            // splitContainer.Panel2
            // 
            this.splitContainer.Panel2.Controls.Add(this.lblDetails);
            this.splitContainer.Panel2.Controls.Add(this.lstDetails);
            this.splitContainer.Panel2.Controls.Add(this.btnRemoveSelected);

            // 
            // dgvDuplicates
            // 
            this.dgvDuplicates.AllowUserToAddRows = false;
            this.dgvDuplicates.AllowUserToDeleteRows = false;
            this.dgvDuplicates.AllowUserToResizeRows = false;
            this.dgvDuplicates.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvDuplicates.BackgroundColor = SystemColors.Window;
            this.dgvDuplicates.BorderStyle = BorderStyle.FixedSingle;
            this.dgvDuplicates.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvDuplicates.Dock = DockStyle.Fill;
            this.dgvDuplicates.Location = new Point(0, 0);
            this.dgvDuplicates.MultiSelect = false;
            this.dgvDuplicates.Name = "dgvDuplicates";
            this.dgvDuplicates.ReadOnly = true;
            this.dgvDuplicates.RowHeadersVisible = false;
            this.dgvDuplicates.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this.dgvDuplicates.Size = new Size(560, 240);
            this.dgvDuplicates.TabIndex = 0;
            this.dgvDuplicates.SelectionChanged += new EventHandler(this.dgvDuplicates_SelectionChanged);

            // 
            // lblDetails
            // 
            this.lblDetails.AutoSize = true;
            this.lblDetails.Location = new Point(3, 5);
            this.lblDetails.Name = "lblDetails";
            this.lblDetails.Size = new Size(100, 15);
            this.lblDetails.Text = "選択した重複の詳細:";

            // 
            // lstDetails
            // 
            this.lstDetails.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.lstDetails.Font = new Font("Consolas", 9F);
            this.lstDetails.FormattingEnabled = true;
            this.lstDetails.ItemHeight = 14;
            this.lstDetails.Location = new Point(3, 25);
            this.lstDetails.Name = "lstDetails";
            this.lstDetails.Size = new Size(450, 88);
            this.lstDetails.TabIndex = 1;

            // 
            // btnRemoveSelected
            // 
            this.btnRemoveSelected.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.btnRemoveSelected.Location = new Point(459, 25);
            this.btnRemoveSelected.Name = "btnRemoveSelected";
            this.btnRemoveSelected.Size = new Size(95, 27);
            this.btnRemoveSelected.TabIndex = 2;
            this.btnRemoveSelected.Text = "選択項目を削除";
            this.btnRemoveSelected.UseVisualStyleBackColor = true;
            this.btnRemoveSelected.Click += new EventHandler(this.btnRemoveSelected_Click);

            // 
            // lblStatus
            // 
            this.lblStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new Point(12, 415);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new Size(80, 15);
            this.lblStatus.Text = "スキャン中...";

            // 
            // btnScan
            // 
            this.btnScan.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            this.btnScan.Location = new Point(12, 440);
            this.btnScan.Name = "btnScan";
            this.btnScan.Size = new Size(90, 27);
            this.btnScan.TabIndex = 2;
            this.btnScan.Text = "再スキャン";
            this.btnScan.UseVisualStyleBackColor = true;
            this.btnScan.Click += new EventHandler(this.btnScan_Click);

            // 
            // btnRemoveDuplicates
            // 
            this.btnRemoveDuplicates.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            this.btnRemoveDuplicates.Location = new Point(108, 440);
            this.btnRemoveDuplicates.Name = "btnRemoveDuplicates";
            this.btnRemoveDuplicates.Size = new Size(130, 27);
            this.btnRemoveDuplicates.TabIndex = 3;
            this.btnRemoveDuplicates.Text = "重複を自動解消";
            this.btnRemoveDuplicates.UseVisualStyleBackColor = true;
            this.btnRemoveDuplicates.Click += new EventHandler(this.btnRemoveDuplicates_Click);

            // 
            // btnClose
            // 
            this.btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.btnClose.DialogResult = DialogResult.Cancel;
            this.btnClose.Location = new Point(497, 440);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new Size(75, 27);
            this.btnClose.TabIndex = 4;
            this.btnClose.Text = "閉じる";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new EventHandler(this.btnClose_Click);

            // 
            // DuplicateCheckForm
            // 
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.CancelButton = this.btnClose;
            this.ClientSize = new Size(584, 480);
            this.Controls.Add(this.lblDescription);
            this.Controls.Add(this.splitContainer);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.btnScan);
            this.Controls.Add(this.btnRemoveDuplicates);
            this.Controls.Add(this.btnClose);
            this.MinimizeBox = false;
            this.MinimumSize = new Size(500, 400);
            this.Name = "DuplicateCheckForm";
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "重複チェック";
            this.Load += new EventHandler(this.DuplicateCheckForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dgvDuplicates)).EndInit();
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel2.ResumeLayout(false);
            this.splitContainer.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private Label lblDescription;
        private SplitContainer splitContainer;
        private DataGridView dgvDuplicates;
        private Label lblDetails;
        private ListBox lstDetails;
        private Button btnRemoveSelected;
        private Label lblStatus;
        private Button btnScan;
        private Button btnRemoveDuplicates;
        private Button btnClose;
    }
}
