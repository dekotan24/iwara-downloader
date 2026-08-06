using IwaraDownloader.Utils;
using IwaraDownloader.Models;
using IwaraDownloader.Services;

namespace IwaraDownloader.Forms
{
    /// <summary>
    /// 重複検出フォーム
    /// </summary>
    public partial class DuplicateCheckForm : Form
    {
        private readonly DatabaseService _database;
        private List<DuplicateGroup> _duplicates = new();

        public DuplicateCheckForm()
        {
            InitializeComponent();
            Utils.Localizer.Apply(this);
            _database = DatabaseService.Instance;
        }

        private void DuplicateCheckForm_Load(object sender, EventArgs e)
        {
            ScanDuplicates();
        }

        /// <summary>
        /// 重複をスキャン
        /// </summary>
        private void ScanDuplicates()
        {
            btnScan.Enabled = false;
            btnScan.Text = L.T("DuplicateCheckForm_D001");
            lblStatus.Text = L.T("DuplicateCheckForm_D001");

            try
            {
                var allVideos = _database.GetAllVideos();
                
                // VideoIdで重複をグループ化(異なるSubscribedUserId間)
                var duplicateGroups = allVideos
                    .GroupBy(v => v.VideoId)
                    .Where(g => g.Select(v => v.SubscribedUserId).Distinct().Count() > 1)
                    .Select(g => new DuplicateGroup
                    {
                        VideoId = g.Key,
                        Title = g.First().Title,
                        Videos = g.ToList(),
                        ChannelCount = g.Select(v => v.SubscribedUserId).Distinct().Count()
                    })
                    .OrderByDescending(d => d.ChannelCount)
                    .ToList();

                _duplicates = duplicateGroups;

                // DataGridViewに表示
                dgvDuplicates.DataSource = duplicateGroups.Select(d => new DuplicateDisplayItem
                {
                    VideoId = d.VideoId,
                    Title = d.Title.Length > 50 ? d.Title[..47] + "..." : d.Title,
                    ChannelCount = d.ChannelCount,
                    Channels = string.Join(", ", d.Videos
                        .Select(v => v.AuthorUsername)
                        .Distinct()
                        .Take(3)) + (d.Videos.Select(v => v.AuthorUsername).Distinct().Count() > 3 ? "..." : ""),
                    StatusSummary = GetStatusSummary(d.Videos)
                }).ToList();

                // カラム設定
                if (dgvDuplicates.Columns.Count > 0)
                {
                    dgvDuplicates.Columns["VideoId"]!.HeaderText = "Video ID";
                    dgvDuplicates.Columns["VideoId"]!.Width = 120;
                    dgvDuplicates.Columns["Title"]!.HeaderText = L.T("DuplicateCheckForm_D024");
                    dgvDuplicates.Columns["Title"]!.Width = 200;
                    dgvDuplicates.Columns["ChannelCount"]!.HeaderText = L.T("DuplicateCheckForm_D025");
                    dgvDuplicates.Columns["ChannelCount"]!.Width = 50;
                    dgvDuplicates.Columns["Channels"]!.HeaderText = L.T("DuplicateCheckForm_D026");
                    dgvDuplicates.Columns["Channels"]!.Width = 150;
                    dgvDuplicates.Columns["StatusSummary"]!.HeaderText = L.T("DuplicateCheckForm_D027");
                    dgvDuplicates.Columns["StatusSummary"]!.Width = 100;
                }

                lblStatus.Text = L.T("DuplicateCheckForm_D002", duplicateGroups.Count, duplicateGroups.Sum(d => d.Videos.Count));
            }
            catch (Exception ex)
            {
                MessageBox.Show(L.T("DuplicateCheckForm_D003", ex.Message),
                    L.T("DuplicateCheckForm_D004"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = L.T("DuplicateCheckForm_D004");
            }
            finally
            {
                btnScan.Enabled = true;
                btnScan.Text = L.T("DuplicateCheckForm_D005");
            }
        }

        private static string GetStatusSummary(List<VideoInfo> videos)
        {
            var completed = videos.Count(v => v.Status == DownloadStatus.Completed);
            var failed = videos.Count(v => v.Status == DownloadStatus.Failed);
            var pending = videos.Count(v => v.Status == DownloadStatus.Pending);

            var parts = new List<string>();
            if (completed > 0) parts.Add(L.T("DuplicateCheckForm_SumCompleted", completed));
            if (failed > 0) parts.Add(L.T("DuplicateCheckForm_SumFailed", failed));
            if (pending > 0) parts.Add(L.T("DuplicateCheckForm_SumPending", pending));
            
            return string.Join(" ", parts);
        }

        /// <summary>
        /// 選択した重複の詳細を表示
        /// </summary>
        private void dgvDuplicates_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvDuplicates.SelectedRows.Count == 0)
            {
                lstDetails.Items.Clear();
                return;
            }

            var videoId = dgvDuplicates.SelectedRows[0].Cells["VideoId"].Value?.ToString();
            if (string.IsNullOrEmpty(videoId)) return;

            var duplicate = _duplicates.FirstOrDefault(d => d.VideoId == videoId);
            if (duplicate == null) return;

            lstDetails.Items.Clear();
            foreach (var video in duplicate.Videos)
            {
                var channelName = video.AuthorUsername ?? L.T("DuplicateCheckForm_UnknownChannel");
                var status = video.Status switch
                {
                    DownloadStatus.Completed => L.T("DuplicateCheckForm_StCompleted"),
                    DownloadStatus.Failed => L.T("DuplicateCheckForm_StFailed"),
                    DownloadStatus.Pending => L.T("DuplicateCheckForm_StPending"),
                    DownloadStatus.Downloading => L.T("DuplicateCheckForm_StDownloading"),
                    _ => "?"
                };
                lstDetails.Items.Add($"[{status}] {channelName} (ID:{video.Id})");
            }
        }

