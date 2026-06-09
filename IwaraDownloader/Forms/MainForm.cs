using IwaraDownloader.Models;
using IwaraDownloader.Services;
using IwaraDownloader.Utils;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace IwaraDownloader.Forms
{
    /// <summary>
    /// メインフォーム(JD2風ツリー構造UI)
    /// </summary>
    public partial class MainForm : Form
    {
        private readonly DownloadManager _downloadManager;
        private readonly DatabaseService _database;
        private readonly WebServerService _webServer;
        private bool _isClosing = false;
        
        // 現在選択中のチャンネル
        private SubscribedUser? _selectedChannel = null;
        
        // 特殊ノード用の定数
        private const string NODE_ALL_VIDEOS = "__ALL_VIDEOS__";
        private const string NODE_ALL_DOWNLOADS = "__ALL_DOWNLOADS__";
        private const string NODE_NOT_DOWNLOADED = "__NOT_DOWNLOADED__";
        private const string NODE_DOWNLOADED = "__DOWNLOADED__";
        private const string NODE_SKIPPED = "__SKIPPED__";
        private const string NODE_FAILED_VIDEOS = "__FAILED_VIDEOS__";
        private const string NODE_SINGLE_VIDEOS = "__SINGLE_VIDEOS__";
        
        // フィルター用の全動画キャッシュ(フィルター前)
        private List<VideoInfo> _allVideoList = new();
        
        // 表示用の動画リスト(フィルター・ソート適用後)
        private List<VideoInfo> _displayVideoList = new();
        
        // 仮想モード用キャッシュ
        private ListViewItem[] _itemCache = Array.Empty<ListViewItem>();
        private int _cacheStartIndex = 0;
        
        // ソート設定
        private int _sortColumn = 5; // デフォルトは追加日時
        private SortOrder _sortOrder = SortOrder.Descending; // デフォルトは降順(新しい順)
        
        // フィルター
        private string _currentFilterText = "";

        // サムネ表示用 (タイルモード)
        private ImageList? _thumbImageList;
        private Image? _placeholderThumb;
        private const int ThumbWidth = 160;
        private const int ThumbHeight = 90;

        public MainForm()
        {
            InitializeComponent();
            _downloadManager = new DownloadManager();
            _database = DatabaseService.Instance;
            _webServer = new WebServerService();
            _webServer.SetDownloadManager(_downloadManager);
            WebServerServiceHolder.Instance = _webServer;
        }

        #region Form Events

        private void MainForm_Load(object sender, EventArgs e)
        {
            // スプラッシュ更新
            SplashForm.UpdateStatus("設定を読み込み中...", 10);

            // 設定読み込み
            var settings = SettingsManager.Instance.Settings;

            // タスクトレイアイコン設定
            SplashForm.UpdateStatus("システムトレイを初期化中...", 20);
            try
            {
                notifyIcon.Icon = this.Icon ?? SystemIcons.Application;
            }
            catch
            {
                notifyIcon.Icon = SystemIcons.Application;
            }

            // 通知サービスにNotifyIconを設定
            NotificationService.Instance.SetNotifyIcon(notifyIcon);

            // 起動時最小化
            if (settings.StartMinimized)
            {
                this.WindowState = FormWindowState.Minimized;
                this.ShowInTaskbar = false;
            }

            // イベント登録
            SplashForm.UpdateStatus("イベントを登録中...", 30);
            _downloadManager.TaskProgressChanged += OnTaskProgressChanged;
            _downloadManager.TaskStatusChanged += OnTaskStatusChanged;
            _downloadManager.NewVideosFound += OnNewVideosFound;
            _downloadManager.AutoCheckCompleted += OnAutoCheckCompleted;
            _downloadManager.BackgroundTaskProgress += OnBackgroundTaskProgress;
            _downloadManager.BackgroundTaskCompleted += OnBackgroundTaskCompleted;
            _downloadManager.UserAddStatusChanged += (_, msg) => PostToUi(() => UpdateStatusBar(msg));
            _downloadManager.UserAdded += (_, _) => PostToUi(() => RefreshChannelTree());

            // 環境チェック
            SplashForm.UpdateStatus("環境をチェック中...", 40);
            CheckEnvironment();

            // ログイン状態確認
            SplashForm.UpdateStatus("ログイン状態を確認中...", 50);
            UpdateLoginStatus();

            // ツリー初期化
            SplashForm.UpdateStatus("チャンネルデータを読み込み中...", 60);
            RefreshChannelTree();

            // ListViewソーター初期化
            SplashForm.UpdateStatus("UIを初期化中...", 70);
            InitializeListViewSorter();

            // 動画コンテキストメニューは Designer で構築済み (Items.AddRange は InitializeComponent 内で完了)

            // UI モード復元 (NSFW フィルタ / 表示モード / クリップボード監視)
            // CheckedChanged ハンドラが走る → リスナー登録・設定保存が連動する
            SetNsfwFilter(settings.NsfwFilterMode);
            btnViewMode.Checked = settings.VideoListViewMode == 1;
            btnClipMonitor.Checked = settings.EnableClipboardMonitor;

            // ダウンロードマネージャー開始
            SplashForm.UpdateStatus("ダウンロードマネージャーを開始中...", 80);
            _downloadManager.Start();

            // 起動時に未完了ダウンロードを再開
            SplashForm.UpdateStatus("未完了タスクを復元中...", 90);
            _downloadManager.ResumeIncompleteDownloads();
            RefreshChannelTree();
            RefreshVideoList();

            // 起動完了
            SplashForm.UpdateStatus("起動完了", 100);

            // Webメディアサーバー自動開始
            if (settings.WebServerAutoStart)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _webServer.StartAsync(settings.WebServerPort, settings.WebServerBindAll);
                        LoggingService.Instance.Info($"Web media server auto-started on port {settings.WebServerPort}");
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Instance.Error("Web media server auto-start failed", ex);
                    }
                });
            }

            // 起動時更新チェック
            if (settings.CheckUpdateOnStartup)
            {
                _ = CheckForUpdatesOnStartupAsync();
            }
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            // フォームが表示されたらスプラッシュを閉じる
            SplashForm.CloseSplash();

            // 初回起動時: 環境が未セットアップなら自動でウィザード起動
            var (pythonReady, scriptReady) = _downloadManager.CheckEnvironment();
            if (!pythonReady || !scriptReady)
            {
                BeginInvoke((Action)(() => ShowSetupWizard(autoTriggered: true)));
            }
        }

        /// <summary>
        /// セットアップウィザードを開く
        /// </summary>
        private void ShowSetupWizard(bool autoTriggered = false)
        {
            using var wiz = new SetupWizardForm();
            var result = wiz.ShowDialog(this);
            if (result == DialogResult.OK)
            {
                btnSetup.BackColor = SystemColors.Control;
                MessageBox.Show("セットアップが完了しました!", "完了",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (autoTriggered)
            {
                UpdateStatusBar("セットアップ未完了。「環境セットアップ」ボタンから再実行できます。");
            }
            CheckEnvironment();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !_isClosing)
            {
                var settings = SettingsManager.Instance.Settings;
                if (settings.MinimizeToTray)
                {
                    e.Cancel = true;
                    this.WindowState = FormWindowState.Minimized;
                    this.ShowInTaskbar = false;
                    return;
                }
            }

            _downloadManager.Stop();
            // Webサーバー停止
            try { _webServer.StopAsync().Wait(5000); } catch { }
            _webServer.Dispose();
            // mp4 タグ書き込み中の moov atom 破損を防ぐため、書き込み完了まで最大10秒待機
            // (進行中の書き込みが無ければ即座に戻る)
            Services.MetadataService.WaitForWritesToComplete(10000);
            _downloadManager.Dispose();
            notifyIcon.Visible = false;

            // クリップボードリスナー / サムネイベントを解除 (OS リソース漏れ防止)
            if (_clipboardListenerRegistered)
            {
                try { RemoveClipboardFormatListener(this.Handle); } catch { }
                _clipboardListenerRegistered = false;
            }
            if (_thumbImageList != null)
            {
                try { Services.ThumbnailCacheService.Instance.ThumbnailReady -= OnThumbnailReady; } catch { }
                try { _thumbImageList.Dispose(); } catch { }
                try { _placeholderThumb?.Dispose(); } catch { }
                _thumbImageList = null;
                _placeholderThumb = null;
            }
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                var settings = SettingsManager.Instance.Settings;
                if (settings.MinimizeToTray)
                {
                    this.ShowInTaskbar = false;
                }
            }
        }

        /// <summary>
        /// フォームレベルのキーボードショートカット
        /// </summary>
        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            // F5: 新着チェック
            if (e.KeyCode == Keys.F5)
            {
                e.Handled = true;
                if (btnCheckNow.Enabled)
                {
                    btnCheckNow_Click(sender, e);
                }
            }
            // Ctrl+D: 選択動画をダウンロード
            else if (e.Control && e.KeyCode == Keys.D)
            {
                e.Handled = true;
                if (listViewVideos.SelectedIndices.Count > 0)
                {
                    menuVidDownload_Click(sender, e);
                }
            }
            // Ctrl+F: フィルターボックスにフォーカス
            else if (e.Control && e.KeyCode == Keys.F)
            {
                e.Handled = true;
                txtVideoFilter.Focus();
                txtVideoFilter.SelectAll();
            }
        }

        #endregion

        #region Environment Check

        private void CheckEnvironment()
        {
            var (pythonReady, scriptReady) = _downloadManager.CheckEnvironment();
            bool setupComplete = pythonReady && scriptReady;

            // セットアップ完了時はボタンと前後セパレータを非表示
            btnSetup.Visible = !setupComplete;
            toolStripSeparator3.Visible = !setupComplete;
            toolStripSeparator4.Visible = !setupComplete;

            if (!setupComplete)
            {
                UpdateStatusBar("環境が未セットアップです。「環境セットアップ」ボタンをクリックしてください。");
                btnSetup.BackColor = Color.Yellow;
            }
            else if (!_downloadManager.IsLoggedIn)
            {
                UpdateStatusBar("ログインが必要です。「ログイン」ボタンをクリックしてください。");
            }
            else
            {
                UpdateStatusBar("準備完了");
                // トークン有効性を API で非同期検証(失敗時は内部でログアウトされる)
                _ = VerifyLoginInBackgroundAsync();
            }
        }

        /// <summary>
        /// 起動時にサーバー側でトークンが有効か検証する。
        /// 失敗した場合はログイン状態 UI を更新し、ステータスバーに案内を出す。
        /// </summary>
        private async Task VerifyLoginInBackgroundAsync()
        {
            try
            {
                var (valid, error) = await _downloadManager.VerifyTokenAsync();
                if (!valid)
                {
                    // VerifyTokenAsync の中で必要に応じて内部トークンは破棄済み
                    this.BeginInvoke((Action)(() =>
                    {
                        UpdateLoginStatus();
                        UpdateStatusBar($"ログインセッションが無効です: {error ?? "不明"}。再ログインしてください。");
                    }));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VerifyToken error: {ex.Message}");
            }
        }

        private void btnSetup_Click(object sender, EventArgs e)
        {
            ShowSetupWizard();
        }

        #endregion

        #region Login

        private void UpdateLoginStatus()
        {
            if (_downloadManager.IsLoggedIn)
            {
                lblLoginStatus.Text = "(ログイン済)";
                lblLoginStatus.ForeColor = Color.Green;
                btnLogin.Text = "ログアウト";
            }
            else
            {
                lblLoginStatus.Text = "(未ログイン)";
                lblLoginStatus.ForeColor = Color.Gray;
                btnLogin.Text = "ログイン";
            }
        }

        private async void btnLogin_Click(object sender, EventArgs e)
        {
            if (_downloadManager.IsLoggedIn)
            {
                var result = MessageBox.Show("ログアウトしますか？", "ログアウト", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    _downloadManager.Logout();
                    UpdateLoginStatus();
                    UpdateStatusBar("ログアウトしました");
                }
            }
            else
            {
                await DoLoginAsync();
            }
        }

        private async Task DoLoginAsync()
        {
            if (!_downloadManager.IsEnvironmentReady)
            {
                MessageBox.Show("先に環境セットアップを実行してください。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var email = ShowInputDialog("ログイン", "iwaraのメールアドレスを入力:");
            if (string.IsNullOrEmpty(email)) return;

            var password = ShowPasswordDialog("ログイン", "パスワードを入力:");
            if (string.IsNullOrEmpty(password)) return;

            btnLogin.Enabled = false;
            UpdateStatusBar("ログイン中...");

            try
            {
                var (success, error) = await _downloadManager.LoginAsync(email, password);
                if (success)
                {
                    // ログイン成功時にメールアドレスを設定に保存(パスワードは保存しない)
                    var settings = SettingsManager.Instance.Settings;
                    settings.IwaraEmail = email;
                    SettingsManager.Instance.Save();
                    
                    UpdateStatusBar("ログイン完了！");
                    MessageBox.Show("ログインに成功しました！", "ログイン成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    UpdateStatusBar("ログインに失敗しました");
                    MessageBox.Show($"ログインに失敗しました:\n{error}", "ログイン失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                UpdateStatusBar("ログインエラー");
                MessageBox.Show($"ログイン中にエラーが発生しました:\n{ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnLogin.Enabled = true;
                UpdateLoginStatus();
            }
        }

        #endregion

        #region Toolbar Buttons

        private void btnAddUser_Click(object sender, EventArgs e)
        {
            var input = ShowInputDialog("チャンネル追加", "ユーザー名またはプロフィールURLを入力:");
            if (string.IsNullOrEmpty(input)) return;

            var isUrl = input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        input.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

            if (isUrl)
            {
                if (!Helpers.IsUserProfileUrl(input))
                {
                    MessageBox.Show(
                        "iwara.tvのプロフィールURLを入力してください。\n\n対応形式: https://www.iwara.tv/profile/username",
                        "無効なURL",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }
            else
            {
                if (!Helpers.IsValidUsername(input))
                {
                    MessageBox.Show(
                        "無効なユーザー名です。\n\nユーザー名には英数字、@、_、- のみ使用できます。",
                        "無効な入力",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }

            _downloadManager.EnqueueSubscribedUser(input);
        }

        private async void btnAddVideo_Click(object sender, EventArgs e)
        {
            var url = ShowInputDialog("動画追加", "動画URLを入力:");
            if (string.IsNullOrEmpty(url)) return;

            if (!Helpers.IsVideoUrl(url))
            {
                MessageBox.Show(
                    "iwara.tvの動画URLを入力してください。\n\n対応形式: https://www.iwara.tv/video/xxxxx",
                    "無効なURL",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            await AddVideoAsync(url);
        }

        private void btnCheckNow_Click(object sender, EventArgs e)
        {
            // 実際の取得は統合ワーカーが 1 件ずつ処理する (チャンネル追加との「被り」防止)。
            // ここではキューに積むだけで即戻る。進捗はステータスバーに随時表示される。
            _downloadManager.EnqueueAllUsersForCheck();
        }

        private void btnStartAll_Click(object sender, EventArgs e)
        {
            _downloadManager.Start();
            UpdateStatusBar("ダウンロード開始");
        }

        private void btnStopAll_Click(object sender, EventArgs e)
        {
            _downloadManager.CancelAllTasks();
            UpdateStatusBar("全てのダウンロードを停止しました");
            RefreshChannelTree();
            RefreshVideoList();
        }

        private void btnSettings_Click(object sender, EventArgs e)
        {
            using var form = new SettingsForm(_downloadManager);
            // Owner を MainForm にしておく (SettingsForm 内からモードレス子ウィンドウを
            // MainForm 直下に置きたい場合に this.Owner で取得できるように)
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                _downloadManager.UpdateAutoCheckTimer();
                _downloadManager.NotifyConcurrentLimitChanged();
            }
        }

        #endregion

        #region URL Input

        private async void txtUrl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await ProcessUrlInput();
            }
        }

        private async void btnPasteAndAdd_Click(object sender, EventArgs e)
        {
            if (Clipboard.ContainsText())
            {
                txtUrl.Text = Clipboard.GetText().Trim();
            }
            await ProcessUrlInput();
        }

        private async Task ProcessUrlInput()
        {
            var input = txtUrl.Text.Trim();
            if (string.IsNullOrEmpty(input)) return;

            txtUrl.Clear();

            // URL形式かどうかチェック
            var isUrl = input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        input.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

            if (Helpers.IsVideoUrl(input))
            {
                // iwaraの動画URL
                await AddVideoAsync(input);
            }
            else if (Helpers.IsUserProfileUrl(input))
            {
                // iwaraのプロフィールURL
                _downloadManager.EnqueueSubscribedUser(input);
            }
            else if (isUrl)
            {
                // URL形式だがiwaraのURLではない
                MessageBox.Show(
                    "iwara.tvのURLを入力してください。\n\n対応形式:\n・動画: https://www.iwara.tv/video/xxxxx\n・チャンネル: https://www.iwara.tv/profile/username",
                    "無効なURL",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            else if (!Helpers.IsValidUsername(input))
            {
                // ユーザー名として無効な文字が含まれている
                MessageBox.Show(
                    "無効なユーザー名です。\n\nユーザー名には英数字、@、_、- のみ使用できます。",
                    "無効な入力",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            else
            {
                // 有効なユーザー名
                _downloadManager.EnqueueSubscribedUser(input);
            }
        }

        private async Task AddVideoAsync(string url)
        {
            UpdateStatusBar("動画を追加中...");

            try
            {
                // 既存チェック
                var videoId = Helpers.ExtractVideoIdFromUrl(url);
                if (!string.IsNullOrEmpty(videoId))
                {
                    var existingVideo = _database.GetVideoByVideoId(videoId);
                    if (existingVideo != null)
                    {
                        var statusText = GetStatusText(existingVideo.Status);
                        var result = MessageBox.Show(
                            $"この動画は既に登録されています。\n\n" +
                            $"タイトル: {existingVideo.Title}\n" +
                            $"状態: {statusText}\n\n" +
                            $"ダウンロードキューに追加しますか？",
                            "重複確認",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);

                        if (result == DialogResult.Yes)
                        {
                            // キューに追加
                            SubscribedUser? user = null;
                            if (existingVideo.SubscribedUserId.HasValue)
                            {
                                user = _database.GetSubscribedUserById(existingVideo.SubscribedUserId.Value);
                            }
                            _downloadManager.EnqueueDownload(existingVideo, existingVideo.SubscribedUserId.HasValue, user);
                            RefreshChannelTree();
                            RefreshVideoList();
                            UpdateStatusBar($"動画「{existingVideo.Title}」をキューに追加しました");
                        }
                        else
                        {
                            UpdateStatusBar("キャンセルされました");
                        }
                        return;
                    }
                }

                var progress = new Progress<string>(msg => UpdateStatusBar(msg));
                var task = await _downloadManager.AddSingleVideoAsync(url, progress);

                if (task != null)
                {
                    RefreshChannelTree();
                    RefreshVideoList();
                    UpdateStatusBar($"動画「{task.Video.Title}」を追加しました");
                }
                else
                {
                    MessageBox.Show("動画の追加に失敗しました。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    UpdateStatusBar("動画追加失敗");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"エラー: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateStatusBar("エラー");
            }
        }

        #endregion

        #region Download Manager Events

        /// <summary>
        /// UI スレッドへ非同期で処理を投げる。Invoke (同期) は大量並列イベント時に
        /// スレッドプール側が UI スレッド完了を待ち続けてフリーズするため使わない。
        /// Disposing/IsDisposed 中は ObjectDisposedException を避けるため何もしない。
        /// </summary>
        private void PostToUi(Action action)
        {
            if (IsDisposed || Disposing || !IsHandleCreated) return;
            try
            {
                BeginInvoke(action);
            }
            catch (InvalidOperationException)
            {
                // ハンドル破棄レース (ObjectDisposedException は InvalidOperationException のサブ): 無視
            }
        }

        private void OnTaskProgressChanged(object? sender, DownloadTask task)
        {
            if (InvokeRequired)
            {
                PostToUi(() => OnTaskProgressChanged(sender, task));
                return;
            }
            UpdateVideoItem(task);
            ScheduleDownloadCountUpdate();
        }

        private void OnTaskStatusChanged(object? sender, DownloadTask task)
        {
            if (InvokeRequired)
            {
                PostToUi(() => OnTaskStatusChanged(sender, task));
                return;
            }

            UpdateVideoItem(task);
            ScheduleDownloadCountUpdate();

            // どのステータス遷移でもキューノード内訳が変わるので Refresh
            // (debounce 200ms 経由で連続呼びはまとまる)
            RefreshChannelTree();
        }

        private void OnNewVideosFound(object? sender, (SubscribedUser User, List<VideoInfo> Videos) e)
        {
            if (InvokeRequired)
            {
                PostToUi(() => OnNewVideosFound(sender, e));
                return;
            }
            RefreshChannelTree();
            RefreshVideoList();
        }

        private void OnAutoCheckCompleted(object? sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                PostToUi(() => OnAutoCheckCompleted(sender, e));
                return;
            }
            RefreshChannelTree();
        }

        /// <summary>バックグラウンドタスクの進捗をステータスバーに表示</summary>
        private void OnBackgroundTaskProgress(object? sender, (string TaskName, string Message) e)
        {
            if (InvokeRequired)
            {
                PostToUi(() => OnBackgroundTaskProgress(sender, e));
                return;
            }
            UpdateStatusBar($"[{e.TaskName}] {e.Message}");
        }

        /// <summary>バックグラウンドタスク完了時の通知</summary>
        private void OnBackgroundTaskCompleted(object? sender, (string TaskName, string Summary, bool Success) e)
        {
            if (InvokeRequired)
            {
                PostToUi(() => OnBackgroundTaskCompleted(sender, e));
                return;
            }
            UpdateStatusBar($"[{e.TaskName}] {e.Summary}");
            // 完了通知 (バルーン)
            try
            {
                Services.NotificationService.Instance.ShowNotification(
                    e.Success ? $"{e.TaskName} 完了" : $"{e.TaskName} 終了 (一部失敗)",
                    e.Summary);
            }
            catch { }
            // 動画リスト/ツリーを更新 (サムネ補完で URL が増えたら再描画したい)
            RefreshChannelTree();
            RefreshVideoList();
        }

        #endregion

        #region Channel Tree

        // 短時間に複数のイベント (DL完了など) が連続発火しても UI 更新を統合するためのデバウンスタイマー
        private System.Windows.Forms.Timer? _channelTreeRefreshTimer;
        private const int TreeRefreshDebounceMs = 200;

        /// <summary>
        /// チャンネルツリーを更新 (debounce + 非同期DBアクセス)
        /// <summary>
        /// 外部 (ImportFromFolderWizard 等) からインポート完了通知を受けたときに
        /// チャンネル一覧 + 動画リストの両方を更新するフック。
        /// debounce 機構が内蔵されてるので連続呼出しでも安全。
        /// </summary>
        public void RefreshAfterImport()
        {
            if (IsDisposed) return;
            if (InvokeRequired) { try { BeginInvoke((Action)RefreshAfterImport); } catch { } return; }
            RefreshChannelTree();
            RefreshVideoList();
        }

        /// 短時間に複数回呼ばれても DB クエリは1回だけ実行される。
        /// </summary>
        private void RefreshChannelTree()
        {
            if (IsDisposed) return;
            if (_channelTreeRefreshTimer == null)
            {
                _channelTreeRefreshTimer = new System.Windows.Forms.Timer { Interval = TreeRefreshDebounceMs };
                _channelTreeRefreshTimer.Tick += async (_, _) =>
                {
                    _channelTreeRefreshTimer.Stop();
                    await RefreshChannelTreeCoreAsync();
                };
            }
            _channelTreeRefreshTimer.Stop();
            _channelTreeRefreshTimer.Start();
        }

        private async Task RefreshChannelTreeCoreAsync()
        {
            if (IsDisposed) return;

            // DB クエリをバックグラウンドスレッドで実行 (DatabaseService は接続毎使い捨てでスレッドセーフ)
            List<VideoInfo> allVideos;
            List<SubscribedUser> users;
            try
            {
                allVideos = await Task.Run(() => _database.GetAllVideos());
                users = await Task.Run(() => _database.GetAllSubscribedUsers());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RefreshChannelTree DB error: {ex.Message}");
                return;
            }

            if (IsDisposed) return;

            treeViewChannels.BeginUpdate();

            // 選択状態を保存
            var selectedTag = treeViewChannels.SelectedNode?.Tag;

            treeViewChannels.Nodes.Clear();

            var totalCount = allVideos.Count;
            var completedCount = allVideos.Count(v => v.Status == DownloadStatus.Completed);
            var failedCount = allVideos.Count(v => v.Status == DownloadStatus.Failed);
            var skippedCount = allVideos.Count(v => v.Status == DownloadStatus.Skipped);
            // 未DL: Completed / Skipped を除外したもの (Pending/Failed/Downloading等)
            var notDownloadedCount = allVideos.Count(v =>
                v.Status != DownloadStatus.Completed && v.Status != DownloadStatus.Skipped);

            // DL中/タグ書込中/待機中は DownloadManager のアクティブなタスクから取得(リアルタイム同期)
            var downloadingCount = _downloadManager.DownloadingCount;
            var writingTagsCount = _downloadManager.WritingTagsCount;
            var pendingCount = _downloadManager.PendingTaskCount;

            // 「全ての動画」ノード
            var allVideosNode = new TreeNode($"📊 全ての動画 [{completedCount}/{totalCount}]")
            {
                Tag = NODE_ALL_VIDEOS,
                NodeFont = new Font(treeViewChannels.Font, FontStyle.Bold)
            };
            treeViewChannels.Nodes.Add(allVideosNode);

            // 「ダウンロードキュー」ノード
            var queueCount = downloadingCount + writingTagsCount + pendingCount;
            var allDownloadsNode = new TreeNode($"📥 ダウンロードキュー")
            {
                Tag = NODE_ALL_DOWNLOADS
            };
            if (queueCount > 0)
            {
                var parts = new List<string>();
                if (downloadingCount > 0) parts.Add($"{downloadingCount}DL中");
                if (writingTagsCount > 0) parts.Add($"{writingTagsCount}タグ書込");
                if (pendingCount > 0) parts.Add($"{pendingCount}待機");
                allDownloadsNode.Text += $" ({string.Join("/", parts)})";
                allDownloadsNode.NodeFont = new Font(treeViewChannels.Font, FontStyle.Bold);
            }
            treeViewChannels.Nodes.Add(allDownloadsNode);

            // 「未DL」ノード
            if (notDownloadedCount > 0)
            {
                var notDownloadedNode = new TreeNode($"⏳ 未DL [{notDownloadedCount}]")
                {
                    Tag = NODE_NOT_DOWNLOADED,
                    ForeColor = Color.DarkOrange
                };
                treeViewChannels.Nodes.Add(notDownloadedNode);
            }

            // 「DL済」ノード
            if (completedCount > 0)
            {
                var downloadedNode = new TreeNode($"✅ DL済 [{completedCount}]")
                {
                    Tag = NODE_DOWNLOADED,
                    ForeColor = Color.Green
                };
                treeViewChannels.Nodes.Add(downloadedNode);
            }

            // 「スキップ」ノード
            if (skippedCount > 0)
            {
                var skippedNode = new TreeNode($"⏭️ スキップ [{skippedCount}]")
                {
                    Tag = NODE_SKIPPED,
                    ForeColor = Color.Gray
                };
                treeViewChannels.Nodes.Add(skippedNode);
            }

            // 「エラー」ノード
            if (failedCount > 0)
            {
                var failedNode = new TreeNode($"❌ エラー [{failedCount}]")
                {
                    Tag = NODE_FAILED_VIDEOS,
                    ForeColor = Color.Red
                };
                treeViewChannels.Nodes.Add(failedNode);
            }

            // 「単発動画」ノード
            var singleVideos = allVideos.Where(v => !v.SubscribedUserId.HasValue).ToList();
            if (singleVideos.Any())
            {
                var singleNode = new TreeNode($"📁 単発動画 [{singleVideos.Count}]")
                {
                    Tag = NODE_SINGLE_VIDEOS
                };
                treeViewChannels.Nodes.Add(singleNode);
            }

            // 登録チャンネル (users は await で取得済み)
            foreach (var user in users)
            {
                var videos = allVideos.Where(v => v.SubscribedUserId == user.Id).ToList();
                var chCompletedCount = videos.Count(v => v.Status == DownloadStatus.Completed);
                var chDownloadingVideos = videos.Count(v => v.Status == DownloadStatus.Downloading);
                var chPendingVideos = videos.Count(v => v.Status == DownloadStatus.Pending);
                
                var statusText = "";
                if (chDownloadingVideos > 0)
                    statusText = $" 🔄{chDownloadingVideos}";
                else if (chPendingVideos > 0)
                    statusText = $" ⏳{chPendingVideos}";
                
                var nodeText = $"{(user.IsEnabled ? "📺" : "⬜")} {user.Username} [{chCompletedCount}/{videos.Count}]{statusText}";
                var node = new TreeNode(nodeText)
                {
                    Tag = user,
                    ForeColor = user.IsEnabled ? Color.Black : Color.Gray
                };
                
                treeViewChannels.Nodes.Add(node);
            }

            // 選択状態を復元
            if (selectedTag != null)
            {
                foreach (TreeNode node in treeViewChannels.Nodes)
                {
                    if (node.Tag?.Equals(selectedTag) == true ||
                        (node.Tag is SubscribedUser u && selectedTag is SubscribedUser su && u.Id == su.Id))
                    {
                        treeViewChannels.SelectedNode = node;
                        break;
                    }
                }
            }
            
            treeViewChannels.EndUpdate();
            UpdateDownloadCount();
        }

        private void treeViewChannels_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag == null) return;

            if (e.Node.Tag is SubscribedUser user)
            {
                _selectedChannel = user;
                lblVideoHeader.Text = $"動画一覧 - {user.Username}";
            }
            else if (e.Node.Tag is string tag)
            {
                _selectedChannel = null;
                lblVideoHeader.Text = tag switch
                {
                    NODE_ALL_VIDEOS => "全ての動画",
                    NODE_ALL_DOWNLOADS => "ダウンロード中/待機中",
                    NODE_NOT_DOWNLOADED => "未DL動画",
                    NODE_DOWNLOADED => "DL済動画",
                    NODE_SKIPPED => "スキップ動画",
                    NODE_FAILED_VIDEOS => "エラー一覧",
                    NODE_SINGLE_VIDEOS => "単発動画",
                    _ => "動画一覧"
                };
            }

            RefreshVideoList();
        }

        private void treeViewChannels_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node?.Tag is SubscribedUser user)
            {
                Helpers.OpenUrl(user.ProfileUrl);
            }
        }

        // NodeMouseClickは不要(contextMenuChannel_Opening内でノード選択を行う)

        #endregion

        #region Video List

        private System.Windows.Forms.Timer? _videoListRefreshTimer;
        private const int VideoListRefreshDebounceMs = 200;

        /// <summary>
        /// 動画リストを更新(仮想モード対応, debounce + 非同期DB)
        /// </summary>
        private void RefreshVideoList()
        {
            if (IsDisposed) return;
            if (_videoListRefreshTimer == null)
            {
                _videoListRefreshTimer = new System.Windows.Forms.Timer { Interval = VideoListRefreshDebounceMs };
                _videoListRefreshTimer.Tick += async (_, _) =>
                {
                    _videoListRefreshTimer.Stop();
                    await RefreshVideoListCoreAsync();
                };
            }
            _videoListRefreshTimer.Stop();
            _videoListRefreshTimer.Start();
        }

        private async Task RefreshVideoListCoreAsync()
        {
            if (IsDisposed) return;

            // 選択状態 / 選択ノードを UI スレッドで先に取得
            var selectedVideoIds = GetSelectedVideoIds();
            var selectedNode = treeViewChannels.SelectedNode;
            var tagObj = selectedNode?.Tag;

            // DB クエリをバックグラウンドで実行
            List<VideoInfo> videos;
            try
            {
                videos = await Task.Run(() =>
                {
                    if (tagObj is SubscribedUser user)
                    {
                        return _database.GetVideosBySubscribedUser(user.Id);
                    }
                    if (tagObj is string tag)
                    {
                        return tag switch
                        {
                            NODE_ALL_VIDEOS => _database.GetAllVideos(),
                            NODE_ALL_DOWNLOADS => _database.GetVideosByStatus(DownloadStatus.Downloading)
                                .Concat(_database.GetVideosByStatus(DownloadStatus.Pending)).ToList(),
                            NODE_NOT_DOWNLOADED => _database.GetAllVideos()
                                .Where(v => v.Status != DownloadStatus.Completed
                                         && v.Status != DownloadStatus.Skipped).ToList(),
                            NODE_DOWNLOADED => _database.GetVideosByStatus(DownloadStatus.Completed),
                            NODE_SKIPPED => _database.GetVideosByStatus(DownloadStatus.Skipped),
                            NODE_FAILED_VIDEOS => _database.GetVideosByStatus(DownloadStatus.Failed),
                            NODE_SINGLE_VIDEOS => _database.GetAllVideos()
                                .Where(v => !v.SubscribedUserId.HasValue).ToList(),
                            _ => new List<VideoInfo>(),
                        };
                    }
                    return new List<VideoInfo>();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RefreshVideoList DB error: {ex.Message}");
                return;
            }

            if (IsDisposed) return;

            // 元データを保存
            _allVideoList = videos;

            // フィルターとソートを適用して表示
            ApplyVideoFilter();

            // 選択状態を復元
            RestoreSelectedVideoIds(selectedVideoIds);
        }

        /// <summary>
        /// 動画リストにフィルターを適用(仮想モード対応)
        /// </summary>
        private void ApplyVideoFilter()
        {
            // NSFW フィルタ (検索テキストとは独立、設定値で常時適用)
            var nsfwMode = SettingsManager.Instance.Settings.NsfwFilterMode;
            IEnumerable<VideoInfo> source = _allVideoList;
            if (nsfwMode == 1) // SFW のみ
                source = source.Where(v => string.IsNullOrEmpty(v.Rating) || v.Rating == "general");
            else if (nsfwMode == 2) // NSFW のみ
                source = source.Where(v => v.Rating == "ecchi" || v.Rating == "nsfw");

            // 詳細検索: お気に入りのみ
            bool favOnly = chkFavOnly != null && chkFavOnly.Checked;
            if (favOnly)
                source = source.Where(v => v.IsFavorite);

            // 詳細検索: タグ絞り込み (スペース/カンマ区切りで AND)
            var tagFilterText = txtTagFilter?.Text ?? "";
            string[] tagTerms = Array.Empty<string>();
            if (!string.IsNullOrWhiteSpace(tagFilterText))
            {
                tagTerms = tagFilterText
                    .Split(new[] { ',', ' ', '　' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(t => t.ToLowerInvariant())
                    .ToArray();
                if (tagTerms.Length > 0)
                    source = source.Where(v => tagTerms.All(tt => (v.Tags ?? "").ToLowerInvariant().Contains(tt)));
            }

            // 検索クエリパース
            var query = SearchQuery.Parse(_currentFilterText);
            if (query.IsEmpty)
            {
                _displayVideoList = source.ToList();
            }
            else
            {
                _displayVideoList = source.Where(query.Match).ToList();
            }

            // ソート適用
            SortDisplayVideoList();

            // キャッシュクリア
            ClearItemCache();

            // モードに応じて Items 投入 / VirtualListSize 設定
            if (listViewVideos.View == View.LargeIcon)
            {
                // サムネモード: VirtualMode false で Items 直接投入 (件数制限あり)
                int limit = Math.Min(_displayVideoList.Count, TileModeMaxItems);
                listViewVideos.BeginUpdate();
                try
                {
                    listViewVideos.Items.Clear();
                    for (int i = 0; i < limit; i++)
                    {
                        try { listViewVideos.Items.Add(CreateVideoListItem(_displayVideoList[i])); }
                        catch (Exception itemEx)
                        {
                            _logger.Warn($"Filter tile add failed at {i}: {itemEx.Message}");
                        }
                    }
                }
                finally { listViewVideos.EndUpdate(); }
            }
            else
            {
                // 詳細モード: 仮想リストサイズ更新
                listViewVideos.VirtualListSize = _displayVideoList.Count;
                listViewVideos.Invalidate();
            }

            // フィルター結果をステータスに表示
            if (!query.IsEmpty || nsfwMode != 0 || favOnly || tagTerms.Length > 0)
            {
                UpdateStatusBar($"フィルター: {_displayVideoList.Count}/{_allVideoList.Count}件");
            }
        }

        /// <summary>
        /// 動画のソース（投稿サイト / 埋め込み元）を表示用文字列で返す
        ///   - iwara 本体: video.Site で iwara.tv / iwara.ai を区別 (空文字は tv 扱い)
        ///   - 外部埋め込み: EmbedUrl のドメインから判定 (YouTube/ニコニコ/Vimeo/X/Bilibili)
        /// </summary>
        private static string GetVideoSourceLabel(VideoInfo video)
        {
            if (!video.IsExternal)
            {
                // iwara 本体 (Site 空文字は旧データ → iwara.tv 扱い)
                if (string.Equals(video.Site, Helpers.SiteAi, StringComparison.OrdinalIgnoreCase))
                    return "iwara.ai";
                return "iwara.tv";
            }

            var url = video.EmbedUrl?.ToLowerInvariant() ?? string.Empty;
            if (url.Contains("youtube.com") || url.Contains("youtu.be"))
                return "YouTube";
            if (url.Contains("vimeo.com"))
                return "Vimeo";
            if (url.Contains("twitter.com") || url.Contains("x.com"))
                return "X/Twitter";
            if (url.Contains("nicovideo.jp"))
                return "ニコニコ";
            if (url.Contains("bilibili.com"))
                return "Bilibili";
            return "外部";
        }

        private ListViewItem CreateVideoListItem(VideoInfo video)
        {
            var task = _downloadManager.GetTask(video.VideoId);

            // 実行中タスクがあれば task.Status を優先 (DB の video.Status より新しい)
            var effectiveStatus = task != null ? task.Status : video.Status;
            var statusIcon = GetStatusIcon(effectiveStatus);
            var statusText = GetStatusText(effectiveStatus);

            // 進捗表示
            var progressText = "-";
            if (task != null && task.Status == DownloadStatus.Downloading)
            {
                if (task.Progress > 0)
                {
                    // 速度と残り時間を表示
                    if (task.DownloadSpeed > 0)
                    {
                        progressText = $"{task.Progress:F0}% ({task.SpeedFormatted})";
                    }
                    else
                    {
                        progressText = $"{task.Progress:F0}%";
                    }
                }
                else
                {
                    progressText = "DL中...";
                }
            }
            else if (task != null && task.Status == DownloadStatus.WritingTags)
            {
                progressText = "タグ書込中...";
            }
            else if (video.Status == DownloadStatus.Completed)
            {
                progressText = "100%";
            }
            else if (video.Status == DownloadStatus.Pending)
            {
                progressText = "待機";
            }

            var item = new ListViewItem(new[]
            {
                $"{statusIcon} {video.Title}",
                GetVideoSourceLabel(video),
                statusText,
                progressText,
                video.FileSizeFormatted,
                video.CreatedAt.ToString("yyyy/MM/dd HH:mm")
            })
            {
                Tag = video
            };

            // タイル (サムネ) モード時はキーを設定 (Details モードでは無視される)
            if (listViewVideos.View == View.LargeIcon && _thumbImageList != null)
            {
                item.ImageKey = EnsureThumbImageKey(video);
            }

            // 状態に応じた色分け (task.Status を優先)
            // Skipped でも LocalFilePath が残っている場合 (過去にDL済) は完了色を維持
            bool hasLocalFile = !string.IsNullOrEmpty(video.LocalFilePath);
            item.ForeColor = effectiveStatus switch
            {
                DownloadStatus.Completed => Color.Green,
                DownloadStatus.Failed => Color.Red,
                DownloadStatus.Downloading => Color.Blue,
                DownloadStatus.WritingTags => Color.MediumPurple,
                DownloadStatus.Pending => Color.DarkOrange,
                DownloadStatus.Skipped => hasLocalFile ? Color.Green : Color.Gray,
                _ => Color.Black
            };

            // ツールチップ(エラー詳細表示)
            if (video.Status == DownloadStatus.Failed && !string.IsNullOrEmpty(video.LastErrorMessage))
            {
                item.ToolTipText = $"エラー: {video.LastErrorMessage}\nリトライ: {video.RetryCount}回";
            }
            else if (video.Status == DownloadStatus.Completed && !string.IsNullOrEmpty(video.LocalFilePath))
            {
                item.ToolTipText = $"保存先: {video.LocalFilePath}";
            }
            else if (task != null && task.Status == DownloadStatus.Downloading && task.EstimatedTimeRemaining.HasValue)
            {
                item.ToolTipText = $"{video.Title}\n残り: {task.EtaFormatted}";
            }
            else
            {
                item.ToolTipText = $"{video.Title}\n投稿者: {video.AuthorUsername}";
            }

            return item;
        }

        /// <summary>
        /// ダウンロードタスクの表示を更新(仮想モード対応)
        /// </summary>
        private void UpdateVideoItem(DownloadTask task)
        {
            // 表示リスト内の動画も更新
            for (int i = 0; i < _displayVideoList.Count; i++)
            {
                if (_displayVideoList[i].VideoId == task.Video.VideoId)
                {
                    // データソースの動画情報を更新
                    _displayVideoList[i] = task.Video;

                    if (listViewVideos.VirtualMode)
                    {
                        // 詳細モード (仮想): _itemCache の該当範囲を更新 → RedrawItems
                        if (i >= _cacheStartIndex && i < _cacheStartIndex + _itemCache.Length)
                        {
                            _itemCache[i - _cacheStartIndex] = CreateVideoListItem(task.Video);
                        }
                        // RefreshVideoListCoreAsync の最中など _displayVideoList と VirtualListSize が
                        // 一時的に不一致になる瞬間に RedrawItems(i, i) で InvalidArgument を踏むのを防ぐ
                        if (i < listViewVideos.VirtualListSize)
                        {
                            listViewVideos.RedrawItems(i, i, false);
                        }
                    }
                    else
                    {
                        // サムネモード (非仮想): Items[i] を再生成 (件数制限で範囲外の場合はスキップ)
                        if (i >= 0 && i < listViewVideos.Items.Count)
                        {
                            listViewVideos.Items[i] = CreateVideoListItem(task.Video);
                        }
                    }
                    break;
                }
            }
            
            // 元データリストも更新
            for (int i = 0; i < _allVideoList.Count; i++)
            {
                if (_allVideoList[i].VideoId == task.Video.VideoId)
                {
                    _allVideoList[i] = task.Video;
                    break;
                }
            }
        }

        private static string GetStatusIcon(DownloadStatus status)
        {
            return status switch
            {
                DownloadStatus.Pending => "⏳",
                DownloadStatus.Downloading => "🔄",
                DownloadStatus.WritingTags => "🏷️",
                DownloadStatus.Completed => "✅",
                DownloadStatus.Failed => "❌",
                DownloadStatus.Skipped => "⏭️",
                DownloadStatus.Paused => "⏸️",
                _ => "❓"
            };
        }

        private static string GetStatusText(DownloadStatus status)
        {
            return status switch
            {
                DownloadStatus.Pending => "待機中",
                DownloadStatus.Downloading => "DL中",
                DownloadStatus.WritingTags => "タグ書込中",
                DownloadStatus.Completed => "完了",
                DownloadStatus.Failed => "失敗",
                DownloadStatus.Skipped => "スキップ",
                DownloadStatus.Paused => "一時停止",
                _ => "不明"
            };
        }

        private void listViewVideos_MouseDoubleClick(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            var video = GetFirstSelectedVideo();
            if (video == null) return;

            if (video.Status == DownloadStatus.Completed && !string.IsNullOrEmpty(video.LocalFilePath) && File.Exists(video.LocalFilePath))
            {
                Process.Start(new ProcessStartInfo { FileName = video.LocalFilePath, UseShellExecute = true });
            }
            else
            {
                Helpers.OpenUrl(video.Url);
            }
        }

        private void listViewVideos_KeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+A で全選択(仮想モード対応)
            if (e.Control && e.KeyCode == Keys.A)
            {
                e.SuppressKeyPress = true; // ビープ音を防ぐ

                listViewVideos.BeginUpdate();
                // サムネモードでは Items.Count が上限 (TileModeMaxItems=500)、
                // 詳細(VirtualMode)では VirtualListSize=_displayVideoList.Count なのでこちらで OK
                int max = listViewVideos.VirtualMode
                    ? listViewVideos.VirtualListSize
                    : listViewVideos.Items.Count;
                for (int i = 0; i < max; i++)
                {
                    listViewVideos.SelectedIndices.Add(i);
                }
                listViewVideos.EndUpdate();
            }
            // Deleteで削除
            else if (e.KeyCode == Keys.Delete)
            {
                e.SuppressKeyPress = true;
                menuVidDelete_Click(sender, e);
            }
        }

        private void listViewVideos_SelectedIndexChanged(object sender, EventArgs e)
        {
            var selectedCount = listViewVideos.SelectedIndices.Count;
            if (selectedCount > 0)
            {
                // 選択中の合計サイズを計算(仮想モード対応)
                long totalSize = 0;
                foreach (int index in listViewVideos.SelectedIndices)
                {
                    if (index >= 0 && index < _displayVideoList.Count)
                    {
                        totalSize += _displayVideoList[index].FileSize;
                    }
                }
                var sizeText = totalSize > 0 ? $" ({FormatFileSize(totalSize)})" : "";
                UpdateStatusBar($"{selectedCount}件選択中{sizeText}");
            }
        }

        /// <summary>
        /// 選択中の動画を取得 (仮想モード/サムネモード両対応)
        /// サムネモード: Items[i].Tag に格納された VideoInfo を直接取り出す (件数制限ありのため _displayVideoList と添字が合わない可能性に備える)
        /// 仮想モード: _displayVideoList のインデックスで引く
        /// </summary>
        private List<VideoInfo> GetSelectedVideos()
        {
            var videos = new List<VideoInfo>();
            bool tileMode = !listViewVideos.VirtualMode;
            foreach (int index in listViewVideos.SelectedIndices)
            {
                if (tileMode)
                {
                    if (index >= 0 && index < listViewVideos.Items.Count
                        && listViewVideos.Items[index].Tag is VideoInfo v)
                    {
                        videos.Add(v);
                    }
                }
                else
                {
                    if (index >= 0 && index < _displayVideoList.Count)
                    {
                        videos.Add(_displayVideoList[index]);
                    }
                }
            }
            return videos;
        }

        /// <summary>
        /// 最初の選択動画を取得 (仮想モード/サムネモード両対応)
        /// </summary>
        private VideoInfo? GetFirstSelectedVideo()
        {
            if (listViewVideos.SelectedIndices.Count == 0) return null;
            int index = listViewVideos.SelectedIndices[0];
            bool tileMode = !listViewVideos.VirtualMode;
            if (tileMode)
            {
                if (index >= 0 && index < listViewVideos.Items.Count
                    && listViewVideos.Items[index].Tag is VideoInfo v)
                    return v;
            }
            else
            {
                if (index >= 0 && index < _displayVideoList.Count)
                    return _displayVideoList[index];
            }
            return null;
        }

        #endregion

        #region Channel Context Menu

        /// <summary>
        /// チャンネルコンテキストメニューを開く前に項目の表示/非表示を制御
        /// </summary>
        private void contextMenuChannel_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // 右クリック位置からノードを取得して選択
            var mousePos = treeViewChannels.PointToClient(Control.MousePosition);
            var clickedNode = treeViewChannels.GetNodeAt(mousePos);
            
            // ノードがない場所で右クリックした場合はメニューをキャンセル
            if (clickedNode == null)
            {
                e.Cancel = true;
                return;
            }
            
            // クリックしたノードを選択状態にする
            treeViewChannels.SelectedNode = clickedNode;

            var selectedNode = clickedNode;
            var isUserNode = selectedNode?.Tag is SubscribedUser;
            var isSpecialNode = selectedNode?.Tag is string;
            var specialTag = selectedNode?.Tag as string;

            // ファイル存在チェックはチャンネルノード or「DL済動画」ノードで表示
            bool canCheckFiles = isUserNode || specialTag == NODE_DOWNLOADED;

            // チャンネル用メニュー項目の表示/非表示
            menuChOpen.Visible = isUserNode;
            menuChCheckNow.Visible = isUserNode;
            menuChCheckFiles.Visible = canCheckFiles;
            menuChSeparator1.Visible = isUserNode;
            menuChSetSavePath.Visible = isUserNode;
            menuChExternalDL.Visible = isUserNode;
            menuChSeparator2.Visible = isUserNode;
            menuChSeparator3.Visible = isUserNode;
            menuChDelete.Visible = isUserNode;

            if (isUserNode && selectedNode?.Tag is SubscribedUser u)
            {
                // 有効/無効はステータスに応じて片方だけ表示
                menuChEnable.Visible = !u.IsEnabled;
                menuChDisable.Visible = u.IsEnabled;

                // 「今すぐ確認」は無効化中だとグレーアウト
                menuChCheckNow.Enabled = u.IsEnabled;
                menuChCheckNow.Text = u.IsEnabled ? "今すぐ確認" : "今すぐ確認 (無効化中)";

                // iwara外動画DL設定の現在値をチェックマーク表示
                menuChExternalDLInherit.Checked = !u.DownloadExternalVideosOverride.HasValue;
                menuChExternalDLOn.Checked = u.DownloadExternalVideosOverride == true;
                menuChExternalDLOff.Checked = u.DownloadExternalVideosOverride == false;

                var globalDefault = Utils.SettingsManager.Instance.Settings.DownloadExternalVideosDefault;
                menuChExternalDLInherit.Text = $"デフォルト設定に従う ({(globalDefault ? "ON" : "OFF")})";

                // 保存先表示（カスタムが設定されているか視覚化）
                menuChSetSavePath.Text = string.IsNullOrEmpty(u.CustomSavePath)
                    ? "保存先を変更..."
                    : "保存先を変更... (カスタム設定済み)";
            }
            else
            {
                menuChEnable.Visible = false;
                menuChDisable.Visible = false;
            }

            // 「全てダウンロード」はチャンネル・未 DL・エラー・単発動画ノードで表示
            var showDownloadAll = isUserNode ||
                (isSpecialNode && (selectedNode?.Tag as string) is NODE_NOT_DOWNLOADED or NODE_FAILED_VIDEOS or NODE_SINGLE_VIDEOS);
            menuChDownloadAll.Visible = showDownloadAll;

            // 「Not Found を除外」はエラーノードでのみ表示
            menuChDeleteNotFound.Visible = isSpecialNode && (selectedNode?.Tag as string) == NODE_FAILED_VIDEOS;

            // メニュー項目がない場合はキャンセル
            if (!showDownloadAll && !isUserNode && !canCheckFiles)
            {
                e.Cancel = true;
            }
        }

        private void menuChOpen_Click(object sender, EventArgs e)
        {
            if (treeViewChannels.SelectedNode?.Tag is SubscribedUser user)
            {
                Helpers.OpenUrl(user.ProfileUrl);
            }
        }

        private void menuChCheckNow_Click(object sender, EventArgs e)
        {
            if (treeViewChannels.SelectedNode?.Tag is SubscribedUser user)
            {
                // 手動の単一チェックは優先キューへ (通常の新着チェックより先に処理される)。
                // 取得は統合ワーカーが処理し、進捗はステータスバーに表示される。
                _downloadManager.EnqueueUserForCheck(user, priority: true);
                UpdateStatusBar($"{user.Username} の新着確認をキューに登録しました");
            }
        }

        private void menuChDownloadAll_Click(object sender, EventArgs e)
        {
            var selectedNode = treeViewChannels.SelectedNode;
            List<VideoInfo> videos;

            if (selectedNode?.Tag is SubscribedUser user)
            {
                // チャンネルの全動画DL
                videos = _database.GetVideosBySubscribedUser(user.Id)
                    .Where(v => v.Status != DownloadStatus.Completed && v.Status != DownloadStatus.Downloading && v.Status != DownloadStatus.Pending)
                    .ToList();

                foreach (var video in videos)
                {
                    _downloadManager.EnqueueDownload(video, true, user);
                }
            }
            else if (selectedNode?.Tag is string tag)
            {
                // 特殊ノードの全DL
                if (tag == NODE_NOT_DOWNLOADED)
                {
                    videos = _database.GetAllVideos()
                        .Where(v => v.Status != DownloadStatus.Completed && v.Status != DownloadStatus.Downloading && v.Status != DownloadStatus.Pending)
                        .ToList();
                }
                else if (tag == NODE_FAILED_VIDEOS)
                {
                    videos = _database.GetVideosByStatus(DownloadStatus.Failed).ToList();
                    // 失敗動画はリトライカウントをリセット
                    foreach (var video in videos)
                    {
                        video.RetryCount = 0;
                        video.LastErrorMessage = null;
                        _database.UpdateVideo(video);
                    }
                }
                else if (tag == NODE_SINGLE_VIDEOS)
                {
                    videos = _database.GetAllVideos()
                        .Where(v => !v.SubscribedUserId.HasValue && v.Status != DownloadStatus.Completed && v.Status != DownloadStatus.Downloading && v.Status != DownloadStatus.Pending)
                        .ToList();
                }
                else
                {
                    return;
                }

                foreach (var video in videos)
                {
                    SubscribedUser? videoUser = null;
                    if (video.SubscribedUserId.HasValue)
                    {
                        videoUser = _database.GetSubscribedUserById(video.SubscribedUserId.Value);
                    }
                    _downloadManager.EnqueueDownload(video, video.SubscribedUserId.HasValue, videoUser);
                }
            }
            else
            {
                return;
            }

            RefreshChannelTree();
            RefreshVideoList();
            UpdateStatusBar($"{videos.Count} 件のダウンロードをキューに追加しました");
        }

        private void menuChDeleteNotFound_Click(object sender, EventArgs e)
        {
            var errors = _database.GetVideosByStatus(DownloadStatus.Failed);
            var notFound = errors.Where(v =>
                v.LastErrorMessage != null &&
                (v.LastErrorMessage.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                 v.LastErrorMessage.Contains("404", StringComparison.OrdinalIgnoreCase) ||
                 v.LastErrorMessage.Contains("deleted", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (notFound.Count == 0)
            {
                MessageBox.Show("Not Found の動画はありません。", "情報", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Not Found (削除済み) の動画 {notFound.Count} 件をデータベースから削除しますか？\n\n※ローカルファイルは削除されません",
                "Not Found を除外",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes) return;

            var ids = notFound.Select(v => v.Id).ToList();
            int deleted = _database.DeleteVideosBatch(ids);
            RefreshChannelTree();
            RefreshVideoList();
            UpdateStatusBar($"Not Found の動画 {deleted} 件を削除しました");
        }

        private void menuChSetSavePath_Click(object sender, EventArgs e)
        {
            if (treeViewChannels.SelectedNode?.Tag is not SubscribedUser user) return;

            var defaultDownloadFolder = SettingsManager.Instance.Settings.DownloadFolder;
            var oldSavePath = user.GetSavePath(defaultDownloadFolder);

            using var dialog = new FolderBrowserDialog
            {
                Description = $"{user.Username} の保存先フォルダを選択",
                UseDescriptionForTitle = true,
                SelectedPath = oldSavePath
            };

            if (dialog.ShowDialog() != DialogResult.OK) return;

            var newSavePath = dialog.SelectedPath;
            if (string.Equals(
                    Path.GetFullPath(newSavePath).TrimEnd('\\'),
                    Path.GetFullPath(oldSavePath).TrimEnd('\\'),
                    StringComparison.OrdinalIgnoreCase))
            {
                // 変更なし
                return;
            }

            // 既存DLファイルを列挙 (oldSavePath 配下のもの)
            var allUserVideos = _database.GetVideosBySubscribedUser(user.Id);
            var movableFiles = allUserVideos
                .Where(v => !string.IsNullOrEmpty(v.LocalFilePath)
                            && File.Exists(v.LocalFilePath)
                            && IsPathUnder(v.LocalFilePath, oldSavePath))
                .ToList();

            bool doMove = false;
            if (movableFiles.Count > 0)
            {
                long totalBytes = 0;
                foreach (var v in movableFiles)
                {
                    try { totalBytes += new FileInfo(v.LocalFilePath).Length; } catch { }
                }

                // ドライブ判定 & 空き容量チェック
                var driveOld = Path.GetPathRoot(Path.GetFullPath(oldSavePath))?.ToUpperInvariant();
                var driveNew = Path.GetPathRoot(Path.GetFullPath(newSavePath))?.ToUpperInvariant();
                bool sameDrive = string.Equals(driveOld, driveNew, StringComparison.OrdinalIgnoreCase);

                string freeSpaceLine = "";
                if (sameDrive)
                {
                    freeSpaceLine = "\n同ドライブのため瞬時に完了します。";
                }
                else if (!string.IsNullOrEmpty(driveNew))
                {
                    try
                    {
                        var di = new DriveInfo(driveNew);
                        freeSpaceLine =
                            $"\n移動先空き容量: {FormatSize(di.AvailableFreeSpace)}" +
                            $" / 必要量: {FormatSize(totalBytes)}";
                        if (di.AvailableFreeSpace < totalBytes)
                        {
                            MessageBox.Show(this,
                                $"移動先ドライブ ({driveNew}) の空き容量が不足しています。\n\n" +
                                $"必要: {FormatSize(totalBytes)}\n" +
                                $"空き: {FormatSize(di.AvailableFreeSpace)}\n\n" +
                                "保存先変更を中止します。",
                                "容量不足", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        freeSpaceLine = $"\n(空き容量取得失敗: {ex.Message})";
                    }
                }

                var confirm = MessageBox.Show(this,
                    $"{user.Username} の保存先を変更します。\n\n" +
                    $"既存DL済みファイル: {movableFiles.Count} 個 ({FormatSize(totalBytes)})\n" +
                    $"移動元: {oldSavePath}\n" +
                    $"移動先: {newSavePath}" + freeSpaceLine + "\n\n" +
                    "これらのファイルを移動先に移しますか?\n\n" +
                    "[はい]   ファイルを移動して保存先を変更\n" +
                    "[いいえ] ファイルは移動せず保存先設定だけ変更\n" +
                    "[キャンセル] 何もしない",
                    "ファイル移動の確認",
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

                if (confirm == DialogResult.Cancel) return;
                doMove = (confirm == DialogResult.Yes);
            }

            // 設定変更
            user.CustomSavePath = newSavePath;
            _database.UpdateSubscribedUser(user);
            UpdateStatusBar($"保存先を変更しました: {newSavePath}");

            if (doMove && movableFiles.Count > 0)
            {
                // 各ファイルの新パスを算出 (oldSavePath からの相対パスを保持)
                var oldFull = Path.GetFullPath(oldSavePath).TrimEnd('\\');
                var items = movableFiles.Select(v =>
                {
                    var full = Path.GetFullPath(v.LocalFilePath);
                    var rel = full.Substring(oldFull.Length).TrimStart('\\', '/');
                    var newPath = Path.Combine(newSavePath, rel);
                    return (Video: v, NewPath: newPath);
                }).ToList();

                using var progressForm = new FileMoveProgressForm(items, _database);
                progressForm.ShowDialog(this);

                // キャッシュ無効化 + UI 再描画
                Services.IndexCacheService.Invalidate(oldSavePath);
                Services.IndexCacheService.Invalidate(newSavePath);
                RefreshChannelTree();
                RefreshVideoList();

                UpdateStatusBar(
                    $"移動完了: 成功 {progressForm.MovedCount} / 失敗 {progressForm.FailedCount}");
            }
        }

        private static bool IsPathUnder(string filePath, string folderPath)
        {
            try
            {
                var fileFull = Path.GetFullPath(filePath);
                var folderFull = Path.GetFullPath(folderPath).TrimEnd('\\') + '\\';
                return fileFull.StartsWith(folderFull, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024L * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024:F1} MB";
            return $"{bytes / 1024.0 / 1024 / 1024:F2} GB";
        }

        private void menuChEnable_Click(object sender, EventArgs e)
        {
            if (treeViewChannels.SelectedNode?.Tag is SubscribedUser user)
            {
                user.IsEnabled = true;
                _database.UpdateSubscribedUser(user);
                RefreshChannelTree();
            }
        }

        private void menuChExternalDLInherit_Click(object sender, EventArgs e)
        {
            SetChannelExternalOverride(null);
        }

        private void menuChExternalDLOn_Click(object sender, EventArgs e)
        {
            SetChannelExternalOverride(true);
        }

        private void menuChExternalDLOff_Click(object sender, EventArgs e)
        {
            SetChannelExternalOverride(false);
        }

        private void SetChannelExternalOverride(bool? value)
        {
            if (treeViewChannels.SelectedNode?.Tag is not SubscribedUser user) return;
            user.DownloadExternalVideosOverride = value;
            _database.UpdateSubscribedUser(user);
            var label = value switch
            {
                true => "DLする",
                false => "DLしない",
                null => "デフォルト設定に従う"
            };
            UpdateStatusBar($"{user.Username} のiwara外動画DL設定を「{label}」に変更しました");
        }

        private void menuChDisable_Click(object sender, EventArgs e)
        {
            if (treeViewChannels.SelectedNode?.Tag is SubscribedUser user)
            {
                user.IsEnabled = false;
                _database.UpdateSubscribedUser(user);
                RefreshChannelTree();
            }
        }

        private void menuChDelete_Click(object sender, EventArgs e)
        {
            if (treeViewChannels.SelectedNode?.Tag is not SubscribedUser user) return;

            var result = MessageBox.Show(
                $"「{user.Username}」を購読リストから削除しますか？",
                "確認",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                _database.DeleteSubscribedUser(user.Id);
                RefreshChannelTree();
                // 仮想モード対応: VirtualListSizeを0に設定
                _allVideoList.Clear();
                _displayVideoList.Clear();
                ClearItemCache();
                listViewVideos.VirtualListSize = 0;
            }
        }

        #endregion

        #region Video Context Menu

        /// <summary>
        /// 右クリック時に項目を選択するための補助。WinForms 標準だと ContextMenuStrip 自動表示時に
        /// 行が選択されない (空クリックでもメニュー出る) ので、MouseDown で行を選択しておく。
        /// 仮想モード ListView では SelectedIndices を直接操作することで確実に反映される。
        /// </summary>
        private void listViewVideos_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            var hit = listViewVideos.HitTest(e.X, e.Y);
            if (hit?.Item == null || hit.Item.Index < 0) return;

            int idx = hit.Item.Index;
            foreach (int si in listViewVideos.SelectedIndices)
            {
                if (si == idx) return;
            }
            listViewVideos.SelectedIndices.Clear();
            listViewVideos.SelectedIndices.Add(idx);
        }

        /// <summary>
        /// Opening: 選択状態に応じて Visible トグルだけ行う (Items 操作は一切しない → AutoClose 正常)
        /// 項目自体は Designer で固定定義済み。
        /// </summary>
        private void OnVideoContextMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // 選択ゼロでも、マウス位置に項目があれば自動選択
            if (listViewVideos.SelectedIndices.Count == 0)
            {
                var mousePos = listViewVideos.PointToClient(Control.MousePosition);
                var hit = listViewVideos.HitTest(mousePos);
                if (hit?.Item != null && hit.Item.Index >= 0)
                {
                    listViewVideos.SelectedIndices.Add(hit.Item.Index);
                }
            }

            var selected = GetSelectedVideos();
            if (selected.Count == 0)
            {
                e.Cancel = true;
                return;
            }

            bool isSingle = selected.Count == 1;
            var single = isSingle ? selected[0] : null;
            bool hasPending = selected.Any(v => v.Status == DownloadStatus.Pending);
            bool hasDownloading = selected.Any(v => v.Status == DownloadStatus.Downloading);
            bool hasCompleted = selected.Any(v => v.Status == DownloadStatus.Completed);
            bool hasFailed = selected.Any(v => v.Status == DownloadStatus.Failed);
            bool hasPaused = selected.Any(v => v.Status == DownloadStatus.Paused);
            bool hasSkipped = selected.Any(v => v.Status == DownloadStatus.Skipped);
            bool canDownload = selected.Any(v =>
                v.Status != DownloadStatus.Completed &&
                v.Status != DownloadStatus.Downloading &&
                v.Status != DownloadStatus.Pending);
            bool canCancel = hasPending || hasDownloading || hasPaused;
            bool canRefreshInfo = selected.Any(v =>
                string.IsNullOrEmpty(v.Title) || v.Title.StartsWith("Video "));
            bool canPlay = isSingle && single!.Status == DownloadStatus.Completed
                && !string.IsNullOrEmpty(single.LocalFilePath) && File.Exists(single.LocalFilePath);
            bool canOpenFolder = isSingle
                && !string.IsNullOrEmpty(single!.LocalFilePath) && File.Exists(single.LocalFilePath);
            bool canOpenPage = isSingle && !string.IsNullOrEmpty(single!.Url);
            bool canOpenAuthor = isSingle && !string.IsNullOrEmpty(single!.AuthorUsername);

            menuVidDownload.Visible = canDownload;
            menuVidDownload.Text = hasFailed && !hasSkipped ? "ダウンロード (失敗をリトライ含む)" : "ダウンロード";
            menuVidCancel.Visible = canCancel;
            menuVidRetryFailed.Visible = hasFailed;
            menuVidReDownload.Visible = hasCompleted;
            menuVidRefreshInfo.Visible = canRefreshInfo;
            menuVidCheckFileExists.Visible = hasCompleted;
            menuVidPlay.Visible = canPlay;
            menuVidOpenFolder.Visible = canOpenFolder;
            menuVidOpenPage.Visible = canOpenPage;
            menuVidOpenAuthor.Visible = canOpenAuthor;
            menuVidCopyUrl.Visible = true;
            menuVidCopyTitle.Visible = true;
            // お気に入り: 選択が全てお気に入りなら「削除」、でなければ「追加」
            bool allFav = selected.All(v => v.IsFavorite);
            menuVidFavorite.Visible = true;
            menuVidFavorite.Text = allFav ? "★ お気に入りから削除" : "★ お気に入りに追加";
            menuVidDetails.Visible = isSingle;
            menuVidDelete.Visible = true;

            AdjustSeparators();
            // 注意: Opening 中は ContextMenuStrip がまだ表示前なので、
            // 子項目の .Visible は親が非表示のため常に false を返す。
            // 表示判定に使うなら .Available を見る必要がある。
            // ここではコピー系/削除が常に有効なので空判定は不要。
        }

        private void AdjustSeparators()
        {
            var items = contextMenuVideo.Items;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] is not ToolStripSeparator) continue;
                // Opening 中は親が未表示のため .Visible は false を返す。
                // 設定値そのものは .Available で取れる。
                bool prev = false;
                for (int j = i - 1; j >= 0; j--)
                {
                    if (items[j] is ToolStripSeparator) break;
                    if (items[j].Available) { prev = true; break; }
                }
                bool next = false;
                for (int j = i + 1; j < items.Count; j++)
                {
                    if (items[j] is ToolStripSeparator) break;
                    if (items[j].Available) { next = true; break; }
                }
                items[i].Visible = prev && next;
            }
        }

        private void menuVidDownload_Click(object sender, EventArgs e)
        {
            var selectedVideos = GetSelectedVideos();
            if (selectedVideos.Count == 0) return;
            
            foreach (var video in selectedVideos)
            {
                if (video.Status != DownloadStatus.Downloading && video.Status != DownloadStatus.Completed && video.Status != DownloadStatus.Pending)
                {
                    // 失敗時はリトライ回数をリセット
                    if (video.Status == DownloadStatus.Failed)
                    {
                        video.RetryCount = 0;
                        video.LastErrorMessage = null;
                        _database.UpdateVideo(video);
                    }
                    
                    SubscribedUser? user = null;
                    if (video.SubscribedUserId.HasValue)
                    {
                        user = _database.GetSubscribedUserById(video.SubscribedUserId.Value);
                    }
                    _downloadManager.EnqueueDownload(video, video.SubscribedUserId.HasValue, user);
                }
            }
            RefreshChannelTree();
            RefreshVideoList();
        }

        private void menuVidCancel_Click(object sender, EventArgs e)
        {
            var selectedVideos = GetSelectedVideos();
            if (selectedVideos.Count == 0) return;
            
            foreach (var video in selectedVideos)
            {
                _downloadManager.CancelTask(video.VideoId);
            }
            RefreshChannelTree();
            RefreshVideoList();
        }

        /// <summary>
        /// 失敗した動画を再試行
        /// </summary>
        private void menuVidRetryFailed_Click(object sender, EventArgs e)
        {
            var selectedVideos = GetSelectedVideos();
            if (selectedVideos.Count == 0) return;
            
            var retryCount = 0;
            foreach (var video in selectedVideos)
            {
                if (video.Status == DownloadStatus.Failed)
                {
                    // リトライカウントをリセット
                    video.RetryCount = 0;
                    video.LastErrorMessage = null;
                    _database.UpdateVideo(video);
                    
                    // キューに追加
                    SubscribedUser? user = null;
                    if (video.SubscribedUserId.HasValue)
                    {
                        user = _database.GetSubscribedUserById(video.SubscribedUserId.Value);
                    }
                    _downloadManager.EnqueueDownload(video, video.SubscribedUserId.HasValue, user);
                    retryCount++;
                }
            }
            
            RefreshChannelTree();
            RefreshVideoList();
            
            if (retryCount > 0)
            {
                UpdateStatusBar($"{retryCount}件の動画を再試行キューに追加しました");
            }
            else
            {
                UpdateStatusBar("失敗した動画が選択されていません");
            }
        }

        private async void menuVidRefreshInfo_Click(object sender, EventArgs e)
        {
            var selectedVideos = GetSelectedVideos();
            if (selectedVideos.Count == 0) return;
            
            var refreshCount = 0;
            var progress = new Progress<string>(msg => UpdateStatusBar(msg));
            
            foreach (var video in selectedVideos)
            {
                // タイトルが「Video XXX」のようなものを再取得
                if (video.Title.StartsWith("Video ") || string.IsNullOrEmpty(video.Title))
                {
                    var success = await _downloadManager.RefreshVideoInfoAsync(video, progress);
                    if (success)
                    {
                        refreshCount++;
                    }
                }
            }
            
            RefreshVideoList();
            UpdateStatusBar($"{refreshCount}件の情報を更新しました");
        }

        private void menuVidPlay_Click(object sender, EventArgs e)
        {
            var video = GetFirstSelectedVideo();
            if (video != null && !string.IsNullOrEmpty(video.LocalFilePath) && File.Exists(video.LocalFilePath))
            {
                Process.Start(new ProcessStartInfo { FileName = video.LocalFilePath, UseShellExecute = true });
            }
        }

        private void menuVidOpenFolder_Click(object sender, EventArgs e)
        {
            var video = GetFirstSelectedVideo();
            if (video != null && !string.IsNullOrEmpty(video.LocalFilePath))
            {
                var folder = Path.GetDirectoryName(video.LocalFilePath);
                if (Directory.Exists(folder))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{video.LocalFilePath}\"",
                        UseShellExecute = true
                    });
                }
            }
        }

        private void menuVidOpenPage_Click(object sender, EventArgs e)
        {
            var video = GetFirstSelectedVideo();
            if (video != null)
            {
                Helpers.OpenUrl(video.Url);
            }
        }

        /// <summary>
        /// URLをクリップボードにコピー
        /// </summary>
        private void menuVidCopyUrl_Click(object sender, EventArgs e)
        {
            var selectedVideos = GetSelectedVideos();
            if (selectedVideos.Count == 0) return;
            
            var urls = selectedVideos
                .Where(v => !string.IsNullOrEmpty(v.Url))
                .Select(v => v.Url)
                .ToList();
            
            if (urls.Count > 0)
            {
                Clipboard.SetText(string.Join(Environment.NewLine, urls));
                UpdateStatusBar($"{urls.Count}件のURLをコピーしました");
            }
        }

        /// <summary>
        /// タイトルをクリップボードにコピー
        /// </summary>
        private void menuVidCopyTitle_Click(object sender, EventArgs e)
        {
            var selectedVideos = GetSelectedVideos();
            if (selectedVideos.Count == 0) return;
            
            var titles = selectedVideos
                .Where(v => !string.IsNullOrEmpty(v.Title))
                .Select(v => v.Title)
                .ToList();
            
            if (titles.Count > 0)
            {
                Clipboard.SetText(string.Join(Environment.NewLine, titles));
                UpdateStatusBar($"{titles.Count}件のタイトルをコピーしました");
            }
        }

        /// <summary>
        /// ファイル存在チェック
        /// </summary>
        private void menuVidCheckFileExists_Click(object sender, EventArgs e)
        {
            var selectedVideos = GetSelectedVideos();
            if (selectedVideos.Count == 0) return;
            CheckFilesExistence(selectedVideos, "ダウンロード済みの動画が選択されていません");
        }

        // ファイル存在チェックの多重起動防止
        private bool _isCheckingFiles = false;

        /// <summary>
        /// 指定動画群の DL 済みファイルの実在をチェックし、見つからないものは
        /// ステータスをリセットして DL キューに再投入する。動画/チャンネル両方の右クリックから利用。
        /// 重い File.Exists 判定と DB 更新はバックグラウンドスレッドで行い、メイン UI を固めない。
        /// </summary>
        private async void CheckFilesExistence(IList<VideoInfo> videos, string noTargetMessage)
        {
            if (_isCheckingFiles)
            {
                UpdateStatusBar("ファイル存在チェックを実行中です…");
                return;
            }
            _isCheckingFiles = true;
            UpdateStatusBar($"ファイル存在チェック中… ({videos.Count}件)");

            try
            {
                // バックグラウンド: File.Exists 判定 + 欠損ファイルのステータスリセット & DB 更新
                var (checkedCount, missing) = await Task.Run(() =>
                {
                    int cnt = 0;
                    var miss = new List<VideoInfo>();
                    foreach (var video in videos)
                    {
                        if (video.Status == DownloadStatus.Completed && !string.IsNullOrEmpty(video.LocalFilePath))
                        {
                            cnt++;
                            if (!File.Exists(video.LocalFilePath))
                            {
                                video.Status = DownloadStatus.Pending;
                                video.LocalFilePath = string.Empty;
                                video.DownloadedAt = null;
                                video.RetryCount = 0;
                                video.LastErrorMessage = null;
                                try { _database.UpdateVideo(video); }
                                catch (Exception ex) { _logger.Warn($"CheckFiles UpdateVideo failed for {video.VideoId}: {ex.Message}"); }
                                miss.Add(video);
                            }
                        }
                    }
                    return (cnt, miss);
                });

                // ここから UI スレッド (await 継続): キュー投入 + 再描画
                foreach (var video in missing)
                {
                    SubscribedUser? user = null;
                    if (video.SubscribedUserId.HasValue)
                        user = _database.GetSubscribedUserById(video.SubscribedUserId.Value);
                    _downloadManager.EnqueueDownload(video, video.SubscribedUserId.HasValue, user);
                }

                RefreshChannelTree();
                RefreshVideoList();

                if (checkedCount == 0)
                    UpdateStatusBar(noTargetMessage);
                else if (missing.Count == 0)
                    UpdateStatusBar($"{checkedCount}件チェック: 全てのファイルが存在します");
                else
                    UpdateStatusBar($"{checkedCount}件チェック: {missing.Count}件のファイルが見つからず、キューに追加しました");
            }
            catch (Exception ex)
            {
                _logger.Error("ファイル存在チェックに失敗しました", ex);
                UpdateStatusBar($"ファイル存在チェック失敗: {ex.Message}");
            }
            finally
            {
                _isCheckingFiles = false;
            }
        }

        /// <summary>
        /// チャンネル右クリック / 「DL済動画」ノード右クリック: DL 済みファイルの存在チェック。
        /// </summary>
        private void menuChCheckFiles_Click(object sender, EventArgs e)
        {
            var tag = treeViewChannels.SelectedNode?.Tag;
            if (tag is SubscribedUser user)
            {
                var videos = _database.GetVideosBySubscribedUser(user.Id);
                if (videos.Count == 0)
                {
                    UpdateStatusBar($"「{user.Username}」には動画がありません");
                    return;
                }
                CheckFilesExistence(videos, $"「{user.Username}」にダウンロード済みの動画がありません");
            }
            else if (tag as string == NODE_DOWNLOADED)
            {
                var videos = _database.GetVideosByStatus(DownloadStatus.Completed);
                if (videos.Count == 0)
                {
                    UpdateStatusBar("DL済みの動画がありません");
                    return;
                }
                CheckFilesExistence(videos, "DL済みの動画がありません");
            }
        }

        /// <summary>
        /// 動画を削除
        /// </summary>
        private void menuVidDelete_Click(object sender, EventArgs e)
        {
            var selectedVideos = GetSelectedVideos();
            if (selectedVideos.Count == 0) return;

            var count = selectedVideos.Count;
            var message = count == 1
                ? $"「{selectedVideos[0].Title}」を削除しますか？"
                : $"{count}件の動画を削除しますか？";

            var result = MessageBox.Show(
                message + "\n\n※ダウンロード済みのファイルは削除されません",
                "確認",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes) return;

            var deletedCount = 0;
            foreach (var video in selectedVideos)
            {
                // ダウンロード中の場合はキャンセル
                if (video.Status == DownloadStatus.Downloading || video.Status == DownloadStatus.Pending)
                {
                    _downloadManager.CancelTask(video.VideoId);
                }
                
                // DBから削除
                _database.DeleteVideo(video.Id);
                deletedCount++;
            }

            RefreshChannelTree();
            RefreshVideoList();
            UpdateStatusBar($"{deletedCount}件の動画を削除しました");
        }

        /// <summary>
        /// 投稿者のページ (iwara プロフィール) を開く
        /// </summary>
        private void menuVidOpenAuthor_Click(object sender, EventArgs e)
        {
            var video = GetFirstSelectedVideo();
            if (video == null || string.IsNullOrEmpty(video.AuthorUsername)) return;
            Helpers.OpenUrl($"https://www.iwara.tv/profile/{video.AuthorUsername}");
        }

        /// <summary>
        /// 再ダウンロード: ローカルファイル削除 → Pending に戻して再キュー。
        /// 完了済みファイルを意図的に取り直す用 (壊れた・別画質でやり直し等)。
        /// </summary>
        private void menuVidReDownload_Click(object sender, EventArgs e)
        {
            var selectedVideos = GetSelectedVideos()
                .Where(v => v.Status == DownloadStatus.Completed)
                .ToList();
            if (selectedVideos.Count == 0) return;

            var count = selectedVideos.Count;
            var totalSize = selectedVideos.Sum(v => v.FileSize);
            var message = count == 1
                ? $"「{selectedVideos[0].Title}」を再ダウンロードします。\n\n" +
                  $"既存ファイルは削除されます。続行しますか？"
                : $"{count}件の動画を再ダウンロードします (合計 {FormatFileSize(totalSize)})。\n\n" +
                  $"既存ファイルは削除されます。続行しますか？";

            var result = MessageBox.Show(message, "再ダウンロード確認",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result != DialogResult.Yes) return;

            int requeuedCount = 0;
            foreach (var video in selectedVideos)
            {
                // ローカルファイル削除 (失敗してもログだけ残して継続)
                if (!string.IsNullOrEmpty(video.LocalFilePath) && File.Exists(video.LocalFilePath))
                {
                    try { File.Delete(video.LocalFilePath); }
                    catch (Exception ex) { Debug.WriteLine($"ファイル削除失敗 ({video.LocalFilePath}): {ex.Message}"); }
                }
                // メタデータ JSON もあれば削除
                if (!string.IsNullOrEmpty(video.LocalFilePath))
                {
                    var metaPath = Path.ChangeExtension(video.LocalFilePath, ".json");
                    if (File.Exists(metaPath))
                    {
                        try { File.Delete(metaPath); } catch { }
                    }
                    // インデックスキャッシュも無効化 (UUID マップから外す)
                    var dir = Path.GetDirectoryName(video.LocalFilePath);
                    if (!string.IsNullOrEmpty(dir))
                        Services.IndexCacheService.Invalidate(dir);
                }

                // DB をリセット
                video.LocalFilePath = string.Empty;
                video.FileSize = 0;
                video.Status = DownloadStatus.Pending;
                video.DownloadedAt = null;
                video.RetryCount = 0;
                video.LastErrorMessage = null;
                _database.UpdateVideo(video);

                SubscribedUser? user = null;
                if (video.SubscribedUserId.HasValue)
                {
                    user = _database.GetSubscribedUserById(video.SubscribedUserId.Value);
                }
                _downloadManager.EnqueueDownload(video, video.SubscribedUserId.HasValue, user);
                requeuedCount++;
            }

            RefreshChannelTree();
            RefreshVideoList();
            UpdateStatusBar($"{requeuedCount}件を再ダウンロードキューに追加しました");
        }

        /// <summary>
        /// 詳細情報ダイアログを開く (タグ・メモ編集も可)
        /// </summary>
        private void menuVidDetails_Click(object sender, EventArgs e)
        {
            var video = GetFirstSelectedVideo();
            if (video == null) return;

            using var form = new VideoDetailsForm(video, _database);
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                // タグ・メモ・お気に入り等の編集を反映 (該当行だけ再描画)
                InvalidateVideoItem(video.VideoId);
                // お気に入りのみ表示中なら一覧から外れる可能性 → 再フィルタ
                if (chkFavOnly != null && chkFavOnly.Checked) ApplyVideoFilter();
                UpdateStatusBar($"「{video.Title}」を更新しました");
            }
        }

        /// <summary>
        /// 右クリックメニュー: 選択動画のお気に入りをトグル。
        /// 全てお気に入りなら解除、そうでなければ全て登録する。
        /// </summary>
        private void menuVidFavorite_Click(object sender, EventArgs e)
        {
            var selected = GetSelectedVideos();
            if (selected.Count == 0) return;
            bool newState = !selected.All(v => v.IsFavorite);
            ToggleFavorite(selected, newState);
        }

        /// <summary>
        /// 指定動画群のお気に入りを newState に設定し、DB 保存 + 該当行を再描画する。
        /// </summary>
        private void ToggleFavorite(IList<VideoInfo> videos, bool newState)
        {
            int changed = 0;
            foreach (var v in videos)
            {
                if (v.IsFavorite == newState) continue;
                v.IsFavorite = newState;
                try { _database.SetVideoFavorite(v.Id, newState); }
                catch (Exception ex) { _logger.Warn($"SetVideoFavorite failed for {v.VideoId}: {ex.Message}"); }
                InvalidateVideoItem(v.VideoId);
                changed++;
            }
            if (changed == 0) return;
            // お気に入りのみ表示中なら、解除で一覧から外れる → 再フィルタ
            if (chkFavOnly != null && chkFavOnly.Checked) ApplyVideoFilter();
            UpdateStatusBar(newState ? $"{changed}件をお気に入りに追加しました" : $"{changed}件をお気に入りから削除しました");
        }

        /// <summary>
        /// サムネ表示時、サムネ右下の星アイコンをクリックしたらお気に入りトグル。
        /// </summary>
        private void listViewVideos_MouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (listViewVideos.View != View.LargeIcon || listViewVideos.VirtualMode) return;

            var hit = listViewVideos.HitTest(e.Location);
            if (hit?.Item == null) return;
            int idx = hit.Item.Index;
            if (idx < 0 || idx >= listViewVideos.Items.Count) return;

            var iconBounds = listViewVideos.GetItemRect(idx, ItemBoundsPortion.Icon);
            var imageRect = GetThumbImageRect(iconBounds);
            if (!GetThumbStarHotspot(imageRect).Contains(e.Location)) return;

            if (listViewVideos.Items[idx].Tag is VideoInfo video)
            {
                ToggleFavorite(new List<VideoInfo> { video }, !video.IsFavorite);
            }
        }

        #endregion

        #region Tray Icon

        private void notifyIcon_DoubleClick(object sender, EventArgs e)
        {
            ShowMainWindow();
        }

        private void menuShow_Click(object sender, EventArgs e)
        {
            ShowMainWindow();
        }

        private void menuExit_Click(object sender, EventArgs e)
        {
            // Application.Exit() を直叩きすると FormClosing 経由の
            // メタデータ書き込み待機 (MetadataService.WaitForWritesToComplete)
            // が走らず、mp4 タグ書き込み中なら moov atom が破損する可能性がある。
            // _isClosing=true により MinimizeToTray 分岐をバイパスして FormClosing 経由で確実に終了する。
            _isClosing = true;
            this.Close();
        }

        private void ShowMainWindow()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            this.Activate();
        }

        #endregion

        #region Helpers

        private void UpdateStatusBar(string message)
        {
            if (InvokeRequired)
            {
                Invoke(() => UpdateStatusBar(message));
                return;
            }
            lblStatus.Text = message;
        }

        // DL イベント高頻度時 (毎チャンクごとに発火) の DB 全件スキャン抑制用 debounce
        private System.Windows.Forms.Timer? _downloadCountTimer;
        private const int DownloadCountDebounceMs = 500;
        private void ScheduleDownloadCountUpdate()
        {
            if (_isClosing || IsDisposed) return;
            if (_downloadCountTimer == null)
            {
                _downloadCountTimer = new System.Windows.Forms.Timer { Interval = DownloadCountDebounceMs };
                _downloadCountTimer.Tick += (_, _) =>
                {
                    _downloadCountTimer!.Stop();
                    try { UpdateDownloadCount(); }
                    catch (Exception ex) { _logger.Error("UpdateDownloadCount 例外", ex); }
                };
            }
            _downloadCountTimer.Stop();
            _downloadCountTimer.Start();
        }

        private void UpdateDownloadCount()
        {
            var downloading = _database.GetVideosByStatus(DownloadStatus.Downloading).Count;
            var pending = _database.GetVideosByStatus(DownloadStatus.Pending).Count;
            var allVideos = _database.GetAllVideos();
            var completed = allVideos.Count(v => v.Status == DownloadStatus.Completed);
            var totalSize = allVideos.Where(v => v.Status == DownloadStatus.Completed).Sum(v => v.FileSize);
            var totalSizeStr = FormatFileSize(totalSize);

            // キュー全体の進捗
            //   - 文字列: 件数表記 ("DL 中 3件: 平均 42% / 待機 10件")
            //   - プログレスバー: DL 中タスクの平均進捗 (ユーザーには 1 件ずつの体感に近い)
            var activeTasks = _downloadManager.GetActiveTasks();
            var dlTasks = activeTasks.Where(t => t.Status == DownloadStatus.Downloading).ToList();
            int progressBarValue = 0;
            string queueText = "";

            if (dlTasks.Count > 0)
            {
                double avgInProgress = dlTasks.Average(t => t.Progress);
                progressBarValue = Math.Clamp((int)avgInProgress, 0, 100);
                queueText = $" | キュー: DL中 {dlTasks.Count}件 平均{avgInProgress:F0}% / 待機 {pending}件";
            }
            else if (pending > 0)
            {
                // DL 開始待ち (セマフォ待ち or rate-limit delay 中)
                queueText = $" | キュー: 待機 {pending}件";
            }

            lblDownloadCount.Text = $"DL: {downloading} / 待機: {pending} | 完了: {completed}件 ({totalSizeStr}){queueText}";

            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Value = progressBarValue;
        }

        /// <summary>
        /// ファイルサイズを表示用にフォーマット
        /// </summary>
        private static string FormatFileSize(long bytes)
        {
            if (bytes <= 0) return "0 B";
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

        private string? ShowInputDialog(string title, string prompt, string defaultValue = "")
        {
            using var form = new Form();
            form.Text = title;
            form.Size = new Size(400, 150);
            form.StartPosition = FormStartPosition.CenterParent;
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.MaximizeBox = false;
            form.MinimizeBox = false;

            var label = new Label { Text = prompt, Location = new Point(10, 15), Size = new Size(360, 20) };
            var textBox = new TextBox { Location = new Point(10, 40), Size = new Size(360, 25), Text = defaultValue };
            var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(210, 75), Size = new Size(75, 25) };
            var btnCancel = new Button { Text = "キャンセル", DialogResult = DialogResult.Cancel, Location = new Point(295, 75), Size = new Size(75, 25) };

            form.Controls.AddRange(new Control[] { label, textBox, btnOk, btnCancel });
            form.AcceptButton = btnOk;
            form.CancelButton = btnCancel;

            return form.ShowDialog() == DialogResult.OK ? textBox.Text : null;
        }

        private string? ShowPasswordDialog(string title, string prompt)
        {
            using var form = new Form();
            form.Text = title;
            form.Size = new Size(400, 150);
            form.StartPosition = FormStartPosition.CenterParent;
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.MaximizeBox = false;
            form.MinimizeBox = false;

            var label = new Label { Text = prompt, Location = new Point(10, 15), Size = new Size(360, 20) };
            var textBox = new TextBox { Location = new Point(10, 40), Size = new Size(360, 25), UseSystemPasswordChar = true };
            var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(210, 75), Size = new Size(75, 25) };
            var btnCancel = new Button { Text = "キャンセル", DialogResult = DialogResult.Cancel, Location = new Point(295, 75), Size = new Size(75, 25) };

            form.Controls.AddRange(new Control[] { label, textBox, btnOk, btnCancel });
            form.AcceptButton = btnOk;
            form.CancelButton = btnCancel;

            return form.ShowDialog() == DialogResult.OK ? textBox.Text : null;
        }

        #endregion

        #region Video Filter

        /// <summary>
        /// フィルターテキスト変更時
        /// </summary>
        private void txtVideoFilter_TextChanged(object sender, EventArgs e)
        {
            _currentFilterText = txtVideoFilter.Text;
            ApplyVideoFilter();
        }

        /// <summary>
        /// フィルターボックスでのキー入力
        /// </summary>
        private void txtVideoFilter_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                // Escでフィルタークリア
                txtVideoFilter.Clear();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                // Enterで動画リストにフォーカス移動(仮想モード対応)
                int max = listViewVideos.VirtualMode
                    ? listViewVideos.VirtualListSize
                    : listViewVideos.Items.Count;
                if (max > 0)
                {
                    listViewVideos.Focus();
                    listViewVideos.SelectedIndices.Clear();
                    listViewVideos.SelectedIndices.Add(0);
                }
                e.SuppressKeyPress = true;
            }
        }

        /// <summary>
        /// フィルタークリアボタン
        /// </summary>
        private void btnClearFilter_Click(object sender, EventArgs e)
        {
            txtVideoFilter.Clear();
        }

        #endregion

        #region ListView Virtual Mode & Sorting

        /// <summary>
        /// ListViewソート設定を初期化(仮想モード対応)
        /// </summary>
        private void InitializeListViewSorter()
        {
            // デフォルトは追加日時の降順
            _sortColumn = 5;
            _sortOrder = SortOrder.Descending;
            UpdateColumnHeaders();
        }

        /// <summary>
        /// 仮想モード: アイテム取得
        /// </summary>
        private void listViewVideos_RetrieveVirtualItem(object? sender, RetrieveVirtualItemEventArgs e)
        {
            // キャッシュにあるか確認
            if (_itemCache.Length > 0 && 
                e.ItemIndex >= _cacheStartIndex && 
                e.ItemIndex < _cacheStartIndex + _itemCache.Length)
            {
                e.Item = _itemCache[e.ItemIndex - _cacheStartIndex];
            }
            else if (e.ItemIndex >= 0 && e.ItemIndex < _displayVideoList.Count)
            {
                // キャッシュにない場合は新規作成
                e.Item = CreateVideoListItem(_displayVideoList[e.ItemIndex]);
            }
            else
            {
                // 範囲外の場合は空のアイテムを返す
                e.Item = new ListViewItem();
            }
        }

        /// <summary>
        /// 仮想モード: キャッシュ更新
        /// </summary>
        private void listViewVideos_CacheVirtualItems(object? sender, CacheVirtualItemsEventArgs e)
        {
            // 既にキャッシュ済みの範囲内なら何もしない
            if (_itemCache.Length > 0 &&
                e.StartIndex >= _cacheStartIndex &&
                e.EndIndex < _cacheStartIndex + _itemCache.Length)
            {
                return;
            }

            // 新しいキャッシュを作成
            _cacheStartIndex = e.StartIndex;
            var cacheLength = Math.Min(e.EndIndex - e.StartIndex + 1, _displayVideoList.Count - e.StartIndex);
            cacheLength = Math.Max(cacheLength, 0);
            _itemCache = new ListViewItem[cacheLength];

            for (int i = 0; i < cacheLength; i++)
            {
                var videoIndex = e.StartIndex + i;
                if (videoIndex < _displayVideoList.Count)
                {
                    _itemCache[i] = CreateVideoListItem(_displayVideoList[videoIndex]);
                }
            }
        }

        /// <summary>
        /// 仮想モード: キーボード検索(タイピングでアイテムにジャンプ)
        /// </summary>
        private void listViewVideos_SearchForVirtualItem(object? sender, SearchForVirtualItemEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text)) return;

            var searchText = e.Text.ToLower();
            var startIndex = e.StartIndex;

            // 前方検索
            for (int i = startIndex; i < _displayVideoList.Count; i++)
            {
                if (_displayVideoList[i].Title.ToLower().StartsWith(searchText))
                {
                    e.Index = i;
                    return;
                }
            }

            // 先頭から検索(ラップアラウンド)
            for (int i = 0; i < startIndex; i++)
            {
                if (_displayVideoList[i].Title.ToLower().StartsWith(searchText))
                {
                    e.Index = i;
                    return;
                }
            }
        }

        /// <summary>
        /// カラムクリックでソート(仮想モード対応)
        /// </summary>
        private void listViewVideos_ColumnClick(object? sender, ColumnClickEventArgs e)
        {
            // 同じカラムをクリックした場合は順序を反転
            if (e.Column == _sortColumn)
            {
                _sortOrder = _sortOrder == SortOrder.Ascending
                    ? SortOrder.Descending
                    : SortOrder.Ascending;
            }
            else
            {
                // 新しいカラムの場合は昇順から開始
                _sortColumn = e.Column;
                _sortOrder = SortOrder.Ascending;
            }

            // データソースをソートして再表示
            SortAndRefreshVideoList();
            UpdateColumnHeaders();
        }

        /// <summary>
        /// データソースをソートして表示を更新
        /// </summary>
        private void SortAndRefreshVideoList()
        {
            // 選択状態を保存
            var selectedVideoIds = GetSelectedVideoIds();

            // ソート実行
            SortDisplayVideoList();

            // キャッシュをクリア
            ClearItemCache();

            // 表示を更新
            listViewVideos.VirtualListSize = _displayVideoList.Count;
            listViewVideos.Invalidate();

            // 選択状態を復元
            RestoreSelectedVideoIds(selectedVideoIds);
        }

        /// <summary>
        /// 表示用リストをソート
        /// </summary>
        private void SortDisplayVideoList()
        {
            if (_sortOrder == SortOrder.None || _displayVideoList.Count == 0)
                return;

            Comparison<VideoInfo> comparison = _sortColumn switch
            {
                0 => (a, b) => string.Compare(a.Title, b.Title, StringComparison.CurrentCulture),
                1 => (a, b) => string.Compare(GetVideoSourceLabel(a), GetVideoSourceLabel(b), StringComparison.Ordinal),
                2 => (a, b) => a.Status.CompareTo(b.Status),
                3 => (a, b) => GetProgressValue(a).CompareTo(GetProgressValue(b)),
                4 => (a, b) => a.FileSize.CompareTo(b.FileSize),
                5 => (a, b) => a.CreatedAt.CompareTo(b.CreatedAt),
                _ => (a, b) => 0
            };

            _displayVideoList.Sort(comparison);

            if (_sortOrder == SortOrder.Descending)
            {
                _displayVideoList.Reverse();
            }
        }

        /// <summary>
        /// 進捗値を取得(ソート用)
        /// </summary>
        private double GetProgressValue(VideoInfo video)
        {
            var task = _downloadManager.GetTask(video.VideoId);
            if (task != null && task.Status == DownloadStatus.Downloading)
                return task.Progress;
            if (video.Status == DownloadStatus.Completed)
                return 100;
            if (video.Status == DownloadStatus.Pending)
                return -1;
            return -2;
        }

        /// <summary>
        /// カラムヘッダーのソート方向表示を更新
        /// </summary>
        private void UpdateColumnHeaders()
        {
            var baseTexts = new[] { "タイトル", "ソース", "状態", "進捗", "サイズ", "追加日時" };

            for (int i = 0; i < listViewVideos.Columns.Count && i < baseTexts.Length; i++)
            {
                if (i == _sortColumn && _sortOrder != SortOrder.None)
                {
                    var arrow = _sortOrder == SortOrder.Ascending ? " ▲" : " ▼";
                    listViewVideos.Columns[i].Text = baseTexts[i] + arrow;
                }
                else
                {
                    listViewVideos.Columns[i].Text = baseTexts[i];
                }
            }
        }

        /// <summary>
        /// アイテムキャッシュをクリア
        /// </summary>
        private void ClearItemCache()
        {
            _itemCache = Array.Empty<ListViewItem>();
            _cacheStartIndex = 0;
        }

        /// <summary>
        /// 選択中の動画IDを取得
        /// </summary>
        private HashSet<string> GetSelectedVideoIds()
        {
            var selectedIds = new HashSet<string>();
            bool isTile = !listViewVideos.VirtualMode;
            foreach (int index in listViewVideos.SelectedIndices)
            {
                if (isTile)
                {
                    if (index >= 0 && index < listViewVideos.Items.Count
                        && listViewVideos.Items[index].Tag is VideoInfo v)
                    {
                        selectedIds.Add(v.VideoId);
                    }
                }
                else if (index >= 0 && index < _displayVideoList.Count)
                {
                    selectedIds.Add(_displayVideoList[index].VideoId);
                }
            }
            return selectedIds;
        }

        /// <summary>
        /// 選択状態を復元
        /// </summary>
        private void RestoreSelectedVideoIds(HashSet<string> selectedVideoIds)
        {
            if (selectedVideoIds.Count == 0) return;

            listViewVideos.SelectedIndices.Clear();
            // サムネモードは Items.Count が上限、詳細は _displayVideoList が上限
            int max = listViewVideos.VirtualMode
                ? _displayVideoList.Count
                : listViewVideos.Items.Count;
            for (int i = 0; i < max; i++)
            {
                string? id = listViewVideos.VirtualMode
                    ? _displayVideoList[i].VideoId
                    : (listViewVideos.Items[i].Tag as VideoInfo)?.VideoId;
                if (id != null && selectedVideoIds.Contains(id))
                {
                    listViewVideos.SelectedIndices.Add(i);
                }
            }
        }

        /// <summary>
        /// 特定の動画IDの行を再描画 (仮想/非仮想モード両対応)
        /// </summary>
        private void InvalidateVideoItem(string videoId)
        {
            for (int i = 0; i < _displayVideoList.Count; i++)
            {
                if (_displayVideoList[i].VideoId != videoId) continue;

                if (listViewVideos.VirtualMode)
                {
                    if (i >= _cacheStartIndex && i < _cacheStartIndex + _itemCache.Length)
                        _itemCache[i - _cacheStartIndex] = CreateVideoListItem(_displayVideoList[i]);
                    listViewVideos.RedrawItems(i, i, false);
                }
                else if (i >= 0 && i < listViewVideos.Items.Count)
                {
                    listViewVideos.Items[i] = CreateVideoListItem(_displayVideoList[i]);
                }
                break;
            }
        }

        #endregion

        #region Update Check

        /// <summary>
        /// 起動時の更新チェック
        /// </summary>
        private async Task CheckForUpdatesOnStartupAsync()
        {
            try
            {
                // 少し待ってからチェック(起動処理を妨げない)
                await Task.Delay(3000);

                var result = await UpdateService.CheckForUpdateAsync();

                if (result.HasUpdate)
                {
                    var dialogResult = MessageBox.Show(
                        $"新しいバージョンがあります！\n\n" +
                        $"現在: {UpdateService.CurrentVersionString}\n" +
                        $"最新: {result.LatestVersion}\n\n" +
                        $"リリースページを開きますか？",
                        "更新のお知らせ",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (dialogResult == DialogResult.Yes)
                    {
                        UpdateService.OpenReleasesPage();
                    }
                }
            }
            catch (Exception ex)
            {
                // 更新チェックの失敗は黙殺
                System.Diagnostics.Debug.WriteLine($"更新チェック失敗: {ex.Message}");
            }
        }

        #endregion

        #region Help Menu

        /// <summary>
        /// バージョン情報ダイアログを開く
        /// </summary>
        private void menuHelpAbout_Click(object sender, EventArgs e)
        {
            using var form = new AboutForm();
            form.ShowDialog();
        }

        /// <summary>
        /// ログフォルダを開く
        /// </summary>
        private void menuHelpOpenLogs_Click(object sender, EventArgs e)
        {
            var logFolder = LoggingService.Instance.LogDirectory;
            if (Directory.Exists(logFolder))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = logFolder,
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show("ログフォルダが存在しません。", "情報", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>
        /// GitHubページを開く
        /// </summary>
        private void menuHelpGitHub_Click(object sender, EventArgs e)
        {
            Helpers.OpenUrl("https://github.com/dekotan24/iwara-downloader");
        }

        /// <summary>
        /// URL一括インポート
        /// </summary>
        private void menuToolsBulkImport_Click(object sender, EventArgs e)
        {
            using var form = new BulkImportForm(_downloadManager);
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                // リストを更新
                RefreshChannelTree();
                RefreshVideoList();
            }
        }

        /// <summary>
        /// フォルダから取り込み (DL済み mp4 をスキャンして DB へ取り込むウィザード)。
        /// </summary>
        private void menuToolsImportFolder_Click(object sender, EventArgs e)
        {
            ImportFromFolderWizard.ShowOrActivate(this, _downloadManager);
        }

        /// <summary>
        /// 重複チェック
        /// </summary>
        private void menuToolsDuplicateCheck_Click(object sender, EventArgs e)
        {
            using var form = new DuplicateCheckForm();
            form.ShowDialog(this);
            // ダイアログ閉じたらリスト更新
            RefreshChannelTree();
            RefreshVideoList();
        }

        /// <summary>
        /// 統計ダッシュボード
        /// </summary>
        private void menuToolsStatistics_Click(object sender, EventArgs e)
        {
            using var form = new StatisticsForm();
            form.ShowDialog(this);
        }

        /// <summary>
        /// iwara 検索インポート (グループ B で実装)
        /// </summary>
        private void menuToolsSearchImport_Click(object sender, EventArgs e)
        {
            using var form = new SearchImportForm(_downloadManager);
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                RefreshChannelTree();
                RefreshVideoList();
            }
        }

        #endregion

        #region Clipboard Monitor

        // クリップボード変更を Win32 メッセージで受け取る (Timer ポーリング不要)
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        private const int WM_CLIPBOARDUPDATE = 0x031D;

        private bool _clipboardListenerRegistered = false;
        private string? _lastProcessedClipboardText = null;

        protected override void WndProc(ref Message m)
        {
            // 起動・破棄レース中はベースに任せる
            if (m.Msg == WM_CLIPBOARDUPDATE)
            {
                try { OnClipboardChanged(); }
                catch (Exception ex) { Debug.WriteLine($"Clipboard handler error: {ex.Message}"); }
            }
            base.WndProc(ref m);
        }

        /// <summary>
        /// クリップボード ON/OFF トグル
        /// </summary>
        private void btnClipMonitor_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = btnClipMonitor.Checked;
            btnClipMonitor.Text = enabled ? "📋監視: ON" : "📋監視: OFF";
            btnClipMonitor.BackColor = enabled ? Color.LightGreen : SystemColors.Control;

            // 設定保存
            var settings = SettingsManager.Instance.Settings;
            settings.EnableClipboardMonitor = enabled;
            SettingsManager.Instance.Save();

            // リスナー登録/解除
            if (!IsHandleCreated) return; // Form_Load 内のセットアップ時など、ハンドル未作成なら後回し
            if (enabled && !_clipboardListenerRegistered)
            {
                if (AddClipboardFormatListener(this.Handle))
                {
                    _clipboardListenerRegistered = true;
                    UpdateStatusBar("クリップボード監視を開始");
                }
                else
                {
                    _logger?.Warn("AddClipboardFormatListener failed");
                }
            }
            else if (!enabled && _clipboardListenerRegistered)
            {
                RemoveClipboardFormatListener(this.Handle);
                _clipboardListenerRegistered = false;
                UpdateStatusBar("クリップボード監視を停止");
            }
        }

        // _logger は LoggingService の薄いラッパ。OnClipboardChanged 内でも使うので参照保持
        private readonly LoggingService _logger = LoggingService.Instance;

        private async void OnClipboardChanged()
        {
            // async void: トップレベル例外を漏らさない (UI スレッドに上がると process crash)
            try
            {
                // 起動直後やシャットダウン中の保護
                if (IsDisposed || !IsHandleCreated || _isClosing) return;
                if (!btnClipMonitor.Checked) return;

                string text;
                try
                {
                    if (!Clipboard.ContainsText()) return;
                    text = Clipboard.GetText() ?? "";
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Clipboard read failed: {ex.Message}");
                    return;
                }

                text = text.Trim();
                if (string.IsNullOrEmpty(text)) return;

                // 同じ文字列を連打されないように直前と比較
                if (text == _lastProcessedClipboardText) return;

                // iwara URL のみ反応 (動画 or プロフィール)
                bool isVideo = Helpers.IsVideoUrl(text);
                bool isUser = Helpers.IsUserProfileUrl(text);
                if (!isVideo && !isUser) return;

                _lastProcessedClipboardText = text;

                // 既に DB に登録済みの動画なら通知だけしてキューには入れない
                if (isVideo)
                {
                    var vid = Helpers.ExtractVideoIdFromUrl(text);
                    if (!string.IsNullOrEmpty(vid) && _database.GetVideoByVideoId(vid) != null)
                    {
                        UpdateStatusBar($"クリップボード: 既に登録済み ({vid})");
                        return;
                    }
                    UpdateStatusBar($"クリップボード検出: 動画追加中...");
                    await AddVideoAsync(text);
                }
                else
                {
                    _downloadManager.EnqueueSubscribedUser(text);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("OnClipboardChanged で予期せぬ例外", ex);
            }
        }

        /// <summary>
        /// 表示モード切替 (詳細 ↔ サムネ): 実装は C グループで完了
        /// </summary>
        private void btnViewMode_CheckedChanged(object sender, EventArgs e)
        {
            ApplyViewMode(btnViewMode.Checked);
        }

        // C グループ用のスタブ: ApplyViewMode は C: サムネ表示で本実装
        private void ApplyViewMode(bool tileMode)
        {
            btnViewMode.Text = tileMode ? "🖼サムネ" : "📋詳細";
            var settings = SettingsManager.Instance.Settings;
            settings.VideoListViewMode = tileMode ? 1 : 0;
            SettingsManager.Instance.Save();
            SetVideoListViewMode(tileMode);
        }

        // サムネモード時の安全な最大表示件数 (これ以上は UI 操作が重くなる)
        private const int TileModeMaxItems = 500;

        /// <summary>
        /// 表示モード切替 (詳細リスト ↔ サムネタイル)。
        /// VirtualMode / Items / SelectedIndices / VirtualListSize の更新順序が不正だと
        /// "InvalidArgument index" の内部例外が出る。
        /// 安全な順序: 選択クリア → サイズ 0 化 → モード切替 → 表示モード切替 → 投入。
        /// </summary>
        private void SetVideoListViewMode(bool tileMode)
        {
            try
            {
                listViewVideos.BeginUpdate();
                try
                {
                    // どちらの方向でも、まず選択状態と Items / VirtualListSize を完全クリア
                    listViewVideos.SelectedIndices.Clear();
                    if (listViewVideos.VirtualMode)
                    {
                        listViewVideos.VirtualListSize = 0;  // VirtualMode true のとき先に 0 化
                    }
                    listViewVideos.Items.Clear();

                    if (tileMode)
                    {
                        EnsureThumbInfrastructure();
                        listViewVideos.VirtualMode = false;
                        listViewVideos.LargeImageList = _thumbImageList;
                        listViewVideos.View = View.LargeIcon;

                        int count = _displayVideoList.Count;
                        int limit = Math.Min(count, TileModeMaxItems);
                        for (int i = 0; i < limit; i++)
                        {
                            try
                            {
                                listViewVideos.Items.Add(CreateVideoListItem(_displayVideoList[i]));
                            }
                            catch (Exception itemEx)
                            {
                                _logger.Warn($"Tile mode item add failed at {i}: {itemEx.GetType().Name}: {itemEx.Message}");
                            }
                        }

                        if (count > limit)
                            UpdateStatusBar($"サムネモード: 全{count}件中先頭{limit}件を表示中 (フィルタで絞ってください)");
                        else
                            UpdateStatusBar($"サムネモード: {limit}件");
                    }
                    else
                    {
                        listViewVideos.View = View.Details;
                        listViewVideos.VirtualMode = true;
                        listViewVideos.VirtualListSize = _displayVideoList.Count;
                    }
                }
                finally { listViewVideos.EndUpdate(); }

                ClearItemCache();
                listViewVideos.Invalidate();
            }
            catch (Exception ex)
            {
                _logger.Error($"SetVideoListViewMode({tileMode}) failed", ex);
                MessageBox.Show($"表示モード切替でエラー:\n{ex.GetType().Name}: {ex.Message}",
                    "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>サムネ用 ImageList とプレースホルダー画像を 1 回だけ用意する</summary>
        private void EnsureThumbInfrastructure()
        {
            if (_thumbImageList != null) return;

            _thumbImageList = new ImageList
            {
                ImageSize = new Size(ThumbWidth, ThumbHeight),
                ColorDepth = ColorDepth.Depth32Bit,
            };
            // プレースホルダー: 灰色 + 中央に絵文字
            _placeholderThumb = new Bitmap(ThumbWidth, ThumbHeight);
            using (var g = Graphics.FromImage(_placeholderThumb))
            {
                g.Clear(Color.FromArgb(50, 50, 60));
                using var f = new Font("Segoe UI Emoji", 24, FontStyle.Regular);
                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("🎬", f, Brushes.LightGray, new RectangleF(0, 0, ThumbWidth, ThumbHeight), sf);
            }
            _thumbImageList.Images.Add("__placeholder__", _placeholderThumb);

            // サムネ準備完了 → UI 更新
            ThumbnailCacheService.Instance.ThumbnailReady += OnThumbnailReady;
        }

        private void OnThumbnailReady(object? sender, string videoId)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke((Action)(() => HandleThumbnailReady(videoId))); }
            catch (InvalidOperationException) { /* ハンドル破棄レース */ }
        }

        private void HandleThumbnailReady(string videoId)
        {
            if (_thumbImageList == null) return;

            // 該当動画のお気に入り状態を反映した星付きサムネを ImageList に用意する
            bool fav = false;
            for (int j = 0; j < _displayVideoList.Count; j++)
            {
                if (_displayVideoList[j].VideoId == videoId) { fav = _displayVideoList[j].IsFavorite; break; }
            }
            var imgKey = ThumbKey(videoId, fav);
            if (!_thumbImageList.Images.ContainsKey(imgKey))
            {
                var img = ThumbnailCacheService.Instance.TryGetCached(videoId);
                if (img != null)
                {
                    try
                    {
                        using var composed = ComposeThumbWithStar(img, fav);
                        _thumbImageList.Images.Add(imgKey, composed);
                    }
                    catch (Exception ex) { Debug.WriteLine($"ImageList add fail in ThumbReady: {ex.Message}"); }
                }
            }

            // 該当行を再描画
            for (int i = 0; i < _displayVideoList.Count; i++)
            {
                if (_displayVideoList[i].VideoId != videoId) continue;

                if (listViewVideos.View == View.LargeIcon && !listViewVideos.VirtualMode)
                {
                    // サムネモード: 通常 ListView → 該当 Item の ImageKey を再セットして RedrawItems
                    if (i < listViewVideos.Items.Count)
                    {
                        listViewVideos.Items[i].ImageKey = imgKey;
                        listViewVideos.RedrawItems(i, i, false);
                    }
                }
                else
                {
                    // 仮想モード Details: キャッシュ更新 + RedrawItems
                    if (i >= _cacheStartIndex && i < _cacheStartIndex + _itemCache.Length)
                    {
                        _itemCache[i - _cacheStartIndex] = CreateVideoListItem(_displayVideoList[i]);
                    }
                    listViewVideos.RedrawItems(i, i, false);
                }
                break;
            }
        }

        /// <summary>サムネを ImageList に確実に入れる (UI スレッドで I/O ゼロ)。返り値はキー名。
        /// メモリキャッシュにあれば即追加、無ければバックグラウンドでロードして
        /// ThumbnailReady で UI 更新する。</summary>
        // サムネ ImageList のキー。お気に入り状態を埋め込み、☆/★ の付いた別画像として共存させる
        // (トグル時は ImageKey を差し替えるだけで済み、ImageList からの Remove によるインデックス破壊を避ける)。
        private static string ThumbKey(string videoId, bool fav) => videoId + (fav ? "#f" : "#n");

        private string EnsureThumbImageKey(VideoInfo video)
        {
            if (_thumbImageList == null) return "__placeholder__";
            var key = ThumbKey(video.VideoId, video.IsFavorite);
            if (_thumbImageList.Images.ContainsKey(key)) return key;

            // メモリキャッシュのみチェック (I/O しない、UI スレッド即時)
            var mem = ThumbnailCacheService.Instance.TryGetMemoryCached(video.VideoId);
            if (mem != null)
            {
                try
                {
                    using var composed = ComposeThumbWithStar(mem, video.IsFavorite);
                    _thumbImageList.Images.Add(key, composed);
                    return key;
                }
                catch (Exception ex) { Debug.WriteLine($"ImageList add fail: {ex.Message}"); }
            }

            // ディスク or ネット をバックグラウンドでロード → ThumbnailReady → HandleThumbnailReady
            ThumbnailCacheService.Instance.EnsureLoadedAsync(video.VideoId, video.ThumbnailUrl);
            return "__placeholder__";
        }

        /// <summary>
        /// サムネ画像の右下に半透明のお気に入り星 (★=登録済 / ☆=未登録) を合成した新しい Bitmap を返す。
        /// 元画像は変更しない。返した Bitmap は ImageList へ Add 後に破棄してよい (内部でコピーされる)。
        /// </summary>
        private static Bitmap ComposeThumbWithStar(Image baseImg, bool isFavorite)
        {
            var bmp = new Bitmap(ThumbWidth, ThumbHeight);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.DrawImage(baseImg, 0, 0, ThumbWidth, ThumbHeight);

                // 右下に半透明の丸い下地 + 星
                const int box = 24;
                int x = ThumbWidth - box - 3;
                int y = ThumbHeight - box - 3;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var bg = new SolidBrush(Color.FromArgb(130, 0, 0, 0)))
                    g.FillEllipse(bg, x, y, box, box);

                var glyph = isFavorite ? "★" : "☆";
                var color = isFavorite ? Color.FromArgb(245, 255, 205, 60) : Color.FromArgb(235, 255, 255, 255);
                using var f = new Font("Segoe UI Symbol", 14f, FontStyle.Regular, GraphicsUnit.Pixel);
                using var br = new SolidBrush(color);
                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(glyph, f, br, new RectangleF(x, y, box, box), sf);
            }
            return bmp;
        }

        /// <summary>
        /// LargeIcon の Icon 領域内で実サムネ画像が占める矩形を求める。
        /// Icon 領域は画像より一回り大きく (実測 176x94 vs 160x90)、画像は水平センタリング・上寄せされるため、
        /// その分を補正しないと星のクリック判定が右へズレる。
        /// </summary>
        private static Rectangle GetThumbImageRect(Rectangle iconBounds)
        {
            int x = iconBounds.X + Math.Max(0, (iconBounds.Width - ThumbWidth) / 2);
            int y = iconBounds.Y; // LargeIcon は上寄せ
            return new Rectangle(x, y, ThumbWidth, ThumbHeight);
        }

        /// <summary>サムネ上のお気に入り星のクリック判定領域 (画像の右下角、広めに取る)。</summary>
        private static Rectangle GetThumbStarHotspot(Rectangle imageRect)
        {
            const int box = 34; // 描画星(24px)より広めにしてクリックしやすく
            return new Rectangle(imageRect.Right - box, imageRect.Bottom - box, box, box);
        }

        /// <summary>
        /// 詳細検索行の開閉トグル
        /// </summary>
        private void btnAdvancedSearch_Click(object sender, EventArgs e)
        {
            panelAdvancedFilter.Visible = !panelAdvancedFilter.Visible;
            btnAdvancedSearch.Text = panelAdvancedFilter.Visible ? "詳細検索 ▴" : "詳細検索 ▾";
        }

        private void chkFavOnly_CheckedChanged(object sender, EventArgs e) => ApplyVideoFilter();

        private void txtTagFilter_TextChanged(object sender, EventArgs e) => ApplyVideoFilter();

        // NSFW フィルタ ComboBox → SetNsfwFilter (再入防止フラグ付き)
        private bool _suppressNsfwEvent = false;
        private void cmbNsfwFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressNsfwEvent) return;
            SetNsfwFilter(cmbNsfwFilter.SelectedIndex);
        }

        /// <summary>
        /// NSFW フィルタ設定 (0=全部 / 1=SFW / 2=NSFW)。詳細検索行の ComboBox と同期する。
        /// </summary>
        private void SetNsfwFilter(int mode)
        {
            if (mode < 0) mode = 0;
            var settings = SettingsManager.Instance.Settings;
            settings.NsfwFilterMode = mode;
            SettingsManager.Instance.Save();
            if (cmbNsfwFilter != null && cmbNsfwFilter.SelectedIndex != mode
                && mode >= 0 && mode < cmbNsfwFilter.Items.Count)
            {
                _suppressNsfwEvent = true;
                try { cmbNsfwFilter.SelectedIndex = mode; }
                finally { _suppressNsfwEvent = false; }
            }
            RefreshVideoList();
        }

        #endregion
    }
}
