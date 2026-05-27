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

        private async void btnAddUser_Click(object sender, EventArgs e)
        {
            var input = ShowInputDialog("チャンネル追加", "ユーザー名またはプロフィールURLを入力:");
            if (string.IsNullOrEmpty(input)) return;

            // URL形式の場合はiwaraのURLかチェック
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
                // ユーザー名のバリデーション
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

            await AddUserAsync(input);
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

        private async void btnCheckNow_Click(object sender, EventArgs e)
        {
            btnCheckNow.Enabled = false;
            UpdateStatusBar("新着確認中...");

            try
            {
                var progress = new Progress<string>(msg => UpdateStatusBar(msg));
                await _downloadManager.CheckForNewVideosAsync(progress);
                RefreshChannelTree();
                RefreshVideoList();
            }
            finally
            {
                btnCheckNow.Enabled = true;
                UpdateStatusBar("確認完了");
            }
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
                await AddUserAsync(input);
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
                await AddUserAsync(input);
            }
        }

        private async Task AddUserAsync(string input)
        {
            UpdateStatusBar("チャンネルを追加中...");

            try
            {
                var progress = new Progress<string>(msg => UpdateStatusBar(msg));
                var user = await _downloadManager.AddSubscribedUserAsync(input, progress);

                if (user != null)
                {
                    RefreshChannelTree();
                    UpdateStatusBar($"チャンネル「{user.Username}」を追加しました");
                }
                else
                {
                    MessageBox.Show("チャンネルの追加に失敗しました。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    UpdateStatusBar("チャンネル追加失敗");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"エラー: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateStatusBar("エラー");
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
            UpdateDownloadCount();
        }

        private void OnTaskStatusChanged(object? sender, DownloadTask task)
        {
            if (InvokeRequired)
            {
                PostToUi(() => OnTaskStatusChanged(sender, task));
                return;
            }

            UpdateVideoItem(task);
            UpdateDownloadCount();

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

            // 仮想リストサイズを更新
            listViewVideos.VirtualListSize = _displayVideoList.Count;
            listViewVideos.Invalidate();

            // フィルター結果をステータスに表示
            if (!query.IsEmpty || nsfwMode != 0)
            {
                UpdateStatusBar($"フィルター: {_displayVideoList.Count}/{_allVideoList.Count}件");
            }
        }

        /// <summary>
        /// 動画のソース（埋め込み元）を表示用文字列で返す
        /// </summary>
        private static string GetVideoSourceLabel(VideoInfo video)
        {
            if (!video.IsExternal) return "iwara";

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
                    
                    // キャッシュを更新
                    if (i >= _cacheStartIndex && i < _cacheStartIndex + _itemCache.Length)
                    {
                        _itemCache[i - _cacheStartIndex] = CreateVideoListItem(task.Video);
                    }
                    
                    // 該当行を再描画
                    listViewVideos.RedrawItems(i, i, false);
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
            // 右ダブルクリックでも DoubleClick イベントが発火する WinForms 仕様への対策。
            // 左ボタンのみ反応にして、右ダブルクリックは MouseUp 経由の右クリックメニュー扱いに任せる。
            if (e.Button != MouseButtons.Left) return;

            var video = GetFirstSelectedVideo();
            if (video == null) return;

            if (video.Status == DownloadStatus.Completed && !string.IsNullOrEmpty(video.LocalFilePath) && File.Exists(video.LocalFilePath))
            {
                // 完了済み → 再生
                Process.Start(new ProcessStartInfo { FileName = video.LocalFilePath, UseShellExecute = true });
            }
            else
            {
                // 未完了 → ページを開く
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
                for (int i = 0; i < _displayVideoList.Count; i++)
                {
                    listViewVideos.SelectedIndices.Add(i);
                }
                listViewVideos.EndUpdate();
            }
            // Apps キー (キーボード右下のメニューキー) / Shift+F10 で右クリックメニューを開く
            // ContextMenuStrip 自動紐付けを外したため、手動で Show() する必要がある
            else if (e.KeyCode == Keys.Apps || (e.Shift && e.KeyCode == Keys.F10))
            {
                e.SuppressKeyPress = true;
                if (listViewVideos.SelectedIndices.Count == 0) return;

                // フォーカスされた行の位置にメニューを表示
                Point showAt;
                var focused = listViewVideos.FocusedItem;
                if (focused != null && focused.Bounds.Height > 0)
                {
                    showAt = new Point(focused.Bounds.X + 16, focused.Bounds.Y + focused.Bounds.Height);
                }
                else
                {
                    showAt = new Point(10, 10);
                }
                contextMenuVideo.Show(listViewVideos, showAt);
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
        /// 選択中の動画を取得(仮想モード対応)
        /// </summary>
        private List<VideoInfo> GetSelectedVideos()
        {
            var videos = new List<VideoInfo>();
            foreach (int index in listViewVideos.SelectedIndices)
            {
                if (index >= 0 && index < _displayVideoList.Count)
                {
                    videos.Add(_displayVideoList[index]);
                }
            }
            return videos;
        }

        /// <summary>
        /// 最初の選択動画を取得(仮想モード対応)
        /// </summary>
        private VideoInfo? GetFirstSelectedVideo()
        {
            if (listViewVideos.SelectedIndices.Count > 0)
            {
                var index = listViewVideos.SelectedIndices[0];
                if (index >= 0 && index < _displayVideoList.Count)
                {
                    return _displayVideoList[index];
                }
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

            // チャンネル用メニュー項目の表示/非表示
            menuChOpen.Visible = isUserNode;
            menuChCheckNow.Visible = isUserNode;
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

            // メニュー項目がない場合はキャンセル
            if (!showDownloadAll && !isUserNode)
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

        private async void menuChCheckNow_Click(object sender, EventArgs e)
        {
            if (treeViewChannels.SelectedNode?.Tag is SubscribedUser user)
            {
                UpdateStatusBar($"{user.Username} の新着を確認中...");
                var progress = new Progress<string>(msg => UpdateStatusBar(msg));
                
                // 選択したチャンネルのみチェック
                await _downloadManager.CheckForNewVideosAsync(user, progress);
                
                RefreshChannelTree();
                RefreshVideoList();
                UpdateStatusBar($"{user.Username} の確認完了");
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
        /// 右クリックでコンテキストメニューを自前表示 (ContextMenuStrip 自動紐付けは外してある)。
        /// 仮想モード ListView では HitTest が返す Item.Selected 設定が反映されないので、
        /// SelectedIndices を直接操作してから手動で contextMenuVideo.Show() を呼ぶ。
        /// </summary>
        private void listViewVideos_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;

            var hit = listViewVideos.HitTest(e.X, e.Y);
            if (hit?.Item != null && hit.Item.Index >= 0)
            {
                int idx = hit.Item.Index;
                bool alreadySelected = false;
                foreach (int si in listViewVideos.SelectedIndices)
                {
                    if (si == idx) { alreadySelected = true; break; }
                }
                if (!alreadySelected)
                {
                    listViewVideos.SelectedIndices.Clear();
                    listViewVideos.SelectedIndices.Add(idx);
                }
                contextMenuVideo.Show(listViewVideos, e.X, e.Y);
            }
            else if (listViewVideos.SelectedIndices.Count > 0)
            {
                // 空白での右クリック: 既存選択があるなら表示
                contextMenuVideo.Show(listViewVideos, e.X, e.Y);
            }
            // 空白 + 選択なし: 何もしない (メニュー非表示)
        }

        /// <summary>
        /// 動画コンテキストメニュー表示前に選択動画のステータスに応じて項目を動的に切替
        /// </summary>
        private void contextMenuVideo_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            var selected = GetSelectedVideos();
            if (selected.Count == 0)
            {
                // 何も選択されていない場合はメニューを表示しない
                e.Cancel = true;
                return;
            }

            bool isSingle = selected.Count == 1;
            var single = isSingle ? selected[0] : null;

            // ステータス集計（複数選択時にどれかが該当すれば true）
            bool hasPending = selected.Any(v => v.Status == DownloadStatus.Pending);
            bool hasDownloading = selected.Any(v => v.Status == DownloadStatus.Downloading);
            bool hasCompleted = selected.Any(v => v.Status == DownloadStatus.Completed);
            bool hasFailed = selected.Any(v => v.Status == DownloadStatus.Failed);
            bool hasPaused = selected.Any(v => v.Status == DownloadStatus.Paused);
            bool hasSkipped = selected.Any(v => v.Status == DownloadStatus.Skipped);

            // ダウンロード可能：完了・進行中・キュー中 以外
            bool canDownload = selected.Any(v =>
                v.Status != DownloadStatus.Completed &&
                v.Status != DownloadStatus.Downloading &&
                v.Status != DownloadStatus.Pending);

            // キャンセル可能：DL中 or キュー中 or 一時停止
            bool canCancel = hasPending || hasDownloading || hasPaused;

            // 再ダウンロード: 完了済みのみ (誤操作防止に確認ダイアログを後で出す)
            bool canReDownload = hasCompleted;

            // タイトル不完全（情報再取得対象）
            bool canRefreshInfo = selected.Any(v =>
                string.IsNullOrEmpty(v.Title) || v.Title.StartsWith("Video "));

            // ファイル存在チェック：完了が含まれる場合
            bool canCheckFile = hasCompleted;

            // 再生・フォルダ：単一選択 + 完了 + LocalFilePath が存在
            bool canPlay = isSingle
                && single!.Status == DownloadStatus.Completed
                && !string.IsNullOrEmpty(single.LocalFilePath)
                && File.Exists(single.LocalFilePath);
            bool canOpenFolder = isSingle
                && !string.IsNullOrEmpty(single!.LocalFilePath)
                && File.Exists(single.LocalFilePath);

            // ページを開く：単一選択 + URL あり
            bool canOpenPage = isSingle && !string.IsNullOrEmpty(single!.Url);

            // 投稿者ページを開く: 単一選択 + AuthorUsername あり
            bool canOpenAuthor = isSingle && !string.IsNullOrEmpty(single!.AuthorUsername);

            // 詳細情報: 単一選択時のみ
            bool canShowDetails = isSingle;

            // 表示切替
            menuVidDownload.Visible = canDownload;
            menuVidDownload.Text = hasFailed && !hasSkipped
                ? "ダウンロード (失敗をリトライ含む)"
                : "ダウンロード";

            menuVidCancel.Visible = canCancel;
            menuVidRetryFailed.Visible = hasFailed;
            menuVidReDownload.Visible = canReDownload;
            menuVidRefreshInfo.Visible = canRefreshInfo;
            menuVidCheckFileExists.Visible = canCheckFile;

            menuVidPlay.Visible = canPlay;
            menuVidOpenFolder.Visible = canOpenFolder;

            menuVidOpenPage.Visible = canOpenPage;
            menuVidOpenAuthor.Visible = canOpenAuthor;
            menuVidCopyUrl.Visible = true;
            menuVidCopyTitle.Visible = true;

            menuVidDetails.Visible = canShowDetails;
            menuVidDelete.Visible = true;

            // セパレータ動的調整
            AdjustSeparatorVisibility(contextMenuVideo);

            // 表示可能な実体項目が0件ならキャンセル
            bool anyVisible = false;
            foreach (ToolStripItem item in contextMenuVideo.Items)
            {
                if (item is ToolStripMenuItem && item.Visible)
                {
                    anyVisible = true;
                    break;
                }
            }
            if (!anyVisible) e.Cancel = true;
        }

        /// <summary>
        /// セパレータの前後に可視メニューがない場合、セパレータも非表示にする
        /// </summary>
        private static void AdjustSeparatorVisibility(ContextMenuStrip menu)
        {
            var items = menu.Items;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] is not ToolStripSeparator) continue;

                // 直前に可視メニュー (実体項目) があるか
                bool prevVisible = false;
                for (int j = i - 1; j >= 0; j--)
                {
                    if (items[j] is ToolStripSeparator) break;
                    if (items[j].Visible) { prevVisible = true; break; }
                }
                // 直後に可視メニューがあるか
                bool nextVisible = false;
                for (int j = i + 1; j < items.Count; j++)
                {
                    if (items[j] is ToolStripSeparator) break;
                    if (items[j].Visible) { nextVisible = true; break; }
                }

                items[i].Visible = prevVisible && nextVisible;
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

            var checkedCount = 0;
            var missingCount = 0;
            var requeuedCount = 0;

            foreach (var video in selectedVideos)
            {
                // ダウンロード済みの動画のみチェック
                if (video.Status == DownloadStatus.Completed && !string.IsNullOrEmpty(video.LocalFilePath))
                {
                    checkedCount++;

                    if (!File.Exists(video.LocalFilePath))
                    {
                        missingCount++;

                        // ステータスをリセット
                        video.Status = DownloadStatus.Pending;
                        video.LocalFilePath = string.Empty;
                        video.DownloadedAt = null;
                        video.RetryCount = 0;
                        video.LastErrorMessage = null;
                        _database.UpdateVideo(video);

                        // DLキューに追加
                        SubscribedUser? user = null;
                        if (video.SubscribedUserId.HasValue)
                        {
                            user = _database.GetSubscribedUserById(video.SubscribedUserId.Value);
                        }
                        _downloadManager.EnqueueDownload(video, video.SubscribedUserId.HasValue, user);
                        requeuedCount++;
                    }
                }
            }

            RefreshChannelTree();
            RefreshVideoList();

            if (checkedCount == 0)
            {
                UpdateStatusBar("ダウンロード済みの動画が選択されていません");
            }
            else if (missingCount == 0)
            {
                UpdateStatusBar($"{checkedCount}件チェック: 全てのファイルが存在します");
            }
            else
            {
                UpdateStatusBar($"{checkedCount}件チェック: {missingCount}件のファイルが見つからず、{requeuedCount}件をキューに追加しました");
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
                // タグ・メモ等の編集を反映 (該当行だけ再描画)
                InvalidateVideoItem(video.VideoId);
                UpdateStatusBar($"「{video.Title}」を更新しました");
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
            _isClosing = true;
            Application.Exit();
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
                if (_displayVideoList.Count > 0)
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
            foreach (int index in listViewVideos.SelectedIndices)
            {
                if (index >= 0 && index < _displayVideoList.Count)
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
            for (int i = 0; i < _displayVideoList.Count; i++)
            {
                if (selectedVideoIds.Contains(_displayVideoList[i].VideoId))
                {
                    listViewVideos.SelectedIndices.Add(i);
                }
            }
        }

        /// <summary>
        /// 特定の動画IDの行を再描画
        /// </summary>
        private void InvalidateVideoItem(string videoId)
        {
            for (int i = 0; i < _displayVideoList.Count; i++)
            {
                if (_displayVideoList[i].VideoId == videoId)
                {
                    // キャッシュを無効化
                    if (i >= _cacheStartIndex && i < _cacheStartIndex + _itemCache.Length)
                    {
                        _itemCache[i - _cacheStartIndex] = CreateVideoListItem(_displayVideoList[i]);
                    }
                    // 該当行を再描画
                    listViewVideos.RedrawItems(i, i, false);
                    break;
                }
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
            using var form = new BulkImportForm();
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                // リストを更新
                RefreshChannelTree();
                RefreshVideoList();
            }
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
                UpdateStatusBar($"クリップボード検出: チャンネル追加中...");
                await AddUserAsync(text);
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

        /// <summary>
        /// 表示モード切替 (詳細リスト ↔ サムネタイル)
        /// </summary>
        private void SetVideoListViewMode(bool tileMode)
        {
            if (tileMode)
            {
                EnsureThumbInfrastructure();
                listViewVideos.LargeImageList = _thumbImageList;
                listViewVideos.View = View.LargeIcon;
                listViewVideos.TileSize = new Size(ThumbWidth + 8, ThumbHeight + 8);
            }
            else
            {
                listViewVideos.View = View.Details;
            }
            ClearItemCache();
            listViewVideos.Invalidate();
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
            // 該当行を再描画 (RetrieveVirtualItem で再度 ImageKey 引き直す)
            for (int i = 0; i < _displayVideoList.Count; i++)
            {
                if (_displayVideoList[i].VideoId == videoId)
                {
                    if (i >= _cacheStartIndex && i < _cacheStartIndex + _itemCache.Length)
                    {
                        _itemCache[i - _cacheStartIndex] = CreateVideoListItem(_displayVideoList[i]);
                    }
                    listViewVideos.RedrawItems(i, i, false);
                    break;
                }
            }
        }

        /// <summary>サムネを ImageList に確実に入れる (まだ無ければ DL 開始)。返り値はキー名。</summary>
        private string EnsureThumbImageKey(VideoInfo video)
        {
            if (_thumbImageList == null) return "__placeholder__";
            var key = video.VideoId;
            if (_thumbImageList.Images.ContainsKey(key)) return key;

            // メモリ/ディスクキャッシュにあれば即追加
            var cached = ThumbnailCacheService.Instance.TryGetCached(video.VideoId);
            if (cached != null)
            {
                try { _thumbImageList.Images.Add(key, cached); return key; }
                catch (Exception ex) { Debug.WriteLine($"ImageList add fail: {ex.Message}"); }
            }

            // 無ければ DL 開始 (完了で ThumbnailReady → 再描画)
            if (!string.IsNullOrEmpty(video.ThumbnailUrl))
                ThumbnailCacheService.Instance.RequestAsync(video.VideoId, video.ThumbnailUrl);

            return "__placeholder__";
        }

        /// <summary>
        /// NSFW フィルタ: 全部
        /// </summary>
        private void menuNsfwAll_Click(object sender, EventArgs e) => SetNsfwFilter(0);
        private void menuNsfwSfw_Click(object sender, EventArgs e) => SetNsfwFilter(1);
        private void menuNsfwNsfw_Click(object sender, EventArgs e) => SetNsfwFilter(2);

        private void SetNsfwFilter(int mode)
        {
            var settings = SettingsManager.Instance.Settings;
            settings.NsfwFilterMode = mode;
            SettingsManager.Instance.Save();
            btnNsfwFilter.Text = mode switch
            {
                1 => "🔞SFW",
                2 => "🔞NSFW",
                _ => "🔞全部"
            };
            menuNsfwAll.Checked = mode == 0;
            menuNsfwSfw.Checked = mode == 1;
            menuNsfwNsfw.Checked = mode == 2;
            RefreshVideoList();
        }

        #endregion
    }
}
