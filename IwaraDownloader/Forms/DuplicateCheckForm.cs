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
            btnScan.Text = "スキャン中...";
            lblStatus.Text = "スキャン中...";

            try
            {
                var allVideos = _database.GetAllVideos();
                
                // VideoIdで重複をグループ化（異なるSubscribedUserId間）
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
                    dgvDuplicates.Columns["VideoId"].HeaderText = "Video ID";
                    dgvDuplicates.Columns["VideoId"].Width = 120;
                    dgvDuplicates.Columns["Title"].HeaderText = "タイトル";
                    dgvDuplicates.Columns["Title"].Width = 200;
                    dgvDuplicates.Columns["ChannelCount"].HeaderText = "CH数";
                    dgvDuplicates.Columns["ChannelCount"].Width = 50;
                    dgvDuplicates.Columns["Channels"].HeaderText = "チャンネル";
                    dgvDuplicates.Columns["Channels"].Width = 150;
                    dgvDuplicates.Columns["StatusSummary"].HeaderText = "状態";
                    dgvDuplicates.Columns["StatusSummary"].Width = 100;
                }

                lblStatus.Text = $"重複: {duplicateGroups.Count}件（{duplicateGroups.Sum(d => d.Videos.Count)}動画）";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"スキャン中にエラーが発生しました:\n{ex.Message}",
                    "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "エラー";
            }
            finally
            {
                btnScan.Enabled = true;
                btnScan.Text = "再スキャン";
            }
        }

        private static string GetStatusSummary(List<VideoInfo> videos)
        {
            var completed = videos.Count(v => v.Status == DownloadStatus.Completed);
            var failed = videos.Count(v => v.Status == DownloadStatus.Failed);
            var pending = videos.Count(v => v.Status == DownloadStatus.Pending);

            var parts = new List<string>();
            if (completed > 0) parts.Add($"完了:{completed}");
            if (failed > 0) parts.Add($"失敗:{failed}");
            if (pending > 0) parts.Add($"待機:{pending}");
            
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
                var channelName = video.AuthorUsername ?? "(不明)";
                var status = video.Status switch
                {
                    DownloadStatus.Completed => "✓ 完了",
                    DownloadStatus.Failed => "✗ 失敗",
                    DownloadStatus.Pending => "○ 待機",
                    DownloadStatus.Downloading => "↓ DL中",
                    _ => "?"
                };
                lstDetails.Items.Add($"[{status}] {channelName} (ID:{video.Id})");
            }
        }

        /// <summary>
        /// 重複を解消（完了以外を削除）
        /// </summary>
        private void btnRemoveDuplicates_Click(object sender, EventArgs e)
        {
            if (_duplicates.Count == 0)
            {
                MessageBox.Show("重複が見つかりませんでした。", "情報", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show(
                "重複動画を解消します。\n\n" +
                "各VideoIdについて、以下の優先順位で1つを残し、他を削除します:\n" +
                "1. 完了済み（ファイルが存在する）\n" +
                "2. 完了済み（ファイルが存在しない）\n" +
                "3. 待機中\n" +
                "4. 失敗\n\n" +
                "続行しますか？",
                "重複解消",
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
                removedCount = _database.DeleteVideosBatch(idsToRemove);
            }

            MessageBox.Show($"{removedCount}件の重複を削除しました。", "完了",
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
                MessageBox.Show("削除する項目を選択してください。", "情報",
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
                $"以下の項目を削除しますか？\n\n" +
                $"チャンネル: {video.AuthorUsername}\n" +
                $"タイトル: {video.Title}\n" +
                $"状態: {video.Status}",
                "削除確認",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
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
