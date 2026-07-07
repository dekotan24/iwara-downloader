using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Windows.Forms;

namespace IwaraDownloader.Utils
{
    /// <summary>
    /// フォームの文言を名前規約でリソースから一括適用するローカライザ。
    /// キー規約: フォームタイトル = "{FormName}_Title"、各コントロール = "{FormName}_{フィールド名}"、
    /// ツールチップ = "..._Tip"、プレースホルダ = "..._Placeholder"、ComboBox項目 = "..._Item0..n"。
    /// リソースにキーが存在するものだけ上書きするため、Designer既定(日本語)へのフォールバックが自然に効く。
    /// 各フォームのコンストラクタで InitializeComponent() の直後に Localizer.Apply(this) を呼ぶ。
    /// </summary>
    /// <remarks>
    /// このリポジトリの Designer.cs は Name プロパティの設定が無い/不完全なフォームが多く
    /// (SearchImportForm はゼロ、SettingsForm も一部欠落)、Control.Name ベースの探索では
    /// 適用漏れが起きるため、リフレクションでフォームのフィールドを直接列挙して適用する。
    /// フィールド名は Designer の "this.xxx" と同じなので抽出キーとそのまま一致する。
    /// この方式は ContextMenuStrip の項目 (Controls ツリーに現れない) にも届く。
    /// ComboBox の Items は「全て」等のフィルタ判定値としてコード側と結合していることがあるため、
    /// SelectedIndex 判定のコンボのみリソースに登録すること。
    /// </remarks>
    public static class Localizer
    {
        private static readonly ResourceManager _rm =
            new("IwaraDownloader.Resources.Strings", typeof(Localizer).Assembly);

        public static void Apply(Form form)
        {
            var culture = CultureInfo.CurrentUICulture;
            var type = form.GetType();
            var formName = type.Name;

            var title = _rm.GetString($"{formName}_Title", culture);
            if (title != null) form.Text = title;

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                object? value;
                try { value = field.GetValue(form); }
                catch { continue; }

                switch (value)
                {
                    case Control control:
                        ApplyControl(formName, field.Name, control, culture);
                        break;
                    case ToolStripItem toolStripItem:
                        ApplyToolStripItem(formName, field.Name, toolStripItem, culture);
                        break;
                    case ColumnHeader columnHeader:
                        var colText = _rm.GetString($"{formName}_{field.Name}", culture);
                        if (colText != null) columnHeader.Text = colText;
                        break;
                    case DataGridViewColumn gridColumn:
                        var headerText = _rm.GetString($"{formName}_{field.Name}", culture);
                        if (headerText != null) gridColumn.HeaderText = headerText;
                        break;
                }
            }
        }

        private static void ApplyControl(string formName, string fieldName, Control control, CultureInfo culture)
        {
            var text = _rm.GetString($"{formName}_{fieldName}", culture);
            if (text != null) control.Text = text;

            if (control is TextBox textBox)
            {
                var placeholder = _rm.GetString($"{formName}_{fieldName}_Placeholder", culture);
                if (placeholder != null) textBox.PlaceholderText = placeholder;
            }

            if (control is ComboBox comboBox && comboBox.Items.Count > 0)
            {
                var selectedIndex = comboBox.SelectedIndex;
                var replaced = false;
                for (var i = 0; i < comboBox.Items.Count; i++)
                {
                    var item = _rm.GetString($"{formName}_{fieldName}_Item{i}", culture);
                    if (item == null) continue;
                    comboBox.Items[i] = item;
                    replaced = true;
                }
                if (replaced && selectedIndex >= 0) comboBox.SelectedIndex = selectedIndex;
            }
        }

        private static void ApplyToolStripItem(string formName, string fieldName, ToolStripItem item, CultureInfo culture)
        {
            var text = _rm.GetString($"{formName}_{fieldName}", culture);
            if (text != null) item.Text = text;

            var tooltip = _rm.GetString($"{formName}_{fieldName}_Tip", culture);
            if (tooltip != null) item.ToolTipText = tooltip;
        }
    }
}
