namespace IwaraDownloader.Forms
{
    partial class RenameResultForm
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
            this.listView = new ListView();
            this.colTitle = new ColumnHeader();
            this.colOriginalName = new ColumnHeader();
            this.colNewName = new ColumnHeader();
            this.colStatus = new ColumnHeader();
            this.contextMenu = new ContextMenuStrip(this.components);
            this.menuPlayOriginal = new ToolStripMenuItem();
            this.menuPlayConflict = new ToolStripMenuItem();
            this.menuCompare = new ToolStripMenuItem();
            this.menuSeparator1 = new ToolStripSeparator();
            this.menuOpenFolder = new ToolStripMenuItem();
            this.menuSeparator2 = new ToolStripSeparator();
            this.menuOverwrite = new ToolStripMenuItem();
            this.menuAddNumber = new ToolStripMenuItem();
            this.menuSkip = new ToolStripMenuItem();
            this.panelButtons = new Panel();
            this.grpPlayback = new GroupBox();
            this.btnPlayOriginal = new Button();
            this.btnPlayConflict = new Button();
            this.btnCompare = new Button();
            this.grpConflictActions = new GroupBox();
            this.btnOverwrite = new Button();
            this.btnAddNumber = new Button();
            this.btnSkip = new Button();
            this.btnSelectAllConflicts = new Button();
            this.lblStatus = new Label();
            this.btnClose = new Button();
            this.contextMenu.SuspendLayout();
            this.panelButtons.SuspendLayout();
            this.grpPlayback.SuspendLayout();
            this.grpConflictActions.SuspendLayout();
            this.SuspendLayout();

            // 
            // listView
            // 
            this.listView.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.listView.Columns.AddRange(new ColumnHeader[] {
                this.colTitle,
                this.colOriginalName,
                this.colNewName,
                this.colStatus
            });
            this.listView.ContextMenuStrip = this.contextMenu;
            this.listView.FullRowSelect = true;
            this.listView.GridLines = true;
            this.listView.Location = new Point(12, 12);
            this.listView.Name = "listView";
            this.listView.Size = new Size(760, 300);
            this.listView.TabIndex = 0;
            this.listView.UseCompatibleStateImageBehavior = false;
            this.listView.View = View.Details;
            this.listView.SelectedIndexChanged += new EventHandler(this.listView_SelectedIndexChanged);
            this.listView.DoubleClick += new EventHandler(this.listView_DoubleClick);

            // 
            // colTitle
            // 
            this.colTitle.Text = "タイトル";
            this.colTitle.Width = 200;

            // 
            // colOriginalName
            // 
            this.colOriginalName.Text = "元ファイル名";
            this.colOriginalName.Width = 180;

            // 
            // colNewName
            // 
            this.colNewName.Text = "新ファイル名";
            this.colNewName.Width = 180;

            // 
            // colStatus
            // 
            this.colStatus.Text = "状態";
            this.colStatus.Width = 180;

            // 
            // contextMenu
            // 
            this.contextMenu.Items.AddRange(new ToolStripItem[] {
                this.menuPlayOriginal,
                this.menuPlayConflict,
                this.menuCompare,
                this.menuSeparator1,
                this.menuOpenFolder,
                this.menuSeparator2,
                this.menuOverwrite,
                this.menuAddNumber,
                this.menuSkip
            });
            this.contextMenu.Name = "contextMenu";
            this.contextMenu.Size = new Size(200, 200);
            this.contextMenu.Opening += new System.ComponentModel.CancelEventHandler(this.contextMenu_Opening);

            // 
            // menuPlayOriginal
            // 
            this.menuPlayOriginal.Name = "menuPlayOriginal";
            this.menuPlayOriginal.Size = new Size(199, 22);
            this.menuPlayOriginal.Text = "元ファイルを再生";
            this.menuPlayOriginal.Click += new EventHandler(this.menuPlayOriginal_Click);

            // 
            // menuPlayConflict
            // 
            this.menuPlayConflict.Name = "menuPlayConflict";
            this.menuPlayConflict.Size = new Size(199, 22);
            this.menuPlayConflict.Text = "重複先ファイルを再生";
            this.menuPlayConflict.Click += new EventHandler(this.menuPlayConflict_Click);

            // 
            // menuCompare
            // 
            this.menuCompare.Name = "menuCompare";
            this.menuCompare.Size = new Size(199, 22);
            this.menuCompare.Text = "両方開いて比較";
            this.menuCompare.Click += new EventHandler(this.menuCompare_Click);

            // 
            // menuSeparator1
            // 
            this.menuSeparator1.Name = "menuSeparator1";
            this.menuSeparator1.Size = new Size(196, 6);

