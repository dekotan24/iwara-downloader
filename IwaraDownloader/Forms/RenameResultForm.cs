using IwaraDownloader.Models;
using IwaraDownloader.Services;
using IwaraDownloader.Utils;

namespace IwaraDownloader.Forms
{
    /// <summary>
    /// リネーム結果フォーム
    /// </summary>
    public partial class RenameResultForm : Form
    {
        private readonly DatabaseService _database;
        private readonly List<RenameItem> _items;
        private readonly string _template;

        public RenameResultForm(List<RenameItem> items, string template)
        {
            InitializeComponent();
            _database = DatabaseService.Instance;
            _items = items;
            _template = template;
        }

        private void RenameResultForm_Load(object sender, EventArgs e)
        {
            PopulateListView();
            UpdateStatusLabel();
        }

        /// <summary>
        /// ListViewにデータを表示
        /// </summary>
        private void PopulateListView()
        {
            listView.Items.Clear();

            foreach (var item in _items)
            {
                var lvi = new ListViewItem(item.Video.Title);
                lvi.SubItems.Add(Path.GetFileName(item.OriginalPath));
                lvi.SubItems.Add(Path.GetFileName(item.NewPath));
                lvi.SubItems.Add(item.GetStatusText());
                lvi.Tag = item;

                // 状態に応じて色分け
                switch (item.Status)
                {
                    case RenameStatus.Success:
                        lvi.ForeColor = Color.Green;
                        break;
                    case RenameStatus.Skipped:
                        lvi.ForeColor = Color.Gray;
                        break;
                    case RenameStatus.Conflict:
                        lvi.ForeColor = Color.Orange;
                        lvi.BackColor = Color.LightYellow;
                        break;
                    case RenameStatus.FileNotFound:
                        lvi.ForeColor = Color.DarkRed;
                        break;
                    case RenameStatus.Error:
                        lvi.ForeColor = Color.Red;
                        break;
                }

                listView.Items.Add(lvi);
            }

            // 列幅を自動調整
            listView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
        }

        /// <summary>
        /// ステータスラベルを更新
        /// </summary>
        private void UpdateStatusLabel()
        {
            var success = _items.Count(i => i.Status == RenameStatus.Success);
            var skipped = _items.Count(i => i.Status == RenameStatus.Skipped);
            var conflict = _items.Count(i => i.Status == RenameStatus.Conflict);
            var notFound = _items.Count(i => i.Status == RenameStatus.FileNotFound);
            var error = _items.Count(i => i.Status == RenameStatus.Error);

            lblStatus.Text = $"成功: {success}  スキップ: {skipped}  重複: {conflict}  ファイル不在: {notFound}  エラー: {error}";
        }

        /// <summary>
        /// 選択中のアイテムを取得
        /// </summary>
        private RenameItem? GetSelectedItem()
        {
            if (listView.SelectedItems.Count == 0)
                return null;
            return listView.SelectedItems[0].Tag as RenameItem;
        }

        /// <summary>
        /// 選択中の重複アイテムをすべて取得
        /// </summary>
        private List<RenameItem> GetSelectedConflictItems()
        {
            var items = new List<RenameItem>();
            foreach (ListViewItem lvi in listView.SelectedItems)
            {
                if (lvi.Tag is RenameItem item && item.Status == RenameStatus.Conflict)
                {
                    items.Add(item);
                }
            }
            return items;
        }

        private void listView_SelectedIndexChanged(object sender, EventArgs e)
        {
            var item = GetSelectedItem();
            var hasSelection = item != null;
            var isConflict = item?.Status == RenameStatus.Conflict;
            var hasConflictSelection = GetSelectedConflictItems().Count > 0;

            btnPlayOriginal.Enabled = hasSelection && File.Exists(item?.OriginalPath);
            btnPlayConflict.Enabled = isConflict && File.Exists(item?.ConflictingPath);
            btnCompare.Enabled = isConflict && File.Exists(item?.OriginalPath) && File.Exists(item?.ConflictingPath);
            btnOverwrite.Enabled = hasConflictSelection;
            btnAddNumber.Enabled = hasConflictSelection;
            btnSkip.Enabled = hasConflictSelection;
        }

