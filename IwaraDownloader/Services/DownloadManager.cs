using System.Collections.Concurrent;
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
        private readonly SemaphoreSlim _downloadSemaphore;
        private readonly System.Timers.Timer _autoCheckTimer;
        private CancellationTokenSource? _globalCts;
        private bool _isRunning;
        private bool _isProcessingQueue;

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

            var maxConcurrent = SettingsManager.Instance.Settings.MaxConcurrentDownloads;
            _downloadSemaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);

            // 自動チェックタイマー
            _autoCheckTimer = new System.Timers.Timer();
            _autoCheckTimer.Elapsed += async (s, e) => await CheckForNewVideosAsync();
        }

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
        /// 同時ダウンロード数を更新
        /// </summary>
        public void UpdateConcurrentLimit(int limit)
        {
            SettingsManager.Instance.Settings.MaxConcurrentDownloads = limit;
            SettingsManager.Instance.Save();
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
            // 重複実行防止
            if (_isProcessingQueue) return;
            _isProcessingQueue = true;

            try
            {
                while (_isRunning && _globalCts != null && !_globalCts.Token.IsCancellationRequested)
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

                    await _downloadSemaphore.WaitAsync(_globalCts.Token);

                    // レート制限：DL開始前に設定値分待機
                    var settings = SettingsManager.Instance.Settings;
                    var delayMs = Math.Max(settings.DownloadDelayMs, 1000); // 最低1秒
                    System.Diagnostics.Debug.WriteLine($"RateLimit: waiting {delayMs}ms before download...");
                    await Task.Delay(delayMs, _globalCts.Token);

                    // スレッドプール上で起動 (呼び出し元が UI スレッドのため、await の continuation が
                    // UI に戻って WriteIwaraTags 等の同期I/Oで詰まるのを防ぐ)
                    _ = Task.Run(() => ExecuteDownloadAsync(task));
                }
            }
            finally
            {
                _isProcessingQueue = false;
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
                _downloadSemaphore.Release();
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

        /// <summary>
        /// 新着動画をチェック(全チャンネル)
        /// </summary>
        public async Task CheckForNewVideosAsync(IProgress<string>? progress = null)
        {
            var users = _database.GetEnabledSubscribedUsers();
            var settings = SettingsManager.Instance.Settings;
            var isFirstUser = true;

            foreach (var user in users)
            {
                // チャンネル間のディレイ(最初のユーザー以外)
                if (!isFirstUser)
                {
                    var channelDelay = Math.Max(settings.ChannelCheckDelayMs, 1000);
                    progress?.Report($"次のチャンネルまで{channelDelay / 1000.0:F1}秒待機中...");
                    System.Diagnostics.Debug.WriteLine($"RateLimit: waiting {channelDelay}ms before next channel...");
                    await Task.Delay(channelDelay);
                }
                isFirstUser = false;

                await CheckForNewVideosAsync(user, progress);
            }

            AutoCheckCompleted?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 新着動画をチェック(単一チャンネル)
        /// </summary>
        public async Task CheckForNewVideosAsync(SubscribedUser user, IProgress<string>? progress = null)
        {
            try
            {
                progress?.Report($"{user.Username}の動画を確認中...");

                // IwaraApiServiceで動画一覧を取得 (user.Site で iwara.tv / iwara.ai 振り分け)
                var siteForApi = string.IsNullOrEmpty(user.Site) ? null : user.Site;
                var videos = await _iwaraApi.GetUserVideosAsync(user.UserId, progress, siteForApi);

                var newVideos = new List<VideoInfo>();

                foreach (var video in videos)
                {
                    if (!_database.VideoExists(video.VideoId))
                    {
                        video.AuthorUserId = user.UserId;
                        video.AuthorUsername = user.Username;
                        video.SubscribedUserId = user.Id;
                        // user.Site を継承 (購読チャンネルが iwara.ai なら動画も iwara.ai)
                        if (string.IsNullOrEmpty(video.Site))
                            video.Site = user.Site;
                        video.Status = DownloadStatus.Pending;
                        video.Id = _database.AddVideo(video);
                        newVideos.Add(video);
                    }
                }

                // ユーザー情報更新
                user.LastCheckedAt = DateTime.Now;
                user.TotalVideoCount = videos.Count;
                _database.UpdateSubscribedUser(user);

                if (newVideos.Count > 0)
                {
                    NewVideosFound?.Invoke(this, (user, newVideos));

                    // 自動DLオプションが有効な場合のみキューに追加
                    if (SettingsManager.Instance.Settings.AutoDownloadOnCheck)
                    {
                        foreach (var video in newVideos)
                        {
                            EnqueueDownload(video, true, user);
                        }
                    }

                    NotificationService.Instance.NotifyNewVideosFound(user.Username, newVideos.Count);
                }

                progress?.Report($"{user.Username}: {videos.Count}件の動画、{newVideos.Count}件の新着");

                // orphan な Pending (前回の追加でエンキュー漏れた等) も拾い直す
                EnqueuePendingVideosForUser(user);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"チェックエラー ({user.Username}): {ex.Message}");
                progress?.Report($"エラー ({user.Username}): {ex.Message}");
            }
        }

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
        /// 購読ユーザーを追加
        /// </summary>
        public async Task<SubscribedUser?> AddSubscribedUserAsync(string input, IProgress<string>? progress = null)
        {
            string? username;
            // URL から site (www.iwara.tv / www.iwara.ai) を判定
            string siteHost = Helpers.ExtractSiteFromUrl(input);

            if (Helpers.IsUserProfileUrl(input))
            {
                username = Helpers.ExtractUsernameFromUrl(input);
            }
            else
            {
                username = input.Trim();
                // URL じゃない (ユーザー名直接入力) → デフォルト iwara.tv
                siteHost = Helpers.SiteTv;
            }

            if (string.IsNullOrEmpty(username))
            {
                return null;
            }

            // 既に存在するか確認
            var existing = _database.GetSubscribedUserByUserId(username);
            if (existing != null)
            {
                progress?.Report($"{username}は既に登録済みです");
                return existing;
            }

            // IwaraApiServiceで動画一覧を取得
            progress?.Report($"{username}のプロフィールを確認中... (site={siteHost})");
            var videos = await _iwaraApi.GetUserVideosAsync(username, progress, siteHost);

            if (videos.Count == 0)
            {
                progress?.Report($"{username}: 動画が見つかりませんでした(ユーザーが存在しないか、動画が0件の可能性があります)");
            }

            var profileUrl = $"https://{siteHost}/profile/{username}/videos";

            var user = new SubscribedUser
            {
                UserId = username,
                Username = username,
                ProfileUrl = profileUrl,
                CreatedAt = DateTime.Now,
                LastCheckedAt = DateTime.Now,
                IsEnabled = true,
                TotalVideoCount = videos.Count,
                Site = siteHost,
            };

            user.Id = _database.AddSubscribedUser(user);

            // 取得した動画をDBに保存
            var addedCount = 0;
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
                    addedCount++;
                }
            }

            progress?.Report($"{username}: {videos.Count}件の動画を登録しました");

            // 新着動画があればイベント発火
            if (addedCount > 0)
            {
                var addedVideos = _database.GetVideosBySubscribedUser(user.Id);
                NewVideosFound?.Invoke(this, (user, addedVideos));
            }

            // AutoDownloadOnCheck が ON なら追加直後にキューへ
            EnqueuePendingVideosForUser(user);

            return user;
        }

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
            _autoCheckTimer.Dispose();
            _downloadSemaphore.Dispose();
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
        public Task<(int Total, int Tagged, int Skipped, int Failed)> MigrateExistingFilesAsync(
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (!_iwaraApi.IsLoggedIn)
            {
                throw new InvalidOperationException("ログインが必要です。マイグレーションを実行する前にログインしてください。");
            }

            // UI スレッドをブロックしないようにバックグラウンドスレッドで実行
            // (TagLib I/O と SQLite I/O が同期処理のため、UI から直接 await すると
            //  最初の await が来るまでフリーズする)
            return Task.Run(() => MigrateExistingFilesCoreAsync(progress, cancellationToken), cancellationToken);
        }

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