            // 
            // menuOpenFolder
            // 
            this.menuOpenFolder.Name = "menuOpenFolder";
            this.menuOpenFolder.Size = new Size(199, 22);
            this.menuOpenFolder.Text = "フォルダを開く";
            this.menuOpenFolder.Click += new EventHandler(this.menuOpenFolder_Click);

            // 
            // menuSeparator2
            // 
            this.menuSeparator2.Name = "menuSeparator2";
            this.menuSeparator2.Size = new Size(196, 6);

            // 
            // menuOverwrite
            // 
            this.menuOverwrite.Name = "menuOverwrite";
            this.menuOverwrite.Size = new Size(199, 22);
            this.menuOverwrite.Text = "上書きする";
            this.menuOverwrite.Click += new EventHandler(this.menuOverwrite_Click);

            // 
            // menuAddNumber
            // 
            this.menuAddNumber.Name = "menuAddNumber";
            this.menuAddNumber.Size = new Size(199, 22);
            this.menuAddNumber.Text = "番号付きでリネーム";
            this.menuAddNumber.Click += new EventHandler(this.menuAddNumber_Click);

            // 
            // menuSkip
            // 
            this.menuSkip.Name = "menuSkip";
            this.menuSkip.Size = new Size(199, 22);
            this.menuSkip.Text = "スキップ";
            this.menuSkip.Click += new EventHandler(this.menuSkip_Click);

            // 
            // panelButtons
            // 
            this.panelButtons.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.panelButtons.Controls.Add(this.grpPlayback);
            this.panelButtons.Controls.Add(this.grpConflictActions);
            this.panelButtons.Location = new Point(12, 318);
            this.panelButtons.Name = "panelButtons";
            this.panelButtons.Size = new Size(760, 70);
            this.panelButtons.TabIndex = 1;

            // 
            // grpPlayback
            // 
            this.grpPlayback.Controls.Add(this.btnPlayOriginal);
            this.grpPlayback.Controls.Add(this.btnPlayConflict);
            this.grpPlayback.Controls.Add(this.btnCompare);
            this.grpPlayback.Location = new Point(0, 0);
            this.grpPlayback.Name = "grpPlayback";
            this.grpPlayback.Size = new Size(340, 65);
            this.grpPlayback.TabIndex = 0;
            this.grpPlayback.TabStop = false;
            this.grpPlayback.Text = "再生・比較";

            // 
            // btnPlayOriginal
            // 
            this.btnPlayOriginal.Enabled = false;
            this.btnPlayOriginal.Location = new Point(10, 25);
            this.btnPlayOriginal.Name = "btnPlayOriginal";
            this.btnPlayOriginal.Size = new Size(100, 30);
            this.btnPlayOriginal.TabIndex = 0;
            this.btnPlayOriginal.Text = "元を再生";
            this.btnPlayOriginal.UseVisualStyleBackColor = true;
            this.btnPlayOriginal.Click += new EventHandler(this.btnPlayOriginal_Click);

            // 
            // btnPlayConflict
            // 
            this.btnPlayConflict.Enabled = false;
            this.btnPlayConflict.Location = new Point(116, 25);
            this.btnPlayConflict.Name = "btnPlayConflict";
            this.btnPlayConflict.Size = new Size(100, 30);
            this.btnPlayConflict.TabIndex = 1;
            this.btnPlayConflict.Text = "重複先を再生";
            this.btnPlayConflict.UseVisualStyleBackColor = true;
            this.btnPlayConflict.Click += new EventHandler(this.btnPlayConflict_Click);

            // 
            // btnCompare
            // 
            this.btnCompare.Enabled = false;
            this.btnCompare.Location = new Point(222, 25);
            this.btnCompare.Name = "btnCompare";
            this.btnCompare.Size = new Size(105, 30);
            this.btnCompare.TabIndex = 2;
            this.btnCompare.Text = "両方開いて比較";
            this.btnCompare.UseVisualStyleBackColor = true;
            this.btnCompare.Click += new EventHandler(this.btnCompare_Click);

            // 
            // grpConflictActions
            // 
            this.grpConflictActions.Controls.Add(this.btnOverwrite);
            this.grpConflictActions.Controls.Add(this.btnAddNumber);
            this.grpConflictActions.Controls.Add(this.btnSkip);
            this.grpConflictActions.Controls.Add(this.btnSelectAllConflicts);
            this.grpConflictActions.Location = new Point(350, 0);
            this.grpConflictActions.Name = "grpConflictActions";
            this.grpConflictActions.Size = new Size(410, 65);
            this.grpConflictActions.TabIndex = 1;
            this.grpConflictActions.TabStop = false;
            this.grpConflictActions.Text = "重複ファイルの処理（選択項目）";

