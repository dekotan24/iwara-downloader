using IwaraDownloader.Models;
using IwaraDownloader.Services;
using IwaraDownloader.Utils;
using System.Diagnostics;

namespace IwaraDownloader.Forms
{
    /// <summary>
    /// メインフォーム（JD2風ツリー構造UI）
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
        private const string NODE_FAILED_VIDEOS = "__FAILED_VIDEOS__";
        private const string NODE_SINGLE_VIDEOS = "__SINGLE_VIDEOS__";
        
        // ListViewソーター
        private readonly ListViewColumnSorter _videoListSorter = new();
        
        // フィルター用の全動画キャッシュ
        private List<VideoInfo> _currentVideoList = new();
        private string _currentFilterText = "";

        public MainForm()
        {
            InitializeComponent();
            _downloadManager = new DownloadManager();
            _database = DatabaseService.Instance;
        }

        #region Form Events

        private void MainForm_Load(object sender, EventArgs e)
        {
            // 設定読み込み
            var settings = SettingsManager.Instance.Settings;

            // タスクトレイアイコン設定
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
            _downloadManager.TaskProgressChanged += OnTaskProgressChanged;
            _downloadManager.TaskStatusChanged += OnTaskStatusChanged;
            _downloadManager.NewVideosFound += OnNewVideosFound;
            _downloadManager.AutoCheckCompleted += OnAutoCheckCompleted;

            // 環境チェック
            CheckEnvironment();

            // ログイン状態確認
            UpdateLoginStatus();

            // ツリー初期化
            RefreshChannelTree();

            // ListViewソーター初期化
            InitializeListViewSorter();

            // ダウンロードマネージャー開始
            _downloadManager.Start();

            // 起動時に未完了ダウンロードを再開
            _downloadManager.ResumeIncompleteDownloads();
            RefreshChannelTree();
            RefreshVideoList();

            // 起動時更新チェック
            if (settings.CheckUpdateOnStartup)
            {
                _ = CheckForUpdatesOnStartupAsync();
            }
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
            _downloadManager.Dispose();
            notifyIcon.Visible = false;
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
                if (listViewVideos.SelectedItems.Count > 0)
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

            if (!pythonReady || !scriptReady)
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
            }
        }

        private async void btnSetup_Click(object sender, EventArgs e)
        {
            var pythonPath = ShowInputDialog(
                "環境セットアップ",
                "Pythonのパスを入力してください（例: C:\\Python311\\python.exe）",
                "python");

            if (string.IsNullOrEmpty(pythonPath))
                return;

            btnSetup.Enabled = false;
            UpdateStatusBar("セットアップ中...");

            try
            {
                var progress = new Progress<string>(msg => UpdateStatusBar(msg));
                var success = await _downloadManager.RunSetupAsync(pythonPath, progress);

                if (success)
                {
                    btnSetup.BackColor = SystemColors.Control;
                    MessageBox.Show("セットアップが完了しました！", "完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("セットアップに失敗しました。\nPythonのパスを確認してください。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"セットアップ中にエラーが発生しました:\n{ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnSetup.Enabled = true;
                CheckEnvironment();
            }
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
            using var form = new SettingsForm();
            if (form.ShowDialog() == DialogResult.OK)
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

        private void OnTaskProgressChanged(object? sender, DownloadTask task)
        {
            if (InvokeRequired)
            {
                Invoke(() => OnTaskProgressChanged(sender, task));
                return;
            }
            UpdateVideoItem(task);
            UpdateDownloadCount();
        }

        private void OnTaskStatusChanged(object? sender, DownloadTask task)
        {
            if (InvokeRequired)
            {
                Invoke(() => OnTaskStatusChanged(sender, task));
                return;
            }

            UpdateVideoItem(task);
            UpdateDownloadCount();
            
            if (task.Status == DownloadStatus.Completed || task.Status == DownloadStatus.Failed)
            {
                RefreshChannelTree();
            }
        }

        private void OnNewVideosFound(object? sender, (SubscribedUser User, List<VideoInfo> Videos) e)
        {
            if (InvokeRequired)
            {
                Invoke(() => OnNewVideosFound(sender, e));
                return;
            }
            RefreshChannelTree();
            RefreshVideoList();
        }

        private void OnAutoCheckCompleted(object? sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(() => OnAutoCheckCompleted(sender, e));
                return;
            }
            RefreshChannelTree();
        }

        #endregion

        #region Channel Tree

        /// <summary>
        /// チャンネルツリーを更新
        /// </summary>
        private void RefreshChannelTree()
        {
            treeViewChannels.BeginUpdate();
            
            // 選択状態を保存
            var selectedTag = treeViewChannels.SelectedNode?.Tag;
            
            treeViewChannels.Nodes.Clear();

            // データベースから一括取得（パフォーマンス最適化）
            var allVideos = _database.GetAllVideos();
            var totalCount = allVideos.Count;
            var completedCount = allVideos.Count(v => v.Status == DownloadStatus.Completed);
            var pendingCount = allVideos.Count(v => v.Status == DownloadStatus.Pending);
            var downloadingCount = allVideos.Count(v => v.Status == DownloadStatus.Downloading);
            var failedCount = allVideos.Count(v => v.Status == DownloadStatus.Failed);
            var notDownloadedCount = allVideos.Count(v => v.Status != DownloadStatus.Completed);

            // 「全ての動画」ノード
            var allVideosNode = new TreeNode($"📊 全ての動画 [{completedCount}/{totalCount}]")
            {
                Tag = NODE_ALL_VIDEOS,
                NodeFont = new Font(treeViewChannels.Font, FontStyle.Bold)
            };
            treeViewChannels.Nodes.Add(allVideosNode);

            // 「ダウンロードキュー」ノード
            var queueCount = downloadingCount + pendingCount;
            var allDownloadsNode = new TreeNode($"📥 ダウンロードキュー")
            {
                Tag = NODE_ALL_DOWNLOADS
            };
            if (queueCount > 0)
            {
                allDownloadsNode.Text += $" ({downloadingCount}DL中/{pendingCount}待機)";
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

            // 登録チャンネル
            var users = _database.GetAllSubscribedUsers();
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

        // NodeMouseClickは不要（contextMenuChannel_Opening内でノード選択を行う）

        #endregion

        #region Video List

        /// <summary>
        /// 動画リストを更新
        /// </summary>
        private void RefreshVideoList()
        {
            List<VideoInfo> videos;
            var selectedNode = treeViewChannels.SelectedNode;

            if (selectedNode?.Tag is SubscribedUser user)
            {
                // チャンネルの動画
                videos = _database.GetVideosBySubscribedUser(user.Id).OrderByDescending(v => v.CreatedAt).ToList();
            }
            else if (selectedNode?.Tag is string tag)
            {
                if (tag == NODE_ALL_VIDEOS)
                {
                    // 全ての動画（パフォーマンス最適化: ソートはUI側で）
                    videos = _database.GetAllVideos();
                }
                else if (tag == NODE_ALL_DOWNLOADS)
                {
                    // ダウンロード中/待機中（DBから取得）
                    var downloadingList = _database.GetVideosByStatus(DownloadStatus.Downloading);
                    var pendingList = _database.GetVideosByStatus(DownloadStatus.Pending);
                    videos = downloadingList.Concat(pendingList).ToList();
                }
                else if (tag == NODE_NOT_DOWNLOADED)
                {
                    // 未DL動画（完了以外全て）
                    videos = _database.GetAllVideos().Where(v => v.Status != DownloadStatus.Completed).ToList();
                }
                else if (tag == NODE_FAILED_VIDEOS)
                {
                    // エラー一覧
                    videos = _database.GetVideosByStatus(DownloadStatus.Failed);
                }
                else // NODE_SINGLE_VIDEOS
                {
                    // 単発動画
                    videos = _database.GetAllVideos().Where(v => !v.SubscribedUserId.HasValue).ToList();
                }
            }
            else
            {
                videos = new List<VideoInfo>();
            }

            // キャッシュに保存
            _currentVideoList = videos;
            
            // フィルター適用して表示
            ApplyVideoFilter();
        }

        /// <summary>
        /// 動画リストにフィルターを適用
        /// </summary>
        private void ApplyVideoFilter()
        {
            listViewVideos.BeginUpdate();
            listViewVideos.Items.Clear();

            var filterText = _currentFilterText.Trim().ToLower();
            var filteredVideos = string.IsNullOrEmpty(filterText)
                ? _currentVideoList
                : _currentVideoList.Where(v => 
                    v.Title.ToLower().Contains(filterText) ||
                    v.AuthorUsername.ToLower().Contains(filterText)).ToList();

            foreach (var video in filteredVideos)
            {
                var item = CreateVideoListItem(video);
                listViewVideos.Items.Add(item);
            }

            listViewVideos.EndUpdate();
            
            // フィルター結果をステータスに表示
            if (!string.IsNullOrEmpty(filterText))
            {
                UpdateStatusBar($"フィルター: {filteredVideos.Count}/{_currentVideoList.Count}件");
            }
        }

        private ListViewItem CreateVideoListItem(VideoInfo video)
        {
            var statusIcon = GetStatusIcon(video.Status);
            var statusText = GetStatusText(video.Status);
            
            // 進捗表示
            var progressText = "-";
            var task = _downloadManager.GetTask(video.VideoId);
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
                statusText,
                progressText,
                video.FileSizeFormatted,
                video.CreatedAt.ToString("yyyy/MM/dd")
            })
            {
                Tag = video
            };

            // 状態に応じた色分け
            item.ForeColor = video.Status switch
            {
                DownloadStatus.Completed => Color.Green,
                DownloadStatus.Failed => Color.Red,
                DownloadStatus.Downloading => Color.Blue,
                DownloadStatus.Pending => Color.DarkOrange,
                _ => Color.Black
            };

            // ツールチップ（エラー詳細表示）
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

        private void UpdateVideoItem(DownloadTask task)
        {
            foreach (ListViewItem item in listViewVideos.Items)
            {
                if (item.Tag is VideoInfo video && video.VideoId == task.Video.VideoId)
                {
                    // 進捗更新（速度表示付き）
                    string progressText;
                    if (task.Status == DownloadStatus.Downloading)
                    {
                        if (task.Progress > 0 && task.DownloadSpeed > 0)
                        {
                            progressText = $"{task.Progress:F0}% ({task.SpeedFormatted})";
                        }
                        else if (task.Progress > 0)
                        {
                            progressText = $"{task.Progress:F0}%";
                        }
                        else
                        {
                            progressText = "DL中...";
                        }
                    }
                    else if (task.Status == DownloadStatus.Completed)
                    {
                        progressText = "100%";
                    }
                    else if (task.Status == DownloadStatus.Pending)
                    {
                        progressText = "待機";
                    }
                    else
                    {
                        progressText = "-";
                    }
                    
                    item.SubItems[1].Text = GetStatusText(task.Status);
                    item.SubItems[2].Text = progressText;
                    item.SubItems[0].Text = $"{GetStatusIcon(task.Status)} {task.Video.Title}";
                    
                    // ツールチップ更新（残り時間表示）
                    if (task.Status == DownloadStatus.Downloading && task.EstimatedTimeRemaining.HasValue)
                    {
                        item.ToolTipText = $"{task.Video.Title}\n残り: {task.EtaFormatted}";
                    }
                    
                    item.ForeColor = task.Status switch
                    {
                        DownloadStatus.Completed => Color.Green,
                        DownloadStatus.Failed => Color.Red,
                        DownloadStatus.Downloading => Color.Blue,
                        DownloadStatus.Pending => Color.DarkOrange,
                        _ => Color.Black
                    };
                    return;
                }
            }
        }

        private static string GetStatusIcon(DownloadStatus status)
        {
            return status switch
            {
                DownloadStatus.Pending => "⏳",
                DownloadStatus.Downloading => "🔄",
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
                DownloadStatus.Completed => "完了",
                DownloadStatus.Failed => "失敗",
                DownloadStatus.Skipped => "スキップ",
                DownloadStatus.Paused => "一時停止",
                _ => "不明"
            };
        }

        private void listViewVideos_DoubleClick(object sender, EventArgs e)
        {
            if (listViewVideos.SelectedItems.Count == 0) return;
            var video = listViewVideos.SelectedItems[0].Tag as VideoInfo;
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
            // Ctrl+A で全選択
            if (e.Control && e.KeyCode == Keys.A)
            {
                e.SuppressKeyPress = true; // ビープ音を防ぐ
                
                listViewVideos.BeginUpdate();
                foreach (ListViewItem item in listViewVideos.Items)
                {
                    item.Selected = true;
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
            var selectedCount = listViewVideos.SelectedItems.Count;
            if (selectedCount > 0)
            {
                // 選択中の合計サイズを計算
                long totalSize = 0;
                foreach (ListViewItem item in listViewVideos.SelectedItems)
                {
                    if (item.Tag is VideoInfo video)
                    {
                        totalSize += video.FileSize;
                    }
                }
                var sizeText = totalSize > 0 ? $" ({FormatFileSize(totalSize)})" : "";
                UpdateStatusBar($"{selectedCount}件選択中{sizeText}");
            }
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
            menuChSeparator2.Visible = isUserNode;
            menuChEnable.Visible = isUserNode;
            menuChDisable.Visible = isUserNode;
            menuChSeparator3.Visible = isUserNode;
            menuChDelete.Visible = isUserNode;

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

            using var dialog = new FolderBrowserDialog
            {
                Description = $"{user.Username} の保存先フォルダを選択",
                UseDescriptionForTitle = true,
                SelectedPath = string.IsNullOrEmpty(user.CustomSavePath) 
                    ? Path.Combine(SettingsManager.Instance.Settings.DownloadFolder, user.Username)
                    : user.CustomSavePath
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                user.CustomSavePath = dialog.SelectedPath;
                _database.UpdateSubscribedUser(user);
                UpdateStatusBar($"保存先を変更しました: {dialog.SelectedPath}");
            }
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
                listViewVideos.Items.Clear();
            }
        }

        #endregion

        #region Video Context Menu

        private void menuVidDownload_Click(object sender, EventArgs e)
        {
            if (listViewVideos.SelectedItems.Count == 0) return;
            
            foreach (ListViewItem item in listViewVideos.SelectedItems)
            {
                if (item.Tag is VideoInfo video && video.Status != DownloadStatus.Downloading && video.Status != DownloadStatus.Completed && video.Status != DownloadStatus.Pending)
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
            if (listViewVideos.SelectedItems.Count == 0) return;
            
            foreach (ListViewItem item in listViewVideos.SelectedItems)
            {
                if (item.Tag is VideoInfo video)
                {
                    _downloadManager.CancelTask(video.VideoId);
                }
            }
            RefreshChannelTree();
            RefreshVideoList();
        }

        /// <summary>
        /// 失敗した動画を再試行
        /// </summary>
        private void menuVidRetryFailed_Click(object sender, EventArgs e)
        {
            if (listViewVideos.SelectedItems.Count == 0) return;
            
            var retryCount = 0;
            foreach (ListViewItem item in listViewVideos.SelectedItems)
            {
                if (item.Tag is VideoInfo video && video.Status == DownloadStatus.Failed)
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
            if (listViewVideos.SelectedItems.Count == 0) return;
            
            var refreshCount = 0;
            var progress = new Progress<string>(msg => UpdateStatusBar(msg));
            
            foreach (ListViewItem item in listViewVideos.SelectedItems)
            {
                if (item.Tag is VideoInfo video)
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
            }
            
            RefreshVideoList();
            UpdateStatusBar($"{refreshCount}件の情報を更新しました");
        }

        private void menuVidPlay_Click(object sender, EventArgs e)
        {
            if (listViewVideos.SelectedItems.Count == 0) return;
            var video = listViewVideos.SelectedItems[0].Tag as VideoInfo;
            
            if (video != null && !string.IsNullOrEmpty(video.LocalFilePath) && File.Exists(video.LocalFilePath))
            {
                Process.Start(new ProcessStartInfo { FileName = video.LocalFilePath, UseShellExecute = true });
            }
        }

        private void menuVidOpenFolder_Click(object sender, EventArgs e)
        {
            if (listViewVideos.SelectedItems.Count == 0) return;
            var video = listViewVideos.SelectedItems[0].Tag as VideoInfo;
            
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
            if (listViewVideos.SelectedItems.Count == 0) return;
            var video = listViewVideos.SelectedItems[0].Tag as VideoInfo;
            
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
            if (listViewVideos.SelectedItems.Count == 0) return;
            
            var urls = new List<string>();
            foreach (ListViewItem item in listViewVideos.SelectedItems)
            {
                if (item.Tag is VideoInfo video && !string.IsNullOrEmpty(video.Url))
                {
                    urls.Add(video.Url);
                }
            }
            
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
            if (listViewVideos.SelectedItems.Count == 0) return;
            
            var titles = new List<string>();
            foreach (ListViewItem item in listViewVideos.SelectedItems)
            {
                if (item.Tag is VideoInfo video && !string.IsNullOrEmpty(video.Title))
                {
                    titles.Add(video.Title);
                }
            }
            
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
            if (listViewVideos.SelectedItems.Count == 0) return;

            var checkedCount = 0;
            var missingCount = 0;
            var requeuedCount = 0;

            foreach (ListViewItem item in listViewVideos.SelectedItems)
            {
                if (item.Tag is VideoInfo video)
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
            if (listViewVideos.SelectedItems.Count == 0) return;

            var count = listViewVideos.SelectedItems.Count;
            var message = count == 1
                ? $"「{(listViewVideos.SelectedItems[0].Tag as VideoInfo)?.Title}」を削除しますか？"
                : $"{count}件の動画を削除しますか？";

            var result = MessageBox.Show(
                message + "\n\n※ダウンロード済みのファイルは削除されません",
                "確認",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes) return;

            var deletedCount = 0;
            foreach (ListViewItem item in listViewVideos.SelectedItems)
            {
                if (item.Tag is VideoInfo video)
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
            }

            RefreshChannelTree();
            RefreshVideoList();
            UpdateStatusBar($"{deletedCount}件の動画を削除しました");
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
            
            lblDownloadCount.Text = $"DL: {downloading} / 待機: {pending} | 完了: {completed}件 ({totalSizeStr})";
            
            if (downloading > 0)
            {
                // ダウンロード中の全体進捗を計算
                var activeTasks = _downloadManager.GetActiveTasks();
                if (activeTasks.Count > 0)
                {
                    var avgProgress = activeTasks.Average(t => t.Progress);
                    progressBar.Style = ProgressBarStyle.Continuous;
                    progressBar.Value = Math.Min(100, (int)avgProgress);
                }
                else
                {
                    progressBar.Style = ProgressBarStyle.Marquee;
                }
            }
            else
            {
                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.Value = 0;
            }
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
                // Enterで動画リストにフォーカス移動
                if (listViewVideos.Items.Count > 0)
                {
                    listViewVideos.Focus();
                    listViewVideos.Items[0].Selected = true;
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

        #region ListView Sorting

        /// <summary>
        /// ListViewソーターを初期化
        /// </summary>
        private void InitializeListViewSorter()
        {
            // カラムごとのソートタイプを設定
            _videoListSorter.SetColumnType(0, ListViewColumnSorter.SortType.String);      // タイトル
            _videoListSorter.SetColumnType(1, ListViewColumnSorter.SortType.String);      // 状態
            _videoListSorter.SetColumnType(2, ListViewColumnSorter.SortType.Percentage);  // 進捗
            _videoListSorter.SetColumnType(3, ListViewColumnSorter.SortType.FileSize);    // サイズ
            _videoListSorter.SetColumnType(4, ListViewColumnSorter.SortType.Date);        // 投稿日

            // ソーターをListViewに設定
            listViewVideos.ListViewItemSorter = _videoListSorter;

            // ColumnClickイベントを登録
            listViewVideos.ColumnClick += listViewVideos_ColumnClick;
        }

        /// <summary>
        /// カラムクリックでソート
        /// </summary>
        private void listViewVideos_ColumnClick(object? sender, ColumnClickEventArgs e)
        {
            // 同じカラムをクリックした場合は順序を反転
            if (e.Column == _videoListSorter.SortColumn)
            {
                _videoListSorter.Order = _videoListSorter.Order == SortOrder.Ascending
                    ? SortOrder.Descending
                    : SortOrder.Ascending;
            }
            else
            {
                // 新しいカラムの場合は昇順から開始
                _videoListSorter.SortColumn = e.Column;
                _videoListSorter.Order = SortOrder.Ascending;
            }

            // ソートを実行
            listViewVideos.Sort();

            // カラムヘッダーにソート方向を表示
            UpdateColumnHeaders();
        }

        /// <summary>
        /// カラムヘッダーのソート方向表示を更新
        /// </summary>
        private void UpdateColumnHeaders()
        {
            // 元のテキストを保持（ソート記号を除去）
            var baseTexts = new[] { "タイトル", "状態", "進捗", "サイズ", "投稿日" };

            for (int i = 0; i < listViewVideos.Columns.Count && i < baseTexts.Length; i++)
            {
                if (i == _videoListSorter.SortColumn)
                {
                    var arrow = _videoListSorter.Order == SortOrder.Ascending ? " ▲" : " ▼";
                    listViewVideos.Columns[i].Text = baseTexts[i] + arrow;
                }
                else
                {
                    listViewVideos.Columns[i].Text = baseTexts[i];
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
                // 少し待ってからチェック（起動処理を妨げない）
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

        #endregion
    }
}
