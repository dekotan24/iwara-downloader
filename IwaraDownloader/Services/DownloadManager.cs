using System.Collections.Concurrent;
using System.Threading.Channels;
using IwaraDownloader.Models;
using IwaraDownloader.Utils;

namespace IwaraDownloader.Services
{
    /// <summary>
    /// ダウンロードマネージャー(IwaraApiService使用版)
    /// </summary>
    public class DownloadManager : IDisposable
    {
        private readonly IwaraApiService _iwaraApi;
        private readonly DatabaseService _database;
        private readonly LoggingService _logger = LoggingService.Instance;
        private readonly ConcurrentDictionary<string, DownloadTask> _activeTasks;
        private readonly ConcurrentDictionary<string, DownloadTask> _pendingTasks;
        private readonly ConcurrentQueue<DownloadTask> _pendingQueue;
        private int _activeDownloadCount;
        private readonly SemaphoreSlim _slotAvailableSignal = new SemaphoreSlim(0, int.MaxValue);
        private readonly System.Timers.Timer _autoCheckTimer;
        private CancellationTokenSource? _globalCts;
        private bool _isRunning;
        private int _isProcessingQueue; // 0=idle, 1=running (Interlocked で更新)

        /// <summary>ダウンロード進捗イベント</summary>
        public event EventHandler<DownloadTask>? TaskProgressChanged;

        /// <summary>タスク状態変更イベント</summary>
        public event EventHandler<DownloadTask>? TaskStatusChanged;

        /// <summary>新着動画検出イベント</summary>
        public event EventHandler<(SubscribedUser User, List<VideoInfo> Videos)>? NewVideosFound;

        /// <summary>自動チェック完了イベント</summary>
        public event EventHandler? AutoCheckCompleted;

        /// <summary>アクティブなタスク数</summary>
        public int ActiveTaskCount => _activeTasks.Count;

        /// <summary>純粋に DL 中のタスク数 (タグ書き込み中は含まない)</summary>
        public int DownloadingCount => _activeTasks.Values.Count(t => t.Status == DownloadStatus.Downloading);

        /// <summary>タグ書き込み中のタスク数</summary>
        public int WritingTagsCount => _activeTasks.Values.Count(t => t.Status == DownloadStatus.WritingTags);

        /// <summary>待機中のタスク数</summary>
        public int PendingTaskCount => _pendingQueue.Count + _pendingTasks.Count;

        /// <summary>実行中かどうか</summary>
        public bool IsRunning => _isRunning;

        /// <summary>IwaraApiService</summary>
        public IwaraApiService IwaraApi => _iwaraApi;

        public DownloadManager()
        {
            _iwaraApi = new IwaraApiService();
            _database = DatabaseService.Instance;
            _activeTasks = new ConcurrentDictionary<string, DownloadTask>();
            _pendingTasks = new ConcurrentDictionary<string, DownloadTask>();
            _pendingQueue = new ConcurrentQueue<DownloadTask>();

            // 自動チェックタイマー (重複起動防止 + 例外捕捉)
            _autoCheckTimer = new System.Timers.Timer();
            _autoCheckTimer.Elapsed += (s, e) =>
            {
                if (Interlocked.CompareExchange(ref _isAutoChecking, 1, 0) != 0)
                {
                    _logger.Debug("AutoCheck: 前回キュー投入中のためスキップ");
                    return;
                }
                try
                {
                    // 実際の取得は統合ワーカーが 1 件ずつ処理する。ここではキューに積むだけ。
                    EnqueueAllUsersForCheck();
                }
                catch (Exception ex)
                {
                    _logger.Error("AutoCheckTimer.Elapsed で例外", ex);
                }
                finally
                {
                    Interlocked.Exchange(ref _isAutoChecking, 0);
                }
            };
        }

        private int _isAutoChecking; // 0=idle, 1=running

        // ---- 動画一覧取得キュー (チャンネル追加 + 新着チェックを統合) ----
        // コピペ検出/追加/新着チェック → 即時重複チェック → キュー積み → 1件ずつ動画一覧取得。
        // チャンネル追加と新着チェックが同じ get_videos を二重に叩く「被り」を構造的に防ぐため、
        // 取得は必ずこの 1 本のワーカー (ProcessFetchQueueAsync) を通す。
        // 手動「今すぐ確認」は _priorityFetchQueue 経由で通常キューより先に処理される。

        /// <summary>取得理由。Add=チャンネル新規追加 (完了で VideosLoaded=true)、Check=新着チェック</summary>
        private enum FetchReason { Add, Check }

        /// <summary>取得キューの 1 項目。Attempt はタイムアウト時のリトライ回数 (エクスポネンシャル)</summary>
        private sealed record FetchRequest(string UserId, FetchReason Reason, int Attempt = 0);

        private readonly Channel<FetchRequest> _fetchQueue =
            Channel.CreateUnbounded<FetchRequest>(new UnboundedChannelOptions { SingleReader = true });
        private readonly Channel<FetchRequest> _priorityFetchQueue =
            Channel.CreateUnbounded<FetchRequest>(new UnboundedChannelOptions { SingleReader = true });
        private readonly HashSet<string> _pendingUserIds = new(StringComparer.OrdinalIgnoreCase);

        // タイムアウト: min(90s * 2^Attempt, 10min)、最大 5 回で諦め (回線が遅い人を切り捨てない)
        private static readonly TimeSpan FetchTimeoutBase = TimeSpan.FromSeconds(90);
        private static readonly TimeSpan FetchTimeoutCap = TimeSpan.FromMinutes(10);
        private const int FetchMaxAttempts = 5;

        /// <summary>取得キューの状態変化通知 (スレッド安全ではないので UI スレッドで購読すること)</summary>
        public event EventHandler<string>? UserAddStatusChanged;

        /// <summary>取得完了通知 (UI ツリー更新用)</summary>
        public event EventHandler<SubscribedUser>? UserAdded;

        /// <summary>
        /// ダウンロードマネージャーを開始
        /// </summary>
        public void Start()
        {
            if (_isRunning) return;

            _globalCts = new CancellationTokenSource();
            _isRunning = true;

            _logger.Info("DownloadManager started");

            // 自動チェック開始
            UpdateAutoCheckTimer();

            // 待機中のタスクを処理開始
            _ = ProcessQueueAsync();

            // 前回アプリ終了時に仮登録のまま残ったユーザーを再キュー (Add 理由で取得し直す)
            var notLoaded = _database.GetUsersWithVideosNotLoaded();
            foreach (var u in notLoaded)
            {
                bool added;
                lock (_pendingUserIds)
                    added = _pendingUserIds.Add(u.UserId);
                if (added)
                    _fetchQueue.Writer.TryWrite(new FetchRequest(u.UserId, FetchReason.Add));
            }
            if (notLoaded.Count > 0)
                _logger.Info($"起動時再キュー: {notLoaded.Count} チャンネルの動画一覧未取得を検出");

            // 取得キューワーカー開始 (チャンネル追加 + 新着チェックを統合処理)
            _ = ProcessFetchQueueAsync();
        }

        /// <summary>
        /// 起動時に未完了のダウンロードを再開。
        /// 大量 (数千件) のときに UI スレッドを止めないよう、すべて Task.Run で背後実行する。
        /// 個別 EnqueueDownload は呼ばず、_pendingTasks/_pendingQueue へ直接バルク投入する。
        /// </summary>
        public void ResumeIncompleteDownloads()
        {
            var settings = SettingsManager.Instance.Settings;
            if (!settings.ResumeDownloadsOnStartup)
            {
                _logger.Debug("Resume on startup is disabled");
                return;
            }

            _ = Task.Run(() =>
            {
                try
                {
                    ResumeIncompleteDownloadsCore();
                }
                catch (Exception ex)
                {
                    _logger.Error("ResumeIncompleteDownloads failed", ex);
                }
            });
        }

        private void ResumeIncompleteDownloadsCore()
        {
            // 0) 孤児 .part / .part.meta クリーンアップ
            //   - .meta 無し .part: リジューム情報無いので削除
            //   - .meta あり、DB照合で「対応動画が Completed (別場所保存済み) or DB から消えてる」→削除
            //   - .meta あり、対応動画が Pending/Failed → 残す (次の DL でリジューム)
            try { CleanupOrphanPartFiles(); }
            catch (Exception ex) { _logger.Warn($"Orphan .part cleanup failed: {ex.Message}"); }

            // 1) 前回中断分 (Downloading) は SQL 一発で Pending に降格
            var reset = _database.BulkUpdateStatus(DownloadStatus.Downloading, DownloadStatus.Pending);
            if (reset > 0) _logger.Info($"Reset {reset} Downloading rows to Pending (bulk)");

            // 2) Pending な動画を取得
            var pendingVideos = _database.GetVideosByStatus(DownloadStatus.Pending);
            if (pendingVideos.Count == 0)
            {
                _logger.Debug("No incomplete downloads to resume");
                return;
            }

            _logger.Info($"Resuming {pendingVideos.Count} incomplete downloads");

            // 3) SubscribedUser を 1 クエリで一括取得 → 辞書化 (個別 SELECT を避ける)
            var userMap = _database.GetAllSubscribedUsers().ToDictionary(u => u.Id);
            var quality = SettingsManager.Instance.Settings.DefaultQuality;

            int enqueued = 0;
            foreach (var video in pendingVideos)
            {
                // 既にメモリキューに居るものはスキップ
                if (_pendingTasks.ContainsKey(video.VideoId) || _activeTasks.ContainsKey(video.VideoId))
                    continue;

                SubscribedUser? user = null;
                if (video.SubscribedUserId.HasValue)
                    userMap.TryGetValue(video.SubscribedUserId.Value, out user);

                var task = new DownloadTask
                {
                    Video = video,
                    Status = DownloadStatus.Pending,
                    IsSubscriptionDownload = video.SubscribedUserId.HasValue,
                    Quality = quality,
                    SubscribedUser = user
                };

                _pendingTasks[video.VideoId] = task;
                _pendingQueue.Enqueue(task);
                enqueued++;
            }

            _logger.Info($"Enqueued {enqueued} tasks (bulk, no per-task event fire)");

            // 4) UI 通知は 1 回だけ (チャンネルツリー再描画)
            AutoCheckCompleted?.Invoke(this, EventArgs.Empty);

            // 5) キュー処理開始
            if (_isRunning)
            {
                _ = ProcessQueueAsync();
            }
        }

