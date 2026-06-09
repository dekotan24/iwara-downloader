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

                // 完了/失敗の全件を1回ずつ取得して各集計で使い回す
                // (既存の日別統計と同じく全件ロードするが、ロード回数は最小に抑える)
                var completed = _database.GetVideosByStatus(DownloadStatus.Completed);
                var failed = _database.GetVideosByStatus(DownloadStatus.Failed);

                // 詳細統計
                LoadDetailedStatistics();
                LoadDailyStatistics(completed);
                LoadErrorStatistics(failed);
                LoadRetryStatistics(failed);
                LoadMonthlyStatistics(completed);
                LoadSizeDistribution(completed);
                LoadDurationDistribution(completed);
                LoadTagRanking(completed);
                LoadRatingStatistics(completed);
                LoadSiteStatistics(completed);
                LoadAuthorRanking(completed);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"統計情報の読み込みに失敗しました:\n{ex.Message}",
                    "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 詳細統計を読み込み(チャンネル別)
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

            // ソート(DL数降順)
            dgvChannelStats.DataSource = channelStats
                .OrderByDescending(c => c.CompletedVideos)
                .ToList();

            // カラム設定
            // 注意: AutoSizeColumnsMode = Fill のため Width は無視される。FillWeight で相対幅を指定する。
            // また dgv は非アクティブタブ上にあり Load 時点でハンドル未生成のため、
            // Width を代入すると set_Thickness 内部で NullReferenceException が発生する (Fill では FillWeight が正解)。
            if (dgvChannelStats.Columns.Count > 0)
            {
                dgvChannelStats.Columns["Username"].HeaderText = "チャンネル";
                dgvChannelStats.Columns["Username"].FillWeight = 150;
                dgvChannelStats.Columns["TotalVideos"].HeaderText = "総動画数";
                dgvChannelStats.Columns["TotalVideos"].FillWeight = 80;
                dgvChannelStats.Columns["CompletedVideos"].HeaderText = "完了";
                dgvChannelStats.Columns["CompletedVideos"].FillWeight = 60;
                dgvChannelStats.Columns["FailedVideos"].HeaderText = "失敗";
                dgvChannelStats.Columns["FailedVideos"].FillWeight = 60;
                dgvChannelStats.Columns["TotalSizeFormatted"].HeaderText = "サイズ";
                dgvChannelStats.Columns["TotalSizeFormatted"].FillWeight = 80;
                dgvChannelStats.Columns["StatusText"].HeaderText = "状態";
                dgvChannelStats.Columns["StatusText"].FillWeight = 60;

                // 非表示カラム
                dgvChannelStats.Columns["TotalSize"].Visible = false;
                dgvChannelStats.Columns["IsEnabled"].Visible = false;
            }
        }

        /// <summary>
        /// 日別統計を読み込み
        /// </summary>
        private void LoadDailyStatistics(List<VideoInfo> completed)
        {
            var dailyStats = completed
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
                dgvDailyStats.Columns["DateFormatted"].FillWeight = 100;
                dgvDailyStats.Columns["Count"].HeaderText = "DL数";
                dgvDailyStats.Columns["Count"].FillWeight = 60;
                dgvDailyStats.Columns["TotalSizeFormatted"].HeaderText = "サイズ";
                dgvDailyStats.Columns["TotalSizeFormatted"].FillWeight = 80;

                // 非表示カラム
                dgvDailyStats.Columns["Date"].Visible = false;
                dgvDailyStats.Columns["TotalSize"].Visible = false;
            }
        }

        /// <summary>
        /// エラー別失敗統計を読み込み
        /// </summary>
        private void LoadErrorStatistics(List<VideoInfo> failed)
        {
            var totalFailed = failed.Count;
            var errorStats = failed
                .GroupBy(v => CategorizeError(v.LastErrorMessage))
                .Select(g => new ErrorStatItem
                {
                    Category = g.Key,
                    Count = g.Count(),
                    Percentage = totalFailed > 0 ? (double)g.Count() / totalFailed * 100 : 0,
                    SampleMessage = TrimMessage(g.First().LastErrorMessage)
                })
                .OrderByDescending(e => e.Count)
                .ToList();

            dgvErrorStats.DataSource = errorStats;

            if (dgvErrorStats.Columns.Count > 0)
            {
                dgvErrorStats.Columns["Category"].HeaderText = "エラー種別";
                dgvErrorStats.Columns["Category"].FillWeight = 110;
                dgvErrorStats.Columns["Count"].HeaderText = "件数";
                dgvErrorStats.Columns["Count"].FillWeight = 50;
                dgvErrorStats.Columns["PercentageFormatted"].HeaderText = "割合";
                dgvErrorStats.Columns["PercentageFormatted"].FillWeight = 50;
                dgvErrorStats.Columns["SampleMessage"].HeaderText = "代表メッセージ";
                dgvErrorStats.Columns["SampleMessage"].FillWeight = 200;

                dgvErrorStats.Columns["Percentage"].Visible = false;
            }
        }

        /// <summary>
        /// リトライ回数別の失敗統計を読み込み
        /// </summary>
        private void LoadRetryStatistics(List<VideoInfo> failed)
        {
            var retryStats = failed
                .GroupBy(v => v.RetryCount)
                .Select(g => new RetryStatItem
                {
                    RetryCount = g.Key,
                    Count = g.Count()
                })
                .OrderBy(r => r.RetryCount)
                .ToList();

            dgvRetryStats.DataSource = retryStats;

            if (dgvRetryStats.Columns.Count > 0)
            {
                dgvRetryStats.Columns["RetryCountFormatted"].HeaderText = "リトライ回数";
                dgvRetryStats.Columns["RetryCountFormatted"].FillWeight = 100;
                dgvRetryStats.Columns["Count"].HeaderText = "失敗動画数";
                dgvRetryStats.Columns["Count"].FillWeight = 100;

                dgvRetryStats.Columns["RetryCount"].Visible = false;
            }
        }

        /// <summary>
        /// 月別統計を読み込み(累積付き)
        /// </summary>
        private void LoadMonthlyStatistics(List<VideoInfo> completed)
        {
            var ordered = completed
                .Where(v => v.DownloadedAt.HasValue)
                .GroupBy(v => new DateTime(v.DownloadedAt!.Value.Year, v.DownloadedAt.Value.Month, 1))
                .Select(g => new MonthlyStatItem
                {
                    Month = g.Key,
                    Count = g.Count(),
                    TotalSize = g.Sum(v => v.FileSize)
                })
                .OrderBy(m => m.Month)
                .ToList();

            // 累積を時系列昇順で計算
            long cumCount = 0;
            long cumSize = 0;
            foreach (var m in ordered)
            {
                cumCount += m.Count;
                cumSize += m.TotalSize;
                m.CumulativeCount = cumCount;
                m.CumulativeSize = cumSize;
            }

            // 表示は新しい月が上
            dgvMonthlyStats.DataSource = ordered
                .OrderByDescending(m => m.Month)
                .ToList();

            if (dgvMonthlyStats.Columns.Count > 0)
            {
                dgvMonthlyStats.Columns["MonthFormatted"].HeaderText = "年月";
                dgvMonthlyStats.Columns["MonthFormatted"].FillWeight = 80;
                dgvMonthlyStats.Columns["Count"].HeaderText = "DL数";
                dgvMonthlyStats.Columns["Count"].FillWeight = 60;
                dgvMonthlyStats.Columns["TotalSizeFormatted"].HeaderText = "サイズ";
                dgvMonthlyStats.Columns["TotalSizeFormatted"].FillWeight = 80;
                dgvMonthlyStats.Columns["CumulativeCount"].HeaderText = "累積DL数";
                dgvMonthlyStats.Columns["CumulativeCount"].FillWeight = 80;
                dgvMonthlyStats.Columns["CumulativeSizeFormatted"].HeaderText = "累積サイズ";
                dgvMonthlyStats.Columns["CumulativeSizeFormatted"].FillWeight = 90;

                dgvMonthlyStats.Columns["Month"].Visible = false;
                dgvMonthlyStats.Columns["TotalSize"].Visible = false;
                dgvMonthlyStats.Columns["CumulativeSize"].Visible = false;
            }
        }

        // サイズ分布のビン定義(順序保持用)
        private static readonly string[] SizeBinOrder =
            { "～100MB", "100-300MB", "300-500MB", "500MB-1GB", "1-2GB", "2GB～" };

        /// <summary>
        /// サイズ分布を読み込み
        /// </summary>
        private void LoadSizeDistribution(List<VideoInfo> completed)
        {
            var grouped = completed
                .GroupBy(v => SizeBin(v.FileSize))
                .ToDictionary(g => g.Key, g => new { Count = g.Count(), Size = g.Sum(v => v.FileSize) });

            var sizeStats = SizeBinOrder
                .Where(bin => grouped.ContainsKey(bin))
                .Select(bin => new DistributionStatItem
                {
                    Bucket = bin,
                    Count = grouped[bin].Count,
                    TotalSize = grouped[bin].Size
                })
                .ToList();

            dgvSizeStats.DataSource = sizeStats;

            if (dgvSizeStats.Columns.Count > 0)
            {
                dgvSizeStats.Columns["Bucket"].HeaderText = "サイズ帯";
                dgvSizeStats.Columns["Bucket"].FillWeight = 100;
                dgvSizeStats.Columns["Count"].HeaderText = "動画数";
                dgvSizeStats.Columns["Count"].FillWeight = 70;
                dgvSizeStats.Columns["TotalSizeFormatted"].HeaderText = "合計サイズ";
                dgvSizeStats.Columns["TotalSizeFormatted"].FillWeight = 90;

                dgvSizeStats.Columns["TotalSize"].Visible = false;
            }
        }

        // 動画長分布のビン定義(順序保持用)
        private static readonly string[] DurationBinOrder =
            { "～1分", "1-5分", "5-10分", "10-30分", "30分～" };

        /// <summary>
        /// 動画長分布を読み込み
        /// </summary>
        private void LoadDurationDistribution(List<VideoInfo> completed)
        {
            var grouped = completed
                .GroupBy(v => DurationBin(v.DurationSeconds))
                .ToDictionary(g => g.Key, g => new { Count = g.Count(), Sec = g.Sum(v => (long)v.DurationSeconds) });

            var durStats = DurationBinOrder
                .Where(bin => grouped.ContainsKey(bin))
                .Select(bin => new DurationStatItem
                {
                    Bucket = bin,
                    Count = grouped[bin].Count,
                    TotalSeconds = grouped[bin].Sec
                })
                .ToList();

            dgvDurationStats.DataSource = durStats;

            if (dgvDurationStats.Columns.Count > 0)
            {
                dgvDurationStats.Columns["Bucket"].HeaderText = "再生時間帯";
                dgvDurationStats.Columns["Bucket"].FillWeight = 100;
                dgvDurationStats.Columns["Count"].HeaderText = "動画数";
                dgvDurationStats.Columns["Count"].FillWeight = 70;
                dgvDurationStats.Columns["TotalDurationFormatted"].HeaderText = "合計再生時間";
                dgvDurationStats.Columns["TotalDurationFormatted"].FillWeight = 90;

                dgvDurationStats.Columns["TotalSeconds"].Visible = false;
            }
        }

        /// <summary>
        /// タグ別ランキングを読み込み(上位50)
        /// </summary>
        private void LoadTagRanking(List<VideoInfo> completed)
        {
            var tagCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var v in completed)
            {
                foreach (var tag in v.TagList)
                {
                    tagCounts[tag] = tagCounts.GetValueOrDefault(tag) + 1;
                }
            }

            var tagStats = tagCounts
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key)
                .Take(50)
                .Select((kv, i) => new TagStatItem
                {
                    Rank = i + 1,
                    Tag = kv.Key,
                    Count = kv.Value
                })
                .ToList();

            dgvTagStats.DataSource = tagStats;

            if (dgvTagStats.Columns.Count > 0)
            {
                dgvTagStats.Columns["Rank"].HeaderText = "#";
                dgvTagStats.Columns["Rank"].FillWeight = 40;
                dgvTagStats.Columns["Tag"].HeaderText = "タグ";
                dgvTagStats.Columns["Tag"].FillWeight = 180;
                dgvTagStats.Columns["Count"].HeaderText = "動画数";
                dgvTagStats.Columns["Count"].FillWeight = 70;
            }
        }

        /// <summary>
        /// Rating(general/ecchi)別統計を読み込み
        /// </summary>
        private void LoadRatingStatistics(List<VideoInfo> completed)
        {
            var total = completed.Count;
            var ratingStats = completed
                .GroupBy(v => string.IsNullOrEmpty(v.Rating) ? "未取得" : v.Rating)
                .Select(g => new RatingStatItem
                {
                    Rating = g.Key,
                    Count = g.Count(),
                    Percentage = total > 0 ? (double)g.Count() / total * 100 : 0
                })
                .OrderByDescending(r => r.Count)
                .ToList();

            dgvRatingStats.DataSource = ratingStats;

            if (dgvRatingStats.Columns.Count > 0)
            {
                dgvRatingStats.Columns["Rating"].HeaderText = "Rating";
                dgvRatingStats.Columns["Rating"].FillWeight = 100;
                dgvRatingStats.Columns["Count"].HeaderText = "動画数";
                dgvRatingStats.Columns["Count"].FillWeight = 70;
                dgvRatingStats.Columns["PercentageFormatted"].HeaderText = "割合";
                dgvRatingStats.Columns["PercentageFormatted"].FillWeight = 70;

                dgvRatingStats.Columns["Percentage"].Visible = false;
            }
        }

        /// <summary>
        /// サイト(iwara.tv/iwara.ai)別統計を読み込み
        /// </summary>
        private void LoadSiteStatistics(List<VideoInfo> completed)
        {
            var siteStats = completed
                .GroupBy(v => string.IsNullOrEmpty(v.Site) ? "www.iwara.tv (旧)" : v.Site)
                .Select(g => new SiteStatItem
                {
                    Site = g.Key,
                    Count = g.Count(),
                    TotalSize = g.Sum(v => v.FileSize)
                })
                .OrderByDescending(s => s.Count)
                .ToList();

            dgvSiteStats.DataSource = siteStats;

            if (dgvSiteStats.Columns.Count > 0)
            {
                dgvSiteStats.Columns["Site"].HeaderText = "サイト";
                dgvSiteStats.Columns["Site"].FillWeight = 130;
                dgvSiteStats.Columns["Count"].HeaderText = "動画数";
                dgvSiteStats.Columns["Count"].FillWeight = 70;
                dgvSiteStats.Columns["TotalSizeFormatted"].HeaderText = "サイズ";
                dgvSiteStats.Columns["TotalSizeFormatted"].FillWeight = 80;

                dgvSiteStats.Columns["TotalSize"].Visible = false;
            }
        }

        /// <summary>
        /// 投稿者別ランキングを読み込み(上位50)
        /// </summary>
        private void LoadAuthorRanking(List<VideoInfo> completed)
        {
            var authorStats = completed
                .Where(v => !string.IsNullOrEmpty(v.AuthorUsername))
                .GroupBy(v => v.AuthorUsername)
                .Select(g => new AuthorStatItem
                {
                    Username = g.Key,
                    Count = g.Count(),
                    TotalSize = g.Sum(v => v.FileSize)
                })
                .OrderByDescending(a => a.Count)
                .ThenByDescending(a => a.TotalSize)
                .Take(50)
                .Select((a, i) => { a.Rank = i + 1; return a; })
                .ToList();

            dgvAuthorStats.DataSource = authorStats;

            if (dgvAuthorStats.Columns.Count > 0)
            {
                dgvAuthorStats.Columns["Rank"].HeaderText = "#";
                dgvAuthorStats.Columns["Rank"].FillWeight = 40;
                dgvAuthorStats.Columns["Username"].HeaderText = "投稿者";
                dgvAuthorStats.Columns["Username"].FillWeight = 180;
                dgvAuthorStats.Columns["Count"].HeaderText = "動画数";
                dgvAuthorStats.Columns["Count"].FillWeight = 70;
                dgvAuthorStats.Columns["TotalSizeFormatted"].HeaderText = "サイズ";
                dgvAuthorStats.Columns["TotalSizeFormatted"].FillWeight = 80;

                dgvAuthorStats.Columns["TotalSize"].Visible = false;
            }
        }

        #region Helpers

        /// <summary>
        /// エラーメッセージをカテゴリに分類する。
        /// DownloadManager が投げる iwara 特有コード(VIDEO_NOT_FOUND 等)と
        /// 汎用例外メッセージ(404/timeout 等)の両方を拾う。
        /// </summary>
        private static string CategorizeError(string? msg)
        {
            if (string.IsNullOrWhiteSpace(msg)) return "不明";
            var m = msg.ToLowerInvariant();

            if (msg.Contains("VIDEO_NOT_FOUND") || msg.Contains("errors.notFound")
                || m.Contains("video not found") || m.Contains("404") || m.Contains("not found"))
                return "動画削除/非公開 (404)";

            if (msg.Contains("PRIVATE_VIDEO") || msg.Contains("errors.privateVideo")
                || m.Contains("private video") || m.Contains("403") || m.Contains("forbidden"))
                return "フレンド限定/拒否 (403)";

            if (msg.Contains("CDN_UNAVAILABLE") || m.Contains("all cdn candidates failed"))
                return "CDN利用不可";

            if (m.Contains("429") || m.Contains("rate limit") || m.Contains("too many"))
                return "レート制限 (429)";

            if (m.Contains("401") || m.Contains("unauthorized"))
                return "認証エラー (401)";

            if (m.Contains("timeout") || m.Contains("timed out"))
                return "タイムアウト";

            if (m.Contains("disk") || m.Contains("space") || m.Contains("容量"))
                return "ディスク容量不足";

            if (m.Contains("ioexception") || m.Contains("being used")
                || m.Contains("access to the path") || m.Contains("denied"))
                return "ファイルI/O";

            if (m.Contains("connection") || m.Contains("network") || m.Contains("socket")
                || m.Contains("ssl") || m.Contains("no such host") || m.Contains("name or service"))
                return "ネットワーク";

            if (m.Contains("外部動画") || m.Contains("スキップ"))
                return "外部動画スキップ";

            return "その他";
        }

        /// <summary>
        /// 代表メッセージを表示用に短く切り詰める
        /// </summary>
        private static string TrimMessage(string? msg)
        {
            if (string.IsNullOrWhiteSpace(msg)) return "";
            var oneLine = msg.Replace("\r", " ").Replace("\n", " ").Trim();
            return oneLine.Length > 80 ? oneLine.Substring(0, 80) + "…" : oneLine;
        }

        private static string SizeBin(long bytes)
        {
            double mb = bytes / 1024.0 / 1024.0;
            if (mb < 100) return "～100MB";
            if (mb < 300) return "100-300MB";
            if (mb < 500) return "300-500MB";
            if (mb < 1024) return "500MB-1GB";
            if (mb < 2048) return "1-2GB";
            return "2GB～";
        }

        private static string DurationBin(int seconds)
        {
            if (seconds < 60) return "～1分";
            if (seconds < 300) return "1-5分";
            if (seconds < 600) return "5-10分";
            if (seconds < 1800) return "10-30分";
            return "30分～";
        }

        /// <summary>
        /// バイト数を表示用にフォーマット
        /// </summary>
        internal static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "-";
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }

        #endregion

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
                    var completed = _database.GetVideosByStatus(DownloadStatus.Completed);
                    var failed = _database.GetVideosByStatus(DownloadStatus.Failed);

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
                        var c = videos.Count(v => v.Status == DownloadStatus.Completed);
                        var f = videos.Count(v => v.Status == DownloadStatus.Failed);
                        var totalSize = videos.Where(v => v.Status == DownloadStatus.Completed).Sum(v => v.FileSize);

                        writer.WriteLine($"{EscapeCsv(user.Username)},{videos.Count},{c},{f},{totalSize},{(user.IsEnabled ? "有効" : "無効")}");
                    }
                    writer.WriteLine();

                    // エラー別失敗内訳
                    writer.WriteLine("# エラー別失敗内訳");
                    writer.WriteLine("エラー種別,件数");
                    foreach (var g in failed.GroupBy(v => CategorizeError(v.LastErrorMessage))
                                            .OrderByDescending(g => g.Count()))
                    {
                        writer.WriteLine($"{EscapeCsv(g.Key)},{g.Count()}");
                    }
                    writer.WriteLine();

                    // 月別推移
                    writer.WriteLine("# 月別推移");
                    writer.WriteLine("年月,DL数,サイズ(byte)");
                    foreach (var g in completed.Where(v => v.DownloadedAt.HasValue)
                                               .GroupBy(v => new DateTime(v.DownloadedAt!.Value.Year, v.DownloadedAt.Value.Month, 1))
                                               .OrderBy(g => g.Key))
                    {
                        writer.WriteLine($"{g.Key:yyyy/MM},{g.Count()},{g.Sum(v => v.FileSize)}");
                    }
                    writer.WriteLine();

                    // 投稿者ランキング(上位50)
                    writer.WriteLine("# 投稿者ランキング(上位50)");
                    writer.WriteLine("順位,投稿者,動画数,サイズ(byte)");
                    int rank = 1;
                    foreach (var g in completed.Where(v => !string.IsNullOrEmpty(v.AuthorUsername))
                                               .GroupBy(v => v.AuthorUsername)
                                               .OrderByDescending(g => g.Count())
                                               .Take(50))
                    {
                        writer.WriteLine($"{rank++},{EscapeCsv(g.Key)},{g.Count()},{g.Sum(v => v.FileSize)}");
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

        public string TotalSizeFormatted => StatisticsForm.FormatBytes(TotalSize);
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
        public string TotalSizeFormatted => StatisticsForm.FormatBytes(TotalSize);
    }

    /// <summary>
    /// エラー別失敗統計アイテム
    /// </summary>
    public class ErrorStatItem
    {
        // プロパティ宣言順 = DataGridView の列表示順(非表示列はスキップされる)
        public string Category { get; set; } = "";
        public int Count { get; set; }
        public string PercentageFormatted => $"{Percentage:F1}%";
        public string SampleMessage { get; set; } = "";
        public double Percentage { get; set; } // 非表示(ソート用の生値)
    }

    /// <summary>
    /// リトライ回数別統計アイテム
    /// </summary>
    public class RetryStatItem
    {
        public string RetryCountFormatted => RetryCount == 0 ? "0回(初回失敗)" : $"{RetryCount}回";
        public int Count { get; set; }
        public int RetryCount { get; set; } // 非表示(ソート用の生値)
    }

    /// <summary>
    /// 月別統計アイテム(累積付き)
    /// </summary>
    public class MonthlyStatItem
    {
        // プロパティ宣言順 = 列表示順
        public string MonthFormatted => Month.ToString("yyyy/MM");
        public int Count { get; set; }
        public string TotalSizeFormatted => StatisticsForm.FormatBytes(TotalSize);
        public long CumulativeCount { get; set; }
        public string CumulativeSizeFormatted => StatisticsForm.FormatBytes(CumulativeSize);

        // 非表示(ソート/集計用の生値)
        public DateTime Month { get; set; }
        public long TotalSize { get; set; }
        public long CumulativeSize { get; set; }
    }

    /// <summary>
    /// サイズ分布アイテム
    /// </summary>
    public class DistributionStatItem
    {
        public string Bucket { get; set; } = "";
        public int Count { get; set; }
        public long TotalSize { get; set; }

        public string TotalSizeFormatted => StatisticsForm.FormatBytes(TotalSize);
    }

    /// <summary>
    /// 動画長分布アイテム
    /// </summary>
    public class DurationStatItem
    {
        public string Bucket { get; set; } = "";
        public int Count { get; set; }
        public long TotalSeconds { get; set; }

        public string TotalDurationFormatted
        {
            get
            {
                var ts = TimeSpan.FromSeconds(TotalSeconds);
                if (ts.TotalHours >= 1)
                    return $"{(int)ts.TotalHours}時間{ts.Minutes}分";
                return $"{ts.Minutes}分{ts.Seconds}秒";
            }
        }
    }

    /// <summary>
    /// タグランキングアイテム
    /// </summary>
    public class TagStatItem
    {
        public int Rank { get; set; }
        public string Tag { get; set; } = "";
        public int Count { get; set; }
    }

    /// <summary>
    /// Rating統計アイテム
    /// </summary>
    public class RatingStatItem
    {
        public string Rating { get; set; } = "";
        public int Count { get; set; }
        public double Percentage { get; set; }

        public string PercentageFormatted => $"{Percentage:F1}%";
    }

    /// <summary>
    /// サイト別統計アイテム
    /// </summary>
    public class SiteStatItem
    {
        public string Site { get; set; } = "";
        public int Count { get; set; }
        public long TotalSize { get; set; }

        public string TotalSizeFormatted => StatisticsForm.FormatBytes(TotalSize);
    }

    /// <summary>
    /// 投稿者ランキングアイテム
    /// </summary>
    public class AuthorStatItem
    {
        public int Rank { get; set; }
        public string Username { get; set; } = "";
        public int Count { get; set; }
        public long TotalSize { get; set; }

        public string TotalSizeFormatted => StatisticsForm.FormatBytes(TotalSize);
    }
}
