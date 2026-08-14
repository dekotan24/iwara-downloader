namespace IwaraDownloader.Forms
{
    partial class VideoDetailsForm
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
            this.components = new System.ComponentModel.Container();

            this.lblTitle = new Label();
            this.txtTitle = new TextBox();
            this.lblSource = new Label();
            this.txtSource = new TextBox();
            this.lblAuthor = new Label();
            this.txtAuthor = new TextBox();
            this.lblVideoId = new Label();
            this.txtVideoId = new TextBox();
            this.lblFileUuid = new Label();
            this.txtFileUuid = new TextBox();
            this.lblStatus = new Label();
            this.txtStatus = new TextBox();
            this.lblDuration = new Label();
            this.txtDuration = new TextBox();
            this.lblFileSize = new Label();
            this.txtFileSize = new TextBox();
            this.lblPostedAt = new Label();
            this.txtPostedAt = new TextBox();
            this.lblDownloadedAt = new Label();
            this.txtDownloadedAt = new TextBox();
            this.lblCreatedAt = new Label();
            this.txtCreatedAt = new TextBox();
            this.lblUrl = new Label();
            this.txtUrl = new TextBox();
            this.btnOpenUrl = new Button();
            this.lblLocalFilePath = new Label();
            this.txtLocalFilePath = new TextBox();
            this.btnOpenFile = new Button();
            this.btnRemapFile = new Button();
            this.btnUnmapFile = new Button();
            this.lblRetry = new Label();
            this.txtRetry = new TextBox();
            this.lblLastError = new Label();
            this.txtLastError = new TextBox();
            this.lblTags = new Label();
            this.txtTags = new TextBox();
            this.lblMemo = new Label();
            this.txtMemo = new TextBox();
            this.lblFavorite = new Label();
            this.chkFavorite = new CheckBox();
            this.btnSave = new Button();
            this.btnCancel = new Button();
            this.tableLayout = new TableLayoutPanel();
            this.SuspendLayout();

            // ============================================
            // Form
            // ============================================
            this.Text = "動画の詳細";
            this.Size = new Size(700, 720);
            this.MinimumSize = new Size(560, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;
            this.Padding = new Padding(10);

            // ============================================
            // tableLayout: 2 列のグリッドで [ラベル / 値] を並べる
            // ============================================
            this.tableLayout.Dock = DockStyle.Fill;
            this.tableLayout.ColumnCount = 2;
            this.tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            this.tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.tableLayout.AutoSize = false;
            this.tableLayout.Padding = new Padding(0, 0, 0, 6);
            this.tableLayout.AutoScroll = true;

            // 行追加 (タイトル, ソース, 投稿者, ...)
            AddRow(lblTitle, Utils.L.T("VideoDetailsForm_RowTitle"), txtTitle, multiline: false);
            AddRow(lblSource, Utils.L.T("VideoDetailsForm_RowSource"), txtSource, multiline: false);
            AddRow(lblAuthor, Utils.L.T("VideoDetailsForm_RowAuthor"), txtAuthor, multiline: false);
            AddRow(lblVideoId, "Video ID", txtVideoId, multiline: false);
            AddRow(lblFileUuid, "File UUID", txtFileUuid, multiline: false);
            AddRow(lblStatus, Utils.L.T("VideoDetailsForm_RowStatus"), txtStatus, multiline: false);
            AddRow(lblDuration, Utils.L.T("VideoDetailsForm_RowDuration"), txtDuration, multiline: false);
            AddRow(lblFileSize, Utils.L.T("VideoDetailsForm_RowFileSize"), txtFileSize, multiline: false);
            AddRow(lblPostedAt, Utils.L.T("VideoDetailsForm_RowPostedAt"), txtPostedAt, multiline: false);
            AddRow(lblDownloadedAt, Utils.L.T("VideoDetailsForm_RowDownloadedAt"), txtDownloadedAt, multiline: false);
            AddRow(lblCreatedAt, Utils.L.T("VideoDetailsForm_RowCreatedAt"), txtCreatedAt, multiline: false);

            // URL 行 (TextBox + ボタン)
            {
                lblUrl.Text = "URL";
                lblUrl.TextAlign = ContentAlignment.MiddleLeft;
                lblUrl.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                lblUrl.AutoSize = false;
                lblUrl.Height = 26;

                var urlRow = new TableLayoutPanel
                {
                    ColumnCount = 2,
                    RowCount = 1,
                    Dock = DockStyle.Fill,
                    Margin = new Padding(0)
                };
                urlRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                urlRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70F));
                txtUrl.ReadOnly = true;
                txtUrl.Dock = DockStyle.Fill;
                btnOpenUrl.Text = Utils.L.T("VideoDetailsForm_btnOpen");
                btnOpenUrl.Dock = DockStyle.Fill;
                btnOpenUrl.Click += btnOpenUrl_Click;
                urlRow.Controls.Add(txtUrl, 0, 0);
                urlRow.Controls.Add(btnOpenUrl, 1, 0);

                tableLayout.Controls.Add(lblUrl);
                tableLayout.Controls.Add(urlRow);
            }

            // LocalFilePath 行 (TextBox + ボタン)
            {
                lblLocalFilePath.Text = Utils.L.T("VideoDetailsForm_lblLocalFilePath");
                lblLocalFilePath.TextAlign = ContentAlignment.MiddleLeft;
                lblLocalFilePath.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                lblLocalFilePath.AutoSize = false;
                lblLocalFilePath.Height = 26;

                var fileRow = new TableLayoutPanel
                {
                    ColumnCount = 4,
                    RowCount = 1,
                    Dock = DockStyle.Fill,
                    Margin = new Padding(0)
                };
                fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70F));
                fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));
                fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
                txtLocalFilePath.ReadOnly = true;
                txtLocalFilePath.Dock = DockStyle.Fill;
                btnOpenFile.Text = Utils.L.T("VideoDetailsForm_btnOpen");
                btnOpenFile.Dock = DockStyle.Fill;
                btnOpenFile.Click += btnOpenFile_Click;
                // マップ/再マップは常時有効 (状態を問わず、未マップ動画への新規マップにも
                // 誤って紐付いたファイルの修正にも使えるように)
                btnRemapFile.Text = Utils.L.T("VideoDetailsForm_btnRemap");
                btnRemapFile.Dock = DockStyle.Fill;
                btnRemapFile.Click += btnRemapFile_Click;
                // マッピング解除は LocalFilePath が設定されている場合のみ有効 (PopulateFields で判定)
                btnUnmapFile.Text = Utils.L.T("VideoDetailsForm_btnUnmap");
                btnUnmapFile.Dock = DockStyle.Fill;
                btnUnmapFile.Click += btnUnmapFile_Click;
                fileRow.Controls.Add(txtLocalFilePath, 0, 0);
                fileRow.Controls.Add(btnOpenFile, 1, 0);
                fileRow.Controls.Add(btnRemapFile, 2, 0);
                fileRow.Controls.Add(btnUnmapFile, 3, 0);

                tableLayout.Controls.Add(lblLocalFilePath);
                tableLayout.Controls.Add(fileRow);
            }

            AddRow(lblRetry, Utils.L.T("VideoDetailsForm_RowRetry"), txtRetry, multiline: false);
            AddRow(lblLastError, Utils.L.T("VideoDetailsForm_RowLastError"), txtLastError, multiline: true, height: 50);
            AddRow(lblTags, Utils.L.T("VideoDetailsForm_RowTags"), txtTags, multiline: false, editable: true);
            AddRow(lblMemo, Utils.L.T("VideoDetailsForm_RowMemo"), txtMemo, multiline: true, height: 100, editable: true);

            // お気に入り行 (ラベル + チェックボックス)
            {
                lblFavorite.Text = Utils.L.T("VideoDetailsForm_lblFavorite");
                lblFavorite.TextAlign = ContentAlignment.MiddleLeft;
                lblFavorite.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
                lblFavorite.AutoSize = false;
                lblFavorite.Height = 26;
                lblFavorite.Margin = new Padding(0, 0, 6, 0);

                this.chkFavorite.Text = "★ お気に入りに登録";
                this.chkFavorite.AutoSize = true;
                this.chkFavorite.Dock = DockStyle.Fill;

                tableLayout.Controls.Add(lblFavorite);
                tableLayout.Controls.Add(this.chkFavorite);
                tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            }

            // ============================================
            // ボタン: 保存 / キャンセル
            // ============================================
            this.btnSave.Text = "保存";
            this.btnSave.DialogResult = DialogResult.OK;
            this.btnSave.Click += new EventHandler(btnSave_Click);
            this.btnSave.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.btnSave.Size = new Size(90, 30);

            this.btnCancel.Text = "閉じる";
            this.btnCancel.DialogResult = DialogResult.Cancel;
            this.btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.btnCancel.Size = new Size(90, 30);

            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 42,
                Padding = new Padding(0, 6, 0, 0)
            };
            btnPanel.Controls.Add(this.btnCancel);
            btnPanel.Controls.Add(this.btnSave);

            this.AcceptButton = this.btnSave;
            this.CancelButton = this.btnCancel;

            this.Controls.Add(this.tableLayout);
            this.Controls.Add(btnPanel);

            this.ResumeLayout(false);
        }

        /// <summary>tableLayout に [ラベル / TextBox] 行を1組追加する</summary>
        private void AddRow(Label label, string labelText, TextBox tb, bool multiline, int height = 26, bool editable = false)
        {
            label.Text = labelText;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            label.AutoSize = false;
            label.Height = height;
            label.Margin = new Padding(0, multiline ? 2 : 0, 6, 0);

            tb.Dock = DockStyle.Fill;
            tb.Multiline = multiline;
            tb.ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None;
            tb.Height = height;
            tb.ReadOnly = !editable;
            tb.WordWrap = multiline;

            tableLayout.Controls.Add(label);
            tableLayout.Controls.Add(tb);
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, height + 4));
        }

        private Label lblTitle, lblSource, lblAuthor, lblVideoId, lblFileUuid, lblStatus, lblDuration, lblFileSize;
        private Label lblPostedAt, lblDownloadedAt, lblCreatedAt, lblUrl, lblLocalFilePath, lblRetry, lblLastError;
        private Label lblTags, lblMemo, lblFavorite;
        private CheckBox chkFavorite;
        private TextBox txtTitle, txtSource, txtAuthor, txtVideoId, txtFileUuid, txtStatus, txtDuration, txtFileSize;
        private TextBox txtPostedAt, txtDownloadedAt, txtCreatedAt, txtUrl, txtLocalFilePath, txtRetry, txtLastError;
        private TextBox txtTags, txtMemo;
        private Button btnOpenUrl, btnOpenFile, btnRemapFile, btnUnmapFile, btnSave, btnCancel;
        private TableLayoutPanel tableLayout;
    }
}
