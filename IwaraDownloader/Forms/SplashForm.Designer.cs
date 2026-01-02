namespace IwaraDownloader.Forms
{
    partial class SplashForm
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
            this.panelMain = new Panel();
            this.lblTitle = new Label();
            this.lblVersion = new Label();
            this.progressBar = new ProgressBar();
            this.lblStatus = new Label();
            this.pictureIcon = new PictureBox();
            this.lblSubtitle = new Label();
            this.panelMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureIcon)).BeginInit();
            this.SuspendLayout();

            // 
            // panelMain
            // 
            this.panelMain.BackColor = Color.Transparent;
            this.panelMain.Controls.Add(this.pictureIcon);
            this.panelMain.Controls.Add(this.lblTitle);
            this.panelMain.Controls.Add(this.lblSubtitle);
            this.panelMain.Controls.Add(this.lblVersion);
            this.panelMain.Controls.Add(this.progressBar);
            this.panelMain.Controls.Add(this.lblStatus);
            this.panelMain.Dock = DockStyle.Fill;
            this.panelMain.Location = new Point(0, 0);
            this.panelMain.Name = "panelMain";
            this.panelMain.Size = new Size(400, 200);
            this.panelMain.TabIndex = 0;

            // 
            // pictureIcon
            // 
            this.pictureIcon.Image = Properties.Resources.icon.ToBitmap();
            this.pictureIcon.Location = new Point(30, 30);
            this.pictureIcon.Name = "pictureIcon";
            this.pictureIcon.Size = new Size(56, 56);
            this.pictureIcon.SizeMode = PictureBoxSizeMode.Zoom;
            this.pictureIcon.TabIndex = 0;
            this.pictureIcon.TabStop = false;
            this.pictureIcon.BackColor = Color.Transparent;

            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.BackColor = Color.Transparent;
            this.lblTitle.Font = new Font("Segoe UI", 22F, FontStyle.Bold, GraphicsUnit.Point);
            this.lblTitle.ForeColor = Color.White;
            this.lblTitle.Location = new Point(96, 28);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new Size(250, 41);
            this.lblTitle.TabIndex = 1;
            this.lblTitle.Text = "IwaraDownloader";

            // 
            // lblSubtitle
            // 
            this.lblSubtitle.AutoSize = true;
            this.lblSubtitle.BackColor = Color.Transparent;
            this.lblSubtitle.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.lblSubtitle.ForeColor = Color.FromArgb(180, 180, 200);
            this.lblSubtitle.Location = new Point(100, 72);
            this.lblSubtitle.Name = "lblSubtitle";
            this.lblSubtitle.Size = new Size(180, 15);
            this.lblSubtitle.TabIndex = 5;
            this.lblSubtitle.Text = "iwara.tv Video Downloader";

            // 
            // lblVersion
            // 
            this.lblVersion.AutoSize = true;
            this.lblVersion.BackColor = Color.Transparent;
            this.lblVersion.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.lblVersion.ForeColor = Color.FromArgb(150, 150, 170);
            this.lblVersion.Location = new Point(30, 170);
            this.lblVersion.Name = "lblVersion";
            this.lblVersion.Size = new Size(60, 15);
            this.lblVersion.TabIndex = 2;
            this.lblVersion.Text = Services.UpdateService.CurrentVersionString;

            // 
            // progressBar
            // 
            this.progressBar.Location = new Point(30, 135);
            this.progressBar.MarqueeAnimationSpeed = 25;
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new Size(340, 6);
            this.progressBar.Style = ProgressBarStyle.Marquee;
            this.progressBar.TabIndex = 3;

            // 
            // lblStatus
            // 
            this.lblStatus.BackColor = Color.Transparent;
            this.lblStatus.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.lblStatus.ForeColor = Color.FromArgb(200, 200, 220);
            this.lblStatus.Location = new Point(30, 110);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new Size(340, 20);
            this.lblStatus.TabIndex = 4;
            this.lblStatus.Text = "起動中...";
            this.lblStatus.TextAlign = ContentAlignment.MiddleLeft;

            // 
            // SplashForm
            // 
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.BackColor = Color.FromArgb(30, 35, 50);
            this.ClientSize = new Size(400, 200);
            this.Controls.Add(this.panelMain);
            this.FormBorderStyle = FormBorderStyle.None;
            this.Name = "SplashForm";
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.TopMost = true;
            this.panelMain.ResumeLayout(false);
            this.panelMain.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureIcon)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        private Panel panelMain;
        private PictureBox pictureIcon;
        private Label lblTitle;
        private Label lblSubtitle;
        private Label lblVersion;
        private ProgressBar progressBar;
        private Label lblStatus;
    }
}
