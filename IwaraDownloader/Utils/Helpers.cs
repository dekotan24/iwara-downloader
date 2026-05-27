using System.Diagnostics;
using System.Text.RegularExpressions;

namespace IwaraDownloader.Utils
{
    /// <summary>
    /// 汎用ヘルパー
    /// </summary>
    public static class Helpers
    {
        // 対応する iwara ドメイン (iwara.tv = 通常 / iwara.ai = AI動画専用)
        // URL のホスト部 + API 呼び出し時の X-Site ヘッダー値として使う
        public const string SiteTv = "www.iwara.tv";
        public const string SiteAi = "www.iwara.ai";

        // 動画/プロフィール URL を判定する正規表現 (両ドメイン対応)
        private static readonly Regex RxVideoUrl =
            new(@"iwara\.(?:tv|ai)/video/([^/\?]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex RxProfileUrl =
            new(@"iwara\.(?:tv|ai)/profile/([^/\?]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// iwara URLからユーザー名を抽出 (iwara.tv / iwara.ai 両対応)
        /// </summary>
        public static string? ExtractUsernameFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            var m = RxProfileUrl.Match(url);
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>
        /// iwara URLから動画IDを抽出 (iwara.tv / iwara.ai 両対応)
        /// </summary>
        public static string? ExtractVideoIdFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            var m = RxVideoUrl.Match(url);
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>
        /// URL から site (www.iwara.tv / www.iwara.ai) を判定する。
        /// X-Site ヘッダー値として使うため、ホスト名そのものを返す。
        /// </summary>
        public static string ExtractSiteFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return SiteTv;
            // iwara.ai を含むなら ai、それ以外は tv
            return url.Contains("iwara.ai", StringComparison.OrdinalIgnoreCase) ? SiteAi : SiteTv;
        }

        /// <summary>
        /// URLがiwaraのユーザーページかどうか (iwara.tv / iwara.ai)
        /// </summary>
        public static bool IsUserProfileUrl(string url)
            => !string.IsNullOrWhiteSpace(url) && RxProfileUrl.IsMatch(url);

        /// <summary>
        /// ユーザー名が有効かどうか(英数字、@、_、-のみ許可)
        /// </summary>
        public static bool IsValidUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return false;

            // 英数字、@、_、- のみ許可(1～50文字)
            return Regex.IsMatch(username, @"^[a-zA-Z0-9@_-]{1,50}$");
        }

        /// <summary>
        /// URLがiwaraの動画ページかどうか (iwara.tv / iwara.ai)
        /// </summary>
        public static bool IsVideoUrl(string url)
            => !string.IsNullOrWhiteSpace(url) && RxVideoUrl.IsMatch(url);

        /// <summary>
        /// ファイル名として使用できない文字を置換
        /// </summary>
        public static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "untitled";

            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = fileName;

            foreach (var c in invalidChars)
            {
                sanitized = sanitized.Replace(c, '_');
            }

            // 連続するアンダースコアを1つにまとめる
            sanitized = Regex.Replace(sanitized, @"_+", "_");

            // 先頭と末尾のアンダースコアとスペースを削除
            sanitized = sanitized.Trim('_', ' ', '.');

            // 長すぎる場合は切り詰め
            if (sanitized.Length > 200)
            {
                sanitized = sanitized.Substring(0, 200);
            }

            return string.IsNullOrWhiteSpace(sanitized) ? "untitled" : sanitized;
        }

        /// <summary>
        /// 指定したパスでファイルを開く(関連付けられたアプリで)
        /// </summary>
        public static void OpenFile(string filePath)
        {
            if (!File.Exists(filePath))
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ファイルを開けませんでした: {ex.Message}");
            }
        }

        /// <summary>
        /// 指定したフォルダをエクスプローラーで開く
        /// </summary>
        public static void OpenFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = folderPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"フォルダを開けませんでした: {ex.Message}");
            }
        }

        /// <summary>
        /// 指定したファイルをエクスプローラーで選択して開く
        /// </summary>
        public static void OpenFolderAndSelectFile(string filePath)
        {
            if (!File.Exists(filePath))
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{filePath}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"エクスプローラーを開けませんでした: {ex.Message}");
            }
        }

        /// <summary>
        /// URLをデフォルトブラウザで開く
        /// </summary>
        public static void OpenUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"URLを開けませんでした: {ex.Message}");
            }
        }

        /// <summary>
        /// ダウンロードフォルダのパスを生成(購読DL用)
        /// </summary>
        /// <param name="baseFolder">ベースフォルダ</param>
        /// <param name="username">ユーザー名</param>
        /// <param name="videoTitle">動画タイトル</param>
        /// <returns>フルパス</returns>
        public static string GetSubscriptionDownloadPath(string baseFolder, string username, string videoTitle)
        {
            var sanitizedUsername = SanitizeFileName(username);
            var sanitizedTitle = SanitizeFileName(videoTitle);
            var userFolder = Path.Combine(baseFolder, sanitizedUsername);

            if (!Directory.Exists(userFolder))
            {
                Directory.CreateDirectory(userFolder);
            }

            return Path.Combine(userFolder, $"{sanitizedTitle}.mp4");
        }

        /// <summary>
        /// ダウンロードフォルダのパスを生成(個別DL用)
        /// </summary>
        /// <param name="baseFolder">ベースフォルダ</param>
        /// <param name="username">ユーザー名</param>
        /// <param name="videoTitle">動画タイトル</param>
        /// <returns>フルパス</returns>
        public static string GetSingleDownloadPath(string baseFolder, string username, string videoTitle)
        {
            var sanitizedUsername = SanitizeFileName(username);
            var sanitizedTitle = SanitizeFileName(videoTitle);

            if (!Directory.Exists(baseFolder))
            {
                Directory.CreateDirectory(baseFolder);
            }

            return Path.Combine(baseFolder, $"{sanitizedUsername}_{sanitizedTitle}.mp4");
        }

        /// <summary>
        /// 重複しないファイル名を生成
        /// </summary>
        public static string GetUniqueFilePath(string filePath)
        {
            if (!File.Exists(filePath))
                return filePath;

            var directory = Path.GetDirectoryName(filePath) ?? "";
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath);

            int counter = 1;
            string newPath;
            do
            {
                newPath = Path.Combine(directory, $"{fileName} ({counter}){extension}");
                counter++;
            } while (File.Exists(newPath));

            return newPath;
        }

        /// <summary>
        /// ファイル名テンプレートを適用してファイル名を生成
        /// </summary>
        /// <param name="template">テンプレート({title}, {author}, {date}, {id}, {quality})</param>
        /// <param name="title">動画タイトル</param>
        /// <param name="author">投稿者名</param>
        /// <param name="videoId">動画ID</param>
        /// <param name="postedAt">投稿日</param>
        /// <param name="quality">画質</param>
        /// <returns>ファイル名(拡張子なし)</returns>
        public static string ApplyFilenameTemplate(string template, string title, string author, string videoId, DateTime? postedAt, string quality = "")
        {
            if (string.IsNullOrWhiteSpace(template))
                template = "{title}";

            var dateStr = postedAt?.ToString("yyyyMMdd") ?? "unknown";

            var result = template
                .Replace("{title}", SanitizeFileName(title))
                .Replace("{author}", SanitizeFileName(author))
                .Replace("{date}", dateStr)
                .Replace("{id}", SanitizeFileName(videoId))
                .Replace("{quality}", SanitizeFileName(quality));

            // 結果が空の場合はデフォルト
            if (string.IsNullOrWhiteSpace(result))
                result = SanitizeFileName(title);

            return result;
        }
    }
}
