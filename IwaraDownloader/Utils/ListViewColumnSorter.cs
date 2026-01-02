using System.Collections;

namespace IwaraDownloader.Utils
{
    /// <summary>
    /// ListViewのカラムソートを行うコンパレータ
    /// </summary>
    public class ListViewColumnSorter : IComparer
    {
        /// <summary>ソート対象のカラムインデックス</summary>
        public int SortColumn { get; set; } = 0;

        /// <summary>ソート順序</summary>
        public SortOrder Order { get; set; } = SortOrder.None;

        /// <summary>カラムごとのソートタイプ</summary>
        private readonly Dictionary<int, SortType> _columnTypes = new();

        /// <summary>
        /// ソートタイプ
        /// </summary>
        public enum SortType
        {
            String,
            Number,
            Date,
            FileSize,
            Percentage
        }

        /// <summary>
        /// カラムのソートタイプを設定
        /// </summary>
        public void SetColumnType(int columnIndex, SortType type)
        {
            _columnTypes[columnIndex] = type;
        }

        /// <summary>
        /// 比較を実行
        /// </summary>
        public int Compare(object? x, object? y)
        {
            if (x is not ListViewItem itemX || y is not ListViewItem itemY)
                return 0;

            if (Order == SortOrder.None)
                return 0;

            var textX = SortColumn < itemX.SubItems.Count ? itemX.SubItems[SortColumn].Text : "";
            var textY = SortColumn < itemY.SubItems.Count ? itemY.SubItems[SortColumn].Text : "";

            // タイトルカラムはアイコン絵文字を除去して比較
            if (SortColumn == 0)
            {
                textX = RemoveStatusIcon(textX);
                textY = RemoveStatusIcon(textY);
            }

            var sortType = _columnTypes.TryGetValue(SortColumn, out var type) ? type : SortType.String;

            int result = sortType switch
            {
                SortType.Number => CompareNumbers(textX, textY),
                SortType.Date => CompareDates(textX, textY),
                SortType.FileSize => CompareFileSizes(textX, textY),
                SortType.Percentage => ComparePercentages(textX, textY),
                _ => string.Compare(textX, textY, StringComparison.CurrentCulture)
            };

            // 降順の場合は結果を反転
            return Order == SortOrder.Descending ? -result : result;
        }

        /// <summary>
        /// ステータスアイコン（絵文字）を除去
        /// </summary>
        private static string RemoveStatusIcon(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
            // 絵文字とスペースを除去（先頭2-3文字程度）
            var icons = new[] { "⏳", "🔄", "✅", "❌", "⏭️", "⏸️", "❓" };
            foreach (var icon in icons)
            {
                if (text.StartsWith(icon))
                {
                    text = text.Substring(icon.Length).TrimStart();
                    break;
                }
            }
            return text;
        }

        /// <summary>
        /// 数値として比較
        /// </summary>
        private static int CompareNumbers(string x, string y)
        {
            if (double.TryParse(x, out var numX) && double.TryParse(y, out var numY))
                return numX.CompareTo(numY);
            return string.Compare(x, y, StringComparison.CurrentCulture);
        }

        /// <summary>
        /// 日付として比較
        /// </summary>
        private static int CompareDates(string x, string y)
        {
            if (DateTime.TryParse(x, out var dateX) && DateTime.TryParse(y, out var dateY))
                return dateX.CompareTo(dateY);
            return string.Compare(x, y, StringComparison.CurrentCulture);
        }

        /// <summary>
        /// ファイルサイズとして比較（KB, MB, GB対応）
        /// </summary>
        private static int CompareFileSizes(string x, string y)
        {
            var bytesX = ParseFileSize(x);
            var bytesY = ParseFileSize(y);
            return bytesX.CompareTo(bytesY);
        }

        /// <summary>
        /// ファイルサイズ文字列をバイト数に変換
        /// </summary>
        private static long ParseFileSize(string text)
        {
            if (string.IsNullOrEmpty(text) || text == "-")
                return 0;

            text = text.Trim().ToUpper();
            
            var multipliers = new Dictionary<string, long>
            {
                { "GB", 1024L * 1024 * 1024 },
                { "MB", 1024L * 1024 },
                { "KB", 1024L },
                { "B", 1L }
            };

            foreach (var (suffix, multiplier) in multipliers)
            {
                if (text.EndsWith(suffix))
                {
                    var numPart = text.Substring(0, text.Length - suffix.Length).Trim();
                    if (double.TryParse(numPart, out var num))
                        return (long)(num * multiplier);
                }
            }

            return 0;
        }

        /// <summary>
        /// パーセントとして比較
        /// </summary>
        private static int ComparePercentages(string x, string y)
        {
            var pctX = ParsePercentage(x);
            var pctY = ParsePercentage(y);
            return pctX.CompareTo(pctY);
        }

        /// <summary>
        /// パーセント文字列を数値に変換
        /// </summary>
        private static double ParsePercentage(string text)
        {
            if (string.IsNullOrEmpty(text) || text == "-" || text == "待機" || text == "DL中...")
                return -1;

            text = text.Replace("%", "").Trim();
            if (double.TryParse(text, out var pct))
                return pct;

            return -1;
        }
    }
}
