namespace IwaraDownloader.Forms
{
    partial class UntaggedFileMatchForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.lblIntro = new System.Windows.Forms.Label();
            this.grpFiles = new System.Windows.Forms.GroupBox();
            this.lblFileList = new System.Windows.Forms.Label();
            this.dgvFiles = new System.Windows.Forms.DataGridView();
            this.colSelected = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colFileName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colRelativePath = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colFullPath = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colAssignedMethod = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colAssignedDetail = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.btnSelectAllFiles = new System.Windows.Forms.Button();
            this.btnClearFileSelection = new System.Windows.Forms.Button();
            this.lblScopeFolder = new System.Windows.Forms.Label();
            this.txtScopeFolder = new System.Windows.Forms.TextBox();
            this.btnBrowseScopeFolder = new System.Windows.Forms.Button();
            this.btnSelectScope = new System.Windows.Forms.Button();
            this.lblSelectionStatus = new System.Windows.Forms.Label();
            this.grpRule = new System.Windows.Forms.GroupBox();
            this.lblMethod = new System.Windows.Forms.Label();
            this.cmbMethod = new System.Windows.Forms.ComboBox();
            this.lblRuleHelp = new System.Windows.Forms.Label();
            this.grpArtist = new System.Windows.Forms.GroupBox();
            this.lblArtistInput = new System.Windows.Forms.Label();
            this.txtArtistInput = new System.Windows.Forms.TextBox();
            this.btnResolveArtist = new System.Windows.Forms.Button();
            this.btnRefetchArtist = new System.Windows.Forms.Button();
            this.lblArtistResolved = new System.Windows.Forms.Label();
            this.lblArtistTemplate = new System.Windows.Forms.Label();
            this.txtArtistTemplate = new System.Windows.Forms.TextBox();
            this.lblArtistTemplateHint = new System.Windows.Forms.Label();
            this.grpFilename = new System.Windows.Forms.GroupBox();
            this.lblFilenameTemplate = new System.Windows.Forms.Label();
            this.txtFilenameTemplate = new System.Windows.Forms.TextBox();
            this.lblFilenameTemplateHint = new System.Windows.Forms.Label();
            this.lblRuleSummary = new System.Windows.Forms.Label();
            this.btnApplyRule = new System.Windows.Forms.Button();
            this.lblStatus = new System.Windows.Forms.Label();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.btnRun = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.grpFiles.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvFiles)).BeginInit();
            this.grpRule.SuspendLayout();
            this.grpArtist.SuspendLayout();
            this.grpFilename.SuspendLayout();
            this.SuspendLayout();

            // lblIntro
            this.lblIntro.AutoSize = false;
            this.lblIntro.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.lblIntro.Location = new System.Drawing.Point(12, 10);
            this.lblIntro.Name = "lblIntro";
            this.lblIntro.Size = new System.Drawing.Size(1076, 36);

            // grpFiles
            this.grpFiles.Controls.Add(this.lblFileList);
            this.grpFiles.Controls.Add(this.dgvFiles);
            this.grpFiles.Controls.Add(this.btnSelectAllFiles);
            this.grpFiles.Controls.Add(this.btnClearFileSelection);
            this.grpFiles.Controls.Add(this.lblScopeFolder);
            this.grpFiles.Controls.Add(this.txtScopeFolder);
            this.grpFiles.Controls.Add(this.btnBrowseScopeFolder);
            this.grpFiles.Controls.Add(this.btnSelectScope);
            this.grpFiles.Location = new System.Drawing.Point(12, 50);
            this.grpFiles.Name = "grpFiles";
            this.grpFiles.Size = new System.Drawing.Size(1076, 304);
            this.grpFiles.TabStop = false;

            // lblFileList
            this.lblFileList.AutoSize = false;
            this.lblFileList.Location = new System.Drawing.Point(10, 21);
            this.lblFileList.Name = "lblFileList";
            this.lblFileList.Size = new System.Drawing.Size(1050, 20);

            // dgvFiles
            this.dgvFiles.AllowUserToAddRows = false;
            this.dgvFiles.AllowUserToDeleteRows = false;
            this.dgvFiles.AllowUserToResizeRows = false;
            this.dgvFiles.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dgvFiles.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.dgvFiles.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvFiles.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
                this.colSelected,
                this.colFileName,
                this.colRelativePath,
                this.colFullPath,
                this.colAssignedMethod,
                this.colAssignedDetail});
            this.dgvFiles.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.dgvFiles.Location = new System.Drawing.Point(10, 45);
            this.dgvFiles.MultiSelect = true;
            this.dgvFiles.Name = "dgvFiles";
            this.dgvFiles.RowHeadersVisible = false;
            this.dgvFiles.RowTemplate.Height = 24;
            this.dgvFiles.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvFiles.Size = new System.Drawing.Size(1056, 205);
            this.dgvFiles.TabIndex = 0;
            this.dgvFiles.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvFiles_CellValueChanged);
            this.dgvFiles.CurrentCellDirtyStateChanged += new System.EventHandler(this.dgvFiles_CurrentCellDirtyStateChanged);

            // colSelected
            this.colSelected.HeaderText = "対象";
            this.colSelected.Name = "colSelected";
            this.colSelected.Width = 45;

            // colFileName
            this.colFileName.HeaderText = "ファイル名";
            this.colFileName.Name = "colFileName";
            this.colFileName.ReadOnly = true;
            this.colFileName.Width = 220;

            // colRelativePath
            this.colRelativePath.HeaderText = "相対パス";
            this.colRelativePath.Name = "colRelativePath";
            this.colRelativePath.ReadOnly = true;
            this.colRelativePath.Width = 280;

            // colFullPath
            this.colFullPath.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.colFullPath.HeaderText = "フルパス";
            this.colFullPath.MinimumWidth = 250;
            this.colFullPath.Name = "colFullPath";
            this.colFullPath.ReadOnly = true;

            // colAssignedMethod
            this.colAssignedMethod.HeaderText = "割り当て方法";
            this.colAssignedMethod.Name = "colAssignedMethod";
            this.colAssignedMethod.ReadOnly = true;
            this.colAssignedMethod.Width = 120;

            // colAssignedDetail
            this.colAssignedDetail.HeaderText = "設定";
            this.colAssignedDetail.Name = "colAssignedDetail";
            this.colAssignedDetail.ReadOnly = true;
            this.colAssignedDetail.Width = 180;

            // btnSelectAllFiles
            this.btnSelectAllFiles.Location = new System.Drawing.Point(10, 260);
            this.btnSelectAllFiles.Name = "btnSelectAllFiles";
            this.btnSelectAllFiles.Size = new System.Drawing.Size(90, 28);
            this.btnSelectAllFiles.UseVisualStyleBackColor = true;
            this.btnSelectAllFiles.Click += new System.EventHandler(this.btnSelectAllFiles_Click);

            // btnClearFileSelection
            this.btnClearFileSelection.Location = new System.Drawing.Point(106, 260);
            this.btnClearFileSelection.Name = "btnClearFileSelection";
            this.btnClearFileSelection.Size = new System.Drawing.Size(90, 28);
            this.btnClearFileSelection.UseVisualStyleBackColor = true;
            this.btnClearFileSelection.Click += new System.EventHandler(this.btnClearFileSelection_Click);

            // lblScopeFolder
            this.lblScopeFolder.AutoSize = true;
            this.lblScopeFolder.Location = new System.Drawing.Point(215, 266);
            this.lblScopeFolder.Name = "lblScopeFolder";

            // txtScopeFolder
            this.txtScopeFolder.Location = new System.Drawing.Point(340, 263);
            this.txtScopeFolder.Name = "txtScopeFolder";
            this.txtScopeFolder.Size = new System.Drawing.Size(510, 23);

            // btnBrowseScopeFolder
            this.btnBrowseScopeFolder.Location = new System.Drawing.Point(856, 261);
            this.btnBrowseScopeFolder.Name = "btnBrowseScopeFolder";
            this.btnBrowseScopeFolder.Size = new System.Drawing.Size(80, 27);
            this.btnBrowseScopeFolder.UseVisualStyleBackColor = true;
            this.btnBrowseScopeFolder.Click += new System.EventHandler(this.btnBrowseScopeFolder_Click);

            // btnSelectScope
            this.btnSelectScope.Location = new System.Drawing.Point(942, 261);
            this.btnSelectScope.Name = "btnSelectScope";
            this.btnSelectScope.Size = new System.Drawing.Size(124, 27);
            this.btnSelectScope.UseVisualStyleBackColor = true;
            this.btnSelectScope.Click += new System.EventHandler(this.btnSelectScope_Click);

            // lblSelectionStatus
            this.lblSelectionStatus.AutoSize = false;
            this.lblSelectionStatus.Location = new System.Drawing.Point(12, 360);
            this.lblSelectionStatus.Name = "lblSelectionStatus";
            this.lblSelectionStatus.Size = new System.Drawing.Size(1076, 20);

            // grpRule
            this.grpRule.Controls.Add(this.lblMethod);
            this.grpRule.Controls.Add(this.cmbMethod);
            this.grpRule.Controls.Add(this.lblRuleHelp);
            this.grpRule.Controls.Add(this.grpArtist);
            this.grpRule.Controls.Add(this.grpFilename);
            this.grpRule.Controls.Add(this.lblRuleSummary);
            this.grpRule.Controls.Add(this.btnApplyRule);
            this.grpRule.Location = new System.Drawing.Point(12, 385);
            this.grpRule.Name = "grpRule";
            this.grpRule.Size = new System.Drawing.Size(1076, 258);
            this.grpRule.TabStop = false;

            // lblMethod
            this.lblMethod.AutoSize = true;
            this.lblMethod.Location = new System.Drawing.Point(10, 25);
            this.lblMethod.Name = "lblMethod";

            // cmbMethod
            this.cmbMethod.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbMethod.FormattingEnabled = true;
            this.cmbMethod.Items.AddRange(new object[] {
                "アーティストフォルダ選択検索",
                "ファイル名検索（DB全体）",
                "スキップ"});
            this.cmbMethod.Location = new System.Drawing.Point(170, 22);
            this.cmbMethod.Name = "cmbMethod";
            this.cmbMethod.Size = new System.Drawing.Size(280, 23);
            this.cmbMethod.SelectedIndexChanged += new System.EventHandler(this.cmbMethod_SelectedIndexChanged);

            // lblRuleHelp
            this.lblRuleHelp.AutoSize = false;
            this.lblRuleHelp.ForeColor = System.Drawing.Color.DimGray;
            this.lblRuleHelp.Location = new System.Drawing.Point(470, 22);
            this.lblRuleHelp.Name = "lblRuleHelp";
            this.lblRuleHelp.Size = new System.Drawing.Size(580, 38);

            // grpArtist
            this.grpArtist.Controls.Add(this.lblArtistInput);
            this.grpArtist.Controls.Add(this.txtArtistInput);
            this.grpArtist.Controls.Add(this.btnResolveArtist);
            this.grpArtist.Controls.Add(this.btnRefetchArtist);
            this.grpArtist.Controls.Add(this.lblArtistResolved);
            this.grpArtist.Controls.Add(this.lblArtistTemplate);
            this.grpArtist.Controls.Add(this.txtArtistTemplate);
            this.grpArtist.Controls.Add(this.lblArtistTemplateHint);
            this.grpArtist.Location = new System.Drawing.Point(10, 62);
            this.grpArtist.Name = "grpArtist";
            this.grpArtist.Size = new System.Drawing.Size(1056, 126);
            this.grpArtist.TabStop = false;

            // lblArtistInput
            this.lblArtistInput.AutoSize = true;
            this.lblArtistInput.Location = new System.Drawing.Point(10, 25);
            this.lblArtistInput.Name = "lblArtistInput";

            // txtArtistInput
            this.txtArtistInput.Location = new System.Drawing.Point(170, 22);
            this.txtArtistInput.Name = "txtArtistInput";
            this.txtArtistInput.Size = new System.Drawing.Size(320, 23);
            this.txtArtistInput.TextChanged += new System.EventHandler(this.txtArtistInput_TextChanged);

            // btnResolveArtist
            this.btnResolveArtist.Location = new System.Drawing.Point(500, 21);
            this.btnResolveArtist.Name = "btnResolveArtist";
            this.btnResolveArtist.Size = new System.Drawing.Size(80, 25);
            this.btnResolveArtist.UseVisualStyleBackColor = true;
            this.btnResolveArtist.Click += new System.EventHandler(this.btnResolveArtist_Click);

            // btnRefetchArtist
            this.btnRefetchArtist.Location = new System.Drawing.Point(588, 21);
            this.btnRefetchArtist.Name = "btnRefetchArtist";
            this.btnRefetchArtist.Size = new System.Drawing.Size(110, 25);
            this.btnRefetchArtist.UseVisualStyleBackColor = true;
            this.btnRefetchArtist.Visible = false;
            this.btnRefetchArtist.Click += new System.EventHandler(this.btnRefetchArtist_Click);

            // lblArtistResolved
            this.lblArtistResolved.AutoSize = false;
            this.lblArtistResolved.ForeColor = System.Drawing.Color.DimGray;
            this.lblArtistResolved.Location = new System.Drawing.Point(170, 52);
            this.lblArtistResolved.Name = "lblArtistResolved";
            this.lblArtistResolved.Size = new System.Drawing.Size(850, 20);

            // lblArtistTemplate
            this.lblArtistTemplate.AutoSize = true;
            this.lblArtistTemplate.Location = new System.Drawing.Point(10, 87);
            this.lblArtistTemplate.Name = "lblArtistTemplate";

            // txtArtistTemplate
            this.txtArtistTemplate.Font = new System.Drawing.Font("Consolas", 9F);
            this.txtArtistTemplate.Location = new System.Drawing.Point(170, 84);
            this.txtArtistTemplate.Name = "txtArtistTemplate";
            this.txtArtistTemplate.Size = new System.Drawing.Size(350, 22);

            // lblArtistTemplateHint
            this.lblArtistTemplateHint.AutoSize = false;
            this.lblArtistTemplateHint.ForeColor = System.Drawing.Color.DimGray;
            this.lblArtistTemplateHint.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.lblArtistTemplateHint.Location = new System.Drawing.Point(530, 82);
            this.lblArtistTemplateHint.Name = "lblArtistTemplateHint";
            this.lblArtistTemplateHint.Size = new System.Drawing.Size(500, 36);

            // grpFilename
            this.grpFilename.Controls.Add(this.lblFilenameTemplate);
            this.grpFilename.Controls.Add(this.txtFilenameTemplate);
            this.grpFilename.Controls.Add(this.lblFilenameTemplateHint);
            this.grpFilename.Location = new System.Drawing.Point(10, 62);
            this.grpFilename.Name = "grpFilename";
            this.grpFilename.Size = new System.Drawing.Size(1056, 126);
            this.grpFilename.TabStop = false;

            // lblFilenameTemplate
            this.lblFilenameTemplate.AutoSize = true;
            this.lblFilenameTemplate.Location = new System.Drawing.Point(10, 28);
            this.lblFilenameTemplate.Name = "lblFilenameTemplate";

            // txtFilenameTemplate
            this.txtFilenameTemplate.Font = new System.Drawing.Font("Consolas", 9F);
            this.txtFilenameTemplate.Location = new System.Drawing.Point(170, 25);
            this.txtFilenameTemplate.Name = "txtFilenameTemplate";
            this.txtFilenameTemplate.Size = new System.Drawing.Size(450, 22);

            // lblFilenameTemplateHint
            this.lblFilenameTemplateHint.AutoSize = false;
            this.lblFilenameTemplateHint.ForeColor = System.Drawing.Color.DimGray;
            this.lblFilenameTemplateHint.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.lblFilenameTemplateHint.Location = new System.Drawing.Point(170, 58);
            this.lblFilenameTemplateHint.Name = "lblFilenameTemplateHint";
            this.lblFilenameTemplateHint.Size = new System.Drawing.Size(850, 45);

            // lblRuleSummary
            this.lblRuleSummary.AutoSize = false;
            this.lblRuleSummary.ForeColor = System.Drawing.Color.DimGray;
            this.lblRuleSummary.Location = new System.Drawing.Point(10, 205);
            this.lblRuleSummary.Name = "lblRuleSummary";
            this.lblRuleSummary.Size = new System.Drawing.Size(730, 30);

            // btnApplyRule
            this.btnApplyRule.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnApplyRule.Location = new System.Drawing.Point(850, 201);
            this.btnApplyRule.Name = "btnApplyRule";
            this.btnApplyRule.Size = new System.Drawing.Size(200, 32);
            this.btnApplyRule.UseVisualStyleBackColor = true;
            this.btnApplyRule.Click += new System.EventHandler(this.btnApplyRule_Click);

            // lblStatus
            this.lblStatus.AutoSize = false;
            this.lblStatus.Location = new System.Drawing.Point(12, 650);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(1076, 20);

            // progressBar
            this.progressBar.Location = new System.Drawing.Point(12, 675);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(1076, 18);
            this.progressBar.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.progressBar.Visible = false;

            // btnRun
            this.btnRun.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
            this.btnRun.Location = new System.Drawing.Point(840, 710);
            this.btnRun.Name = "btnRun";
            this.btnRun.Size = new System.Drawing.Size(120, 30);
            this.btnRun.UseVisualStyleBackColor = true;
            this.btnRun.Click += new System.EventHandler(this.btnRun_Click);

            // btnCancel
            this.btnCancel.Location = new System.Drawing.Point(970, 710);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(118, 30);
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);

            // UntaggedFileMatchForm
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1100, 755);
            this.Controls.Add(this.lblIntro);
            this.Controls.Add(this.grpFiles);
            this.Controls.Add(this.lblSelectionStatus);
            this.Controls.Add(this.grpRule);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.btnRun);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "UntaggedFileMatchForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.UntaggedFileMatchForm_FormClosing);
            this.grpFilename.ResumeLayout(false);
            this.grpFilename.PerformLayout();
            this.grpArtist.ResumeLayout(false);
            this.grpArtist.PerformLayout();
            this.grpRule.ResumeLayout(false);
            this.grpRule.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvFiles)).EndInit();
            this.grpFiles.ResumeLayout(false);
            this.grpFiles.PerformLayout();
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Label lblIntro;
        private System.Windows.Forms.GroupBox grpFiles;
        private System.Windows.Forms.Label lblFileList;
        private System.Windows.Forms.DataGridView dgvFiles;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colSelected;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFileName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colRelativePath;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFullPath;
        private System.Windows.Forms.DataGridViewTextBoxColumn colAssignedMethod;
        private System.Windows.Forms.DataGridViewTextBoxColumn colAssignedDetail;
        private System.Windows.Forms.Button btnSelectAllFiles;
        private System.Windows.Forms.Button btnClearFileSelection;
        private System.Windows.Forms.Label lblScopeFolder;
        private System.Windows.Forms.TextBox txtScopeFolder;
        private System.Windows.Forms.Button btnBrowseScopeFolder;
        private System.Windows.Forms.Button btnSelectScope;
        private System.Windows.Forms.Label lblSelectionStatus;
        private System.Windows.Forms.GroupBox grpRule;
        private System.Windows.Forms.Label lblMethod;
        private System.Windows.Forms.ComboBox cmbMethod;
        private System.Windows.Forms.Label lblRuleHelp;
        private System.Windows.Forms.GroupBox grpArtist;
        private System.Windows.Forms.Label lblArtistInput;
        private System.Windows.Forms.TextBox txtArtistInput;
        private System.Windows.Forms.Button btnResolveArtist;
        private System.Windows.Forms.Button btnRefetchArtist;
        private System.Windows.Forms.Label lblArtistResolved;
        private System.Windows.Forms.Label lblArtistTemplate;
        private System.Windows.Forms.TextBox txtArtistTemplate;
        private System.Windows.Forms.Label lblArtistTemplateHint;
        private System.Windows.Forms.GroupBox grpFilename;
        private System.Windows.Forms.Label lblFilenameTemplate;
        private System.Windows.Forms.TextBox txtFilenameTemplate;
        private System.Windows.Forms.Label lblFilenameTemplateHint;
        private System.Windows.Forms.Label lblRuleSummary;
        private System.Windows.Forms.Button btnApplyRule;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Button btnRun;
        private System.Windows.Forms.Button btnCancel;
    }
}
