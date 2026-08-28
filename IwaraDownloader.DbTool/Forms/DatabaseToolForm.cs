using System.Data;
using IwaraDownloader.Services;
using IwaraDownloader.Utils;

namespace IwaraDownloader.Forms
{
    /// <summary>
    /// DB操作ツール(上級者向け): SQLエディタ + テーブルブラウザ。
    /// IwaraDownloader本体とは別プロセスの独立ツール(downloader終了中に使う前提、Program.cs参照)。
    /// DownloadManagerを共有できないため、本体側にあった「編集/削除前に該当タスクを自動キャンセルする」
    /// 安全策は無い。その代わり起動時に本体プロセスの有無を確認して警告する設計にしている。
    /// 既定は読み取り専用モードで開く。書き込みモードへの切替は強制バックアップ + 確認ダイアログを必須にする。
    /// SELECT/UPDATE等の判定は行わず、読み取り専用接続(Mode=ReadOnly)かどうかでSQLite自身に書き込みを拒否させる
    /// (DatabaseService.ExecuteAdminSql 参照)。
    /// </summary>
    public partial class DatabaseToolForm : Form
    {
        private readonly DatabaseService _database;
        private bool _writeModeEnabled;
        private string? _writeModeBackupFileName;
        private DataTable? _browserTable;
        private List<string> _browserPkColumns = new();
        private string? _browserTableName;

        public DatabaseToolForm()
        {
            InitializeComponent();
            Localizer.Apply(this);
            _database = DatabaseService.Instance;
        }

