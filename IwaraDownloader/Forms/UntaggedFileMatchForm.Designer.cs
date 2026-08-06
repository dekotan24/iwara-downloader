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

            this.rbArtist = new System.Windows.Forms.RadioButton();
            this.lblArtistDesc = new System.Windows.Forms.Label();
            this.rbFilename = new System.Windows.Forms.RadioButton();
            this.lblFilenameDesc = new System.Windows.Forms.Label();
            this.rbSkip = new System.Windows.Forms.RadioButton();
            this.lblSkipDesc = new System.Windows.Forms.Label();

            this.grpArtist = new System.Windows.Forms.GroupBox();
            this.lblArtistFolder = new System.Windows.Forms.Label();
            this.txtArtistFolder = new System.Windows.Forms.TextBox();
            this.btnBrowseArtistFolder = new System.Windows.Forms.Button();
            this.lblArtistInput = new System.Windows.Forms.Label();
            this.txtArtistInput = new System.Windows.Forms.TextBox();
            this.btnResolveArtist = new System.Windows.Forms.Button();
            this.lblArtistResolved = new System.Windows.Forms.Label();
            this.btnRefetchArtist = new System.Windows.Forms.Button();
            this.lblArtistTemplate = new System.Windows.Forms.Label();
            this.txtArtistTemplate = new System.Windows.Forms.TextBox();
            this.lblArtistTemplateHint = new System.Windows.Forms.Label();

            this.grpFilename = new System.Windows.Forms.GroupBox();
            this.lblFilenameTemplate = new System.Windows.Forms.Label();
            this.txtFilenameTemplate = new System.Windows.Forms.TextBox();
            this.lblFilenameTemplateHint = new System.Windows.Forms.Label();

            this.lblStatus = new System.Windows.Forms.Label();
            this.progressBar = new System.Windows.Forms.ProgressBar();

            this.btnRun = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();

            this.SuspendLayout();

            // ラベル列の幅 (長いラベル "アーティスト URL/username:" が収まる余裕を持たせる)
            const int labelX = 10;
            const int inputX = 200;

            // lblIntro
            this.lblIntro.AutoSize = false;
            this.lblIntro.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.lblIntro.Location = new System.Drawing.Point(12, 10);
            this.lblIntro.Size = new System.Drawing.Size(680, 20);
            this.lblIntro.Name = "lblIntro";

            // rbArtist
            this.rbArtist.AutoSize = true;
            this.rbArtist.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
            this.rbArtist.Location = new System.Drawing.Point(12, 36);
            this.rbArtist.Name = "rbArtist";
            this.rbArtist.Checked = true;
            this.rbArtist.CheckedChanged += new System.EventHandler(this.RadioChanged);

            this.lblArtistDesc.AutoSize = false;
            this.lblArtistDesc.ForeColor = System.Drawing.Color.DimGray;
            this.lblArtistDesc.Font = new System.Drawing.Font("Segoe UI", 8.5F);
            this.lblArtistDesc.Location = new System.Drawing.Point(30, 58);
            this.lblArtistDesc.Size = new System.Drawing.Size(660, 16);
            this.lblArtistDesc.Name = "lblArtistDesc";

            // rbFilename
            this.rbFilename.AutoSize = true;
            this.rbFilename.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
            this.rbFilename.Location = new System.Drawing.Point(12, 266);
            this.rbFilename.Name = "rbFilename";
            this.rbFilename.CheckedChanged += new System.EventHandler(this.RadioChanged);

            this.lblFilenameDesc.AutoSize = false;
            this.lblFilenameDesc.ForeColor = System.Drawing.Color.DimGray;
            this.lblFilenameDesc.Font = new System.Drawing.Font("Segoe UI", 8.5F);
            this.lblFilenameDesc.Location = new System.Drawing.Point(30, 288);
            this.lblFilenameDesc.Size = new System.Drawing.Size(660, 16);
            this.lblFilenameDesc.Name = "lblFilenameDesc";

            // rbSkip
            this.rbSkip.AutoSize = true;
            this.rbSkip.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
            this.rbSkip.Location = new System.Drawing.Point(12, 406);
            this.rbSkip.Name = "rbSkip";
            this.rbSkip.CheckedChanged += new System.EventHandler(this.RadioChanged);

            this.lblSkipDesc.AutoSize = false;
            this.lblSkipDesc.ForeColor = System.Drawing.Color.DimGray;
            this.lblSkipDesc.Font = new System.Drawing.Font("Segoe UI", 8.5F);
            this.lblSkipDesc.Location = new System.Drawing.Point(30, 428);
            this.lblSkipDesc.Size = new System.Drawing.Size(660, 16);
            this.lblSkipDesc.Name = "lblSkipDesc";

            // grpArtist
            this.grpArtist.Controls.Add(this.lblArtistFolder);
            this.grpArtist.Controls.Add(this.txtArtistFolder);
            this.grpArtist.Controls.Add(this.btnBrowseArtistFolder);
            this.grpArtist.Controls.Add(this.lblArtistInput);
            this.grpArtist.Controls.Add(this.txtArtistInput);
            this.grpArtist.Controls.Add(this.btnResolveArtist);
            this.grpArtist.Controls.Add(this.lblArtistResolved);
            this.grpArtist.Controls.Add(this.btnRefetchArtist);
            this.grpArtist.Controls.Add(this.lblArtistTemplate);
            this.grpArtist.Controls.Add(this.txtArtistTemplate);
            this.grpArtist.Controls.Add(this.lblArtistTemplateHint);
            this.grpArtist.Location = new System.Drawing.Point(30, 78);
            this.grpArtist.Size = new System.Drawing.Size(660, 178);
            this.grpArtist.Name = "grpArtist";
            this.grpArtist.TabStop = false;

            this.lblArtistFolder.AutoSize = true;
            this.lblArtistFolder.Location = new System.Drawing.Point(labelX, 25);
            this.lblArtistFolder.Name = "lblArtistFolder";

            this.txtArtistFolder.Location = new System.Drawing.Point(inputX, 22);
            this.txtArtistFolder.Size = new System.Drawing.Size(360, 23);
            this.txtArtistFolder.Name = "txtArtistFolder";

            this.btnBrowseArtistFolder.Location = new System.Drawing.Point(568, 21);
            this.btnBrowseArtistFolder.Size = new System.Drawing.Size(80, 25);
            this.btnBrowseArtistFolder.Name = "btnBrowseArtistFolder";
            this.btnBrowseArtistFolder.UseVisualStyleBackColor = true;
            this.btnBrowseArtistFolder.Click += new System.EventHandler(this.btnBrowseArtistFolder_Click);

            this.lblArtistInput.AutoSize = true;
            this.lblArtistInput.Location = new System.Drawing.Point(labelX, 58);
            this.lblArtistInput.Name = "lblArtistInput";

            this.txtArtistInput.Location = new System.Drawing.Point(inputX, 55);
            this.txtArtistInput.Size = new System.Drawing.Size(260, 23);
            this.txtArtistInput.Name = "txtArtistInput";

            this.btnResolveArtist.Location = new System.Drawing.Point(468, 54);
            this.btnResolveArtist.Size = new System.Drawing.Size(80, 25);
            this.btnResolveArtist.Name = "btnResolveArtist";
            this.btnResolveArtist.UseVisualStyleBackColor = true;
            this.btnResolveArtist.Click += new System.EventHandler(this.btnResolveArtist_Click);

            this.btnRefetchArtist.Location = new System.Drawing.Point(556, 54);
            this.btnRefetchArtist.Size = new System.Drawing.Size(94, 25);
            this.btnRefetchArtist.Name = "btnRefetchArtist";
            this.btnRefetchArtist.UseVisualStyleBackColor = true;
            this.btnRefetchArtist.Visible = false;
            this.btnRefetchArtist.Click += new System.EventHandler(this.btnRefetchArtist_Click);

            this.lblArtistResolved.AutoSize = false;
            this.lblArtistResolved.ForeColor = System.Drawing.Color.DimGray;
            this.lblArtistResolved.Location = new System.Drawing.Point(inputX, 82);
            this.lblArtistResolved.Size = new System.Drawing.Size(450, 20);
            this.lblArtistResolved.Name = "lblArtistResolved";

            this.lblArtistTemplate.AutoSize = true;
            this.lblArtistTemplate.Location = new System.Drawing.Point(labelX, 115);
            this.lblArtistTemplate.Name = "lblArtistTemplate";

            this.txtArtistTemplate.Location = new System.Drawing.Point(inputX, 112);
            this.txtArtistTemplate.Size = new System.Drawing.Size(300, 23);
            this.txtArtistTemplate.Font = new System.Drawing.Font("Consolas", 9F);
            this.txtArtistTemplate.Name = "txtArtistTemplate";

            this.lblArtistTemplateHint.AutoSize = false;
            this.lblArtistTemplateHint.ForeColor = System.Drawing.Color.DimGray;
            this.lblArtistTemplateHint.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.lblArtistTemplateHint.Location = new System.Drawing.Point(labelX, 140);
            this.lblArtistTemplateHint.Size = new System.Drawing.Size(640, 32);
            this.lblArtistTemplateHint.Name = "lblArtistTemplateHint";

            // grpFilename
            this.grpFilename.Controls.Add(this.lblFilenameTemplate);
            this.grpFilename.Controls.Add(this.txtFilenameTemplate);
            this.grpFilename.Controls.Add(this.lblFilenameTemplateHint);
            this.grpFilename.Location = new System.Drawing.Point(30, 308);
            this.grpFilename.Size = new System.Drawing.Size(660, 88);
            this.grpFilename.Name = "grpFilename";
            this.grpFilename.TabStop = false;

            this.lblFilenameTemplate.AutoSize = true;
            this.lblFilenameTemplate.Location = new System.Drawing.Point(labelX, 25);
            this.lblFilenameTemplate.Name = "lblFilenameTemplate";

            this.txtFilenameTemplate.Location = new System.Drawing.Point(inputX, 22);
            this.txtFilenameTemplate.Size = new System.Drawing.Size(300, 23);
            this.txtFilenameTemplate.Font = new System.Drawing.Font("Consolas", 9F);
            this.txtFilenameTemplate.Name = "txtFilenameTemplate";

            this.lblFilenameTemplateHint.AutoSize = false;
            this.lblFilenameTemplateHint.ForeColor = System.Drawing.Color.DimGray;
            this.lblFilenameTemplateHint.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.lblFilenameTemplateHint.Location = new System.Drawing.Point(labelX, 50);
            this.lblFilenameTemplateHint.Size = new System.Drawing.Size(640, 32);
            this.lblFilenameTemplateHint.Name = "lblFilenameTemplateHint";

            // lblStatus
            this.lblStatus.AutoSize = false;
            this.lblStatus.Location = new System.Drawing.Point(30, 454);
            this.lblStatus.Size = new System.Drawing.Size(660, 20);
            this.lblStatus.Name = "lblStatus";

            // progressBar
            this.progressBar.Location = new System.Drawing.Point(30, 478);
            this.progressBar.Size = new System.Drawing.Size(660, 18);
            this.progressBar.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.progressBar.Visible = false;
            this.progressBar.Name = "progressBar";

            // btnRun
            this.btnRun.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
            this.btnRun.Location = new System.Drawing.Point(503, 514);
            this.btnRun.Size = new System.Drawing.Size(100, 30);
            this.btnRun.Name = "btnRun";
            this.btnRun.UseVisualStyleBackColor = true;
            this.btnRun.Click += new System.EventHandler(this.btnRun_Click);

            // btnCancel
            this.btnCancel.Location = new System.Drawing.Point(613, 514);
            this.btnCancel.Size = new System.Drawing.Size(95, 30);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);

            // UntaggedFileMatchForm
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(720, 560);
            this.Controls.Add(this.lblIntro);
            this.Controls.Add(this.rbArtist);
            this.Controls.Add(this.lblArtistDesc);
            this.Controls.Add(this.grpArtist);
            this.Controls.Add(this.rbFilename);
            this.Controls.Add(this.lblFilenameDesc);
            this.Controls.Add(this.grpFilename);
            this.Controls.Add(this.rbSkip);
            this.Controls.Add(this.lblSkipDesc);
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

            this.grpArtist.ResumeLayout(false);
            this.grpArtist.PerformLayout();
            this.grpFilename.ResumeLayout(false);
            this.grpFilename.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Label lblIntro;
        private System.Windows.Forms.RadioButton rbArtist;
        private System.Windows.Forms.Label lblArtistDesc;
        private System.Windows.Forms.RadioButton rbFilename;
        private System.Windows.Forms.Label lblFilenameDesc;
        private System.Windows.Forms.RadioButton rbSkip;
        private System.Windows.Forms.Label lblSkipDesc;

        private System.Windows.Forms.GroupBox grpArtist;
        private System.Windows.Forms.Label lblArtistFolder;
        private System.Windows.Forms.TextBox txtArtistFolder;
        private System.Windows.Forms.Button btnBrowseArtistFolder;
        private System.Windows.Forms.Label lblArtistInput;
        private System.Windows.Forms.TextBox txtArtistInput;
        private System.Windows.Forms.Button btnResolveArtist;
        private System.Windows.Forms.Label lblArtistResolved;
        private System.Windows.Forms.Button btnRefetchArtist;
        private System.Windows.Forms.Label lblArtistTemplate;
        private System.Windows.Forms.TextBox txtArtistTemplate;
        private System.Windows.Forms.Label lblArtistTemplateHint;

        private System.Windows.Forms.GroupBox grpFilename;
        private System.Windows.Forms.Label lblFilenameTemplate;
        private System.Windows.Forms.TextBox txtFilenameTemplate;
        private System.Windows.Forms.Label lblFilenameTemplateHint;

        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.ProgressBar progressBar;

        private System.Windows.Forms.Button btnRun;
        private System.Windows.Forms.Button btnCancel;
    }
}
