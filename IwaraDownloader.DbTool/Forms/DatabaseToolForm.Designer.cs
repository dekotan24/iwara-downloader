namespace IwaraDownloader.Forms
{
    partial class DatabaseToolForm
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

        private void InitializeComponent()
        {
            this.panelHeader = new Panel();
            this.btnToggleWriteMode = new Button();
            this.lblIntegrityWarning = new Label();
            this.lblBanner = new Label();
            this.tabControl = new TabControl();
            this.tabSql = new TabPage();
            this.gridSqlResult = new DataGridView();
            this.panelSqlTop = new Panel();
            this.panelSqlButtons = new Panel();
            this.chkApplyRowLimit = new CheckBox();
            this.lblMaxRows = new Label();
            this.numMaxRows = new NumericUpDown();
            this.btnExecuteSql = new Button();
            this.txtSql = new TextBox();
            this.lblSqlHint = new Label();
            this.lblSqlStatus = new Label();
            this.tabBrowser = new TabPage();
            this.gridBrowser = new DataGridView();
            this.panelBrowserTop = new Panel();
            this.btnDeleteSelectedRow = new Button();
            this.btnRefreshTable = new Button();
            this.cmbTable = new ComboBox();
            this.lblTable = new Label();
            this.lblBrowserStatus = new Label();

            this.panelHeader.SuspendLayout();
            this.tabControl.SuspendLayout();
            this.tabSql.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridSqlResult)).BeginInit();
            this.panelSqlTop.SuspendLayout();
            this.panelSqlButtons.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numMaxRows)).BeginInit();
            this.tabBrowser.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridBrowser)).BeginInit();
            this.panelBrowserTop.SuspendLayout();
            this.SuspendLayout();

            //
            // panelHeader
            //
            this.panelHeader.Controls.Add(this.btnToggleWriteMode);
            this.panelHeader.Controls.Add(this.lblIntegrityWarning);
            this.panelHeader.Controls.Add(this.lblBanner);
            this.panelHeader.Dock = DockStyle.Top;
            this.panelHeader.Name = "panelHeader";
            this.panelHeader.Padding = new Padding(8, 6, 8, 4);
            this.panelHeader.Size = new Size(950, 100);
            this.panelHeader.TabIndex = 0;

            //
            // lblBanner
            //
            this.lblBanner.Dock = DockStyle.Top;
            this.lblBanner.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            this.lblBanner.Name = "lblBanner";
            this.lblBanner.Size = new Size(934, 26);
            this.lblBanner.Text = "読み取り専用モード";
            this.lblBanner.TextAlign = ContentAlignment.MiddleLeft;

            //
            // lblIntegrityWarning
            //
            this.lblIntegrityWarning.Dock = DockStyle.Top;
            this.lblIntegrityWarning.ForeColor = Color.OrangeRed;
            this.lblIntegrityWarning.Name = "lblIntegrityWarning";
            this.lblIntegrityWarning.Size = new Size(934, 20);
            this.lblIntegrityWarning.Visible = false;

            //
            // btnToggleWriteMode
            //
            this.btnToggleWriteMode.Location = new Point(0, 34);
            this.btnToggleWriteMode.Name = "btnToggleWriteMode";
            this.btnToggleWriteMode.Size = new Size(180, 27);
            this.btnToggleWriteMode.TabIndex = 2;
            this.btnToggleWriteMode.Text = "書き込みモードにする";
            this.btnToggleWriteMode.UseVisualStyleBackColor = true;
            this.btnToggleWriteMode.Click += new EventHandler(this.btnToggleWriteMode_Click);

            //
            // tabControl
            //
            this.tabControl.Controls.Add(this.tabSql);
            this.tabControl.Controls.Add(this.tabBrowser);
            this.tabControl.Dock = DockStyle.Fill;
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new Size(950, 480);
            this.tabControl.TabIndex = 1;

            //
            // tabSql
            //
            this.tabSql.Controls.Add(this.gridSqlResult);
            this.tabSql.Controls.Add(this.panelSqlTop);
            this.tabSql.Controls.Add(this.lblSqlStatus);
            this.tabSql.Location = new Point(4, 24);
            this.tabSql.Name = "tabSql";
            this.tabSql.Padding = new Padding(6);
            this.tabSql.Size = new Size(942, 452);
            this.tabSql.TabIndex = 0;
            this.tabSql.Text = "SQL実行";
            this.tabSql.UseVisualStyleBackColor = true;

            //
            // panelSqlTop
            //
            this.panelSqlTop.Controls.Add(this.txtSql);
            this.panelSqlTop.Controls.Add(this.panelSqlButtons);
            this.panelSqlTop.Controls.Add(this.lblSqlHint);
            this.panelSqlTop.Dock = DockStyle.Top;
            this.panelSqlTop.Name = "panelSqlTop";
            this.panelSqlTop.Padding = new Padding(0, 0, 0, 4);
            this.panelSqlTop.Size = new Size(930, 160);
            this.panelSqlTop.TabIndex = 0;

            //
            // lblSqlHint
            //
            this.lblSqlHint.Dock = DockStyle.Top;
            this.lblSqlHint.ForeColor = Color.Gray;
            this.lblSqlHint.Name = "lblSqlHint";
            this.lblSqlHint.Size = new Size(930, 18);
            this.lblSqlHint.Text = "SQLを入力して実行してください。読み取り専用モード中は SELECT のみ結果が返り、書き込み系は自動的に拒否されます。";

            //
            // panelSqlButtons
            //
            this.panelSqlButtons.Controls.Add(this.btnExecuteSql);
            this.panelSqlButtons.Controls.Add(this.numMaxRows);
            this.panelSqlButtons.Controls.Add(this.lblMaxRows);
            this.panelSqlButtons.Controls.Add(this.chkApplyRowLimit);
            this.panelSqlButtons.Dock = DockStyle.Bottom;
            this.panelSqlButtons.Name = "panelSqlButtons";
            this.panelSqlButtons.Size = new Size(930, 34);
            this.panelSqlButtons.TabIndex = 2;

            //
            // chkApplyRowLimit
            //
            this.chkApplyRowLimit.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.chkApplyRowLimit.AutoSize = true;
            this.chkApplyRowLimit.Checked = true;
            this.chkApplyRowLimit.CheckState = CheckState.Checked;
            this.chkApplyRowLimit.Location = new Point(470, 8);
            this.chkApplyRowLimit.Name = "chkApplyRowLimit";
            this.chkApplyRowLimit.Text = "上限を適用";
            this.chkApplyRowLimit.UseVisualStyleBackColor = true;
            this.chkApplyRowLimit.CheckedChanged += new EventHandler(this.chkApplyRowLimit_CheckedChanged);

            //
            // lblMaxRows
            //
            this.lblMaxRows.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.lblMaxRows.AutoSize = true;
            this.lblMaxRows.Location = new Point(600, 9);
            this.lblMaxRows.Name = "lblMaxRows";
            this.lblMaxRows.Text = "最大取得件数:";

            //
            // numMaxRows
            //
            this.numMaxRows.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.numMaxRows.Location = new Point(695, 5);
            this.numMaxRows.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            this.numMaxRows.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numMaxRows.Increment = new decimal(new int[] { 500, 0, 0, 0 });
            this.numMaxRows.Name = "numMaxRows";
            this.numMaxRows.Size = new Size(100, 23);
            this.numMaxRows.TabIndex = 3;
            this.numMaxRows.ThousandsSeparator = true;
            this.numMaxRows.Value = new decimal(new int[] { 1000, 0, 0, 0 });

            //
            // btnExecuteSql
            //
            this.btnExecuteSql.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.btnExecuteSql.Location = new Point(810, 4);
            this.btnExecuteSql.Name = "btnExecuteSql";
            this.btnExecuteSql.Size = new Size(120, 27);
            this.btnExecuteSql.TabIndex = 0;
            this.btnExecuteSql.Text = "実行 (F5)";
            this.btnExecuteSql.UseVisualStyleBackColor = true;
            this.btnExecuteSql.Click += new EventHandler(this.btnExecuteSql_Click);

            //
            // txtSql
            //
            this.txtSql.AcceptsReturn = true;
            this.txtSql.AcceptsTab = true;
            this.txtSql.Dock = DockStyle.Fill;
            this.txtSql.Font = new Font("Consolas", 9.75F);
            this.txtSql.Multiline = true;
            this.txtSql.Name = "txtSql";
            this.txtSql.ScrollBars = ScrollBars.Vertical;
            this.txtSql.Size = new Size(930, 108);
            this.txtSql.TabIndex = 1;
            this.txtSql.KeyDown += new KeyEventHandler(this.txtSql_KeyDown);

            //
            // gridSqlResult
            //
            this.gridSqlResult.AllowUserToAddRows = false;
            this.gridSqlResult.AllowUserToDeleteRows = false;
            this.gridSqlResult.AllowUserToOrderColumns = true;
            this.gridSqlResult.AutoGenerateColumns = true;
            this.gridSqlResult.Dock = DockStyle.Fill;
            this.gridSqlResult.Name = "gridSqlResult";
            this.gridSqlResult.ReadOnly = true;
            this.gridSqlResult.Size = new Size(930, 250);
            this.gridSqlResult.TabIndex = 1;

            //
            // lblSqlStatus
            //
            this.lblSqlStatus.Dock = DockStyle.Bottom;
            this.lblSqlStatus.Name = "lblSqlStatus";
            this.lblSqlStatus.Padding = new Padding(0, 4, 0, 0);
            this.lblSqlStatus.Size = new Size(930, 24);

            //
            // tabBrowser
            //
            this.tabBrowser.Controls.Add(this.gridBrowser);
            this.tabBrowser.Controls.Add(this.panelBrowserTop);
            this.tabBrowser.Controls.Add(this.lblBrowserStatus);
            this.tabBrowser.Location = new Point(4, 24);
            this.tabBrowser.Name = "tabBrowser";
            this.tabBrowser.Padding = new Padding(6);
            this.tabBrowser.Size = new Size(942, 452);
            this.tabBrowser.TabIndex = 1;
            this.tabBrowser.Text = "テーブルブラウザ";
            this.tabBrowser.UseVisualStyleBackColor = true;

            //
            // panelBrowserTop
            //
            this.panelBrowserTop.Controls.Add(this.btnDeleteSelectedRow);
            this.panelBrowserTop.Controls.Add(this.btnRefreshTable);
            this.panelBrowserTop.Controls.Add(this.cmbTable);
            this.panelBrowserTop.Controls.Add(this.lblTable);
            this.panelBrowserTop.Dock = DockStyle.Top;
            this.panelBrowserTop.Name = "panelBrowserTop";
            this.panelBrowserTop.Size = new Size(930, 38);
            this.panelBrowserTop.TabIndex = 0;

            //
            // lblTable
            //
            this.lblTable.AutoSize = true;
            this.lblTable.Location = new Point(0, 10);
            this.lblTable.Name = "lblTable";
            this.lblTable.Text = "テーブル:";

            //
            // cmbTable
            //
            this.cmbTable.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbTable.Location = new Point(70, 6);
            this.cmbTable.Name = "cmbTable";
            this.cmbTable.Size = new Size(220, 23);
            this.cmbTable.TabIndex = 0;
            this.cmbTable.SelectedIndexChanged += new EventHandler(this.cmbTable_SelectedIndexChanged);

            //
            // btnRefreshTable
            //
            this.btnRefreshTable.Location = new Point(300, 4);
            this.btnRefreshTable.Name = "btnRefreshTable";
            this.btnRefreshTable.Size = new Size(90, 27);
            this.btnRefreshTable.TabIndex = 1;
            this.btnRefreshTable.Text = "再読込";
            this.btnRefreshTable.UseVisualStyleBackColor = true;
            this.btnRefreshTable.Click += new EventHandler(this.btnRefreshTable_Click);

            //
            // btnDeleteSelectedRow
            //
            this.btnDeleteSelectedRow.Location = new Point(400, 4);
            this.btnDeleteSelectedRow.Name = "btnDeleteSelectedRow";
            this.btnDeleteSelectedRow.Size = new Size(160, 27);
            this.btnDeleteSelectedRow.TabIndex = 2;
            this.btnDeleteSelectedRow.Text = "選択行を削除";
            this.btnDeleteSelectedRow.UseVisualStyleBackColor = true;
            this.btnDeleteSelectedRow.Click += new EventHandler(this.btnDeleteSelectedRow_Click);

            //
            // gridBrowser
            //
            this.gridBrowser.AllowUserToAddRows = false;
            this.gridBrowser.AllowUserToDeleteRows = false;
            this.gridBrowser.AutoGenerateColumns = true;
            this.gridBrowser.Dock = DockStyle.Fill;
            this.gridBrowser.Name = "gridBrowser";
            this.gridBrowser.RowHeadersWidth = 30;
            this.gridBrowser.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this.gridBrowser.Size = new Size(930, 250);
            this.gridBrowser.TabIndex = 1;
            this.gridBrowser.CellEndEdit += new DataGridViewCellEventHandler(this.gridBrowser_CellEndEdit);
            this.gridBrowser.DataError += new DataGridViewDataErrorEventHandler(this.gridBrowser_DataError);

            //
            // lblBrowserStatus
            //
            this.lblBrowserStatus.Dock = DockStyle.Bottom;
            this.lblBrowserStatus.Name = "lblBrowserStatus";
            this.lblBrowserStatus.Padding = new Padding(0, 4, 0, 0);
            this.lblBrowserStatus.Size = new Size(930, 24);

            //
            // DatabaseToolForm
            //
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(950, 580);
            this.Controls.Add(this.tabControl);
            this.Controls.Add(this.panelHeader);
            this.MinimumSize = new Size(760, 480);
            this.Name = "DatabaseToolForm";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "DB操作ツール (上級者向け)";
            this.Load += new EventHandler(this.DatabaseToolForm_Load);

            this.panelHeader.ResumeLayout(false);
            this.panelHeader.PerformLayout();
            this.tabControl.ResumeLayout(false);
            this.tabSql.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridSqlResult)).EndInit();
            this.panelSqlTop.ResumeLayout(false);
            this.panelSqlTop.PerformLayout();
            this.panelSqlButtons.ResumeLayout(false);
            this.panelSqlButtons.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numMaxRows)).EndInit();
            this.tabBrowser.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridBrowser)).EndInit();
            this.panelBrowserTop.ResumeLayout(false);
            this.panelBrowserTop.PerformLayout();
            this.ResumeLayout(false);
        }

        private Panel panelHeader;
        private Label lblBanner;
        private Label lblIntegrityWarning;
        private Button btnToggleWriteMode;
        private TabControl tabControl;
        private TabPage tabSql;
        private Panel panelSqlTop;
        private Label lblSqlHint;
        private Panel panelSqlButtons;
        private CheckBox chkApplyRowLimit;
        private Label lblMaxRows;
        private NumericUpDown numMaxRows;
        private Button btnExecuteSql;
        private TextBox txtSql;
        private DataGridView gridSqlResult;
        private Label lblSqlStatus;
        private TabPage tabBrowser;
        private Panel panelBrowserTop;
        private Label lblTable;
        private ComboBox cmbTable;
        private Button btnRefreshTable;
        private Button btnDeleteSelectedRow;
        private DataGridView gridBrowser;
        private Label lblBrowserStatus;
    }
}