        private void DatabaseToolForm_Load(object sender, EventArgs e)
        {
            var confirmed = MessageBox.Show(
                L.T("DatabaseToolForm_D010"),
                L.T("DatabaseToolForm_D011"),
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (confirmed != DialogResult.OK)
            {
                BeginInvoke(new Action(Close));
                return;
            }

            UpdateBanner();
            RefreshIntegrityWarning();
            LoadTableNames();
        }

        #region 書き込みモード切替

        private void UpdateBanner()
        {
            if (_writeModeEnabled)
            {
                lblBanner.Text = L.T("DatabaseToolForm_D002", _writeModeBackupFileName ?? "");
                lblBanner.ForeColor = Color.White;
                lblBanner.BackColor = Color.OrangeRed;
                btnToggleWriteMode.Text = L.T("DatabaseToolForm_D004");
            }
            else
            {
                lblBanner.Text = L.T("DatabaseToolForm_D001");
                lblBanner.ForeColor = SystemColors.ControlText;
                lblBanner.BackColor = SystemColors.Control;
                btnToggleWriteMode.Text = L.T("DatabaseToolForm_D003");
            }

            gridBrowser.ReadOnly = !_writeModeEnabled;
            btnDeleteSelectedRow.Enabled = _writeModeEnabled;
            UpdateBrowserColumnEditability();
        }

        private void btnToggleWriteMode_Click(object sender, EventArgs e)
        {
            if (_writeModeEnabled)
            {
                // 読み取り専用へ戻すのは常に安全な方向なので確認ダイアログは出さない。
                _writeModeEnabled = false;
                UpdateBanner();
                return;
            }

            var confirmed = MessageBox.Show(L.T("DatabaseToolForm_D005"), L.T("DatabaseToolForm_D007"),
                MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (confirmed != DialogResult.OK) return;

            try
            {
                var backupPath = _database.CreateForcedBackup();
                MessageBox.Show(L.T("DatabaseToolForm_D008", backupPath), L.T("DatabaseToolForm_D007"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                _writeModeBackupFileName = Path.GetFileName(backupPath);
                _writeModeEnabled = true;
                UpdateBanner();
            }
            catch (Exception ex)
            {
                MessageBox.Show(L.T("DatabaseToolForm_D009", ex.Message), L.T("DatabaseToolForm_D007"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region 共通

        private void RefreshIntegrityWarning()
        {
            try
            {
                var violations = _database.CheckAdminXorViolationCount();
                lblIntegrityWarning.Visible = violations > 0;
                if (violations > 0)
                    lblIntegrityWarning.Text = L.T("DatabaseToolForm_D017", violations);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Warn($"DB操作ツール: 整合性チェック失敗: {ex.Message}", ex);
            }
        }

        #endregion

        #region SQLエディタ

        private void txtSql_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5)
            {
                e.Handled = true;
                ExecuteSql();
            }
        }

        private void btnExecuteSql_Click(object sender, EventArgs e) => ExecuteSql();

        private void chkApplyRowLimit_CheckedChanged(object sender, EventArgs e)
        {
            numMaxRows.Enabled = chkApplyRowLimit.Checked;
        }

        private void ExecuteSql()
        {
            var sql = txtSql.Text.Trim();
            if (string.IsNullOrEmpty(sql)) return;

            try
            {
                // maxRowsは大テーブルの全件フェッチによるUIフリーズ防止の安全キャップに過ぎない。
                // SQL側にLIMIT句を書いた場合、その値がこのキャップより小さければそのまま反映される
                // (キャップより大きいLIMITを指定した場合のみキャップ側で打ち切られる)。
                // 「上限を適用」のチェックを外した場合はキャップ自体を外す(=SQLにLIMITを書かない限り全件返す)。
                // 上級者向けツールであることを踏まえ、大テーブルのフリーズは理解の上での選択として許容する。
                var maxRows = chkApplyRowLimit.Checked ? (int)numMaxRows.Value : int.MaxValue;
                var (result, affected) = _database.ExecuteAdminSql(sql, writable: _writeModeEnabled, maxRows: maxRows);

                if (result != null)
                {
                    gridSqlResult.DataSource = result;
                    var truncated = result.ExtendedProperties.Contains("Truncated");
                    lblSqlStatus.ForeColor = SystemColors.ControlText;
                    lblSqlStatus.Text = truncated
                        ? L.T("DatabaseToolForm_D013", result.Rows.Count)
                        : L.T("DatabaseToolForm_D012", result.Rows.Count);
                }
                else
                {
                    gridSqlResult.DataSource = null;
                    lblSqlStatus.ForeColor = SystemColors.ControlText;
                    lblSqlStatus.Text = L.T("DatabaseToolForm_D014", affected);

                    if (_writeModeEnabled)
                    {
                        RefreshIntegrityWarning();
                        if (_browserTableName != null)
                            LoadBrowserTable(); // ブラウザで開いているテーブルへの生SQL変更を反映
                    }
                }
            }
            catch (Exception ex)
            {
                // SqliteException(SQL構文/権限エラー)に加え、想定外のスキーマに対する
                // 型変換エラー等も含めてここで受け止め、ツール自体を落とさない。
                gridSqlResult.DataSource = null;
                lblSqlStatus.ForeColor = Color.OrangeRed;
                lblSqlStatus.Text = L.T("DatabaseToolForm_D015", ex.Message);
            }
        }

        #endregion

        #region テーブルブラウザ

        private void LoadTableNames()
        {
            cmbTable.Items.Clear();
            foreach (var name in _database.GetAdminTableNames())
                cmbTable.Items.Add(name);
            if (cmbTable.Items.Count > 0)
                cmbTable.SelectedIndex = 0;
        }

        private void cmbTable_SelectedIndexChanged(object sender, EventArgs e) => LoadBrowserTable();

        private void btnRefreshTable_Click(object sender, EventArgs e) => LoadBrowserTable();

        private void LoadBrowserTable()
        {
            _browserTableName = cmbTable.SelectedItem as string;
            if (_browserTableName == null) return;

            try
            {
                _browserPkColumns = _database.GetAdminPrimaryKeyColumns(_browserTableName);
                _browserTable = _database.GetAdminTableRows(_browserTableName, limit: 1000, offset: 0);
                gridBrowser.DataSource = _browserTable;
                UpdateBrowserColumnEditability();

                var truncated = _browserTable.ExtendedProperties.Contains("Truncated");
                var pkNote = _browserPkColumns.Count == 0 ? " " + L.T("DatabaseToolForm_D025") : "";
                lblBrowserStatus.Text = L.T("DatabaseToolForm_D018", _browserTable.Rows.Count, _browserTableName)
                    + (truncated ? " " + L.T("DatabaseToolForm_D028") : "") + pkNote;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, L.T("DatabaseToolForm_D007"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateBrowserColumnEditability()
        {
            if (gridBrowser.Columns.Count == 0) return;
            var editable = _writeModeEnabled && _browserPkColumns.Count > 0;
            foreach (DataGridViewColumn col in gridBrowser.Columns)
            {
                // 主キー列は編集不可 (WHERE句の整合性を保つため)。
                var isPk = _browserPkColumns.Contains(col.DataPropertyName);
                col.ReadOnly = !editable || isPk;
            }
        }

        private void gridBrowser_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (!_writeModeEnabled || _browserTable == null || _browserTableName == null || _browserPkColumns.Count == 0)
                return;
            if (e.RowIndex < 0 || e.RowIndex >= _browserTable.Rows.Count) return;

            // 列ヘッダクリックでのソート後はDataGridView側のRowIndexとDataTable.Rowsのインデックスが
            // 一致しなくなるため、DataBoundItem (DataRowView) 経由で正しいDataRowを取得する。
            if ((gridBrowser.Rows[e.RowIndex].DataBoundItem as DataRowView)?.Row is not DataRow row) return;
            // 表示上のヘッダ文言(HeaderText)はi18n適用等で列名と食い違いうるため、
            // DataTable列名と直結している DataPropertyName を使う。
            var columnName = gridBrowser.Columns[e.ColumnIndex].DataPropertyName;

            var pkValues = new Dictionary<string, object?>();
            foreach (var pkCol in _browserPkColumns)
                pkValues[pkCol] = row[pkCol] == DBNull.Value ? null : row[pkCol];

            var newValue = row[columnName] == DBNull.Value ? null : row[columnName];

            try
            {
                _database.UpdateAdminCell(_browserTableName, pkValues, columnName, newValue);
                lblBrowserStatus.Text = L.T("DatabaseToolForm_D019");
                RefreshIntegrityWarning();
            }
            catch (Exception ex)
            {
                MessageBox.Show(L.T("DatabaseToolForm_D020", ex.Message), L.T("DatabaseToolForm_D007"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                LoadBrowserTable(); // DB側が更新されていないため表示を再読込して不整合を防ぐ
            }
        }

        private void gridBrowser_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            // 型不一致等の入力エラーでクラッシュさせない。編集は破棄され、次のLoadBrowserTableで復元される。
            e.ThrowException = false;
        }

        private void btnDeleteSelectedRow_Click(object sender, EventArgs e)
        {
            if (!_writeModeEnabled || _browserTable == null || _browserTableName == null || _browserPkColumns.Count == 0)
                return;

            var selectedRows = gridBrowser.SelectedRows.Cast<DataGridViewRow>()
                .Where(r => r.Index >= 0 && r.Index < _browserTable.Rows.Count)
                .ToList();
            if (selectedRows.Count == 0)
            {
                MessageBox.Show(L.T("DatabaseToolForm_D027"), L.T("DatabaseToolForm_D007"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirmed = MessageBox.Show(
                L.T("DatabaseToolForm_D021", selectedRows.Count),
                L.T("DatabaseToolForm_D022"),
                MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (confirmed != DialogResult.OK) return;

            var deleted = 0;
            try
            {
                foreach (var gridRow in selectedRows)
                {
                    // gridBrowser は DataTable にバインドされており、列ヘッダクリックでのソート後は
                    // DataGridView側の行インデックスとDataTable.Rowsのインデックスが一致しなくなる。
                    // DataBoundItem (DataRowView) 経由でDataRowを取得することで、ソート後も
                    // 表示中の行と正しく対応するDataRowを参照できる。
                    if ((gridRow.DataBoundItem as DataRowView)?.Row is not DataRow row) continue;
                    var pkValues = new Dictionary<string, object?>();
                    foreach (var pkCol in _browserPkColumns)
                        pkValues[pkCol] = row[pkCol] == DBNull.Value ? null : row[pkCol];
                    _database.DeleteAdminRow(_browserTableName, pkValues);
                    deleted++;
                }
                lblBrowserStatus.Text = L.T("DatabaseToolForm_D023", deleted);
            }
            catch (Exception ex)
            {
                MessageBox.Show(L.T("DatabaseToolForm_D024", ex.Message), L.T("DatabaseToolForm_D007"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                LoadBrowserTable();
                RefreshIntegrityWarning();
            }
        }

        #endregion
    }
}
