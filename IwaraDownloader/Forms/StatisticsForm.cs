using IwaraDownloader.Models;
using IwaraDownloader.Services;

namespace IwaraDownloader.Forms
{
    /// <summary>
    /// 統計ダッシュボードフォーム
    /// </summary>
    public partial class StatisticsForm : Form
    {
        private readonly DatabaseService _database;

        public StatisticsForm()
        {
            InitializeComponent();
            _database = DatabaseService.Instance;
        }

        private void StatisticsForm_Load(object sender, EventArgs e)
        {
            LoadStatistics();
        }

        /// <summary>
        /// 統計情報を読み込み
        /// </summary>
        private void LoadStatistics()
        {
            try
            {
                var stats = _database.GetDownloadStatistics();

                // 概要
                lblTotalVideos.Text = stats.TotalVideoCount.ToString("N0");
                lblCompletedVideos.Text = stats.CompletedCount.ToString("N0");
                lblFailedVideos.Text = stats.FailedCount.ToString("N0");
                lblPendingVideos.Text = stats.PendingCount.ToString("N0");
                lblTotalSize.Text = stats.TotalDownloadedSizeFormatted;

                // チャンネル
                lblTotalChannels.Text = stats.ChannelCount.ToString("N0");
                lblActiveChannels.Text = stats.EnabledChannelCount.ToString("N0");

                // 成功率
                if (stats.TotalVideoCount > 0)
                {
                    var successRate = (double)stats.CompletedCount / stats.TotalVideoCount * 100;
                    lblSuccessRate.Text = $"{successRate:F1}%";
                    progressSuccess.Value = Math.Min(100, (int)successRate);
                }
                else
                {
                    lblSuccessRate.Text = "-";
                    progressSuccess.Value = 0;
                }

                // 詳細統計
                LoadDetailedStatistics();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"統計情報の読み込みに失敗しました:\n{ex.Message}",
                    "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 詳細統計を読み込み
        /// </summary>
        private void LoadDetailedStatistics()
        {
            // チャンネル別統計
            var users = _database.GetAllSubscribedUsers();
            var channelStats = new List<ChannelStatItem>();

            foreach (var user in users)
            {
                var videos = _database.GetVideosBySubscribedUser(user.Id);
                var completed = videos.Count(v => v.Status == DownloadStatus.Completed);
                var failed = videos.Count(v => v.Status == DownloadStatus.Failed);
                var totalSize = videos.Where(v => v.Status == DownloadStatus.Completed).Sum(v => v.FileSize);

                channelStats.Add(new ChannelStatItem
                {
                    Username = user.Username,
                    TotalVideos = videos.Count,
                    CompletedVideos = completed,
                    FailedVideos = failed,
                    TotalSize = totalSize,
                    IsEnabled = user.IsEnabled
                });
            }

            // ソート（DL数降順）
            dgvChannelStats.DataSource = channelStats
                .OrderByDescending(c => c.CompletedVideos)
                .ToList();

            // カラム設定
            if (dgvChannelStats.Columns.Count > 0)
            {
                dgvChannelStats.Columns["Username"].HeaderText = "チャンネル";
                dgvChannelStats.Columns["Username"].Width = 150;
                dgvChannelStats.Columns["TotalVideos"].HeaderText = "総動画数";
                dgvChannelStats.Columns["TotalVideos"].Width = 80;
                dgvChannelStats.Columns["CompletedVideos"].HeaderText = "完了";
                dgvChannelStats.Columns["CompletedVideos"].Width = 60;
                dgvChannelStats.Columns["FailedVideos"].HeaderText = "失敗";
                dgvChannelStats.Columns["FailedVideos"].Width = 60;
                dgvChannelStats.Columns["TotalSizeFormatted"].HeaderText = "サイズ";
                dgvChannelStats.Columns["TotalSizeFormatted"].Width = 80;
                dgvChannelStats.Columns["StatusText"].HeaderText = "状態";
                dgvChannelStats.Columns["StatusText"].Width = 60;

                // 非表示カラム
                dgvChannelStats.Columns["TotalSize"].Visible = false;
                dgvChannelStats.Columns["IsEnabled"].Visible = false;
            }

            // 日別統計
            LoadDailyStatistics();
        }

        /// <summary>
        /// 日別統計を読み込み
        /// </summary>
        private void LoadDailyStatistics()
        {
            var videos = _database.GetVideosByStatus(DownloadStatus.Completed);
            
            var dailyStats = videos
                .Where(v => v.DownloadedAt.HasValue)
                .GroupBy(v => v.DownloadedAt!.Value.Date)
                .Select(g => new DailyStatItem
                {
                    Date = g.Key,
                    Count = g.Count(),
                    TotalSize = g.Sum(v => v.FileSize)
                })
                .OrderByDescending(d => d.Date)
                .Take(30) // 直近30日
                .ToList();

            dgvDailyStats.DataSource = dailyStats;

            // カラム設定
            if (dgvDailyStats.Columns.Count > 0)
            {
                dgvDailyStats.Columns["DateFormatted"].HeaderText = "日付";
                dgvDailyStats.Columns["DateFormatted"].Width = 100;
                dgvDailyStats.Columns["Count"].HeaderText = "DL数";
                dgvDailyStats.Columns["Count"].Width = 60;
                dgvDailyStats.Columns["TotalSizeFormatted"].HeaderText = "サイズ";
                dgvDailyStats.Columns["TotalSizeFormatted"].Width = 80;

                // 非表示カラム
                dgvDailyStats.Columns["Date"].Visible = false;
                dgvDailyStats.Columns["TotalSize"].Visible = false;
            }
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            LoadStatistics();
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// CSVエクスポート
        /// </summary>
        private void btnExportCsv_Click(object sender, EventArgs e)
        {
            using var dialog = new SaveFileDialog
            {
                Title = "統計をエクスポート",
                Filter = "CSVファイル (*.csv)|*.csv",
                FileName = $"iwara_stats_{DateTime.Now:yyyyMMdd}.csv"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var stats = _database.GetDownloadStatistics();
                    var users = _database.GetAllSubscribedUsers();

                    using var writer = new StreamWriter(dialog.FileName, false, System.Text.Encoding.UTF8);
                    
                    // 概要
                    writer.WriteLine("# 概要統計");
                    writer.WriteLine($"総動画数,{stats.TotalVideoCount}");
                    writer.WriteLine($"完了,{stats.CompletedCount}");
                    writer.WriteLine($"失敗,{stats.FailedCount}");
                    writer.WriteLine($"待機中,{stats.PendingCount}");
                    writer.WriteLine($"総サイズ,{stats.TotalDownloadedSizeFormatted}");
                    writer.WriteLine($"チャンネル数,{stats.ChannelCount}");
                    writer.WriteLine();

                    // チャンネル別
                    writer.WriteLine("# チャンネル別統計");
                    writer.WriteLine("チャンネル,総動画数,完了,失敗,サイズ,状態");
                    
                    foreach (var user in users)
                    {
                        var videos = _database.GetVideosBySubscribedUser(user.Id);
                        var completed = videos.Count(v => v.Status == DownloadStatus.Completed);
                        var failed = videos.Count(v => v.Status == DownloadStatus.Failed);
                        var totalSize = videos.Where(v => v.Status == DownloadStatus.Completed).Sum(v => v.FileSize);

                        writer.WriteLine($"{EscapeCsv(user.Username)},{videos.Count},{completed},{failed},{totalSize},{(user.IsEnabled ? "有効" : "無効")}");
                    }

                    MessageBox.Show("エクスポートしました。", "完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"エクスポートに失敗しました:\n{ex.Message}",
                        "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }
            return value;
        }
    }

    /// <summary>
    /// チャンネル統計アイテム
    /// </summary>
    public class ChannelStatItem
    {
        public string Username { get; set; } = "";
        public int TotalVideos { get; set; }
        public int CompletedVideos { get; set; }
        public int FailedVideos { get; set; }
        public long TotalSize { get; set; }
        public bool IsEnabled { get; set; }

        public string TotalSizeFormatted
        {
            get
            {
                if (TotalSize <= 0) return "-";
                string[] sizes = { "B", "KB", "MB", "GB" };
                int order = 0;
                double size = TotalSize;
                while (size >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    size /= 1024;
                }
                return $"{size:0.##} {sizes[order]}";
            }
        }

        public string StatusText => IsEnabled ? "有効" : "無効";
    }

    /// <summary>
    /// 日別統計アイテム
    /// </summary>
    public class DailyStatItem
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
        public long TotalSize { get; set; }

        public string DateFormatted => Date.ToString("yyyy/MM/dd");

        public string TotalSizeFormatted
        {
            get
            {
                if (TotalSize <= 0) return "-";
                string[] sizes = { "B", "KB", "MB", "GB" };
                int order = 0;
                double size = TotalSize;
                while (size >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    size /= 1024;
                }
                return $"{size:0.##} {sizes[order]}";
            }
        }
    }
}
