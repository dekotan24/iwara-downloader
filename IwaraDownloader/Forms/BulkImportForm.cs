using IwaraDownloader.Models;
using IwaraDownloader.Services;
using IwaraDownloader.Utils;
using System.Text.RegularExpressions;

namespace IwaraDownloader.Forms
{
    /// <summary>
    /// URL一括インポートフォーム。
    /// 動画URL / 動画ID と、ユーザー(プロフィール)URL の両方に対応 (iwara.tv / iwara.ai)。
    /// </summary>
    public partial class BulkImportForm : Form
    {
        private readonly DatabaseService _database;
        private readonly DownloadManager? _downloadManager;

        /// <summary>インポートされた動画リスト</summary>
        public List<VideoInfo> ImportedVideos { get; } = new();

        /// <summary>重複としてスキップされた数</summary>
        public int DuplicateCount { get; private set; }

        public BulkImportForm(DownloadManager? downloadManager = null)
        {
            InitializeComponent();
            Utils.Localizer.Apply(this);
            _database = DatabaseService.Instance;
            _downloadManager = downloadManager;
        }

        private void BulkImportForm_Load(object sender, EventArgs e)
        {
            UpdateStats();
        }

        /// <summary>
        /// クリップボードから貼り付け
        /// </summary>
        private void btnPaste_Click(object sender, EventArgs e)
        {
            if (Clipboard.ContainsText())
            {
                var clipText = Clipboard.GetText();
                if (!string.IsNullOrEmpty(txtUrls.Text) && !txtUrls.Text.EndsWith(Environment.NewLine))
                {
                    txtUrls.AppendText(Environment.NewLine);
                }
                txtUrls.AppendText(clipText);
                UpdateStats();
            }
        }

        /// <summary>
        /// ファイルから読み込み
        /// </summary>
        private void btnLoadFile_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = L.T("BulkImportForm_D014"),
                Filter = L.T("BulkImportForm_D001")
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var content = File.ReadAllText(dialog.FileName);
                    txtUrls.Text = content;
                    UpdateStats();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(L.T("BulkImportForm_D002", ex.Message), 
                        L.T("BulkImportForm_D003"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// クリア
        /// </summary>
        private void btnClear_Click(object sender, EventArgs e)
        {
            txtUrls.Clear();
            UpdateStats();
        }

        /// <summary>
        /// テキスト変更時に統計更新
        /// </summary>
        private void txtUrls_TextChanged(object sender, EventArgs e)
        {
            UpdateStats();
        }

        /// <summary>
        /// 統計を更新
        /// </summary>
        private void UpdateStats()
        {
            var (videos, profiles) = ExtractEntries(txtUrls.Text);
            lblStats.Text = L.T("BulkImportForm_D004", videos.Count, profiles.Count);
        }

        /// <summary>
        /// テキストから「動画(id+url+site)」と「ユーザープロフィールURL」を抽出する。
        /// iwara.tv / iwara.ai 両対応。プロフィールURL・動画URL・裸の動画IDを解釈する。
        /// </summary>
        private (List<VideoEntry> Videos, List<string> Profiles) ExtractEntries(string text)
        {
            var videos = new List<VideoEntry>();
            var seenVideo = new HashSet<string>();
            var profiles = new List<string>();
            var seenProfile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(text))
                return (videos, profiles);

            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                    continue;

                // 1. ユーザープロフィールURL (iwara.tv/ai/profile/xxx)
                if (Helpers.IsUserProfileUrl(trimmed))
                {
                    var uname = Helpers.ExtractUsernameFromUrl(trimmed) ?? trimmed;
                    var site = Helpers.ExtractSiteFromUrl(trimmed);
                    var key = $"{uname.ToLowerInvariant()}@{site}";
                    if (seenProfile.Add(key))
                        profiles.Add(trimmed);
                    continue;
                }

                // 2. 動画URL (iwara.tv/ai/video/xxx)
                var vid = Helpers.ExtractVideoIdFromUrl(trimmed);
                if (!string.IsNullOrEmpty(vid))
                {
                    if (seenVideo.Add(vid))
                        videos.Add(new VideoEntry(vid, trimmed, Helpers.ExtractSiteFromUrl(trimmed)));
                    continue;
                }

                // 3. 裸の動画ID (英数字8〜20文字)
                if (trimmed.Length >= 8 && trimmed.Length <= 20 && Regex.IsMatch(trimmed, @"^[a-zA-Z0-9]+$"))
                {
                    if (seenVideo.Add(trimmed))
                        videos.Add(new VideoEntry(trimmed, $"https://{Helpers.SiteTv}/video/{trimmed}", Helpers.SiteTv));
                }
            }

            return (videos, profiles);
        }

