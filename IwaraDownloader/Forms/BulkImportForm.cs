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
        private CancellationTokenSource? _importCancellation;
        private bool _isImporting;
        private bool _cancelRequested;
        private bool _allowClose;

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
        /// 一括インポートする動画の表示用情報を取得する。
        ///
        /// 一括URLインポートは、従来はAPIを呼ばずに仮タイトルだけでDBへ登録していたため、
        /// その後ダウンロードしない動画は「[未取得] VideoId」のまま残っていた。
        /// ログイン済みなら get_info を使って表示用メタデータを補完し、取得できない場合は
        /// インポート自体を失敗させずに従来の仮タイトルへフォールバックする。
        /// </summary>
        private async Task<VideoInfo> CreateImportedVideoAsync(
            VideoEntry entry, bool immediateDownload, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var video = new VideoInfo
            {
                VideoId = entry.Id,
                Title = L.T("BulkImportForm_D015", entry.Id),
                Url = entry.Url,
                Site = entry.Site,
                // 即DLチェックOFFなら Paused で保存 (Pending だと次回起動時の
                // レジュームで意図せず自動DLされてしまうため。issue #21)
                Status = immediateDownload ? DownloadStatus.Pending : DownloadStatus.Paused,
                CreatedAt = DateTime.Now
            };

            // メタデータ取得にはログイン済みの IwaraApiService が必要。
            // 未ログイン時は従来どおり仮登録し、後のログイン/情報更新で補完できるようにする。
            if (_downloadManager?.IsLoggedIn != true)
                return video;

            try
            {
                // ダウンロードURLやCDN情報は不要なので、get_info の軽い経路を使う。
                var info = await _downloadManager.IwaraApi.GetVideoInfoAsync(
                    entry.Id, entry.Site, cancellationToken);
                if (!info.Success)
                {
                    LoggingService.Instance.Warn(
                        $"一括インポートの動画情報取得に失敗 ({entry.Id}): {info.Error ?? "unknown error"}");
                    return video;
                }

                if (!string.IsNullOrWhiteSpace(info.Title))
                    video.Title = info.Title;
                if (!string.IsNullOrEmpty(info.FileUuid))
                    video.FileUuid = info.FileUuid;
                if (!string.IsNullOrEmpty(info.AuthorUsername))
                    video.AuthorUsername = info.AuthorUsername;
                if (!string.IsNullOrEmpty(info.Rating))
                    video.Rating = info.Rating;
                if (!string.IsNullOrEmpty(info.ThumbnailUrl))
                    video.ThumbnailUrl = info.ThumbnailUrl;
                if (info.DurationSeconds > 0)
                    video.DurationSeconds = info.DurationSeconds;
                if (!string.IsNullOrEmpty(info.EmbedUrl))
                    video.EmbedUrl = info.EmbedUrl;
                if (info.PostedAt.HasValue)
                    video.PostedAt = info.PostedAt;
                if (!string.IsNullOrEmpty(info.ApiRawJson))
                    video.ApiRawJson = info.ApiRawJson;

                // site未指定時の自動フォールバックが将来有効になった場合にも、
                // 保存後のダウンロード先が実際に解決したサイトと一致するようにする。
                if (!string.IsNullOrEmpty(info.ResolvedSite))
                    video.Site = info.ResolvedSite;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // 1件のメタデータ取得失敗で、残りのURLまで取り込めなくならないようにする。
                LoggingService.Instance.Warn(
                    $"一括インポートの動画情報取得中に例外 ({entry.Id}): {ex.Message}");
            }

            return video;
        }

        /// <summary>
        /// 一括追加した動画をダウンロードキューへ投入する。
        /// AddVideosBatch はDBへの保存だけを行い、DownloadManagerのメモリキューは
        /// 更新しないため、ヘッダの即DL設定が有効な場合は保存後に明示的に投入する必要がある。
        /// </summary>
        private void EnqueueImportedVideos(IEnumerable<VideoInfo> videos)
        {
            if (_downloadManager == null) return;

            foreach (var importedVideo in videos)
            {
                // AddVideosBatch は高速化のため VideoInfo.Id を採番して返さない。
                // DownloadManager.UpdateVideo は Id で更新するため、Bulk Import の新規行は
                // DBから再読込して通常の単体登録と同じ実体をキューへ渡す。
                var video = importedVideo.Id > 0
                    ? importedVideo
                    : _database.GetVideoByVideoId(importedVideo.VideoId);
                if (video == null)
                {
                    LoggingService.Instance.Warn(
                        $"一括インポート後の動画再読込に失敗 ({importedVideo.VideoId})");
                    continue;
                }

                SubscribedUser? subscribedUser = null;
                if (video.SubscribedUserId.HasValue)
                    subscribedUser = _database.GetSubscribedUserById(video.SubscribedUserId.Value);

                _downloadManager.EnqueueDownload(
                    video, video.SubscribedUserId.HasValue, subscribedUser);
            }
        }

        /// <summary>
        /// 単発登録と同じく、作者が判明した動画を作者チャンネルへ紐付ける。
        /// 新規動画は一括INSERT前にフィールドへ反映し、既存動画はチャンネル列だけを
        /// 限定更新して、ダウンロード中のステータス等を古いスナップショットで上書きしない。
        /// </summary>
        private void AssociateVideosWithAuthorChannels(
            IEnumerable<VideoInfo> videos,
            IDictionary<string, SubscribedUser> channelCache,
            bool persistExisting)
        {
            foreach (var video in videos)
            {
                var author = video.AuthorUsername?.Trim();
                if (string.IsNullOrEmpty(author)) continue;

                if (!channelCache.TryGetValue(author, out var channel))
                {
                    channel = _database.EnsureChannelForAuthor(author, video.Site);
                    channelCache[author] = channel;
                }

                var channelChanged = video.SubscribedUserId != channel.Id
                    || !string.Equals(video.AuthorUserId, channel.UserId, StringComparison.Ordinal);
                video.AuthorUserId = channel.UserId;
                video.SubscribedUserId = channel.Id;

                if (persistExisting && video.Id > 0 && channelChanged)
                    _database.UpdateVideoChannelAssignment(video.Id, channel.UserId, channel.Id);
            }
        }

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
            btnCancel.Enabled = true;
            btnImport.Text = L.T("BulkImportForm_D010");
            progressBar.Visible = true;
            progressBar.Value = 0;
            progressBar.Maximum = Math.Max(1, videos.Count + profiles.Count);

            ImportedVideos.Clear();
            DuplicateCount = 0;
            int addedChannels = 0, channelFailed = 0;
            bool videosPersisted = false;
            // Bulk Importも通常の単体登録と同じく、ヘッダの即DL設定だけに従う。
            var immediateDownload = SettingsManager.Instance.Settings.ImmediateDownloadOnAdd;
            var videosToEnqueue = new List<VideoInfo>();
            var existingVideos = new List<VideoInfo>();
            var channelCache = new Dictionary<string, SubscribedUser>(StringComparer.OrdinalIgnoreCase);
            using var cancellation = new CancellationTokenSource();
            _importCancellation = cancellation;
            _isImporting = true;
            _cancelRequested = false;

            try
            {
                var cancellationToken = cancellation.Token;

                // --- 動画 (id) の処理 ---
                if (videos.Count > 0)
                {
                    var videoIds = videos.Select(v => v.Id).ToList();
                    var existingIds = _database.GetExistingVideoIds(videoIds);

                    foreach (var v in videos)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (existingIds.Contains(v.Id))
                        {
                            DuplicateCount++;

                            // 以前の一括登録で作者チャンネルへの紐付けが漏れた動画も、
                            // 再インポート時に通常登録と同じ所属へ修復する。
                            var existing = _database.GetVideoByVideoId(v.Id);
                            if (existing != null)
                            {
                                existingVideos.Add(existing);
                                if (existing.Status != DownloadStatus.Completed)
                                {
                                    if (immediateDownload && _downloadManager != null)
                                        videosToEnqueue.Add(existing);
                                }
                            }
                        }
                        else
                        {
                            // API呼び出しは非同期なのでUIスレッドをブロックせず、
                            // 取得に失敗した場合も仮タイトルで取り込みを継続する。
                            var imported = await CreateImportedVideoAsync(
                                v, immediateDownload, cancellationToken);
                            ImportedVideos.Add(imported);
                            if (immediateDownload)
                                videosToEnqueue.Add(imported);
                        }

                        if (!IsDisposed && !Disposing)
                            progressBar.Value = Math.Min(progressBar.Value + 1, progressBar.Maximum);
                    }

                    // 新規動画・既存動画とも、単体登録と同じく作者チャンネルへ所属させる。
                    AssociateVideosWithAuthorChannels(ImportedVideos, channelCache, persistExisting: false);
                    AssociateVideosWithAuthorChannels(existingVideos, channelCache, persistExisting: true);

                    if (ImportedVideos.Count > 0)
                    {
                        _database.AddVideosBatch(ImportedVideos);
                        videosPersisted = true;
                    }

                    // AddVideosBatch はDB保存のみで、DownloadManagerのメモリキューには
                    // 入らない。ここで初めて投入することで、画面上もPending/待機中のまま
                    // 取り残されず、通常の単体追加と同じく即時に処理が始まる。
                    if (immediateDownload && videosToEnqueue.Count > 0)
                        EnqueueImportedVideos(videosToEnqueue);
                }

                // --- チャンネル (profile) の処理 ---
                // 動画一覧取得は共通キューで 1 件ずつ処理されるのでここではエンキューのみ
                if (profiles.Count > 0 && _downloadManager != null)
                {
                    foreach (var profileUrl in profiles)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (_downloadManager.EnqueueSubscribedUser(profileUrl))
                            addedChannels++;
                        else
                            channelFailed++;
                        progressBar.Value = Math.Min(progressBar.Value + 1, progressBar.Maximum);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

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
                    CompleteDialog(DialogResult.OK);
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                // キャンセル時も、API取得が完了してリストへ追加済みの分は失わない。
                if (!videosPersisted && ImportedVideos.Count > 0)
                {
                    AssociateVideosWithAuthorChannels(ImportedVideos, channelCache, persistExisting: false);

                    // キャンセルでキュー投入を省略した分は、Pendingのまま残すと
                    // 次回起動時に意図せず自動DLされるため、明示的に保留へ戻す。
                    if (immediateDownload)
                    {
                        foreach (var video in ImportedVideos)
                            video.Status = DownloadStatus.Paused;
                    }
                    _database.AddVideosBatch(ImportedVideos);
                    videosPersisted = true;
                }

                if (ImportedVideos.Count > 0 || addedChannels > 0)
                    CompleteDialog(DialogResult.OK);
                else
                    CompleteDialog(DialogResult.Cancel);
            }
            catch (Exception ex)
            {
                MessageBox.Show(L.T("BulkImportForm_D012", ex.Message),
                    L.T("BulkImportForm_D003"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _importCancellation = null;
                _isImporting = false;
                btnImport.Enabled = true;
                btnCancel.Enabled = true;
                btnImport.Text = L.T("BulkImportForm_D013");
                progressBar.Visible = false;
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            if (_isImporting)
            {
                RequestCancellation();
                return;
            }

            CompleteDialog(DialogResult.Cancel);
        }

        private void RequestCancellation()
        {
            if (_cancelRequested) return;

            _cancelRequested = true;
            btnCancel.Enabled = false;
            _importCancellation?.Cancel();
        }

        private void CompleteDialog(DialogResult result)
        {
            _allowClose = true;
            _isImporting = false;
            DialogResult = result;
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_isImporting && !_allowClose)
            {
                e.Cancel = true;
                RequestCancellation();
                return;
            }

            base.OnFormClosing(e);
        }
    }
}