        private void listView_DoubleClick(object sender, EventArgs e)
        {
            // ダブルクリックで元ファイルを再生
            var item = GetSelectedItem();
            if (item != null && File.Exists(item.OriginalPath))
            {
                Helpers.OpenFile(item.OriginalPath);
            }
        }

        private void btnPlayOriginal_Click(object sender, EventArgs e)
        {
            var item = GetSelectedItem();
            if (item != null && File.Exists(item.OriginalPath))
            {
                Helpers.OpenFile(item.OriginalPath);
            }
        }

        private void btnPlayConflict_Click(object sender, EventArgs e)
        {
            var item = GetSelectedItem();
            if (item != null && !string.IsNullOrEmpty(item.ConflictingPath) && File.Exists(item.ConflictingPath))
            {
                Helpers.OpenFile(item.ConflictingPath);
            }
        }

        private void btnCompare_Click(object sender, EventArgs e)
        {
            var item = GetSelectedItem();
            if (item == null) return;

            // 両方のファイルを開いて比較
            if (File.Exists(item.OriginalPath))
            {
                Helpers.OpenFile(item.OriginalPath);
            }
            if (!string.IsNullOrEmpty(item.ConflictingPath) && File.Exists(item.ConflictingPath))
            {
                Helpers.OpenFile(item.ConflictingPath);
            }
        }

        private void btnOverwrite_Click(object sender, EventArgs e)
        {
            var conflictItems = GetSelectedConflictItems();
            if (conflictItems.Count == 0) return;

            var result = MessageBox.Show(
                $"{conflictItems.Count}件のファイルを上書きします。\n既存のファイルは削除されます。続行しますか？",
                "上書き確認",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes) return;

            foreach (var item in conflictItems)
            {
                try
                {
                    // 既存ファイルを削除
                    if (File.Exists(item.ConflictingPath))
                    {
                        File.Delete(item.ConflictingPath);
                    }

                    // リネーム実行
                    File.Move(item.OriginalPath, item.NewPath);

                    // メタデータファイルも処理
                    var originalJsonPath = Path.ChangeExtension(item.OriginalPath, ".json");
                    if (File.Exists(originalJsonPath))
                    {
                        var newJsonPath = Path.ChangeExtension(item.NewPath, ".json");
                        if (File.Exists(newJsonPath))
                            File.Delete(newJsonPath);
                        File.Move(originalJsonPath, newJsonPath);
                    }

                    // DB更新
                    item.Video.LocalFilePath = item.NewPath;
                    _database.UpdateVideo(item.Video);

                    item.Status = RenameStatus.Success;
                    item.ConflictingPath = null;
                }
                catch (Exception ex)
                {
                    item.Status = RenameStatus.Error;
                    item.ErrorMessage = ex.Message;
                }
            }

            PopulateListView();
            UpdateStatusLabel();
        }

        private void btnAddNumber_Click(object sender, EventArgs e)
        {
            var conflictItems = GetSelectedConflictItems();
            if (conflictItems.Count == 0) return;

            foreach (var item in conflictItems)
            {
                try
                {
                    // 番号付きのパスを取得
                    var uniquePath = Helpers.GetUniqueFilePath(item.NewPath);

                    // リネーム実行
                    File.Move(item.OriginalPath, uniquePath);

                    // メタデータファイルも処理
                    var originalJsonPath = Path.ChangeExtension(item.OriginalPath, ".json");
                    if (File.Exists(originalJsonPath))
                    {
                        var newJsonPath = Path.ChangeExtension(uniquePath, ".json");
                        File.Move(originalJsonPath, newJsonPath);
                    }

                    // DB更新
                    item.Video.LocalFilePath = uniquePath;
                    _database.UpdateVideo(item.Video);

                    item.NewPath = uniquePath;
                    item.Status = RenameStatus.Success;
                    item.ConflictingPath = null;
                }
                catch (Exception ex)
                {
                    item.Status = RenameStatus.Error;
                    item.ErrorMessage = ex.Message;
                }
            }

            PopulateListView();
            UpdateStatusLabel();
        }