            // 
            // btnOverwrite
            // 
            this.btnOverwrite.Enabled = false;
            this.btnOverwrite.Location = new Point(10, 25);
            this.btnOverwrite.Name = "btnOverwrite";
            this.btnOverwrite.Size = new Size(90, 30);
            this.btnOverwrite.TabIndex = 0;
            this.btnOverwrite.Text = "上書き";
            this.btnOverwrite.UseVisualStyleBackColor = true;
            this.btnOverwrite.Click += new EventHandler(this.btnOverwrite_Click);

            // 
            // btnAddNumber
            // 
            this.btnAddNumber.Enabled = false;
            this.btnAddNumber.Location = new Point(106, 25);
            this.btnAddNumber.Name = "btnAddNumber";
            this.btnAddNumber.Size = new Size(90, 30);
            this.btnAddNumber.TabIndex = 1;
            this.btnAddNumber.Text = "番号付け";
            this.btnAddNumber.UseVisualStyleBackColor = true;
            this.btnAddNumber.Click += new EventHandler(this.btnAddNumber_Click);

            // 
            // btnSkip
            // 
            this.btnSkip.Enabled = false;
            this.btnSkip.Location = new Point(202, 25);
            this.btnSkip.Name = "btnSkip";
            this.btnSkip.Size = new Size(90, 30);
            this.btnSkip.TabIndex = 2;
            this.btnSkip.Text = "スキップ";
            this.btnSkip.UseVisualStyleBackColor = true;
            this.btnSkip.Click += new EventHandler(this.btnSkip_Click);

            // 
            // btnSelectAllConflicts
            // 
            this.btnSelectAllConflicts.Location = new Point(298, 25);
            this.btnSelectAllConflicts.Name = "btnSelectAllConflicts";
            this.btnSelectAllConflicts.Size = new Size(100, 30);
            this.btnSelectAllConflicts.TabIndex = 3;
            this.btnSelectAllConflicts.Text = "重複を全選択";
            this.btnSelectAllConflicts.UseVisualStyleBackColor = true;
            this.btnSelectAllConflicts.Click += new EventHandler(this.btnSelectAllConflicts_Click);

            // 
            // lblStatus
            // 
            this.lblStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new Point(12, 400);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new Size(300, 15);
            this.lblStatus.TabIndex = 2;
            this.lblStatus.Text = "成功: 0  スキップ: 0  重複: 0  ファイル不在: 0  エラー: 0";

            // 
            // btnClose
            // 
            this.btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.btnClose.Location = new Point(697, 395);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new Size(75, 27);
            this.btnClose.TabIndex = 3;
            this.btnClose.Text = "閉じる";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new EventHandler(this.btnClose_Click);

            // 
            // RenameResultForm
            // 
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(784, 431);
            this.Controls.Add(this.listView);
            this.Controls.Add(this.panelButtons);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.btnClose);
            this.Icon = Properties.Resources.icon;
            this.MinimizeBox = false;
            this.MinimumSize = new Size(700, 400);
            this.Name = "RenameResultForm";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "リネーム結果";
            this.Load += new EventHandler(this.RenameResultForm_Load);
            this.contextMenu.ResumeLayout(false);
            this.panelButtons.ResumeLayout(false);
            this.grpPlayback.ResumeLayout(false);
            this.grpConflictActions.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private ListView listView;
        private ColumnHeader colTitle;
        private ColumnHeader colOriginalName;
        private ColumnHeader colNewName;
        private ColumnHeader colStatus;
        private ContextMenuStrip contextMenu;
        private ToolStripMenuItem menuPlayOriginal;
        private ToolStripMenuItem menuPlayConflict;
        private ToolStripMenuItem menuCompare;
        private ToolStripSeparator menuSeparator1;
        private ToolStripMenuItem menuOpenFolder;
        private ToolStripSeparator menuSeparator2;
        private ToolStripMenuItem menuOverwrite;
        private ToolStripMenuItem menuAddNumber;
        private ToolStripMenuItem menuSkip;
        private Panel panelButtons;
        private GroupBox grpPlayback;
        private Button btnPlayOriginal;
        private Button btnPlayConflict;
        private Button btnCompare;
        private GroupBox grpConflictActions;
        private Button btnOverwrite;
        private Button btnAddNumber;
        private Button btnSkip;
        private Button btnSelectAllConflicts;
        private Label lblStatus;
        private Button btnClose;
    }
}
