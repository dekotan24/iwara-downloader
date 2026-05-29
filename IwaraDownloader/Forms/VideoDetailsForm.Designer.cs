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
            AddRow(lblTitle, "タイトル", txtTitle, multiline: false);
            AddRow(lblSource, "ソース", txtSource, multiline: false);
            AddRow(lblAuthor, "投稿者", txtAuthor, multiline: false);
            AddRow(lblVideoId, "Video ID", txtVideoId, multiline: false);
            AddRow(lblFileUuid, "File UUID", txtFileUuid, multiline: false);
            AddRow(lblStatus, "ステータス", txtStatus, multiline: false);
            AddRow(lblDuration, "長さ", txtDuration, multiline: false);
            AddRow(lblFileSize, "ファイルサイズ", txtFileSize, multiline: false);
            AddRow(lblPostedAt, "投稿日時", txtPostedAt, multiline: false);
            AddRow(lblDownloadedAt, "DL日時", txtDownloadedAt, multiline: false);
            AddRow(lblCreatedAt, "登録日時", txtCreatedAt, multiline: false);

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
                btnOpenUrl.Text = "開く";
                btnOpenUrl.Dock = DockStyle.Fill;
                btnOpenUrl.Click += btnOpenUrl_Click;
                urlRow.Controls.Add(txtUrl, 0, 0);
                urlRow.Controls.Add(btnOpenUrl, 1, 0);

                tableLayout.Controls.Add(lblUrl);
                tableLayout.Controls.Add(urlRow);
            }

            // LocalFilePath 行 (TextBox + ボタン)
            {
                lblLocalFilePath.Text = "保存先";
                lblLocalFilePath.TextAlign = ContentAlignment.MiddleLeft;
                lblLocalFilePath.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                lblLocalFilePath.AutoSize = false;
                lblLocalFilePath.Height = 26;

                var fileRow = new TableLayoutPanel
                {
                    ColumnCount = 2,
                    RowCount = 1,
                    Dock = DockStyle.Fill,
                    Margin = new Padding(0)
                };
                fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70F));
                txtLocalFilePath.ReadOnly = true;
                txtLocalFilePath.Dock = DockStyle.Fill;
                btnOpenFile.Text = "開く";
                btnOpenFile.Dock = DockStyle.Fill;
                btnOpenFile.Click += btnOpenFile_Click;
                fileRow.Controls.Add(txtLocalFilePath, 0, 0);
                fileRow.Controls.Add(btnOpenFile, 1, 0);

                tableLayout.Controls.Add(lblLocalFilePath);
                tableLayout.Controls.Add(fileRow);
            }

            AddRow(lblRetry, "リトライ回数", txtRetry, multiline: false);
            AddRow(lblLastError, "最終エラー", txtLastError, multiline: true, height: 50);
            AddRow(lblTags, "タグ (カンマ区切り)", txtTags, multiline: false, editable: true);
            AddRow(lblMemo, "メモ", txtMemo, multiline: true, height: 100, editable: true);

            // お気に入り行 (ラベル + チェックボックス)
            {
                lblFavorite.Text = "お気に入り";
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
        private Button btnOpenUrl, btnOpenFile, btnSave, btnCancel;
        private TableLayoutPanel tableLayout;
    }
}
