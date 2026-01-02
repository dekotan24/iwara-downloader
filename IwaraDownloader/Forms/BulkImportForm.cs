using IwaraDownloader.Models;
using IwaraDownloader.Services;
using System.Text.RegularExpressions;

namespace IwaraDownloader.Forms
{
    /// <summary>
    /// URL一括インポートフォーム
    /// </summary>
    public partial class BulkImportForm : Form
    {
        private readonly DatabaseService _database;
        
        /// <summary>インポートされた動画リスト</summary>
        public List<VideoInfo> ImportedVideos { get; } = new();

        /// <summary>重複としてスキップされた数</summary>
        public int DuplicateCount { get; private set; }

        public BulkImportForm()
        {
            InitializeComponent();
            _database = DatabaseService.Instance;
        }

        private void BulkImportForm_Load(object sender, EventArgs e)
        {
            UpdateStats();
        }

        /// <summary>
        /// クリップボードから貼り付け
        /// </summary>
        private void btnPaste_Click(object sender, EventArgs e)
        {
            if (Clipboard.ContainsText())
            {
                var clipText = Clipboard.GetText();
                if (!string.IsNullOrEmpty(txtUrls.Text) && !txtUrls.Text.EndsWith(Environment.NewLine))
                {
                    txtUrls.AppendText(Environment.NewLine);
                }
                txtUrls.AppendText(clipText);
                UpdateStats();
            }
        }

        /// <summary>
        /// ファイルから読み込み
        /// </summary>
        private void btnLoadFile_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "URLリストファイルを開く",
                Filter = "テキストファイル (*.txt)|*.txt|すべてのファイル (*.*)|*.*"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var content = File.ReadAllText(dialog.FileName);
                    txtUrls.Text = content;
                    UpdateStats();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"ファイルの読み込みに失敗しました:\n{ex.Message}", 
                        "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// クリア
        /// </summary>
        private void btnClear_Click(object sender, EventArgs e)
        {
            txtUrls.Clear();
            UpdateStats();
        }

        /// <summary>
        /// テキスト変更時に統計更新
        /// </summary>
        private void txtUrls_TextChanged(object sender, EventArgs e)
        {
            UpdateStats();
        }

        /// <summary>
        /// 統計を更新
        /// </summary>
        private void UpdateStats()
        {
            var (videoIds, _) = ExtractVideoIds(txtUrls.Text);
            lblStats.Text = $"検出されたURL: {videoIds.Count}件";
        }

        /// <summary>
        /// テキストからVideoIdを抽出
        /// </summary>
        private (List<string> VideoIds, Dictionary<string, string> IdToUrl) ExtractVideoIds(string text)
        {
            var videoIds = new List<string>();
            var idToUrl = new Dictionary<string, string>();
            
            if (string.IsNullOrWhiteSpace(text))
                return (videoIds, idToUrl);

            // iwara.tv/video/{id} パターン
            var regex = new Regex(@"(?:https?://)?(?:www\.)?iwara\.tv/video/([a-zA-Z0-9]+)", RegexOptions.IgnoreCase);
            
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                    continue;

                var match = regex.Match(trimmed);
                if (match.Success)
                {
                    var videoId = match.Groups[1].Value;
                    if (!videoIds.Contains(videoId))
                    {
                        videoIds.Add(videoId);
                        idToUrl[videoId] = trimmed;
                    }
                }
                else if (trimmed.Length >= 8 && trimmed.Length <= 20 && 
                         Regex.IsMatch(trimmed, @"^[a-zA-Z0-9]+$"))
                {
                    // VideoIdのみの場合
                    if (!videoIds.Contains(trimmed))
                    {
                        videoIds.Add(trimmed);
                        idToUrl[trimmed] = $"https://www.iwara.tv/video/{trimmed}";
                    }
                }
            }

            return (videoIds, idToUrl);
        }

        /// <summary>
        /// インポート実行
        /// </summary>
        private async void btnImport_Click(object sender, EventArgs e)
        {
            var (videoIds, idToUrl) = ExtractVideoIds(txtUrls.Text);
            
            if (videoIds.Count == 0)
            {
                MessageBox.Show("有効なURLが見つかりませんでした。", "エラー", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnImport.Enabled = false;
            btnImport.Text = "処理中...";
            progressBar.Visible = true;
            progressBar.Value = 0;
            progressBar.Maximum = videoIds.Count;

            ImportedVideos.Clear();
            DuplicateCount = 0;

            try
            {
                // 既存のVideoIdを一括取得
                var existingIds = _database.GetExistingVideoIds(videoIds);

                await Task.Run(() =>
                {
                    foreach (var videoId in videoIds)
                    {
                        // 重複チェック
                        if (existingIds.Contains(videoId))
                        {
                            DuplicateCount++;
                        }
                        else
                        {
                            var video = new VideoInfo
                            {
                                VideoId = videoId,
                                Title = $"[未取得] {videoId}",
                                Url = idToUrl[videoId],
                                Status = DownloadStatus.Pending,
                                CreatedAt = DateTime.Now
                            };
                            ImportedVideos.Add(video);
                        }

                        // UI更新
                        this.Invoke(() =>
                        {
                            progressBar.Value = Math.Min(progressBar.Value + 1, progressBar.Maximum);
                        });
                    }
                });

                // 結果表示
                var message = $"処理完了\n\n" +
                    $"・追加対象: {ImportedVideos.Count}件\n" +
                    $"・重複スキップ: {DuplicateCount}件";

                if (ImportedVideos.Count > 0)
                {
                    var result = MessageBox.Show(
                        message + "\n\nダウンロードキューに追加しますか？",
                        "インポート結果",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        // バッチ追加
                        var addedCount = _database.AddVideosBatch(ImportedVideos);
                        MessageBox.Show($"{addedCount}件をキューに追加しました。", 
                            "完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                    }
                }
                else
                {
                    MessageBox.Show(message, "インポート結果", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"インポート中にエラーが発生しました:\n{ex.Message}", 
                    "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnImport.Enabled = true;
                btnImport.Text = "インポート";
                progressBar.Visible = false;
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
