using IwaraDownloader.Models;
using IwaraDownloader.Services;
using IwaraDownloader.Utils;

namespace IwaraDownloader.Forms
{
    /// <summary>
    /// iwara で検索キーワード → 動画一覧 → チェックして一括キュー追加。
    /// 購読してないチャンネルの動画も DL 対象にできる。
    /// </summary>
    public partial class SearchImportForm : Form
    {
        private readonly DownloadManager _downloadManager;
        private readonly IwaraSearch _search;
        private readonly DatabaseService _database = DatabaseService.Instance;

        private const int PageLimit = 32;
        private int _currentPage = 0;
        private string _currentQuery = "";
        private string _currentSite = Helpers.SiteTv;  // 検索結果インポート時に動画に紐付ける site
        private int _totalCount = 0;

        public SearchImportForm(DownloadManager downloadManager)
        {
            _downloadManager = downloadManager;
            _search = new IwaraSearch(downloadManager.IwaraApi);
            InitializeComponent();
        }

        private void txtQuery_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                _ = ExecuteSearchAsync(0);
            }
        }

        private void btnSearch_Click(object? sender, EventArgs e)
            => _ = ExecuteSearchAsync(0);

        /// <summary>
        /// Site 切替時: 現在の検索結果は別 site のものなのでクリアして混乱を防ぐ
        /// </summary>
        private void cmbSite_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (listResults.Items.Count == 0) return;
            listResults.Items.Clear();
            lblStatus.Text = $"検索 site を {cmbSite.SelectedItem} に切り替えました。再検索してください。";
            lblPage.Text = "Page -";
            btnPrevPage.Enabled = false;
            btnNextPage.Enabled = false;
        }

        private void btnPrevPage_Click(object? sender, EventArgs e)
        {
            if (_currentPage > 0) _ = ExecuteSearchAsync(_currentPage - 1);
        }

        private void btnNextPage_Click(object? sender, EventArgs e)
            => _ = ExecuteSearchAsync(_currentPage + 1);

        private async Task ExecuteSearchAsync(int page)
        {
            var q = (txtQuery.Text ?? "").Trim();
            if (string.IsNullOrEmpty(q))
            {
                MessageBox.Show("検索キーワードを入力してください。", "入力エラー",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // ComboBox 選択値 → site ホスト名
            var siteHost = cmbSite.SelectedIndex == 1 ? Helpers.SiteAi : Helpers.SiteTv;
            _currentSite = siteHost;

            // UI 状態
            btnSearch.Enabled = false;
            btnImport.Enabled = false;
            btnPrevPage.Enabled = false;
            btnNextPage.Enabled = false;
            lblStatus.Text = $"検索中: \"{q}\" [{siteHost}] (page {page + 1})...";

            try
            {
                _currentQuery = q;
                _currentPage = page;
                var result = await _search.SearchAsync(q, page, PageLimit, siteHost);
                if (!result.Success)
                {
                    listResults.Items.Clear();
                    lblStatus.Text = $"エラー: {result.Error}";
                    return;
                }
                _totalCount = result.TotalCount;
                PopulateResults(result.Items);

                int totalPages = Math.Max(1, (int)Math.Ceiling((double)_totalCount / PageLimit));
                lblPage.Text = $"Page {_currentPage + 1} / {totalPages}";
                btnPrevPage.Enabled = _currentPage > 0;
                btnNextPage.Enabled = (_currentPage + 1) < totalPages;
                lblStatus.Text = $"検索結果: 全{_totalCount}件中 {result.Items.Count}件表示 (page {_currentPage + 1}/{totalPages})";
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"検索失敗: {ex.Message}";
            }
            finally
            {
                btnSearch.Enabled = true;
                btnImport.Enabled = true;
            }
        }

        private void PopulateResults(IEnumerable<SearchResultItem> items)
        {
            listResults.BeginUpdate();
            listResults.Items.Clear();
            // 既に DB に登録済みの動画は一目で分かるよう色分けする
            var allIds = items.Select(i => i.VideoId).ToList();
            var existing = _database.GetExistingVideoIds(allIds);

            foreach (var item in items)
            {
                var alreadyInDb = existing.Contains(item.VideoId);
                item.AlreadyInDb = alreadyInDb;
                var title = alreadyInDb ? $"[登録済] {item.Title}" : item.Title;
                var lvi = new ListViewItem(new[]
                {
                    title,
                    item.AuthorUsername,
                    string.IsNullOrEmpty(item.Rating) ? "-" : item.Rating,
                    item.DurationFormatted,
                    item.CreatedAt?.ToString("yyyy/MM/dd") ?? "-",
                    item.VideoId,
                })
                {
                    Tag = item,
                };
                if (alreadyInDb)
                {
                    lvi.ForeColor = Color.Gray;
                    // 既存はデフォルトでチェック解除
                    lvi.Checked = false;
                }
                else
                {
                    lvi.Checked = true; // 新規はデフォルトでチェック
                }
                listResults.Items.Add(lvi);
            }
            listResults.EndUpdate();
        }

        private void btnSelectAll_Click(object? sender, EventArgs e)
        {
            foreach (ListViewItem lvi in listResults.Items) lvi.Checked = true;
        }

        /// <summary>DL済 (DB登録済) を除いて全選択する。</summary>
        private void btnSelectNew_Click(object? sender, EventArgs e)
        {
            foreach (ListViewItem lvi in listResults.Items)
                lvi.Checked = lvi.Tag is SearchResultItem it && !it.AlreadyInDb;
        }

        private void btnSelectNone_Click(object? sender, EventArgs e)
        {
            foreach (ListViewItem lvi in listResults.Items) lvi.Checked = false;
        }

        private void listResults_DoubleClick(object? sender, EventArgs e)
        {
            // ダブルクリックで iwara ページを開く
            if (listResults.SelectedItems.Count == 0) return;
            if (listResults.SelectedItems[0].Tag is SearchResultItem item)
                Helpers.OpenUrl(item.Url);
        }

        private void btnImport_Click(object? sender, EventArgs e)
        {
            var checkedItems = new List<SearchResultItem>();
            foreach (ListViewItem lvi in listResults.Items)
            {
                if (lvi.Checked && lvi.Tag is SearchResultItem it)
                    checkedItems.Add(it);
            }
            if (checkedItems.Count == 0)
            {
                MessageBox.Show("インポートする動画を選択してください。", "情報",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var msg = $"{checkedItems.Count}件の動画をダウンロードキューに追加します。続行しますか？";
            if (MessageBox.Show(msg, "確認",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            int addedNew = 0, skippedExisting = 0;
            var existingIds = _database.GetExistingVideoIds(checkedItems.Select(c => c.VideoId));

            foreach (var item in checkedItems)
            {
                if (existingIds.Contains(item.VideoId))
                {
                    // 既存動画 → 既存 VideoInfo を再キュー (DL されてなければ DL する)
                    var ex = _database.GetVideoByVideoId(item.VideoId);
                    if (ex != null && ex.Status != DownloadStatus.Completed)
                    {
                        SubscribedUser? user = null;
                        if (ex.SubscribedUserId.HasValue)
                            user = _database.GetSubscribedUserById(ex.SubscribedUserId.Value);
                        _downloadManager.EnqueueDownload(ex, ex.SubscribedUserId.HasValue, user);
                    }
                    skippedExisting++;
                    continue;
                }

                // 新規登録 (検索した site を継承して DL 時の X-Site 振り分けに使う)
                var video = new VideoInfo
                {
                    VideoId = item.VideoId,
                    Title = item.Title,
                    Url = $"https://{_currentSite}/video/{item.VideoId}",
                    ThumbnailUrl = item.ThumbnailUrl,
                    DurationSeconds = item.DurationSeconds,
                    EmbedUrl = item.EmbedUrl,
                    Rating = item.Rating,
                    Site = _currentSite,
                    AuthorUsername = item.AuthorUsername,
                    AuthorUserId = item.AuthorUsername, // UserId = username 運用
                    PostedAt = item.CreatedAt,
                    Status = DownloadStatus.Pending,
                    CreatedAt = DateTime.Now,
                };
                video.Id = _database.AddVideo(video);
                _downloadManager.EnqueueDownload(video, isSubscriptionDownload: false);
                addedNew++;
            }

            lblStatus.Text = $"インポート完了: 新規 {addedNew}件 / 再キュー {skippedExisting}件";
            DialogResult = DialogResult.OK; // 親フォームに更新通知
        }
    }
}
