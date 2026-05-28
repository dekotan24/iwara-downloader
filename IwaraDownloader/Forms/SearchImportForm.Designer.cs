namespace IwaraDownloader.Forms
{
    partial class SearchImportForm
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

            this.txtQuery = new TextBox();
            this.btnSearch = new Button();
            this.cmbSite = new ComboBox();
            this.lblStatus = new Label();
            this.listResults = new ListView();
            this.btnSelectAll = new Button();
            this.btnSelectNone = new Button();
            this.btnImport = new Button();
            this.btnClose = new Button();
            this.btnPrevPage = new Button();
            this.btnNextPage = new Button();
            this.lblPage = new Label();

            this.Text = "iwara 検索インポート";
            this.Size = new Size(900, 600);
            this.MinimumSize = new Size(640, 480);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Padding = new Padding(10);

            // --- 検索バー (Site ドロップダウン + キーワード + 検索ボタン) ---
            var searchPanel = new Panel { Dock = DockStyle.Top, Height = 36 };
            this.cmbSite.Location = new Point(0, 6);
            this.cmbSite.Size = new Size(120, 25);
            this.cmbSite.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbSite.Items.AddRange(new object[] { "iwara.tv", "iwara.ai" });
            this.cmbSite.SelectedIndex = 0;
            this.cmbSite.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            this.cmbSite.SelectedIndexChanged += new EventHandler(cmbSite_SelectedIndexChanged);

            this.txtQuery.Location = new Point(126, 6);
            this.txtQuery.Size = new Size(554, 25);
            this.txtQuery.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            this.txtQuery.PlaceholderText = "検索キーワード (Enter で検索)";
            this.txtQuery.KeyDown += new KeyEventHandler(txtQuery_KeyDown);

            this.btnSearch.Text = "検索";
            this.btnSearch.Location = new Point(686, 5);
            this.btnSearch.Size = new Size(80, 27);
            this.btnSearch.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.btnSearch.Click += new EventHandler(btnSearch_Click);

            searchPanel.Controls.Add(this.cmbSite);
            searchPanel.Controls.Add(this.txtQuery);
            searchPanel.Controls.Add(this.btnSearch);

            // --- 結果リスト ---
            this.listResults.Dock = DockStyle.Fill;
            this.listResults.View = View.Details;
            this.listResults.CheckBoxes = true;
            this.listResults.FullRowSelect = true;
            this.listResults.GridLines = true;
            this.listResults.Columns.Add("タイトル", 380);
            this.listResults.Columns.Add("投稿者", 150);
            this.listResults.Columns.Add("Rating", 70);
            this.listResults.Columns.Add("長さ", 60);
            this.listResults.Columns.Add("投稿日", 110);
            this.listResults.Columns.Add("Video ID", 110);
            this.listResults.MultiSelect = true;
            this.listResults.DoubleClick += new EventHandler(listResults_DoubleClick);

            // --- 下部ボタンエリア ---
            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 80 };
            this.lblStatus.Text = "キーワードを入力して「検索」";
            this.lblStatus.Dock = DockStyle.Top;
            this.lblStatus.Height = 18;
            this.lblStatus.TextAlign = ContentAlignment.MiddleLeft;

            // ページネーション (上段)
            var pagePanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 32,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 4, 0, 0),
            };
            this.btnPrevPage.Text = "◀ 前のページ";
            this.btnPrevPage.Size = new Size(110, 26);
            this.btnPrevPage.Enabled = false;
            this.btnPrevPage.Click += new EventHandler(btnPrevPage_Click);
            this.btnNextPage.Text = "次のページ ▶";
            this.btnNextPage.Size = new Size(110, 26);
            this.btnNextPage.Enabled = false;
            this.btnNextPage.Click += new EventHandler(btnNextPage_Click);
            this.lblPage.Text = "Page -";
            this.lblPage.AutoSize = false;
            this.lblPage.Size = new Size(120, 26);
            this.lblPage.TextAlign = ContentAlignment.MiddleLeft;
            this.lblPage.Margin = new Padding(8, 0, 0, 0);
            pagePanel.Controls.Add(this.btnPrevPage);
            pagePanel.Controls.Add(this.btnNextPage);
            pagePanel.Controls.Add(this.lblPage);

            // アクション (下段)
            var actionPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 32,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 2, 0, 0),
            };
            this.btnSelectAll.Text = "全選択";
            this.btnSelectAll.Size = new Size(80, 26);
            this.btnSelectAll.Click += new EventHandler(btnSelectAll_Click);
            this.btnSelectNone.Text = "選択解除";
            this.btnSelectNone.Size = new Size(80, 26);
            this.btnSelectNone.Click += new EventHandler(btnSelectNone_Click);
            this.btnImport.Text = "選択をインポート";
            this.btnImport.Size = new Size(130, 26);
            this.btnImport.Click += new EventHandler(btnImport_Click);
            this.btnClose.Text = "閉じる";
            this.btnClose.Size = new Size(80, 26);
            this.btnClose.DialogResult = DialogResult.Cancel;
            this.btnClose.Click += new EventHandler((s, e) => Close());
            actionPanel.Controls.Add(this.btnSelectAll);
            actionPanel.Controls.Add(this.btnSelectNone);
            actionPanel.Controls.Add(this.btnImport);
            actionPanel.Controls.Add(this.btnClose);

            bottomPanel.Controls.Add(actionPanel);
            bottomPanel.Controls.Add(pagePanel);
            bottomPanel.Controls.Add(this.lblStatus);

            // 親 Form へ
            this.Controls.Add(this.listResults);
            this.Controls.Add(bottomPanel);
            this.Controls.Add(searchPanel);

            this.AcceptButton = this.btnSearch;
            this.CancelButton = this.btnClose;
        }

        private TextBox txtQuery;
        private Button btnSearch;
        private ComboBox cmbSite;
        private Label lblStatus;
        private ListView listResults;
        private Button btnSelectAll;
        private Button btnSelectNone;
        private Button btnImport;
        private Button btnClose;
        private Button btnPrevPage;
        private Button btnNextPage;
        private Label lblPage;
    }
}
