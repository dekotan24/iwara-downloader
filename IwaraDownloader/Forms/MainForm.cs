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
        private const string NODE_ALL_DOWNLOADS = "__ALL_DOWNLOADS__";
        private const string NODE_FAILED_VIDEOS = "__FAILED_VIDEOS__";
        private const string NODE_SINGLE_VIDEOS = "__SINGLE_VIDEOS__";

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

            // ダウンロードマネージャー開始
            _downloadManager.Start();
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
            await AddUserAsync(input);
        }

        private async void btnAddVideo_Click(object sender, EventArgs e)
        {
            var url = ShowInputDialog("動画追加", "動画URLを入力:");
            if (string.IsNullOrEmpty(url)) return;
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

            if (Helpers.IsVideoUrl(input))
            {
                await AddVideoAsync(input);
            }
            else if (Helpers.IsUserProfileUrl(input))
            {
                await AddUserAsync(input);
            }
            else
            {
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

            // 「全てのダウンロード」ノード
            var allDownloadsNode = new TreeNode("📥 ダウンロードキュー")
            {
                Tag = NODE_ALL_DOWNLOADS,
                NodeFont = new Font(treeViewChannels.Font, FontStyle.Bold)
            };
            
            var pendingVideos = _database.GetVideosByStatus(DownloadStatus.Pending);
            var downloadingVideos = _database.GetVideosByStatus(DownloadStatus.Downloading);
            var failedVideos = _database.GetVideosByStatus(DownloadStatus.Failed);
            var pendingCount = pendingVideos.Count;
            var downloadingCount = downloadingVideos.Count;
            var failedCount = failedVideos.Count;
            if (pendingCount + downloadingCount > 0)
            {
                allDownloadsNode.Text += $" ({downloadingCount}DL中/{pendingCount}待機)";
            }
            treeViewChannels.Nodes.Add(allDownloadsNode);

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
            var singleVideos = _database.GetAllVideos().Where(v => !v.SubscribedUserId.HasValue).ToList();
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
                var videos = _database.GetVideosBySubscribedUser(user.Id);
                var completedCount = videos.Count(v => v.Status == DownloadStatus.Completed);
                var chDownloadingVideos = videos.Count(v => v.Status == DownloadStatus.Downloading);
                var chPendingVideos = videos.Count(v => v.Status == DownloadStatus.Pending);
                
                var statusText = "";
                if (chDownloadingVideos > 0)
                    statusText = $" 🔄{chDownloadingVideos}";
                else if (chPendingVideos > 0)
                    statusText = $" ⏳{chPendingVideos}";
                
                var nodeText = $"{(user.IsEnabled ? "📺" : "⬜")} {user.Username} [{completedCount}/{videos.Count}]{statusText}";
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
                    NODE_ALL_DOWNLOADS => "ダウンロード中/待機中",
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

        /// <summary>
        /// 右クリック時にノードを選択状態にする
        /// </summary>
        private void treeViewChannels_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            // 右クリック時はクリックしたノードを選択状態にする
            if (e.Button == MouseButtons.Right && e.Node != null)
            {
                treeViewChannels.SelectedNode = e.Node;
            }
        }

        #endregion

        #region Video List

        /// <summary>
        /// 動画リストを更新
        /// </summary>
        private void RefreshVideoList()
        {
            listViewVideos.BeginUpdate();
            listViewVideos.Items.Clear();

            List<VideoInfo> videos;
            var selectedNode = treeViewChannels.SelectedNode;

            if (selectedNode?.Tag is SubscribedUser user)
            {
                // チャンネルの動画
                videos = _database.GetVideosBySubscribedUser(user.Id).OrderByDescending(v => v.CreatedAt).ToList();
            }
            else if (selectedNode?.Tag is string tag)
            {
                if (tag == NODE_ALL_DOWNLOADS)
                {
                    // ダウンロード中/待機中（DBから取得）
                    var downloadingList = _database.GetVideosByStatus(DownloadStatus.Downloading);
                    var pendingList = _database.GetVideosByStatus(DownloadStatus.Pending);
                    videos = downloadingList.Concat(pendingList).ToList();
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

            foreach (var video in videos)
            {
                var item = CreateVideoListItem(video);
                listViewVideos.Items.Add(item);
            }

            listViewVideos.EndUpdate();
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
                progressText = task.Progress > 0 ? $"{task.Progress:F0}%" : "DL中...";
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

            return item;
        }

        private void UpdateVideoItem(DownloadTask task)
        {
            foreach (ListViewItem item in listViewVideos.Items)
            {
                if (item.Tag is VideoInfo video && video.VideoId == task.Video.VideoId)
                {
                    // 進捗更新
                    var progressText = task.Status == DownloadStatus.Downloading
                        ? (task.Progress > 0 ? $"{task.Progress:F0}%" : "DL中...")
                        : (task.Status == DownloadStatus.Completed ? "100%" : 
                           task.Status == DownloadStatus.Pending ? "待機" : "-");
                    
                    item.SubItems[1].Text = GetStatusText(task.Status);
                    item.SubItems[2].Text = progressText;
                    item.SubItems[0].Text = $"{GetStatusIcon(task.Status)} {task.Video.Title}";
                    
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
        }

        #endregion

        #region Channel Context Menu

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
            if (treeViewChannels.SelectedNode?.Tag is SubscribedUser user)
            {
                var videos = _database.GetVideosBySubscribedUser(user.Id)
                    .Where(v => v.Status != DownloadStatus.Completed && v.Status != DownloadStatus.Downloading && v.Status != DownloadStatus.Pending)
                    .ToList();

                foreach (var video in videos)
                {
                    _downloadManager.EnqueueDownload(video, true, user);
                }

                RefreshChannelTree();
                RefreshVideoList();
                UpdateStatusBar($"{videos.Count} 件のダウンロードをキューに追加しました");
            }
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
            lblDownloadCount.Text = $"DL: {downloading} / 待機: {pending}";
            
            if (downloading > 0)
            {
                progressBar.Style = ProgressBarStyle.Marquee;
            }
            else
            {
                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.Value = 0;
            }
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
    }
}
