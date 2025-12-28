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

            // 自動チェック開始
            UpdateAutoCheckTimer();

            // 待機中のタスクを処理開始
            _ = ProcessQueueAsync();
        }

        /// <summary>
        /// ダウンロードマネージャーを停止
        /// </summary>
        public void Stop()
        {
            if (!_isRunning) return;

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

                    // 待機中リストから削除
                    _pendingTasks.TryRemove(task.Video.VideoId, out _);

                    await _downloadSemaphore.WaitAsync(_globalCts.Token);
                    
                    // レート制限：DL開始前に設定値分待機
                    var settings = SettingsManager.Instance.Settings;
                    var delayMs = Math.Max(settings.DownloadDelayMs, 1000); // 最侎1秒
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

                // 出力パスを決定
                string outputPath;
                if (task.IsSubscriptionDownload && task.SubscribedUser != null)
                {
                    // チャンネル別保存先を使用
                    var savePath = task.SubscribedUser.GetSavePath(settings.DownloadFolder);
                    outputPath = Path.Combine(savePath, Helpers.SanitizeFileName(video.Title) + ".mp4");
                }
                else if (task.IsSubscriptionDownload)
                {
                    outputPath = Helpers.GetSubscriptionDownloadPath(
                        settings.DownloadFolder,
                        video.AuthorUsername,
                        video.Title);
                }
                else
                {
                    outputPath = Helpers.GetSingleDownloadPath(
                        settings.DownloadFolder,
                        video.AuthorUsername,
                        video.Title);
                }

                // フォルダ作成
                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

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
                    _database.UpdateVideo(video);

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
                task.Status = DownloadStatus.Paused;
                task.Video.Status = DownloadStatus.Paused;
                _database.UpdateVideo(task.Video);
            }
            catch (Exception ex)
            {
                task.Status = DownloadStatus.Failed;
                task.ErrorMessage = ex.Message;
                task.Video.RetryCount++;
                task.Video.LastErrorMessage = ex.Message;
                task.Video.Status = DownloadStatus.Failed;
                _database.UpdateVideo(task.Video);

                var maxRetry = SettingsManager.Instance.Settings.MaxRetryCount;
                if (task.Video.RetryCount < maxRetry)
                {
                    await Task.Delay(5000);
                    EnqueueDownload(task.Video, task.IsSubscriptionDownload, task.SubscribedUser);
                }
                else
                {
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

                var (success, downloadUrl, quality, title, error) = await _iwaraApi.GetDownloadUrlAsync(video.VideoId);

                if (success && !string.IsNullOrEmpty(title))
                {
                    video.Title = title;
                    _database.UpdateVideo(video);
                    progress?.Report($"タイトル取得成功: {title}");
                    return true;
                }
                else
                {
                    progress?.Report($"取得失敗: {error}");
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
            var (success, downloadUrl, quality, title, error) = await _iwaraApi.GetDownloadUrlAsync(videoId);

            if (!success)
            {
                progress?.Report($"エラー: {error}");
                // 失敗しても仮登録（後で再取得可能）
                var failedVideo = new VideoInfo
                {
                    VideoId = videoId,
                    Title = $"Video {videoId}",
                    Url = url,
                    Status = DownloadStatus.Failed,
                    LastErrorMessage = error
                };
                failedVideo.Id = _database.AddVideo(failedVideo);
                return null;
            }

            var video = new VideoInfo
            {
                VideoId = videoId,
                Title = title ?? videoId,
                Url = url,
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

        /// <summary>環境が準備できているか</summary>
        public bool IsEnvironmentReady => _iwaraApi.IsPythonConfigured && _iwaraApi.IsSetupDone && _iwaraApi.IsScriptReady;

        /// <summary>ログイン済みか</summary>
        public bool IsLoggedIn => _iwaraApi.IsLoggedIn;

        /// <summary>ログイン</summary>
        public Task<(bool Success, string? Error)> LoginAsync(string email, string password)
            => _iwaraApi.LoginAsync(email, password);

        /// <summary>ログアウト</summary>
        public void Logout() => _iwaraApi.Logout();

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
    }
}
