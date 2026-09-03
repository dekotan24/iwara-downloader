using IwaraDownloader.Models;
using IwaraDownloader.Services;
using IwaraDownloader.Utils;

namespace IwaraDownloader.Forms
{
    /// <summary>
    /// DBが保持するローカルファイルの欠損を一覧化し、再マップまたは再ダウンロードを行う画面。
    /// スキャンは読み取り専用で、操作時に対象行をDBから再取得してから変更する。
    /// </summary>
    public partial class LocalFileIntegrityForm : Form
    {
        private readonly DatabaseService _database;
        private readonly DownloadManager _downloadManager;
        private readonly List<LocalFileIntegrityService.Issue> _issues = new();
        private CancellationTokenSource? _scanCts;
        private bool _busy;
        private bool _mapInProgress;

        public LocalFileIntegrityForm(DatabaseService database, DownloadManager downloadManager)
        {
            _database = database;
            _downloadManager = downloadManager;
            InitializeComponent();
            Utils.Localizer.Apply(this);
        }

        private async void LocalFileIntegrityForm_Load(object? sender, EventArgs e)
            => await ScanAsync();

        private async void btnScan_Click(object? sender, EventArgs e)
            => await ScanAsync();

        private async Task ScanAsync()
        {
            if (_busy) return;

            _busy = true;
            UpdateActionState();
            lblStatus.Text = L.T("LocalFileIntegrityForm_D001");
            _scanCts?.Dispose();
            _scanCts = new CancellationTokenSource();
            var ct = _scanCts.Token;

            try
            {
                var result = await Task.Run(
                    () =>
                    {
                        var videos = _database.GetAllVideos();
                        ct.ThrowIfCancellationRequested();
                        return LocalFileIntegrityService.Scan(videos, cancellationToken: ct);
                    }, ct);

                if (IsDisposed) return;
                _issues.Clear();
                _issues.AddRange(result.Issues);
                PopulateGrid();
                lblStatus.Text = BuildScanStatus(result);

                // 未接続の外付けドライブを「全部消えた」と誤認したまま一括再ダウンロードを
                // 押されるのが最悪ケースなので、無視できない警告として明示する。
                if (result.UnreachableRoots.Count > 0)
                {
                    MessageBox.Show(this,
                        L.T("LocalFileIntegrityForm_D016",
                            string.Join(", ", result.UnreachableRoots),
                            result.SkippedOnUnreachableRoots),
                        L.T("LocalFileIntegrityForm_D006"),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (OperationCanceledException)
            {
                if (!IsDisposed) lblStatus.Text = L.T("LocalFileIntegrityForm_D003");
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                {
                    lblStatus.Text = L.T("LocalFileIntegrityForm_D004", ex.Message);
                    MessageBox.Show(this, L.T("LocalFileIntegrityForm_D005", ex.Message),
                        L.T("LocalFileIntegrityForm_D006"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                _busy = false;
                UpdateActionState();
            }
        }

        private void PopulateGrid()
        {
            // 4万件規模でも1行ごとの再描画・レイアウトを発生させない。
            // Rows.AddRangeでまとめて差し替え、一覧生成中のUI負荷を抑える。
            dgvIssues.SuspendLayout();
            try
            {
                dgvIssues.Rows.Clear();
                if (_issues.Count == 0) return;

                var rows = new List<DataGridViewRow>(_issues.Count);
                foreach (var issue in _issues)
                {
                    var video = issue.Video;
                    var row = new DataGridViewRow();
                    row.CreateCells(
                        dgvIssues,
                        video.Title,
                        video.VideoId,
                        GetStatusText(video.Status),
                        string.IsNullOrWhiteSpace(video.LocalFilePath) ? "" : video.LocalFilePath,
                        GetIssueText(issue.Kind));
                    row.Tag = issue;
                    row.Cells[colDbPath.Index].ToolTipText = video.LocalFilePath;
                    rows.Add(row);
                }

                dgvIssues.Rows.AddRange(rows.ToArray());
            }
            finally
            {
                dgvIssues.ResumeLayout();
            }
        }

        private static string GetStatusText(DownloadStatus status) => status switch
        {
            DownloadStatus.Pending => L.T("LocalFileIntegrityForm_StatusPending"),
            DownloadStatus.Downloading => L.T("LocalFileIntegrityForm_StatusDownloading"),
            DownloadStatus.WritingTags => L.T("LocalFileIntegrityForm_StatusWritingTags"),
            DownloadStatus.Completed => L.T("LocalFileIntegrityForm_StatusCompleted"),
            DownloadStatus.Failed => L.T("LocalFileIntegrityForm_StatusFailed"),
            DownloadStatus.Skipped => L.T("LocalFileIntegrityForm_StatusSkipped"),
            DownloadStatus.Paused => L.T("LocalFileIntegrityForm_StatusPaused"),
            _ => status.ToString(),
        };

        private static string GetIssueText(LocalFileIntegrityService.IssueKind kind)
            => kind == LocalFileIntegrityService.IssueKind.MissingPath
                ? L.T("LocalFileIntegrityForm_IssueMissingPath")
                : L.T("LocalFileIntegrityForm_IssueMissingFile");

        private List<LocalFileIntegrityService.Issue> GetSelectedIssues()
            => dgvIssues.SelectedRows
                .Cast<DataGridViewRow>()
                .Select(row => row.Tag as LocalFileIntegrityService.Issue)
                .Where(issue => issue != null)
                .Select(issue => issue!)
                .GroupBy(issue => issue.Video.Id)
                .Select(group => group.First())
                .ToList();

        private void dgvIssues_SelectionChanged(object? sender, EventArgs e)
            => UpdateActionState();

        private void UpdateActionState()
        {
            if (IsDisposed) return;
            var selectedCount = GetSelectedIssues().Count;
            btnMap.Enabled = !_busy && selectedCount == 1;
            btnRedownload.Enabled = !_busy && selectedCount > 0;
            btnScan.Enabled = !_busy;
            btnClose.Enabled = !_busy;
            dgvIssues.Enabled = !_busy;
        }

        private async void btnMap_Click(object? sender, EventArgs e)
        {
            var issue = GetSelectedIssues().SingleOrDefault();
            if (issue == null)
            {
                MessageBox.Show(this, L.T("LocalFileIntegrityForm_D007"),
                    L.T("LocalFileIntegrityForm_D006"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var video = _database.GetVideoById(issue.Video.Id);
            if (video == null)
            {
                await ScanAsync();
                return;
            }

            if (!LocalFileIntegrityService.IsIssue(video))
            {
                lblStatus.Text = L.T("LocalFileIntegrityForm_D008");
                await ScanAsync();
                return;
            }

            // ダウンロード中のタスクとマップを同時に進めると、完了処理が選択した
            // ローカルパスを上書きする可能性がある。先に状態を確定させるため、
            // キューに残っている場合はこの操作を行わず、一覧の再確認を促す。
            if (_downloadManager.GetTask(video.VideoId) != null)
            {
                lblStatus.Text = L.T("LocalFileIntegrityForm_D015");
                await ScanAsync();
                return;
            }

            _busy = true;
            _mapInProgress = true;
            UpdateActionState();
            try
            {
                var result = await LocalFileMapHelper.MapAsync(
                    this, video, _downloadManager.IwaraApi, _database, _downloadManager,
                    allowForeignTag: false,
                    useAtomicExistingUpdate: true);
                if (result == LocalFileMapHelper.MapResult.Mapped)
                {
                    // 全件再スキャンは数万件の File.Exists を伴うため、解決した行だけ取り除く。
                    RemoveResolvedRows(new[] { issue.Video.Id });
                    lblStatus.Text = L.T("LocalFileIntegrityForm_D009", video.Title);
                }
            }
            finally
            {
                _mapInProgress = false;
                _busy = false;
                UpdateActionState();
            }
        }

        /// <summary>解決済みの動画IDを一覧から取り除く。</summary>
        private void RemoveResolvedRows(IReadOnlyCollection<int> videoIds)
        {
            if (IsDisposed || videoIds.Count == 0) return;

            var ids = videoIds.ToHashSet();
            _issues.RemoveAll(issue => ids.Contains(issue.Video.Id));
            for (int i = dgvIssues.Rows.Count - 1; i >= 0; i--)
            {
                if (dgvIssues.Rows[i].Tag is LocalFileIntegrityService.Issue issue
                    && ids.Contains(issue.Video.Id))
                {
                    dgvIssues.Rows.RemoveAt(i);
                }
            }
        }

        private async void btnRedownload_Click(object? sender, EventArgs e)
        {
            var selected = GetSelectedIssues();
            if (selected.Count == 0) return;

            var confirmed = MessageBox.Show(
                this,
                L.T("LocalFileIntegrityForm_D010", selected.Count),
                L.T("LocalFileIntegrityForm_D011"),
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (confirmed != DialogResult.OK) return;

            _busy = true;
            UpdateActionState();
            var queued = 0;
            var skipped = 0;
            var errors = new List<string>();
            var resolvedIds = new List<int>();
            try
            {
                foreach (var issue in selected)
                {
                    try
                    {
                        var video = _database.GetVideoById(issue.Video.Id);
                        if (video == null || !LocalFileIntegrityService.IsIssue(video))
                        {
                            skipped++;
                            continue;
                        }

                        // 実行中/待機中のタスクがある場合は、既存タスクを壊さず二重投入もしない。
                        if (_downloadManager.GetTask(video.VideoId) != null)
                        {
                            skipped++;
                            continue;
                        }

                        // ここでは既に欠損している実体を対象にするため、ファイル削除は行わない。
                        // DBの状態だけを未DLへ戻し、EnqueueDownloadの既存経路へ渡す。
                        // EnqueueDownload自身がDBを更新するため、先に全カラムUPDATEを行わない。
                        video.LocalFilePath = string.Empty;
                        video.FileSize = 0;
                        video.DownloadedAt = null;
                        video.Status = DownloadStatus.Pending;
                        video.RetryCount = 0;
                        video.LastErrorMessage = null;

                        var user = video.SubscribedUserId.HasValue
                            ? _database.GetSubscribedUserById(video.SubscribedUserId.Value)
                            : null;
                        var task = _downloadManager.EnqueueDownload(video, video.SubscribedUserId.HasValue, user);
                        if (task.Status == DownloadStatus.Skipped)
                        {
                            skipped++;
                        }
                        else
                        {
                            queued++;
                            resolvedIds.Add(video.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        skipped++;
                        errors.Add($"{issue.Video.Title}: {ex.Message}");
                    }
                }

                RemoveResolvedRows(resolvedIds);
                lblStatus.Text = skipped == 0 && errors.Count == 0
                    ? L.T("LocalFileIntegrityForm_D012", queued)
                    : L.T("LocalFileIntegrityForm_D013", queued, skipped);
                if (errors.Count > 0)
                {
                    MessageBox.Show(this, L.T("LocalFileIntegrityForm_D014", string.Join(Environment.NewLine, errors)),
                        L.T("LocalFileIntegrityForm_D006"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                _busy = false;
                UpdateActionState();
            }
        }

        private void btnClose_Click(object? sender, EventArgs e) => Close();

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_mapInProgress)
            {
                // API取得中にフォームだけ破棄すると、await後のUI更新が破棄済みコントロールへ
                // 到達するため、マップ処理が終わるまで閉じる操作を保留する。
                // 無反応に見えないよう、保留していることを伝える。
                e.Cancel = true;
                lblStatus.Text = L.T("LocalFileIntegrityForm_D017");
                return;
            }

            _scanCts?.Cancel();
            base.OnFormClosing(e);
        }

        /// <summary>打ち切りの有無を含めてスキャン結果の一行サマリを組み立てる。</summary>
        private static string BuildScanStatus(LocalFileIntegrityService.ScanResult result)
            => result.Truncated
                ? L.T("LocalFileIntegrityForm_D018", result.Issues.Count, result.TotalIssueCount)
                : L.T("LocalFileIntegrityForm_D002", result.Issues.Count);
    }
}