        private readonly record struct VideoEntry(string Id, string Url, string Site);

        /// <summary>
        /// インポート実行
        /// </summary>
        private async void btnImport_Click(object sender, EventArgs e)
        {
            var (videos, profiles) = ExtractEntries(txtUrls.Text);

            if (videos.Count == 0 && profiles.Count == 0)
            {
                MessageBox.Show(L.T("BulkImportForm_D005"),
                    L.T("BulkImportForm_D003"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (profiles.Count > 0 && _downloadManager == null)
            {
                MessageBox.Show(L.T("BulkImportForm_D006"),
                    L.T("BulkImportForm_D007"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            if (profiles.Count > 0 && _downloadManager != null && !_downloadManager.IsLoggedIn)
            {
                MessageBox.Show(L.T("BulkImportForm_D008"),
                    L.T("BulkImportForm_D009"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnImport.Enabled = false;
            btnImport.Text = L.T("BulkImportForm_D010");
            progressBar.Visible = true;
            progressBar.Value = 0;
            progressBar.Maximum = Math.Max(1, videos.Count + profiles.Count);

            ImportedVideos.Clear();
            DuplicateCount = 0;
            int addedChannels = 0, channelFailed = 0;

            try
            {
                // --- 動画 (id) の処理 ---
                if (videos.Count > 0)
                {
                    var videoIds = videos.Select(v => v.Id).ToList();
                    var existingIds = _database.GetExistingVideoIds(videoIds);

                    await Task.Run(() =>
                    {
                        foreach (var v in videos)
                        {
                            if (existingIds.Contains(v.Id))
                            {
                                DuplicateCount++;
                            }
                            else
                            {
                                ImportedVideos.Add(new VideoInfo
                                {
                                    VideoId = v.Id,
                                    Title = L.T("BulkImportForm_D015", v.Id),
                                    Url = v.Url,
                                    Site = v.Site,
                                    Status = DownloadStatus.Pending,
                                    CreatedAt = DateTime.Now
                                });
                            }
                            this.Invoke(() => progressBar.Value = Math.Min(progressBar.Value + 1, progressBar.Maximum));
                        }
                    });

                    if (ImportedVideos.Count > 0)
                        _database.AddVideosBatch(ImportedVideos);
                }

                // --- チャンネル (profile) の処理 ---
                // 動画一覧取得は共通キューで 1 件ずつ処理されるのでここではエンキューのみ
                if (profiles.Count > 0 && _downloadManager != null)
                {
                    foreach (var profileUrl in profiles)
                    {
                        if (_downloadManager.EnqueueSubscribedUser(profileUrl))
                            addedChannels++;
                        else
                            channelFailed++;
                        progressBar.Value = Math.Min(progressBar.Value + 1, progressBar.Maximum);
                    }
                }

                // 結果表示
                var channelMsg = addedChannels > 0
                    ? L.T("BulkImportForm_ResultChannels", addedChannels,
                        channelFailed > 0 ? L.T("BulkImportForm_ResultChannelSkip", channelFailed) : "")
                    : channelFailed > 0 ? L.T("BulkImportForm_ResultChannelsAllSkip", channelFailed) : "";
                var message = L.T("BulkImportForm_ResultHeader") +
                    L.T("BulkImportForm_ResultVideos", ImportedVideos.Count, DuplicateCount) +
                    (channelMsg.Length > 0 ? channelMsg : "");

                MessageBox.Show(message, L.T("BulkImportForm_D011"), MessageBoxButtons.OK, MessageBoxIcon.Information);

                if (ImportedVideos.Count > 0 || addedChannels > 0)
                {
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(L.T("BulkImportForm_D012", ex.Message),
                    L.T("BulkImportForm_D003"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnImport.Enabled = true;
                btnImport.Text = L.T("BulkImportForm_D013");
                progressBar.Visible = false;
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
