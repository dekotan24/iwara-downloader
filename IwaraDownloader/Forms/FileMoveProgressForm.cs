using IwaraDownloader.Models;
using IwaraDownloader.Services;

namespace IwaraDownloader.Forms
{
    /// <summary>
    /// 保存先変更時の既存ファイル移動進捗フォーム
    /// </summary>
    public partial class FileMoveProgressForm : Form
    {
        private readonly List<(VideoInfo Video, string NewPath)> _items;
        private readonly DatabaseService _database;
        private CancellationTokenSource? _cts;
        private bool _running;

        public int MovedCount { get; private set; }
        public int FailedCount { get; private set; }
        public long MovedBytes { get; private set; }

        public FileMoveProgressForm(
            List<(VideoInfo Video, string NewPath)> items,
            DatabaseService database)
        {
            InitializeComponent();
            _items = items;
            _database = database;
            lblCount.Text = $"0 / {_items.Count}";
            progressBar.Maximum = Math.Max(1, _items.Count);
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _running = true;
            _cts = new CancellationTokenSource();
            try
            {
                await RunMoveAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                AppendCurrent("[中止] ユーザー操作で中止されました");
            }
            catch (Exception ex)
            {
                AppendCurrent($"[エラー] {ex.Message}");
            }
            finally
            {
                _running = false;
                _cts?.Dispose();
                _cts = null;
                btnCancel.Text = "閉じる";

                lblTitle.Text = $"完了: 移動 {MovedCount} / 失敗 {FailedCount}";
                lblCount.Text = $"{MovedCount + FailedCount} / {_items.Count}";
            }
        }

        private async Task RunMoveAsync(CancellationToken ct)
        {
            await Task.Run(() =>
            {
                int processed = 0;
                foreach (var (video, newPath) in _items)
                {
                    ct.ThrowIfCancellationRequested();

                    var oldPath = video.LocalFilePath;
                    long fileSize = 0;
                    try { fileSize = new FileInfo(oldPath).Length; } catch { }

                    ReportUi(processed, oldPath, newPath, fileSize);

                    try
                    {
                        var newDir = Path.GetDirectoryName(newPath);
                        if (!string.IsNullOrEmpty(newDir) && !Directory.Exists(newDir))
                        {
                            Directory.CreateDirectory(newDir);
                        }

                        // 既に同名ファイルがある場合は連番付与
                        var actualNewPath = newPath;
                        if (File.Exists(actualNewPath))
                        {
                            actualNewPath = Utils.Helpers.GetUniqueFilePath(newPath);
                        }

                        File.Move(oldPath, actualNewPath);

                        // メタデータ (.json) サイドカーも一緒に移動して置き去りを防ぐ
                        var oldJsonPath = Path.ChangeExtension(oldPath, ".json");
                        if (File.Exists(oldJsonPath))
                        {
                            try
                            {
                                var newJsonPath = Path.ChangeExtension(actualNewPath, ".json");
                                if (File.Exists(newJsonPath))
                                    File.Delete(newJsonPath);
                                File.Move(oldJsonPath, newJsonPath);
                            }
                            catch (Exception jsonEx)
                            {
                                LoggingService.Instance.Warn(
                                    $"メタデータ移動失敗 (動画本体は移動済): {oldJsonPath}: {jsonEx.Message}");
                            }
                        }

                        // DB 更新 (LocalFilePath)
                        video.LocalFilePath = actualNewPath;
                        _database.UpdateVideo(video);

                        MovedCount++;
                        MovedBytes += fileSize;
                    }
                    catch (Exception ex)
                    {
                        FailedCount++;
                        AppendCurrent($"[失敗] {Path.GetFileName(oldPath)}: {ex.Message}");
                        LoggingService.Instance.Warn($"ファイル移動失敗: {oldPath} -> {newPath}: {ex.Message}");
                    }

                    processed++;
                    ReportUi(processed, null, null, 0);
                }
            }, ct);
        }

        private void ReportUi(int processed, string? oldPath, string? newPath, long size)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                try { BeginInvoke((Action)(() => ReportUi(processed, oldPath, newPath, size))); } catch { }
                return;
            }

            progressBar.Value = Math.Min(progressBar.Maximum, processed);
            lblCount.Text = $"{processed} / {_items.Count}";
            lblSize.Text = $"  ({FormatSize(MovedBytes)} 移動済)";

            if (!string.IsNullOrEmpty(oldPath))
            {
                lblCurrent.Text =
                    $"{Path.GetFileName(oldPath)}  ({FormatSize(size)})\r\n" +
                    $"  → {Path.GetDirectoryName(newPath)}";
            }
        }

        private void AppendCurrent(string msg)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                try { BeginInvoke((Action)(() => AppendCurrent(msg))); } catch { }
                return;
            }
            lblCurrent.Text = msg;
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024L * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024:F1} MB";
            return $"{bytes / 1024.0 / 1024 / 1024:F2} GB";
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            if (_running)
            {
                _cts?.Cancel();
                btnCancel.Enabled = false;
                lblTitle.Text = "中止中...";
            }
            else
            {
                Close();
            }
        }

        private void FileMoveProgressForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_running)
            {
                var r = MessageBox.Show(this,
                    "移動処理中です。本当に中止しますか?",
                    "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
                _cts?.Cancel();
            }
        }
    }
}
