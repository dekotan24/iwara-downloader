using System.Globalization;
using System.Resources;
using System.Windows.Forms;

namespace IwaraDownloader.Utils
{
    /// <summary>
    /// フォーム上のコントロール文言を名前規約でリソースから一括適用するローカライザ。
    /// キー規約: フォームタイトル = "{FormName}_Title"、各コントロール = "{FormName}_{ControlName}"。
    /// リソースにキーが存在するものだけ上書きするため、Designer既定(日本語)へのフォールバックが自然に効く。
    /// 各フォームのコンストラクタで InitializeComponent() の直後に Localizer.Apply(this) を呼ぶ。
    /// </summary>
    /// <remarks>
    /// 対象は Text / ToolTipText のみ。ComboBox の Items は「全て」等のフィルタ判定値として
    /// コード側と結合していることがあるため、ここでは一切触らない(必要なら各フォームで個別対応)。
    /// </remarks>
    public static class Localizer
    {
        private static readonly ResourceManager _rm =
            new("IwaraDownloader.Resources.Strings", typeof(Localizer).Assembly);

        public static void Apply(Form form)
        {
            var culture = CultureInfo.CurrentUICulture;
            // form.Name は Designer が this.Name を設定していないフォームだと空になり
            // キーが一致しなくなる (SearchImportForm/VideoDetailsForm で実際に発生) ため、
            // 常にクラス名を使う
            var formName = form.GetType().Name;

            var title = _rm.GetString($"{formName}_Title", culture);
            if (title != null) form.Text = title;

            ApplyControls(formName, form.Controls, culture);
        }

        private static void ApplyControls(string formName, Control.ControlCollection controls, CultureInfo culture)
        {
            foreach (Control control in controls)
            {
                if (!string.IsNullOrEmpty(control.Name))
                {
                    var text = _rm.GetString($"{formName}_{control.Name}", culture);
                    if (text != null) control.Text = text;

                    if (control is TextBox textBox)
                    {
                        var placeholder = _rm.GetString($"{formName}_{control.Name}_Placeholder", culture);
                        if (placeholder != null) textBox.PlaceholderText = placeholder;
                    }

                    // ComboBox の項目: "{Form}_{Name}_Item0" 形式のキーが存在するものだけ差し替える。
                    // 対象は SelectedIndex で判定しているコンボのみリソースに登録すること
                    // (項目文字列そのもので判定しているコンボに登録すると判定が壊れる)
                    if (control is ComboBox comboBox && comboBox.Items.Count > 0)
                    {
                        var selectedIndex = comboBox.SelectedIndex;
                        var replaced = false;
                        for (var i = 0; i < comboBox.Items.Count; i++)
                        {
                            var item = _rm.GetString($"{formName}_{control.Name}_Item{i}", culture);
                            if (item == null) continue;
                            comboBox.Items[i] = item;
                            replaced = true;
                        }
                        if (replaced && selectedIndex >= 0) comboBox.SelectedIndex = selectedIndex;
                    }
                }

                // ツールバー/メニュー/ステータスバーの項目
                if (control is ToolStrip toolStrip)
                {
                    ApplyToolStripItems(formName, toolStrip.Items, culture);
                }

                // ListView の列ヘッダー
                if (control is ListView listView)
                {
                    foreach (ColumnHeader column in listView.Columns)
                    {
                        if (string.IsNullOrEmpty(column.Name)) continue;
                        var text = _rm.GetString($"{formName}_{column.Name}", culture);
                        if (text != null) column.Text = text;
                    }
                }

                // DataGridView の列ヘッダー
                if (control is DataGridView grid)
                {
                    foreach (DataGridViewColumn column in grid.Columns)
                    {
                        var text = _rm.GetString($"{formName}_{column.Name}", culture);
                        if (text != null) column.HeaderText = text;
                    }
                }

                if (control.HasChildren)
                {
                    ApplyControls(formName, control.Controls, culture);
                }
            }
        }

        private static void ApplyToolStripItems(string formName, ToolStripItemCollection items, CultureInfo culture)
        {
            foreach (ToolStripItem item in items)
            {
                if (!string.IsNullOrEmpty(item.Name))
                {
                    var text = _rm.GetString($"{formName}_{item.Name}", culture);
                    if (text != null) item.Text = text;

                    var tooltip = _rm.GetString($"{formName}_{item.Name}_Tip", culture);
                    if (tooltip != null) item.ToolTipText = tooltip;
                }

                if (item is ToolStripDropDownItem dropDown && dropDown.HasDropDownItems)
                {
                    ApplyToolStripItems(formName, dropDown.DropDownItems, culture);
                }
            }
        }
    }
}