        /// <summary>
        /// 孤児 .part / .part.meta を判定して削除する。
        /// 起動時の ResumeIncompleteDownloadsCore から呼ばれる。
        /// 判定:
        ///   - .meta が無い .part → 削除 (リジューム不能)
        ///   - .meta あり / .meta から file_id 取得 → DB 照合
        ///       - 対応動画が Completed (= 別パスに保存済み) → 削除
        ///       - DB に対応動画が無い → 削除
        ///       - 対応動画が Pending/Failed/Downloading → 残す (次回 DL でリジューム)
        ///   - .meta パース失敗 → 削除 (壊れたメタは再 DL の方が確実)
        /// </summary>
        private void CleanupOrphanPartFiles()
        {
            var scanFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var defaultFolder = SettingsManager.Instance.Settings.DownloadFolder;
            if (!string.IsNullOrEmpty(defaultFolder)) scanFolders.Add(defaultFolder);
            foreach (var u in _database.GetAllSubscribedUsers())
            {
                if (!string.IsNullOrEmpty(u.CustomSavePath))
                    scanFolders.Add(u.CustomSavePath);
            }

            int scanned = 0, deletedNoMeta = 0, deletedBadMeta = 0, deletedOrphan = 0, deletedCompleted = 0, kept = 0;

            foreach (var folder in scanFolders)
            {
                if (!Directory.Exists(folder)) continue;

                IEnumerable<string> partFiles;
                try
                {
                    partFiles = Directory.EnumerateFiles(folder, "*.part", SearchOption.AllDirectories);
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Cannot enumerate .part files in {folder}: {ex.Message}");
                    continue;
                }

                foreach (var partPath in partFiles)
                {
                    scanned++;
                    var metaPath = partPath + ".meta";

                    // ① .meta が無い → 削除
                    if (!File.Exists(metaPath))
                    {
                        if (TryDelete(partPath)) deletedNoMeta++;
                        continue;
                    }

                    // .meta から file_id を取り出す
                    string? fileUuid = null;
                    try
                    {
                        var json = File.ReadAllText(metaPath);
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("file_id", out var fidProp))
                            fileUuid = fidProp.GetString();
                    }
                    catch (Exception ex)
                    {
                        _logger.Debug($"Bad .meta json ({metaPath}): {ex.Message}");
                    }

                    if (string.IsNullOrEmpty(fileUuid))
                    {
                        // ② .meta 壊れ → 削除
                        if (TryDelete(partPath)) deletedBadMeta++;
                        TryDelete(metaPath);
                        continue;
                    }

                    // ③ DB 照合
                    var video = _database.GetVideoByFileUuid(fileUuid);
                    if (video == null)
                    {
                        // DB に対応動画なし (購読解除等) → 孤児
                        if (TryDelete(partPath)) deletedOrphan++;
                        TryDelete(metaPath);
                        continue;
                    }
                    if (video.Status == DownloadStatus.Completed
                        && !string.IsNullOrEmpty(video.LocalFilePath)
                        && File.Exists(video.LocalFilePath))
                    {
                        // 別場所に完成品あり → 孤児
                        if (TryDelete(partPath)) deletedCompleted++;
                        TryDelete(metaPath);
                        continue;
                    }

                    // それ以外 (Pending/Failed/Downloading) → 残す
                    kept++;
                }
            }

            if (scanned > 0)
            {
                _logger.Info(
                    $".part cleanup: scanned={scanned}, kept={kept}, " +
                    $"deletedNoMeta={deletedNoMeta}, deletedBadMeta={deletedBadMeta}, " +
                    $"deletedOrphan={deletedOrphan}, deletedCompleted={deletedCompleted}");
            }
        }

