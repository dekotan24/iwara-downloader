namespace IwaraDownloader.Forms
{
    partial class FileMoveProgressForm
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
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblCurrent = new System.Windows.Forms.Label();
            this.lblCount = new System.Windows.Forms.Label();
            this.lblSize = new System.Windows.Forms.Label();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();

            // lblTitle
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Location = new System.Drawing.Point(15, 12);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Text = "ファイルを移動しています...";

            // lblCount
            this.lblCount.AutoSize = true;
            this.lblCount.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblCount.Location = new System.Drawing.Point(15, 42);
            this.lblCount.Name = "lblCount";
            this.lblCount.Text = "0 / 0";

            // lblSize
            this.lblSize.AutoSize = true;
            this.lblSize.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblSize.ForeColor = System.Drawing.Color.DimGray;
            this.lblSize.Location = new System.Drawing.Point(120, 42);
            this.lblSize.Name = "lblSize";
            this.lblSize.Text = "";

            // progressBar
            this.progressBar.Location = new System.Drawing.Point(15, 64);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(450, 18);
            this.progressBar.Style = System.Windows.Forms.ProgressBarStyle.Continuous;

            // lblCurrent
            this.lblCurrent.Font = new System.Drawing.Font("Consolas", 9F);
            this.lblCurrent.ForeColor = System.Drawing.Color.FromArgb(50, 90, 150);
            this.lblCurrent.Location = new System.Drawing.Point(15, 90);
            this.lblCurrent.Name = "lblCurrent";
            this.lblCurrent.Size = new System.Drawing.Size(450, 36);
            this.lblCurrent.Text = "準備中...";

            // btnCancel
            this.btnCancel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this.btnCancel.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.btnCancel.Location = new System.Drawing.Point(375, 135);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(90, 28);
            this.btnCancel.Text = "中止";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);

            // FileMoveProgressForm
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(480, 180);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.lblCurrent);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.lblSize);
            this.Controls.Add(this.lblCount);
            this.Controls.Add(this.lblTitle);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FileMoveProgressForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "ファイル移動";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FileMoveProgressForm_FormClosing);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblCount;
        private System.Windows.Forms.Label lblSize;
        private System.Windows.Forms.Label lblCurrent;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Button btnCancel;
    }
}
