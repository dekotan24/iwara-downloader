using System.Collections.Concurrent;
using IwaraDownloader.Models;
using IwaraDownloader.Utils;

namespace IwaraDownloader.Services
{
    /// <summary>
    /// ダウンロードマネージャー（IwaraApiService使用版）
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
        /// 起動時に未完了のダウンロードを再開
        /// </summary>
        public void ResumeIncompleteDownloads()
        {
            var settings = SettingsManager.Instance.Settings;
            if (!settings.ResumeDownloadsOnStartup)
            {
                _logger.Debug("Resume on startup is disabled");
                return;
            }

            // PendingまたはDownloading状態の動画を取得
            var pendingVideos = _database.GetVideosByStatus(DownloadStatus.Pending);
            var downloadingVideos = _database.GetVideosByStatus(DownloadStatus.Downloading);

            // Downloading状態のものはPendingにリセット（前回中断されたもの）
            foreach (var video in downloadingVideos)
            {
                video.Status = DownloadStatus.Pending;
                _database.UpdateVideo(video);
                pendingVideos.Add(video);
            }

            if (pendingVideos.Count == 0)
            {
                _logger.Debug("No incomplete downloads to resume");
                return;
            }

            _logger.Info($"Resuming {pendingVideos.Count} incomplete downloads");

            // キューに追加
            foreach (var video in pendingVideos)
            {
                SubscribedUser? user = null;
                if (video.SubscribedUserId.HasValue)
                {
                    user = _database.GetSubscribedUserById(video.SubscribedUserId.Value);
                }
                EnqueueDownload(video, video.SubscribedUserId.HasValue, user);
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

            // イベント発火（UIに反映）
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

                    // キャンセルされたタスク（_pendingTasksから削除済み）はスキップ
                    if (!_pendingTasks.ContainsKey(task.Video.VideoId))
                    {
                        _logger.Debug($"Skipping cancelled task: {task.Video.VideoId}");
                        continue;
                    }

                    // 待機中リストから削除
                    _pendingTasks.TryRemove(task.Video.VideoId, out _);

                    await _downloadSemaphore.WaitAsync(_globalCts.Token);
                    
                    // レート制限：DL開始前に設定値分待機
                    var settings = SettingsManager.Instance.Settings;
                    var delayMs = Math.Max(settings.DownloadDelayMs, 1000); // 最低1秒
                    System.Diagnostics.Debug.WriteLine($"RateLimit: waiting {delayMs}ms before download...");
                    await Task.Delay(delayMs, _globalCts.Token);
                    
                    _ = ExecuteDownloadAsync(task);
                }
            }
            finally
            {
                _isProcessingQueue = false;
            }
        }

        /// <summary>
        /// ダウンロードを実行（IwaraApiService使用）
        /// </summary>
        private async Task ExecuteDownloadAsync(DownloadTask task)
        {
            try
            {
                task.CancellationTokenSource = new CancellationTokenSource();
                task.Status = DownloadStatus.Downloading;
                task.StartedAt = DateTime.Now;
                _activeTasks[task.Video.VideoId] = task;

                // DBのステータスも更新
                task.Video.Status = DownloadStatus.Downloading;
                _database.UpdateVideo(task.Video);

                TaskStatusChanged?.Invoke(this, task);

                var settings = SettingsManager.Instance.Settings;
                var video = task.Video;

                // --- 事前に動画情報を取得 (FileUuid / 最新 title / author) ---
                // ここで得た FileUuid を用いて既存ファイル検出を行う
                var urlInfo = await _iwaraApi.GetDownloadUrlAsync(video.VideoId);
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

                // ダウンロード実行（IwaraApiService使用）
                var progress = new Progress<string>(msg =>
                {
                    System.Diagnostics.Debug.WriteLine($"Download progress: {msg}");
                });

                // パーセント進捗を受け取ってイベント発火
                var percentProgress = new Progress<double>(pct =>
                {
                    task.Progress = pct;
                    TaskProgressChanged?.Invoke(this, task);
                });

                var (success, error) = await _iwaraApi.DownloadVideoAsync(
                    video.VideoId,
                    outputPath,
                    progress,
                    percentProgress);

                if (success && File.Exists(outputPath))
                {
                    task.Status = DownloadStatus.Completed;
                    task.Progress = 100;
                    task.CompletedAt = DateTime.Now;

                    video.LocalFilePath = outputPath;
                    video.Status = DownloadStatus.Completed;
                    video.DownloadedAt = DateTime.Now;
                    video.FileSize = new FileInfo(outputPath).Length;

                    // mp4 にカスタムタグ (video_id / file_id) を書き込む
                    if (!string.IsNullOrEmpty(video.FileUuid))
                    {
                        var tagOk = MetadataService.WriteIwaraTags(outputPath, video.VideoId, video.FileUuid);
                        if (tagOk)
                            _logger.Debug($"Wrote iwara tags: {Path.GetFileName(outputPath)}");
                        else
                            _logger.Warn($"Failed to write iwara tags: {Path.GetFileName(outputPath)}");
                    }

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
                
                if (task.Video.RetryCount < maxRetry)
                {
                    _logger.Info($"Retrying download: {task.Video.Title} (attempt {task.Video.RetryCount + 1}/{maxRetry})");
                    await Task.Delay(5000);
                    EnqueueDownload(task.Video, task.IsSubscriptionDownload, task.SubscribedUser);
                }
                else
                {
                    // 最終的に失敗した場合はエラー音を再生して通知
                    _logger.Info($"Final failure, playing error sound: {task.Video.Title}");
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
        /// 動画情報を再取得（タイトル等が取れていない場合用）
        /// </summary>
        public async Task<bool> RefreshVideoInfoAsync(VideoInfo video, IProgress<string>? progress = null)
        {
            try
            {
                progress?.Report($"動画情報を再取得中: {video.VideoId}");

                var urlInfo = await _iwaraApi.GetDownloadUrlAsync(video.VideoId);

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
        /// 新着動画をチェック（全チャンネル）
        /// </summary>
        public async Task CheckForNewVideosAsync(IProgress<string>? progress = null)
        {
            var users = _database.GetEnabledSubscribedUsers();
            var settings = SettingsManager.Instance.Settings;
            var isFirstUser = true;

            foreach (var user in users)
            {
                // チャンネル間のディレイ（最初のユーザー以外）
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
        /// 新着動画をチェック（単一チャンネル）
        /// </summary>
        public async Task CheckForNewVideosAsync(SubscribedUser user, IProgress<string>? progress = null)
        {
            try
            {
                progress?.Report($"{user.Username}の動画を確認中...");

                // IwaraApiServiceで動画一覧を取得
                var videos = await _iwaraApi.GetUserVideosAsync(user.UserId, progress);

                var newVideos = new List<VideoInfo>();

                foreach (var video in videos)
                {
                    if (!_database.VideoExists(video.VideoId))
                    {
                        video.AuthorUserId = user.UserId;
                        video.AuthorUsername = user.Username;
                        video.SubscribedUserId = user.Id;
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"チェックエラー ({user.Username}): {ex.Message}");
                progress?.Report($"エラー ({user.Username}): {ex.Message}");
            }
        }

        /// <summary>
        /// 購読ユーザーを追加
        /// </summary>
        public async Task<SubscribedUser?> AddSubscribedUserAsync(string input, IProgress<string>? progress = null)
        {
            string? username;

            if (Helpers.IsUserProfileUrl(input))
            {
                username = Helpers.ExtractUsernameFromUrl(input);
            }
            else
            {
                username = input.Trim();
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
            progress?.Report($"{username}のプロフィールを確認中...");
            var videos = await _iwaraApi.GetUserVideosAsync(username, progress);

            if (videos.Count == 0)
            {
                progress?.Report($"{username}: 動画が見つかりませんでした（ユーザーが存在しないか、動画が0件の可能性があります）");
            }

            var profileUrl = $"https://www.iwara.tv/profile/{username}/videos";

            var user = new SubscribedUser
            {
                UserId = username,
                Username = username,
                ProfileUrl = profileUrl,
                CreatedAt = DateTime.Now,
                LastCheckedAt = DateTime.Now,
                IsEnabled = true,
                TotalVideoCount = videos.Count
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

            // 既にDBにあるか確認
            var existing = _database.GetVideoByVideoId(videoId);
            if (existing != null)
            {
                return EnqueueDownload(existing, false);
            }

            // IwaraApiServiceでダウンロードURL取得（動画情報確認）
            progress?.Report("動画情報を取得中...");
            var urlInfo = await _iwaraApi.GetDownloadUrlAsync(videoId);

            if (!urlInfo.Success)
            {
                progress?.Report($"エラー: {urlInfo.Error}");
                // 失敗しても仮登録（後で再取得可能）
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
                Status = DownloadStatus.Pending
            };

            video.Id = _database.AddVideo(video);

            return EnqueueDownload(video, false);
        }

        /// <summary>
        /// VideoIdでタスクを取得（アクティブ＋待機中）
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

        /// <summary>トークン有効性検証（API 問い合わせ）</summary>
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
        public async Task<(int Total, int Tagged, int Skipped, int Failed)> MigrateExistingFilesAsync(
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (!_iwaraApi.IsLoggedIn)
            {
                throw new InvalidOperationException("ログインが必要です。マイグレーションを実行する前にログインしてください。");
            }

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
                        var urlInfo = await _iwaraApi.GetDownloadUrlAsync(video.VideoId);

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