        private bool TryDelete(string path)
        {
            try { File.Delete(path); return true; }
            catch (Exception ex)
            {
                _logger.Debug($"Failed to delete {path}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// ダウンロードマネージャーを停止
        /// </summary>
        public void Stop()
        {
            if (!_isRunning) return;

            _logger.Info("DownloadManager stopping");
            _autoCheckTimer.Stop();
            _globalCts?.Cancel();
            _isRunning = false;
        }

        /// <summary>
        /// 自動チェックタイマーを更新
        /// </summary>
        public void UpdateAutoCheckTimer()
        {
            var settings = SettingsManager.Instance.Settings;
            if (settings.AutoCheckEnabled)
            {
                _autoCheckTimer.Interval = settings.CheckIntervalMinutes * 60 * 1000;
                _autoCheckTimer.Start();
            }
            else
            {
                _autoCheckTimer.Stop();
            }
        }

        /// <summary>
        /// 同時DL数の設定変更を通知 (ProcessQueueAsync を起こして再評価させる)
        /// </summary>
        public void NotifyConcurrentLimitChanged()
        {
            _slotAvailableSignal.Release();
        }

        /// <summary>
        /// 動画をダウンロードキューに追加
        /// </summary>
        public DownloadTask EnqueueDownload(VideoInfo video, bool isSubscriptionDownload = false, SubscribedUser? subscribedUser = null)
        {
            // 既にキューに入っているか確認
            if (_pendingTasks.ContainsKey(video.VideoId) || _activeTasks.ContainsKey(video.VideoId))
            {
                var existingTask = GetTask(video.VideoId);
                if (existingTask != null) return existingTask;
            }

            // 外部動画 (YouTube埋め込み等) のDL設定判定
            if (video.IsExternal)
            {
                var settings = SettingsManager.Instance.Settings;
                bool shouldDownload = subscribedUser != null
                    ? subscribedUser.ResolveDownloadExternal(settings.DownloadExternalVideosDefault)
                    : settings.DownloadExternalVideosDefault;

                if (!shouldDownload)
                {
                    _logger.Info($"外部動画スキップ (設定によりDLしない): {video.Title} [{video.EmbedUrl}]");
                    video.Status = DownloadStatus.Skipped;
                    video.LastErrorMessage = "外部動画DL設定がOFFのためスキップ";
                    _database.UpdateVideo(video);

                    var skippedTask = new DownloadTask
                    {
                        Video = video,
                        Status = DownloadStatus.Skipped,
                        IsSubscriptionDownload = isSubscriptionDownload,
                        Quality = SettingsManager.Instance.Settings.DefaultQuality,
                        SubscribedUser = subscribedUser
                    };
                    TaskStatusChanged?.Invoke(this, skippedTask);
                    return skippedTask;
                }
            }

            var task = new DownloadTask
            {
                Video = video,
                Status = DownloadStatus.Pending,
                IsSubscriptionDownload = isSubscriptionDownload,
                Quality = SettingsManager.Instance.Settings.DefaultQuality,
                SubscribedUser = subscribedUser
            };

            // DBのステータスも更新
            video.Status = DownloadStatus.Pending;
            _database.UpdateVideo(video);

            // 待機中タスクとして登録
            _pendingTasks[video.VideoId] = task;
            _pendingQueue.Enqueue(task);

            // イベント発火(UIに反映)
            TaskStatusChanged?.Invoke(this, task);

            // キュー処理を開始
            if (_isRunning)
            {
                _ = ProcessQueueAsync();
            }

            return task;
        }

        /// <summary>
        /// キューを処理
        /// </summary>
        private async Task ProcessQueueAsync()
        {
            // 重複実行防止 (Interlocked で TOCTOU 排除)
            if (Interlocked.CompareExchange(ref _isProcessingQueue, 1, 0) != 0) return;

            try
            {
                // Stop→Start で _globalCts が差し替わる前に Token を local snapshot。
                // ループ中に Dispose されても ObjectDisposedException が起きないように
                // 取得済みの token を最後まで使う。
                var cts = _globalCts;
                if (cts == null) return;
                var token = cts.Token;
                while (_isRunning && !token.IsCancellationRequested)
                {
                    if (!_pendingQueue.TryDequeue(out var task))
                    {
                        break;
                    }

                    // キャンセルされたタスク(_pendingTasksから削除済み)はスキップ
                    if (!_pendingTasks.ContainsKey(task.Video.VideoId))
                    {
                        _logger.Debug($"Skipping cancelled task: {task.Video.VideoId}");
                        continue;
                    }

                    // 注: _pendingTasks の削除は ExecuteDownloadAsync 冒頭で行う。
                    // ここで先に削除すると、セマフォ待機/レート制限遅延の間タスクが
                    // _pendingTasks にも _activeTasks にも居ない死角ができ、UI 上で
                    // 「0DL中」と表示されてしまうため。

                    // 同時DL数制限: 現在の設定値を都度参照し、動的変更に対応
                    while (_activeDownloadCount >= SettingsManager.Instance.Settings.MaxConcurrentDownloads)
                    {
                        await _slotAvailableSignal.WaitAsync(token);
                    }
                    Interlocked.Increment(ref _activeDownloadCount);

                    try
                    {
                        // レート制限：DL開始前に設定値分待機
                        var settings = SettingsManager.Instance.Settings;
                        var delayMs = Math.Max(settings.DownloadDelayMs, 1000); // 最低1秒
                        System.Diagnostics.Debug.WriteLine($"RateLimit: waiting {delayMs}ms before download...");
                        await Task.Delay(delayMs, token);

                        // スレッドプール上で起動 (呼び出し元が UI スレッドのため、await の continuation が
                        // UI に戻って WriteIwaraTags 等の同期I/Oで詰まるのを防ぐ)
                        _ = Task.Run(() => ExecuteDownloadAsync(task));
                    }
                    catch
                    {
                        Interlocked.Decrement(ref _activeDownloadCount);
                        _slotAvailableSignal.Release();
                        throw;
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isProcessingQueue, 0);
            }
        }

        /// <summary>
        /// ダウンロードを実行(IwaraApiService使用)
        /// </summary>
        private async Task ExecuteDownloadAsync(DownloadTask task)
        {
            try
            {
                // pending → active へ同タイミングで切り替え (UI 表示に死角を作らない)
                _pendingTasks.TryRemove(task.Video.VideoId, out _);
                _activeTasks[task.Video.VideoId] = task;

                task.CancellationTokenSource = new CancellationTokenSource();
                task.Status = DownloadStatus.Downloading;
                task.StartedAt = DateTime.Now;

                // DBのステータスも更新
                task.Video.Status = DownloadStatus.Downloading;
                _database.UpdateVideo(task.Video);

                TaskStatusChanged?.Invoke(this, task);

                var settings = SettingsManager.Instance.Settings;
                var video = task.Video;

                // --- 外部動画 (YouTube埋め込み等) の場合は yt-dlp 経路へ ---
                if (video.IsExternal)
                {
                    await ExecuteExternalDownloadAsync(task);
                    return;
                }

                // --- 事前に動画情報を取得 (FileUuid / 最新 title / author) ---
                // ここで得た FileUuid を用いて既存ファイル検出を行う
                // site が空なら iwara.tv 扱い (旧データ互換)。iwara.ai 動画なら "www.iwara.ai" 指定。
                var siteForApi = string.IsNullOrEmpty(video.Site) ? null : video.Site;
                var urlInfo = await _iwaraApi.GetDownloadUrlAsync(video.VideoId, siteForApi);
                if (!urlInfo.Success)
                {
                    throw new Exception(urlInfo.Error ?? "動画情報の取得に失敗しました");
                }

                // video レコードを最新情報で補強
                if (!string.IsNullOrEmpty(urlInfo.FileUuid))
                    video.FileUuid = urlInfo.FileUuid;
                if (!string.IsNullOrEmpty(urlInfo.AuthorUsername) && string.IsNullOrEmpty(video.AuthorUsername))
                    video.AuthorUsername = urlInfo.AuthorUsername;
                if (!string.IsNullOrEmpty(urlInfo.Title) && string.IsNullOrEmpty(video.Title))
                    video.Title = urlInfo.Title;
                if (!string.IsNullOrEmpty(urlInfo.Rating))
                    video.Rating = urlInfo.Rating;
                if (!string.IsNullOrEmpty(urlInfo.ThumbnailUrl) && string.IsNullOrEmpty(video.ThumbnailUrl))
                    video.ThumbnailUrl = urlInfo.ThumbnailUrl;

                // --- 既存ファイル検出 (UUID ベース) ---
                if (TryReuseExistingLocalFile(task, video))
                {
                    return; // スキップ扱いで完了
                }

                // 出力パスを決定
                string outputPath;

                // ファイル名テンプレートを適用
                var filenameTemplate = settings.FilenameTemplate;
                var generatedFilename = Helpers.ApplyFilenameTemplate(
                    filenameTemplate,
                    video.Title,
                    video.AuthorUsername,
                    video.VideoId,
                    video.PostedAt) + ".mp4";

                if (task.IsSubscriptionDownload && task.SubscribedUser != null)
                {
                    // チャンネル別保存先を使用
                    var savePath = task.SubscribedUser.GetSavePath(settings.DownloadFolder);
                    outputPath = Path.Combine(savePath, generatedFilename);
                }
                else if (task.IsSubscriptionDownload)
                {
                    var sanitizedUsername = Helpers.SanitizeFileName(video.AuthorUsername);
                    var userFolder = Path.Combine(settings.DownloadFolder, sanitizedUsername);
                    if (!Directory.Exists(userFolder))
                        Directory.CreateDirectory(userFolder);
                    outputPath = Path.Combine(userFolder, generatedFilename);
                }
                else
                {
                    // 単発動画はベースフォルダ直下
                    outputPath = Path.Combine(settings.DownloadFolder, generatedFilename);
                }

                // フォルダ作成
                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                // --- リネーム追従: 保存先フォルダを FileUuid でスキャン ---
                // 購読DL時のみ実行 (アーティストフォルダに限定されるため高速)
                // IndexCacheService が .iwara_index.json に前回スキャン結果を保持しているため
                // ファイルが更新されていない限り TagLib# 呼び出しは発生しない
                if (task.IsSubscriptionDownload && !string.IsNullOrEmpty(video.FileUuid))
                {
                    var folderToScan = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(folderToScan) && Directory.Exists(folderToScan))
                    {
                        var uuidMap = IndexCacheService.GetOrScan(folderToScan);
                        if (uuidMap.TryGetValue(video.FileUuid, out var foundPath))
                        {
                            _logger.Info($"既存ファイルを再発見 (UUID マッチ): {video.Title} -> {foundPath}");
                            MarkAsReusedFile(task, video, foundPath);
                            return;
                        }
                    }
                }

                outputPath = Helpers.GetUniqueFilePath(outputPath);

                // ダウンロード実行(IwaraApiService使用)
                var progress = new Progress<string>(msg =>
                {
                    System.Diagnostics.Debug.WriteLine($"Download progress: {msg}");
                });

                // パーセント進捗を受け取ってイベント発火
                // throttle: 並列DLで毎%発火 → UIスレッド詰まりを防ぐため 250ms 間引き。
                // 0% 開始と 100% 完了は確実に通すため境界条件を別扱い。
                long lastFireMs = 0;
                bool sentZero = false;
                var percentProgress = new Progress<double>(pct =>
                {
                    task.Progress = pct;
                    var nowMs = Environment.TickCount64;
                    bool shouldFire = pct >= 100.0
                                   || (!sentZero && pct > 0)
                                   || nowMs - lastFireMs >= 250;
                    if (shouldFire)
                    {
                        if (pct > 0) sentZero = true;
                        lastFireMs = nowMs;
                        TaskProgressChanged?.Invoke(this, task);
                    }
                });

                // タスク個別CTとアプリ全体CTをリンク (どちらかキャンセルされたらpython側もKill)
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    task.CancellationTokenSource.Token,
                    _globalCts?.Token ?? CancellationToken.None);

                var (success, error) = await _iwaraApi.DownloadVideoAsync(
                    video.VideoId,
                    outputPath,
                    progress,
                    percentProgress,
                    linkedCts.Token,
                    siteForApi);

                if (success && File.Exists(outputPath))
                {
                    task.Progress = 100;
                    video.LocalFilePath = outputPath;
                    video.DownloadedAt = DateTime.Now;
                    video.FileSize = new FileInfo(outputPath).Length;

                    // mp4 にカスタムタグ (video_id / file_id) を書き込む
                    if (!string.IsNullOrEmpty(video.FileUuid))
                    {
                        // ステータスを「タグ書き込み中」に切り替えて UI に反映
                        task.Status = DownloadStatus.WritingTags;
                        TaskStatusChanged?.Invoke(this, task);

                        var tagOk = MetadataService.WriteIwaraTags(outputPath, video.VideoId, video.FileUuid);
                        if (tagOk)
                            _logger.Debug($"Wrote iwara tags: {Path.GetFileName(outputPath)}");
                        else
                            _logger.Warn($"Failed to write iwara tags: {Path.GetFileName(outputPath)}");
                    }

                    // タグ書き込みが終わってから Completed に確定
                    task.Status = DownloadStatus.Completed;
                    task.CompletedAt = DateTime.Now;
                    video.Status = DownloadStatus.Completed;

                    // フォルダのインデックスキャッシュを無効化 (新規ファイルを次回スキャンに反映)
                    var outputDir = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(outputDir))
                    {
                        IndexCacheService.Invalidate(outputDir);
                    }

                    _database.UpdateVideo(video);

                    _logger.Info($"Download completed: {video.Title} ({video.FileSizeFormatted})");

                    // サムネがまだキャッシュされていなければDL
                    if (video.ThumbnailStatus != 1 && !string.IsNullOrEmpty(video.ThumbnailUrl) && !string.IsNullOrEmpty(video.VideoId))
                    {
                        ThumbnailCacheService.Instance.RequestAsync(video.VideoId, video.ThumbnailUrl);
                    }

                    // 完了音を再生
                    SoundService.Instance.PlayCompletionSound();

                    // メタデータ保存
                    if (SettingsManager.Instance.Settings.SaveMetadata)
                    {
                        SaveVideoMetadata(video, outputPath);
                    }

                    if (video.SubscribedUserId.HasValue)
                    {
                        var user = _database.GetSubscribedUserById(video.SubscribedUserId.Value);
                        if (user != null)
                        {
                            user.DownloadedCount++;
                            _database.UpdateSubscribedUser(user);
                        }
                    }

                    NotificationService.Instance.NotifyDownloadComplete(video.Title, outputPath);
                }
                else
                {
                    throw new Exception(error ?? "ダウンロードに失敗しました");
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Info($"Download cancelled: {task.Video.Title}");
                task.Status = DownloadStatus.Paused;
                task.Video.Status = DownloadStatus.Paused;
                _database.UpdateVideo(task.Video);
            }
            catch (Exception ex)
            {
                _logger.Error($"Download failed: {task.Video.Title}", ex);
                task.Status = DownloadStatus.Failed;
                task.ErrorMessage = ex.Message;
                task.Video.RetryCount++;
                task.Video.LastErrorMessage = ex.Message;
                task.Video.Status = DownloadStatus.Failed;
                _database.UpdateVideo(task.Video);

                var maxRetry = SettingsManager.Instance.Settings.MaxRetryCount;
                _logger.Debug($"Download error: {task.Video.Title} - RetryCount={task.Video.RetryCount}, MaxRetry={maxRetry}");

                // CDN_UNAVAILABLE / All CDN candidates failed: iwara 側の CDN 振り分けが壊れていて
                // Python ヘルパーが既に内部で 6 回 CDN ガチャを引いてる。
                // ここから更にリトライしても無駄なので即時諦める (CPU・帯域節約)。
                bool isCdnUnavailable = ex.Message.Contains("CDN_UNAVAILABLE")
                                     || ex.Message.Contains("All CDN candidates failed");

                // フレンド限定動画 (相互承認しないと見れない): 何度叩いても 403 が返るので即時諦める
                bool isPrivateVideo = ex.Message.Contains("PRIVATE_VIDEO")
                                   || ex.Message.Contains("errors.privateVideo")
                                   || ex.Message.Contains("Private video");

                // 動画削除・存在しない: iwara 側で完全に消えてるのでリトライ無駄
                bool isVideoNotFound = ex.Message.Contains("VIDEO_NOT_FOUND")
                                    || ex.Message.Contains("errors.notFound")
                                    || ex.Message.Contains("Video not found");

                bool isUnrecoverable = isCdnUnavailable || isPrivateVideo || isVideoNotFound;

                if (task.Video.RetryCount < maxRetry && !isUnrecoverable)
                {
                    _logger.Info($"Retrying download: {task.Video.Title} (attempt {task.Video.RetryCount + 1}/{maxRetry})");
                    // finally で _activeTasks から削除された後にエンキューする必要があるため
                    // fire-and-forget で 5 秒待ってから再投入 (この catch 内で EnqueueDownload を
                    // 呼ぶと重複チェックに引っかかって新規エンキューされない)
                    var videoToRetry = task.Video;
                    var isSubRetry = task.IsSubscriptionDownload;
                    var userToRetry = task.SubscribedUser;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(5000, _globalCts?.Token ?? CancellationToken.None);
                            EnqueueDownload(videoToRetry, isSubRetry, userToRetry);
                        }
                        catch (OperationCanceledException) { /* シャットダウン中: 諦める */ }
                        catch (Exception retryEx)
                        {
                            _logger.Warn($"Retry enqueue failed: {videoToRetry.Title} - {retryEx.Message}");
                        }
                    });
                }
                else
                {
                    // 最終的に失敗した場合はエラー音を再生して通知
                    if (isPrivateVideo)
                    {
                        _logger.Warn($"Private video (friend-only), skipping retries: {task.Video.Title}");
                        task.Video.RetryCount = Math.Max(task.Video.RetryCount, maxRetry);
                        _database.UpdateVideo(task.Video);
                    }
                    else if (isVideoNotFound)
                    {
                        _logger.Warn($"Video not found on iwara, skipping retries: {task.Video.Title}");
                        task.Video.RetryCount = Math.Max(task.Video.RetryCount, maxRetry);
                        _database.UpdateVideo(task.Video);
                    }
                    else if (isCdnUnavailable)
                    {
                        _logger.Warn($"CDN unavailable, skipping retries: {task.Video.Title}");
                        // RetryCount を max にしておいて、ユーザーが「失敗動画を一括再試行」しない限り
                        // 自動チェックで再 DL が走らないようにする (iwara 側修正待ち)
                        task.Video.RetryCount = Math.Max(task.Video.RetryCount, maxRetry);
                        _database.UpdateVideo(task.Video);
                    }
                    else
                    {
                        _logger.Info($"Final failure, playing error sound: {task.Video.Title}");
                    }
                    SoundService.Instance.PlayErrorSound();
                    NotificationService.Instance.NotifyDownloadError(task.Video.Title, ex.Message);
                }
            }
            finally
            {
                _activeTasks.TryRemove(task.Video.VideoId, out _);
                Interlocked.Decrement(ref _activeDownloadCount);
                _slotAvailableSignal.Release();
                TaskStatusChanged?.Invoke(this, task);
            }
        }

        /// <summary>
        /// 外部動画 (YouTube埋め込み等) を yt-dlp 経由でダウンロード
        /// </summary>
        private async Task ExecuteExternalDownloadAsync(DownloadTask task)
        {
            var settings = SettingsManager.Instance.Settings;
            var video = task.Video;

            // ファイル名生成(拡張子は yt-dlp 側で補完される)
            var generatedFilename = Helpers.ApplyFilenameTemplate(
                settings.FilenameTemplate,
                video.Title,
                video.AuthorUsername,
                video.VideoId,
                video.PostedAt);

            string outputPathNoExt;
            if (task.IsSubscriptionDownload && task.SubscribedUser != null)
            {
                var savePath = task.SubscribedUser.GetSavePath(settings.DownloadFolder);
                outputPathNoExt = Path.Combine(savePath, generatedFilename);
            }
            else if (task.IsSubscriptionDownload && !string.IsNullOrEmpty(video.AuthorUsername))
            {
                var sanitizedUsername = Helpers.SanitizeFileName(video.AuthorUsername);
                outputPathNoExt = Path.Combine(settings.DownloadFolder, sanitizedUsername, generatedFilename);
            }
            else
            {
                outputPathNoExt = Path.Combine(settings.DownloadFolder, generatedFilename);
            }

            var dir = Path.GetDirectoryName(outputPathNoExt);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            _logger.Info($"外部動画DL開始: {video.Title} [{video.EmbedUrl}] -> {outputPathNoExt}");

            var progress = new Progress<string>(msg =>
            {
                System.Diagnostics.Debug.WriteLine($"Ext DL progress: {msg}");
            });
            // throttle: 並列DLで毎%発火 → UIスレッド詰まりを防ぐため 250ms 間引き
            long lastExtFireMs = 0;
            bool sentExtZero = false;
            var percentProgress = new Progress<double>(pct =>
            {
                task.Progress = pct;
                var nowMs = Environment.TickCount64;
                bool shouldFire = pct >= 100.0
                               || (!sentExtZero && pct > 0)
                               || nowMs - lastExtFireMs >= 250;
                if (shouldFire)
                {
                    if (pct > 0) sentExtZero = true;
                    lastExtFireMs = nowMs;
                    TaskProgressChanged?.Invoke(this, task);
                }
            });

            // タスク個別CTとアプリ全体CTをリンク (どちらかキャンセルされたらyt-dlp側もKill)
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                task.CancellationTokenSource?.Token ?? CancellationToken.None,
                _globalCts?.Token ?? CancellationToken.None);

            var (success, error, filePath) = await _iwaraApi.DownloadExternalVideoAsync(
                video.EmbedUrl, outputPathNoExt, progress, percentProgress, linkedCts.Token);

            if (success && !string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                task.Status = DownloadStatus.Completed;
                task.Progress = 100;
                task.CompletedAt = DateTime.Now;

                video.LocalFilePath = filePath;
                video.Status = DownloadStatus.Completed;
                video.DownloadedAt = DateTime.Now;
                try { video.FileSize = new FileInfo(filePath).Length; } catch { }

                _database.UpdateVideo(video);
                _logger.Info($"外部動画DL完了: {video.Title} ({video.FileSizeFormatted})");

                SoundService.Instance.PlayCompletionSound();

                if (video.SubscribedUserId.HasValue)
                {
                    var user = _database.GetSubscribedUserById(video.SubscribedUserId.Value);
                    if (user != null)
                    {
                        user.DownloadedCount++;
                        _database.UpdateSubscribedUser(user);
                    }
                }

                NotificationService.Instance.NotifyDownloadComplete(video.Title, filePath);
            }
            else
            {
                throw new Exception(error ?? "外部動画ダウンロードに失敗しました");
            }
        }

        /// <summary>
        /// 既存のローカルファイルが FileUuid で照合できる場合に再利用する。
        /// true を返した場合、呼び出し元はダウンロードを行わずタスクを完了扱いにする。
        /// </summary>
        private bool TryReuseExistingLocalFile(DownloadTask task, VideoInfo video)
        {
            if (string.IsNullOrEmpty(video.FileUuid)) return false;

            // 1. 自分自身の DB.LocalFilePath が有効かチェック
            if (!string.IsNullOrEmpty(video.LocalFilePath) && File.Exists(video.LocalFilePath))
            {
                // タグが一致するか確認 (安全策: iwara 側でマスター差替時は再DL)
                var (_, tagUuid) = MetadataService.ReadIwaraTags(video.LocalFilePath);
                if (string.IsNullOrEmpty(tagUuid) || tagUuid == video.FileUuid)
                {
                    _logger.Info($"既にダウンロード済み: {video.Title}");
                    MarkAsReusedFile(task, video, video.LocalFilePath);
                    return true;
                }
            }

            // 2. 別 VideoId で同じ FileUuid を持つ動画が DB にあれば再利用
            var dup = _database.GetVideoByFileUuid(video.FileUuid);
            if (dup != null && dup.Id != video.Id
                && !string.IsNullOrEmpty(dup.LocalFilePath) && File.Exists(dup.LocalFilePath))
            {
                _logger.Info($"既存ファイルを再利用 (別 VideoId で登録済み): {video.Title} -> {dup.LocalFilePath}");
                MarkAsReusedFile(task, video, dup.LocalFilePath);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 既存ファイルを再利用した扱いとして DB・タスクを完了状態に遷移させる。
        /// </summary>
        private void MarkAsReusedFile(DownloadTask task, VideoInfo video, string existingPath)
        {
            try
            {
                // mp4 にタグが無ければ書き足しておく (後の判定のため)
                if (!string.IsNullOrEmpty(video.FileUuid))
                {
                    var (_, tagUuid) = MetadataService.ReadIwaraTags(existingPath);
                    if (string.IsNullOrEmpty(tagUuid))
                    {
                        MetadataService.WriteIwaraTags(existingPath, video.VideoId, video.FileUuid);
                    }
                }

                video.LocalFilePath = existingPath;
                video.Status = DownloadStatus.Completed;
                video.FileSize = new FileInfo(existingPath).Length;
                if (video.DownloadedAt == null)
                    video.DownloadedAt = DateTime.Now;
                _database.UpdateVideo(video);

                task.Status = DownloadStatus.Skipped;
                task.Progress = 100;
                task.CompletedAt = DateTime.Now;
                TaskStatusChanged?.Invoke(this, task);
            }
            catch (Exception ex)
            {
                _logger.Warn($"MarkAsReusedFile failed: {ex.Message}");
            }
        }

        /// <summary>
        /// タスクをキャンセル
        /// </summary>
        public void CancelTask(string videoId)
        {
            // アクティブなタスクをキャンセル
            if (_activeTasks.TryGetValue(videoId, out var task))
            {
                task.Cancel();
            }

            // 待機中のタスクも削除
            if (_pendingTasks.TryRemove(videoId, out var pendingTask))
            {
                pendingTask.Video.Status = DownloadStatus.Paused;
                _database.UpdateVideo(pendingTask.Video);
                TaskStatusChanged?.Invoke(this, pendingTask);
            }
        }

        /// <summary>
        /// 全タスクをキャンセル
        /// </summary>
        public void CancelAllTasks()
        {
            foreach (var task in _activeTasks.Values)
            {
                task.Cancel();
            }

            // 待機中タスクもすべてクリア
            while (_pendingQueue.TryDequeue(out var task))
            {
                _pendingTasks.TryRemove(task.Video.VideoId, out _);
                task.Video.Status = DownloadStatus.Paused;
                _database.UpdateVideo(task.Video);
            }
        }

        /// <summary>
        /// 失敗したタスクをリトライ
        /// </summary>
        public void RetryFailedTask(VideoInfo video, bool isSubscriptionDownload = false, SubscribedUser? user = null)
        {
            video.RetryCount = 0;
            video.Status = DownloadStatus.Pending;
            video.LastErrorMessage = null;
            _database.UpdateVideo(video);

            EnqueueDownload(video, isSubscriptionDownload, user);
        }

        /// <summary>
        /// 動画情報を再取得(タイトル等が取れていない場合用)
        /// </summary>
        public async Task<bool> RefreshVideoInfoAsync(VideoInfo video, IProgress<string>? progress = null)
        {
            try
            {
                progress?.Report($"動画情報を再取得中: {video.VideoId}");

                var siteForApi = string.IsNullOrEmpty(video.Site) ? null : video.Site;
                var urlInfo = await _iwaraApi.GetDownloadUrlAsync(video.VideoId, siteForApi);

                if (urlInfo.Success && !string.IsNullOrEmpty(urlInfo.Title))
                {
                    video.Title = urlInfo.Title;
                    if (!string.IsNullOrEmpty(urlInfo.AuthorUsername))
                        video.AuthorUsername = urlInfo.AuthorUsername;
                    if (!string.IsNullOrEmpty(urlInfo.FileUuid))
                        video.FileUuid = urlInfo.FileUuid;
                    _database.UpdateVideo(video);
                    progress?.Report($"タイトル取得成功: {urlInfo.Title}");
                    return true;
                }
                else
                {
                    progress?.Report($"取得失敗: {urlInfo.Error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"エラー: {ex.Message}");
                return false;
            }
        }

        // 旧 CheckForNewVideosAsync(全チャンネル/単一) は ProcessFetchRequestAsync に統合した。
        // 新着チェックは EnqueueAllUsersForCheck / EnqueueUserForCheck からキュー投入し、
        // 取得・保存はワーカー 1 本に集約 (チャンネル追加との get_videos 二重実行を防止)。

        /// <summary>
        /// 指定ユーザーの DB.Pending かつメモリキューに居ない動画を
        /// EnqueueDownload で登録し直す。AutoDownloadOnCheck が OFF なら何もしない。
        /// </summary>
        private void EnqueuePendingVideosForUser(SubscribedUser user)
        {
            if (!SettingsManager.Instance.Settings.AutoDownloadOnCheck) return;

            var orphans = _database.GetVideosBySubscribedUser(user.Id)
                .Where(v => v.Status == DownloadStatus.Pending
                         && !_pendingTasks.ContainsKey(v.VideoId)
                         && !_activeTasks.ContainsKey(v.VideoId))
                .ToList();

            foreach (var video in orphans)
            {
                EnqueueDownload(video, true, user);
            }
        }

        /// <summary>
        /// チャンネル追加をキューに積む。
        /// エンキュー時点で DB に仮登録 (VideosLoaded=false) するので、
        /// アプリを終了しても起動時に自動再キューされる。
        /// </summary>
        /// <returns>キューに追加できた場合 true</returns>
        public bool EnqueueSubscribedUser(string input)
        {
            var username = Helpers.IsUserProfileUrl(input)
                ? Helpers.ExtractUsernameFromUrl(input)
                : input.Trim();
            if (string.IsNullOrEmpty(username)) return false;

            var siteHost = Helpers.IsUserProfileUrl(input)
                ? Helpers.ExtractSiteFromUrl(input)
                : Helpers.SiteTv;

            // DB 確認: 完全登録済み (VideosLoaded=true) なら重複
            var existing = _database.GetSubscribedUserByUserId(username);
            if (existing != null && existing.VideosLoaded)
            {
                UserAddStatusChanged?.Invoke(this, $"{username} は既に登録済みです");
                return false;
            }

            // キュー内重複チェック (仮登録済みでキュー待ちの場合も弾く)
            lock (_pendingUserIds)
            {
                if (!_pendingUserIds.Add(username))
                {
                    UserAddStatusChanged?.Invoke(this, $"{username} は追加処理待ちです");
                    return false;
                }
            }

            // ここから先で失敗したら _pendingUserIds に残骸が残らないよう必ず巻き戻す
            // (残ると以後そのチャンネルが「追加処理待ち」で永久に弾かれてしまう)
            try
            {
                // 仮登録 (まだ DB にない場合のみ)
                if (existing == null)
                {
                    var profileUrl = $"https://{siteHost}/profile/{username}/videos";
                    var user = new SubscribedUser
                    {
                        UserId = username,
                        Username = username,
                        ProfileUrl = profileUrl,
                        CreatedAt = DateTime.Now,
                        IsEnabled = true,
                        TotalVideoCount = 0,
                        Site = siteHost,
                        VideosLoaded = false,
                    };
                    user.Id = _database.AddSubscribedUser(user);
                    UserAdded?.Invoke(this, user); // UIツリーに仮表示
                }

                // キューが既に閉じている (Dispose 後) 場合は仮登録だけ残し、巻き戻す
                if (!_fetchQueue.Writer.TryWrite(new FetchRequest(username, FetchReason.Add)))
                {
                    lock (_pendingUserIds)
                        _pendingUserIds.Remove(username);
                    UserAddStatusChanged?.Invoke(this, $"チャンネル追加を受け付けられません: {username}");
                    return false;
                }

                UserAddStatusChanged?.Invoke(this, $"チャンネル追加をキューに登録: {username}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"EnqueueSubscribedUser: {username} のキュー登録に失敗", ex);
                lock (_pendingUserIds)
                    _pendingUserIds.Remove(username);
                UserAddStatusChanged?.Invoke(this, $"チャンネル追加失敗: {username}");
                return false;
            }
        }

        /// <summary>
        /// 既存の有効チャンネルを全件、新着チェックとして取得キューに積む。
        /// (自動チェックタイマー / 手動「今すぐ確認」から呼ばれる)
        /// </summary>
        public void EnqueueAllUsersForCheck()
        {
            var users = _database.GetEnabledSubscribedUsers();
            var queued = 0;
            foreach (var user in users)
            {
                if (string.IsNullOrEmpty(user.UserId)) continue;
                lock (_pendingUserIds)
                {
                    if (!_pendingUserIds.Add(user.UserId)) continue; // 既にキュー/処理中
                }
                if (_fetchQueue.Writer.TryWrite(new FetchRequest(user.UserId, FetchReason.Check)))
                    queued++;
                else
                    lock (_pendingUserIds) _pendingUserIds.Remove(user.UserId);
            }
            if (queued > 0)
                UserAddStatusChanged?.Invoke(this, $"新着チェック: {queued} チャンネルをキューに登録");
        }

        /// <summary>
        /// 単一チャンネルを新着チェックとして取得キューに積む。
        /// priority=true なら手動「今すぐ確認」用の優先キューへ (通常キューより先に処理)。
        /// </summary>
        public void EnqueueUserForCheck(SubscribedUser user, bool priority)
        {
            if (user == null || string.IsNullOrEmpty(user.UserId)) return;
            lock (_pendingUserIds)
            {
                if (!_pendingUserIds.Add(user.UserId)) return; // 既にキュー/処理中
            }
            var queue = priority ? _priorityFetchQueue : _fetchQueue;
            if (!queue.Writer.TryWrite(new FetchRequest(user.UserId, FetchReason.Check)))
                lock (_pendingUserIds) _pendingUserIds.Remove(user.UserId);
        }

        /// <summary>
        /// 取得キューを 1 件ずつ処理する統合ワーカー。Start() から 1 本だけ起動。
        /// 優先キュー (_priorityFetchQueue) を通常キュー (_fetchQueue) より先に捌く。
        /// チャンネル追加・新着チェックの両方がここを通るので get_videos が二重に走らない。
        /// </summary>
        private async Task ProcessFetchQueueAsync()
        {
            var token = _globalCts?.Token ?? CancellationToken.None;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    // 優先キューを先にドレイン。両方空ならどちらかにデータが来るまで待機。
                    if (!_priorityFetchQueue.Reader.TryRead(out var req) &&
                        !_fetchQueue.Reader.TryRead(out req))
                    {
                        var pWait = _priorityFetchQueue.Reader.WaitToReadAsync(token).AsTask();
                        var nWait = _fetchQueue.Reader.WaitToReadAsync(token).AsTask();
                        await Task.WhenAny(pWait, nWait);
                        // canceled/faulted を観測 (UnobservedTaskException 抑制)。次ループ先頭で TryRead。
                        _ = pWait.ContinueWith(t => _ = t.Exception, TaskScheduler.Default);
                        _ = nWait.ContinueWith(t => _ = t.Exception, TaskScheduler.Default);
                        continue;
                    }

                    await ProcessFetchRequestAsync(req, token);

                    // レート制限: 1 件処理ごとに最低間隔を空ける (大量アクセス警告対策)
                    var delayMs = Math.Max(SettingsManager.Instance.Settings.ChannelCheckDelayMs, 1000);
                    await Task.Delay(delayMs, token);
                }
            }
            catch (OperationCanceledException)
            {
                // アプリ終了。正常脱出。
            }
        }

        /// <summary>
        /// 取得キューの 1 件を処理。Add/Check 共通の保存処理＋タイムアウト＋エクスポネンシャルなリトライ。
        /// </summary>
        private async Task ProcessFetchRequestAsync(FetchRequest req, CancellationToken outerToken)
        {
            var username = req.UserId;
            var user = _database.GetSubscribedUserByUserId(username);
            if (user == null)
            {
                _logger.Warn($"ProcessFetchQueue: {username} がDBに見つかりません");
                lock (_pendingUserIds) _pendingUserIds.Remove(username);
                return;
            }

            // タイムアウト = min(90s * 2^Attempt, 10min)
            var timeout = TimeSpan.FromTicks(Math.Min(
                FetchTimeoutBase.Ticks * (1L << req.Attempt), FetchTimeoutCap.Ticks));

            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(outerToken, timeoutCts.Token);

            var reasonLabel = req.Reason == FetchReason.Add ? "追加" : "新着確認";
            try
            {
                UserAddStatusChanged?.Invoke(this, $"{user.Username} の動画一覧を取得中... ({reasonLabel})");
                var progress = new Progress<string>(msg => UserAddStatusChanged?.Invoke(this, msg));
                var siteHost = string.IsNullOrEmpty(user.Site) ? Helpers.SiteTv : user.Site;
                var videos = await _iwaraApi.GetUserVideosAsync(user.UserId, progress, siteHost, linked.Token);

                // --- Add/Check 共通: 動画を先に保存してから VideosLoaded=true にする ---
                // (保存前に中断すると動画一覧が欠損するため。保存は VideoExists で重複スキップ)
                var newVideos = new List<VideoInfo>();
                foreach (var video in videos)
                {
                    if (!_database.VideoExists(video.VideoId))
                    {
                        video.AuthorUserId = user.UserId;
                        video.AuthorUsername = user.Username;
                        video.SubscribedUserId = user.Id;
                        if (string.IsNullOrEmpty(video.Site))
                            video.Site = user.Site;
                        video.Status = DownloadStatus.Pending;
                        video.Id = _database.AddVideo(video);
                        newVideos.Add(video);
                    }
                }

                user.TotalVideoCount = videos.Count;
                user.LastCheckedAt = DateTime.Now;
                // 取得成功 = 本登録扱い。Add はもちろん、5 回失敗で諦めた仮登録 (VideosLoaded=false) が
                // 後の新着チェックで取れた場合もここで解除し、起動時の無駄な Add 再取得ループを防ぐ。
                user.VideosLoaded = true;
                _database.UpdateSubscribedUser(user);

                if (req.Reason == FetchReason.Add)
                    UserAddStatusChanged?.Invoke(this, $"チャンネル「{user.Username}」を追加しました ({videos.Count}件)");
                else
                    UserAddStatusChanged?.Invoke(this, $"{user.Username}: {videos.Count}件 / 新着 {newVideos.Count}件");

                UserAdded?.Invoke(this, user);

                if (newVideos.Count > 0)
                {
                    NewVideosFound?.Invoke(this, (user, newVideos));
                    if (SettingsManager.Instance.Settings.AutoDownloadOnCheck)
                        foreach (var v in newVideos)
                            EnqueueDownload(v, true, user);
                    // 通知は新着チェック時のみ (追加直後は全件が「新着」になり大量通知になるため抑制)
                    if (req.Reason == FetchReason.Check)
                        NotificationService.Instance.NotifyNewVideosFound(user.Username, newVideos.Count);
                }

                EnqueuePendingVideosForUser(user);

                lock (_pendingUserIds) _pendingUserIds.Remove(username);
            }
            catch (OperationCanceledException) when (outerToken.IsCancellationRequested)
            {
                // アプリ終了によるキャンセル。pending は揮発させ、VideosLoaded=false なら次回起動で再キュー。
                throw; // ワーカーループを終了させる
            }
            catch (OperationCanceledException)
            {
                // タイムアウト → スキップして他を進め、後でエクスポネンシャルに延ばした時間でリトライ
                var nextAttempt = req.Attempt + 1;
                if (nextAttempt < FetchMaxAttempts)
                {
                    var backoff = TimeSpan.FromTicks(Math.Min(
                        FetchTimeoutBase.Ticks * (1L << nextAttempt), FetchTimeoutCap.Ticks));
                    _logger.Warn($"ProcessFetchQueue: {username} 取得タイムアウト ({timeout.TotalSeconds:F0}s)。" +
                                 $"{backoff.TotalSeconds:F0}s 後にリトライ ({nextAttempt}/{FetchMaxAttempts})");
                    UserAddStatusChanged?.Invoke(this, $"{user.Username} 取得タイムアウト、後でリトライ ({nextAttempt}/{FetchMaxAttempts})");
                    // pending は保持したまま (二重投入防止)、別タスクで backoff 後に末尾再投入
                    RequeueAfterDelay(req with { Attempt = nextAttempt }, backoff);
                }
                else
                {
                    _logger.Error($"ProcessFetchQueue: {username} が {FetchMaxAttempts} 回タイムアウト。諦めます (仮登録は残す)");
                    UserAddStatusChanged?.Invoke(this, $"{user.Username} の取得に失敗 (時間切れ)");
                    lock (_pendingUserIds) _pendingUserIds.Remove(username);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"ProcessFetchQueue: {username} 取得失敗", ex);
                UserAddStatusChanged?.Invoke(this, $"取得失敗: {user.Username}");
                lock (_pendingUserIds) _pendingUserIds.Remove(username);
            }
        }

        /// <summary>
        /// タイムアウトしたリクエストを delay 後に通常キュー末尾へ再投入する。
        /// ワーカーをブロックしないよう別タスクで待機。pending は保持されたまま。
        /// </summary>
        private void RequeueAfterDelay(FetchRequest req, TimeSpan delay)
        {
            var token = _globalCts?.Token ?? CancellationToken.None;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delay, token);
                    if (!_fetchQueue.Writer.TryWrite(req))
                        lock (_pendingUserIds) _pendingUserIds.Remove(req.UserId);
                }
                catch (OperationCanceledException)
                {
                    // アプリ終了。pending を解放 (DB の VideosLoaded=false なら次回起動で再キュー)
                    lock (_pendingUserIds) _pendingUserIds.Remove(req.UserId);
                }
            });
        }

        // 旧 AddSubscribedUserAsync は EnqueueSubscribedUser (仮登録 → キュー → ProcessFetchRequestAsync) に置換した。

        /// <summary>
        /// 単一動画をダウンロードキューに追加
        /// </summary>
        public async Task<DownloadTask?> AddSingleVideoAsync(string url, IProgress<string>? progress = null)
        {
            if (!Helpers.IsVideoUrl(url))
            {
                return null;
            }

            var videoId = Helpers.ExtractVideoIdFromUrl(url);
            if (string.IsNullOrEmpty(videoId))
            {
                return null;
            }

            // URL から site 判定 (iwara.ai 動画かどうか)
            var siteHost = Helpers.ExtractSiteFromUrl(url);

            // 既にDBにあるか確認
            var existing = _database.GetVideoByVideoId(videoId);
            if (existing != null)
            {
                // 既存動画の Site が未設定なら URL から推定して補完
                if (string.IsNullOrEmpty(existing.Site))
                {
                    existing.Site = siteHost;
                    _database.UpdateVideo(existing);
                }
                return EnqueueDownload(existing, false);
            }

            // IwaraApiServiceでダウンロードURL取得(動画情報確認)
            progress?.Report($"動画情報を取得中... (site={siteHost})");
            var urlInfo = await _iwaraApi.GetDownloadUrlAsync(videoId, siteHost);

            if (!urlInfo.Success)
            {
                progress?.Report($"エラー: {urlInfo.Error}");
                // 失敗しても仮登録(後で再取得可能)
                var failedVideo = new VideoInfo
                {
                    VideoId = videoId,
                    Title = $"Video {videoId}",
                    Url = url,
                    Status = DownloadStatus.Failed,
                    LastErrorMessage = urlInfo.Error
                };
                failedVideo.Id = _database.AddVideo(failedVideo);
                return null;
            }

            // 既に FileUuid で DB 内に同一ファイルがあれば(別 VideoId で登録済み等)、
            // ローカルファイルを再利用できるかチェック
            if (!string.IsNullOrEmpty(urlInfo.FileUuid))
            {
                var dup = _database.GetVideoByFileUuid(urlInfo.FileUuid);
                if (dup != null && !string.IsNullOrEmpty(dup.LocalFilePath) && File.Exists(dup.LocalFilePath))
                {
                    progress?.Report($"既存ファイルを再利用: {dup.LocalFilePath}");
                    // 既存レコードを返す(これ以上何もしない)
                    return null;
                }
            }

            var video = new VideoInfo
            {
                VideoId = videoId,
                Title = urlInfo.Title ?? videoId,
                Url = url,
                AuthorUsername = urlInfo.AuthorUsername ?? "",
                FileUuid = urlInfo.FileUuid ?? "",
                Site = siteHost,
                Status = DownloadStatus.Pending
            };

            video.Id = _database.AddVideo(video);

            return EnqueueDownload(video, false);
        }

        /// <summary>
        /// VideoIdでタスクを取得(アクティブ＋待機中)
        /// </summary>
        public DownloadTask? GetTask(string videoId)
        {
            if (_activeTasks.TryGetValue(videoId, out var activeTask))
                return activeTask;
            if (_pendingTasks.TryGetValue(videoId, out var pendingTask))
                return pendingTask;
            return null;
        }

        /// <summary>
        /// アクティブなタスク一覧を取得
        /// </summary>
        public List<DownloadTask> GetActiveTasks()
        {
            return _activeTasks.Values.ToList();
        }

        /// <summary>環境が準備できているか</summary>
        public bool IsEnvironmentReady => _iwaraApi.IsPythonConfigured && _iwaraApi.IsSetupDone && _iwaraApi.IsScriptReady;

        /// <summary>ログイン済みか</summary>
        public bool IsLoggedIn => _iwaraApi.IsLoggedIn;

        /// <summary>ログイン</summary>
        public Task<(bool Success, string? Error)> LoginAsync(string email, string password)
            => _iwaraApi.LoginAsync(email, password);

        /// <summary>ログアウト</summary>
        public void Logout() => _iwaraApi.Logout();

        /// <summary>トークン有効性検証(API 問い合わせ)</summary>
        public Task<(bool Valid, string? Error)> VerifyTokenAsync()
            => _iwaraApi.VerifyTokenAsync();

        /// <summary>トークンの有効期限</summary>
        public DateTime? TokenExpiresAt => _iwaraApi.TokenExpiresAt;

        /// <summary>セットアップ実行</summary>
        public Task<bool> RunSetupAsync(string pythonPath, IProgress<string>? progress = null)
            => _iwaraApi.RunSetupAsync(pythonPath, progress);

        /// <summary>環境チェック</summary>
        public (bool PythonReady, bool ScriptReady) CheckEnvironment()
            => _iwaraApi.CheckEnvironment();

        public void Dispose()
        {
            Stop();
            _fetchQueue.Writer.TryComplete();
            _priorityFetchQueue.Writer.TryComplete();
            _autoCheckTimer.Stop();
            // Elapsed のコールバックが ThreadPool に既に enqueue されていたら、
            // _slotAvailableSignal / _globalCts を触る経路があるため最大 5 秒だけ完了を待つ
            SpinWait.SpinUntil(
                () => Interlocked.CompareExchange(ref _isAutoChecking, 0, 0) == 0
                      && Interlocked.CompareExchange(ref _isProcessingQueue, 0, 0) == 0,
                5000);
            _autoCheckTimer.Dispose();
            _slotAvailableSignal.Dispose();
            _globalCts?.Dispose();
        }

        #region Migration

        /// <summary>
        /// Phase 3 マイグレーション: 既に DL 済みだが mp4 に iwara タグが
        /// 未書き込みのファイルに対して一括でタグを書き込む。
        ///
        /// DB.FileUuid が空の場合は iwara API で /video/{id} を叩いて FileUuid を
        /// 取得してから書き込む。削除された動画は API エラーで失敗扱い。
        /// </summary>
        /// <returns>(対象件数, タグ書き込み成功件数, スキップ件数, 失敗件数)</returns>
        // 旧 public API は StartMigrateExistingFiles() に統一して二重起動を防止
        private async Task<(int Total, int Tagged, int Skipped, int Failed)> MigrateExistingFilesCoreAsync(
            IProgress<string>? progress,
            CancellationToken cancellationToken)
        {
            var all = _database.GetAllVideos();
            var candidates = all.Where(v =>
                !string.IsNullOrEmpty(v.LocalFilePath)
                && File.Exists(v.LocalFilePath)
                && (v.LocalFilePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                    || v.LocalFilePath.EndsWith(".m4v", StringComparison.OrdinalIgnoreCase))
            ).ToList();

            int total = candidates.Count;
            int tagged = 0;
            int skipped = 0;
            int failed = 0;
            int processed = 0;

            var apiDelayMs = SettingsManager.Instance.Settings.ApiRequestDelayMs;

            progress?.Report($"マイグレーション開始: {total} 件");
            _logger.Info($"Phase3 マイグレーション開始: 候補 {total} 件");

            foreach (var video in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                processed++;

                try
                {
                    // 1. 既存タグ確認: ファイル側に既に正しいタグがあればスキップ
                    var (_, existingFid) = MetadataService.ReadIwaraTags(video.LocalFilePath);

                    // DB 側 FileUuid が空で、ファイル側に既にあれば取り込む
                    if (string.IsNullOrEmpty(video.FileUuid) && !string.IsNullOrEmpty(existingFid))
                    {
                        video.FileUuid = existingFid;
                        _database.UpdateVideo(video);
                    }

                    // タグが既に正しく書き込まれていればスキップ
                    if (!string.IsNullOrEmpty(existingFid)
                        && !string.IsNullOrEmpty(video.FileUuid)
                        && existingFid == video.FileUuid)
                    {
                        skipped++;
                        ReportProgressIfNeeded(progress, processed, total, tagged, skipped, failed);
                        continue;
                    }

                    // 2. DB に FileUuid が無い場合は iwara API で取得
                    if (string.IsNullOrEmpty(video.FileUuid))
                    {
                        progress?.Report($"API 取得中 ({processed}/{total}): {video.VideoId}");
                        var siteForApi = string.IsNullOrEmpty(video.Site) ? null : video.Site;
                        var urlInfo = await _iwaraApi.GetDownloadUrlAsync(video.VideoId, siteForApi);

                        if (!urlInfo.Success || string.IsNullOrEmpty(urlInfo.FileUuid))
                        {
                            failed++;
                            _logger.Warn($"FileUuid 取得失敗 ({video.VideoId} / {video.Title}): {urlInfo.Error ?? "no file_id"}");
                            ReportProgressIfNeeded(progress, processed, total, tagged, skipped, failed);
                            // レート制限遵守のため delay
                            if (apiDelayMs > 0)
                                await Task.Delay(apiDelayMs, cancellationToken);
                            continue;
                        }

                        video.FileUuid = urlInfo.FileUuid;
                        // 自動 site フォールバックで取れた場合は DB にも反映 (次回以降に直接 iwara.ai 指定)
                        if (!string.IsNullOrEmpty(urlInfo.ResolvedSite) && string.IsNullOrEmpty(video.Site))
                            video.Site = urlInfo.ResolvedSite;
                        _database.UpdateVideo(video);

                        // レート制限遵守のため delay
                        if (apiDelayMs > 0)
                            await Task.Delay(apiDelayMs, cancellationToken);
                    }

                    // 3. mp4 タグ書き込み
                    var ok = MetadataService.WriteIwaraTags(video.LocalFilePath, video.VideoId, video.FileUuid);
                    if (ok)
                    {
                        tagged++;
                        // 書き込んだフォルダのキャッシュを無効化
                        var dir = Path.GetDirectoryName(video.LocalFilePath);
                        if (!string.IsNullOrEmpty(dir)) IndexCacheService.Invalidate(dir);
                    }
                    else
                    {
                        failed++;
                        _logger.Warn($"タグ書き込み失敗: {video.LocalFilePath}");
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.Warn($"マイグレーション失敗 ({video.Title}): {ex.Message}");
                }

                ReportProgressIfNeeded(progress, processed, total, tagged, skipped, failed);
            }

            _logger.Info($"Phase3 マイグレーション完了: 合計 {total}, タグ付与 {tagged}, スキップ {skipped}, 失敗 {failed}");
            progress?.Report($"完了: タグ付与 {tagged} / {total} (スキップ {skipped}, 失敗 {failed})");
            return (total, tagged, skipped, failed);
        }

        private static void ReportProgressIfNeeded(IProgress<string>? progress, int processed, int total, int tagged, int skipped, int failed)
        {
            if (processed % 5 == 0 || processed == total)
            {
                progress?.Report($"処理中: {processed}/{total} (タグ付与 {tagged}, スキップ {skipped}, 失敗 {failed})");
            }
        }

        /// <summary>
        /// DB 内動画のサムネ URL を一括補完 + 実画像もキャッシュに DL。
        ///   - FileUuid あり → API ゼロで URL 組立 → サムネ画像をネット DL してキャッシュ
        ///   - FileUuid 無し → GetDownloadUrlAsync で URL 取得 → サムネ画像 DL してキャッシュ
        /// サムネ画像 DL は ThumbnailCacheService 経由 (レート制限あり: ApiRequestDelayMs)
        /// </summary>
        /// <returns>(対象, URL補完, サムネ画像DL成功, 失敗)</returns>
        // 旧 public API は StartBackfillThumbnails() に統一して二重起動を防止
        private async Task<(int Total, int UrlResolved, int ThumbDownloaded, int Failed)> BackfillThumbnailsCoreAsync(
            IProgress<string>? progress, CancellationToken ct)
        {
            var all = _database.GetAllVideos();
            // サムネ URL が空、もしくは URL あるがキャッシュファイルが無い動画を対象に
            var candidates = all.Where(v =>
                string.IsNullOrEmpty(v.ThumbnailUrl)
                || !System.IO.File.Exists(Services.ThumbnailCacheService.Instance.GetCachePath(v.VideoId))
            ).ToList();
            int total = candidates.Count;
            int urlResolved = 0, thumbDl = 0, failed = 0;
            var apiDelayMs = SettingsManager.Instance.Settings.ApiRequestDelayMs;
            var thumbSvc = Services.ThumbnailCacheService.Instance;

            progress?.Report($"サムネ補完開始: {total} 件");
            _logger.Info($"Thumbnail backfill: {total} candidates");

            int processed = 0;
            foreach (var video in candidates)
            {
                ct.ThrowIfCancellationRequested();
                processed++;

                try
                {
                    // 1. URL を確定 (UUID あれば組立, 無ければ API 取得)
                    if (string.IsNullOrEmpty(video.ThumbnailUrl))
                    {
                        if (!string.IsNullOrEmpty(video.FileUuid))
                        {
                            video.ThumbnailUrl = BuildThumbnailUrlFromUuid(video.FileUuid);
                            _database.UpdateVideo(video);
                            urlResolved++;
                        }
                        else if (_iwaraApi.IsLoggedIn)
                        {
                            progress?.Report($"API 取得中 ({processed}/{total}): {video.VideoId}");
                            var siteForApi = string.IsNullOrEmpty(video.Site) ? null : video.Site;
                            var info = await _iwaraApi.GetDownloadUrlAsync(video.VideoId, siteForApi);
                            if (info.Success && !string.IsNullOrEmpty(info.ThumbnailUrl))
                            {
                                video.ThumbnailUrl = info.ThumbnailUrl;
                                if (!string.IsNullOrEmpty(info.FileUuid) && string.IsNullOrEmpty(video.FileUuid))
                                    video.FileUuid = info.FileUuid;
                                _database.UpdateVideo(video);
                                urlResolved++;
                            }
                            else
                            {
                                failed++;
                                continue;
                            }
                            if (apiDelayMs > 0) await Task.Delay(apiDelayMs, ct);
                        }
                        else
                        {
                            failed++;
                            continue;
                        }
                    }

                    // 2. サムネ画像をネット DL (キャッシュ済みならスキップ)
                    bool ok = await thumbSvc.EnsureCachedAsync(video.VideoId, video.ThumbnailUrl, ct);
                    if (ok) thumbDl++;
                    else failed++;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    failed++;
                    _logger.Warn($"Thumbnail backfill failed ({video.VideoId}): {ex.Message}");
                }

                if (processed % 25 == 0 || processed == total)
                {
                    progress?.Report($"処理中: {processed}/{total} (URL確定 {urlResolved}, サムネDL {thumbDl}, 失敗 {failed})");
                }
            }

            _logger.Info($"Thumbnail backfill done: total={total}, urlResolved={urlResolved}, thumbDl={thumbDl}, failed={failed}");
            progress?.Report($"完了: URL確定 {urlResolved} / サムネDL {thumbDl} / 失敗 {failed} (合計 {total} 件)");
            return (total, urlResolved, thumbDl, failed);
        }

        /// <summary>iwara のサムネ URL を file_id (UUID) から組み立てる</summary>
        public static string BuildThumbnailUrlFromUuid(string fileUuid)
            => $"https://i.iwara.tv/image/thumbnail/{fileUuid}/thumbnail-00.jpg";

        #endregion

        #region Background Tasks (settings 画面を閉じても継続する長時間タスク)

        public event EventHandler<(string TaskName, string Message)>? BackgroundTaskProgress;
        public event EventHandler<(string TaskName, string Summary, bool Success)>? BackgroundTaskCompleted;

        public bool IsBackfillRunning { get; private set; }
        public bool IsMigrationRunning { get; private set; }

        /// <summary>サムネ URL + 画像をバックグラウンドで一括補完。既に走ってたら false (二重起動拒否)</summary>
        public bool StartBackfillThumbnails()
        {
            if (IsBackfillRunning) return false;
            IsBackfillRunning = true;
            var name = "サムネ補完";
            BackgroundTaskProgress?.Invoke(this, (name, "開始..."));

            // Task.Run 外で token をスナップショット (Stop→Start で _globalCts が差し替わる前に固定)
            var snapshot = _globalCts?.Token ?? CancellationToken.None;
            _ = Task.Run(async () =>
            {
                try
                {
                    var progress = new Progress<string>(msg =>
                        BackgroundTaskProgress?.Invoke(this, (name, msg)));
                    var (total, urlResolved, thumbDl, failed) = await BackfillThumbnailsCoreAsync(
                        progress, snapshot);
                    BackgroundTaskCompleted?.Invoke(this, (
                        name,
                        $"完了: URL確定 {urlResolved} / サムネDL {thumbDl} / 失敗 {failed} (合計 {total})",
                        failed < total));
                }
                catch (OperationCanceledException)
                {
                    BackgroundTaskCompleted?.Invoke(this, (name, "中止しました", false));
                }
                catch (Exception ex)
                {
                    _logger.Error("BackfillThumbnails error", ex);
                    BackgroundTaskCompleted?.Invoke(this, (name, $"エラー: {ex.Message}", false));
                }
                finally
                {
                    IsBackfillRunning = false;
                }
            });
            return true;
        }

        /// <summary>既存ファイルのタグ書き込みをバックグラウンドで実行</summary>
        public bool StartMigrateExistingFiles()
        {
            if (IsMigrationRunning) return false;
            if (!_iwaraApi.IsLoggedIn)
            {
                BackgroundTaskCompleted?.Invoke(this, ("タグマイグレーション", "ログインが必要です", false));
                return false;
            }
            IsMigrationRunning = true;
            var name = "タグマイグレーション";
            BackgroundTaskProgress?.Invoke(this, (name, "開始..."));

            // Task.Run 外で token をスナップショット
            var snapshot = _globalCts?.Token ?? CancellationToken.None;
            _ = Task.Run(async () =>
            {
                try
                {
                    var progress = new Progress<string>(msg =>
                        BackgroundTaskProgress?.Invoke(this, (name, msg)));
                    var (total, tagged, skipped, failed) = await MigrateExistingFilesCoreAsync(
                        progress, snapshot);
                    BackgroundTaskCompleted?.Invoke(this, (
                        name,
                        $"完了: タグ {tagged} / スキップ {skipped} / 失敗 {failed} (合計 {total})",
                        failed < total));
                }
                catch (OperationCanceledException)
                {
                    BackgroundTaskCompleted?.Invoke(this, (name, "中止しました", false));
                }
                catch (Exception ex)
                {
                    _logger.Error("MigrateExistingFiles error", ex);
                    BackgroundTaskCompleted?.Invoke(this, (name, $"エラー: {ex.Message}", false));
                }
                finally
                {
                    IsMigrationRunning = false;
                }
            });
            return true;
        }

        #endregion

        /// <summary>
        /// 動画のメタデータをJSONで保存
        /// </summary>
        private void SaveVideoMetadata(VideoInfo video, string videoPath)
        {
            try
            {
                var metadataPath = Path.ChangeExtension(videoPath, ".json");
                var metadata = new
                {
                    title = video.Title,
                    author = video.AuthorUsername,
                    authorId = video.AuthorUserId,
                    videoId = video.VideoId,
                    fileSize = video.FileSize,
                    fileSizeFormatted = video.FileSizeFormatted,
                    duration = video.DurationSeconds,
                    durationFormatted = video.DurationFormatted,
                    postedAt = video.PostedAt?.ToString("yyyy-MM-dd HH:mm:ss"),
                    downloadedAt = video.DownloadedAt?.ToString("yyyy-MM-dd HH:mm:ss"),
                    url = video.Url,
                    thumbnailUrl = video.ThumbnailUrl
                };

                var json = System.Text.Json.JsonSerializer.Serialize(metadata, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(metadataPath, json);
                _logger.Debug($"Metadata saved: {metadataPath}");
            }
            catch (Exception ex)
            {
                _logger.Warn($"Failed to save metadata for {video.Title}", ex);
            }
        }
    }
}