        /// <summary>
        /// 重複を解消(完了以外を削除)
        /// </summary>
        private void btnRemoveDuplicates_Click(object sender, EventArgs e)
        {
            if (_duplicates.Count == 0)
            {
                MessageBox.Show(L.T("DuplicateCheckForm_D006"), L.T("DuplicateCheckForm_D007"), 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show(
                L.T("DuplicateCheckForm_D008") +
                L.T("DuplicateCheckForm_D009") +
                L.T("DuplicateCheckForm_D010") +
                L.T("DuplicateCheckForm_D011") +
                L.T("DuplicateCheckForm_D012") +
                L.T("DuplicateCheckForm_D013") +
                L.T("DuplicateCheckForm_D014"),
                L.T("DuplicateCheckForm_D015"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return;

            int removedCount = 0;
            var idsToRemove = new List<int>();

            foreach (var group in _duplicates)
            {
                // 優先順位でソート
                var sorted = group.Videos
                    .OrderByDescending(v => v.Status == DownloadStatus.Completed && v.LocalFileExists)
                    .ThenByDescending(v => v.Status == DownloadStatus.Completed)
                    .ThenByDescending(v => v.Status == DownloadStatus.Pending)
                    .ThenByDescending(v => v.Status == DownloadStatus.Failed)
                    .ToList();

                // 最初の1つを残して削除対象に追加
                for (int i = 1; i < sorted.Count; i++)
                {
                    idsToRemove.Add(sorted[i].Id);
                }
            }

            if (idsToRemove.Count > 0)
            {
                // 重複解消は「除外(ゴミ箱)」ではなく生削除が正しい。
                // 同一 VideoId の冗長行を消すだけで、残す行が VideoExists=true を保つため
                // 自動取得で復活しない (再取得バグは無関係)。ExcludeVideos に通すと逆に
                // 残す行と同じ VideoId が除外表にも入り不変条件が壊れ、共有ファイルを消す事故になる。
                removedCount = _database.DeleteVideosBatch(idsToRemove);
            }

            MessageBox.Show(L.T("DuplicateCheckForm_D016", removedCount), L.T("DuplicateCheckForm_D017"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);

            // 再スキャン
            ScanDuplicates();
        }

        /// <summary>
        /// 選択した重複グループの詳細を削除
        /// </summary>
        private void btnRemoveSelected_Click(object sender, EventArgs e)
        {
            if (lstDetails.SelectedIndex < 0)
            {
                MessageBox.Show(L.T("DuplicateCheckForm_D018"), L.T("DuplicateCheckForm_D007"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (dgvDuplicates.SelectedRows.Count == 0)
                return;

            var videoId = dgvDuplicates.SelectedRows[0].Cells["VideoId"].Value?.ToString();
            if (string.IsNullOrEmpty(videoId)) return;

            var duplicate = _duplicates.FirstOrDefault(d => d.VideoId == videoId);
            if (duplicate == null || lstDetails.SelectedIndex >= duplicate.Videos.Count) return;

            var video = duplicate.Videos[lstDetails.SelectedIndex];

            var result = MessageBox.Show(
                L.T("DuplicateCheckForm_D019") +
                L.T("DuplicateCheckForm_D020", video.AuthorUsername) +
                L.T("DuplicateCheckForm_D021", video.Title) +
                L.T("DuplicateCheckForm_D022", video.Status),
                L.T("DuplicateCheckForm_D023"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                // 重複解消は生削除が正しい (btnRemoveDuplicates_Click のコメント参照)。
                _database.DeleteVideo(video.Id);
                ScanDuplicates();
            }
        }

        private void btnScan_Click(object sender, EventArgs e)
        {
            ScanDuplicates();
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }

    /// <summary>
    /// 重複グループ
    /// </summary>
    public class DuplicateGroup
    {
        public string VideoId { get; set; } = "";
        public string Title { get; set; } = "";
        public List<VideoInfo> Videos { get; set; } = new();
        public int ChannelCount { get; set; }
    }

    /// <summary>
    /// 重複表示用アイテム
    /// </summary>
    public class DuplicateDisplayItem
    {
        public string VideoId { get; set; } = "";
        public string Title { get; set; } = "";
        public int ChannelCount { get; set; }
        public string Channels { get; set; } = "";
        public string StatusSummary { get; set; } = "";
    }
}
