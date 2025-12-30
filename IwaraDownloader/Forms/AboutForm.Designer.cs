namespace IwaraDownloader.Forms
{
    partial class AboutForm
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
            this.lblTitle = new Label();
            this.lblVersion = new Label();
            this.lblCopyright = new Label();
            this.lblDescription = new Label();
            this.linkGitHub = new LinkLabel();
            this.btnOK = new Button();
            this.btnCheckUpdate = new Button();
            this.lblUpdateStatus = new Label();
            this.pictureBoxIcon = new PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxIcon)).BeginInit();
            this.SuspendLayout();

            // 
            // pictureBoxIcon
            // 
            this.pictureBoxIcon.Location = new Point(20, 20);
            this.pictureBoxIcon.Name = "pictureBoxIcon";
            this.pictureBoxIcon.Size = new Size(64, 64);
            this.pictureBoxIcon.SizeMode = PictureBoxSizeMode.Zoom;
            this.pictureBoxIcon.TabIndex = 0;
            this.pictureBoxIcon.TabStop = false;
            try
            {
                this.pictureBoxIcon.Image = Properties.Resources.icon.ToBitmap();
            }
            catch { }

            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new Font("Yu Gothic UI", 16F, FontStyle.Bold);
            this.lblTitle.Location = new Point(100, 20);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new Size(180, 30);
            this.lblTitle.TabIndex = 1;
            this.lblTitle.Text = "IwaraDownloader";

            // 
            // lblVersion
            // 
            this.lblVersion.AutoSize = true;
            this.lblVersion.Font = new Font("Yu Gothic UI", 10F);
            this.lblVersion.Location = new Point(100, 55);
            this.lblVersion.Name = "lblVersion";
            this.lblVersion.Size = new Size(100, 19);
            this.lblVersion.TabIndex = 2;
            this.lblVersion.Text = "Version 1.0.0";

            // 
            // lblDescription
            // 
            this.lblDescription.Location = new Point(20, 100);
            this.lblDescription.Name = "lblDescription";
            this.lblDescription.Size = new Size(350, 40);
            this.lblDescription.TabIndex = 3;
            this.lblDescription.Text = "iwara.tv から動画をダウンロードするためのデスクトップアプリケーションです。";

            // 
            // lblCopyright
            // 
            this.lblCopyright.AutoSize = true;
            this.lblCopyright.ForeColor = Color.Gray;
            this.lblCopyright.Location = new Point(20, 145);
            this.lblCopyright.Name = "lblCopyright";
            this.lblCopyright.Size = new Size(150, 15);
            this.lblCopyright.TabIndex = 4;
            this.lblCopyright.Text = "© 2024 IwaraDownloader";

            // 
            // linkGitHub
            // 
            this.linkGitHub.AutoSize = true;
            this.linkGitHub.Location = new Point(20, 170);
            this.linkGitHub.Name = "linkGitHub";
            this.linkGitHub.Size = new Size(250, 15);
            this.linkGitHub.TabIndex = 5;
            this.linkGitHub.TabStop = true;
            this.linkGitHub.Text = "https://github.com/dekotan24/iwara-downloader";
            this.linkGitHub.LinkClicked += new LinkLabelLinkClickedEventHandler(this.linkGitHub_LinkClicked);

            // 
            // btnCheckUpdate
            // 
            this.btnCheckUpdate.Location = new Point(20, 205);
            this.btnCheckUpdate.Name = "btnCheckUpdate";
            this.btnCheckUpdate.Size = new Size(100, 28);
            this.btnCheckUpdate.TabIndex = 6;
            this.btnCheckUpdate.Text = "更新を確認";
            this.btnCheckUpdate.UseVisualStyleBackColor = true;
            this.btnCheckUpdate.Click += new EventHandler(this.btnCheckUpdate_Click);

            // 
            // lblUpdateStatus
            // 
            this.lblUpdateStatus.AutoSize = true;
            this.lblUpdateStatus.ForeColor = Color.Gray;
            this.lblUpdateStatus.Location = new Point(130, 212);
            this.lblUpdateStatus.Name = "lblUpdateStatus";
            this.lblUpdateStatus.Size = new Size(0, 15);
            this.lblUpdateStatus.TabIndex = 7;

            // 
            // btnOK
            // 
            this.btnOK.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.btnOK.DialogResult = DialogResult.OK;
            this.btnOK.Location = new Point(295, 205);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new Size(80, 28);
            this.btnOK.TabIndex = 8;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new EventHandler(this.btnOK_Click);

            // 
            // AboutForm
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(390, 250);
            this.Controls.Add(this.pictureBoxIcon);
            this.Controls.Add(this.lblTitle);
            this.Controls.Add(this.lblVersion);
            this.Controls.Add(this.lblDescription);
            this.Controls.Add(this.lblCopyright);
            this.Controls.Add(this.linkGitHub);
            this.Controls.Add(this.btnCheckUpdate);
            this.Controls.Add(this.lblUpdateStatus);
            this.Controls.Add(this.btnOK);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AboutForm";
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "バージョン情報";
            this.Load += new EventHandler(this.AboutForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxIcon)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private PictureBox pictureBoxIcon;
        private Label lblTitle;
        private Label lblVersion;
        private Label lblDescription;
        private Label lblCopyright;
        private LinkLabel linkGitHub;
        private Button btnCheckUpdate;
        private Label lblUpdateStatus;
        private Button btnOK;
    }
}