        private void btnSkip_Click(object sender, EventArgs e)
        {
            var conflictItems = GetSelectedConflictItems();
            if (conflictItems.Count == 0) return;

            foreach (var item in conflictItems)
            {
                item.Status = RenameStatus.Skipped;
                item.ConflictingPath = null;
            }

            PopulateListView();
            UpdateStatusLabel();
        }

        private void btnSelectAllConflicts_Click(object sender, EventArgs e)
        {
            listView.SelectedItems.Clear();
            foreach (ListViewItem lvi in listView.Items)
            {
                if (lvi.Tag is RenameItem item && item.Status == RenameStatus.Conflict)
                {
                    lvi.Selected = true;
                }
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void contextMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var item = GetSelectedItem();
            var hasSelection = item != null;
            var isConflict = item?.Status == RenameStatus.Conflict;

            menuPlayOriginal.Enabled = hasSelection && File.Exists(item?.OriginalPath);
            menuPlayConflict.Enabled = isConflict && File.Exists(item?.ConflictingPath);
            menuCompare.Enabled = isConflict && File.Exists(item?.OriginalPath) && File.Exists(item?.ConflictingPath);
            menuOpenFolder.Enabled = hasSelection && File.Exists(item?.OriginalPath);
            menuOverwrite.Enabled = isConflict;
            menuAddNumber.Enabled = isConflict;
            menuSkip.Enabled = isConflict;
        }

        private void menuPlayOriginal_Click(object sender, EventArgs e) => btnPlayOriginal_Click(sender, e);
        private void menuPlayConflict_Click(object sender, EventArgs e) => btnPlayConflict_Click(sender, e);
        private void menuCompare_Click(object sender, EventArgs e) => btnCompare_Click(sender, e);

        private void menuOpenFolder_Click(object sender, EventArgs e)
        {
            var item = GetSelectedItem();
            if (item != null && File.Exists(item.OriginalPath))
            {
                Helpers.OpenFolderAndSelectFile(item.OriginalPath);
            }
        }

        private void menuOverwrite_Click(object sender, EventArgs e) => btnOverwrite_Click(sender, e);
        private void menuAddNumber_Click(object sender, EventArgs e) => btnAddNumber_Click(sender, e);
        private void menuSkip_Click(object sender, EventArgs e) => btnSkip_Click(sender, e);
    }

    /// <summary>
    /// リネーム項目
    /// </summary>
    public class RenameItem
    {
        public VideoInfo Video { get; set; } = null!;
        public string OriginalPath { get; set; } = string.Empty;
        public string NewPath { get; set; } = string.Empty;
        public RenameStatus Status { get; set; }
        public string? ConflictingPath { get; set; }
        public string? ErrorMessage { get; set; }

        public string GetStatusText()
        {
            return Status switch
            {
                RenameStatus.Success => "成功",
                RenameStatus.Skipped => "スキップ（同名）",
                RenameStatus.Conflict => $"重複: {Path.GetFileName(ConflictingPath)}",
                RenameStatus.FileNotFound => "ファイル不在",
                RenameStatus.Error => $"エラー: {ErrorMessage}",
                RenameStatus.Pending => "処理待ち",
                _ => "不明"
            };
        }
    }

    /// <summary>
    /// リネーム状態
    /// </summary>
    public enum RenameStatus
    {
        Pending,
        Success,
        Skipped,
        Conflict,
        FileNotFound,
        Error
    }
}
