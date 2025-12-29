using System.Diagnostics;
using System.Text.RegularExpressions;

namespace IwaraDownloader.Utils
{
    /// <summary>
    /// 汎用ヘルパー
    /// </summary>
    public static class Helpers
    {
        /// <summary>
        /// iwara URLからユーザー名を抽出
        /// </summary>
        /// <param name="url">プロフィールURL</param>
        /// <returns>ユーザー名、抽出できない場合はnull</returns>
        public static string? ExtractUsernameFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            // https://www.iwara.tv/profile/username または https://iwara.tv/profile/username/videos
            var match = Regex.Match(url, @"iwara\.tv/profile/([^/\?]+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return null;
        }

        /// <summary>
        /// iwara URLから動画IDを抽出
        /// </summary>
        /// <param name="url">動画URL</param>
        /// <returns>動画ID、抽出できない場合はnull</returns>
        public static string? ExtractVideoIdFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            // https://www.iwara.tv/video/xxxxx/title
            var match = Regex.Match(url, @"iwara\.tv/video/([^/\?]+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return null;
        }

        /// <summary>
        /// URLがiwaraのユーザーページかどうか
        /// </summary>
        public static bool IsUserProfileUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;
            return Regex.IsMatch(url, @"iwara\.tv/profile/[^/\?]+", RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// ユーザー名が有効かどうか（英数字、@、_、-のみ許可）
        /// </summary>
        public static bool IsValidUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return false;
            
            // 英数字、@、_、- のみ許可（1～50文字）
            return Regex.IsMatch(username, @"^[a-zA-Z0-9@_-]{1,50}$");
        }

        /// <summary>
        /// URLがiwaraの動画ページかどうか
        /// </summary>
        public static bool IsVideoUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;
            return Regex.IsMatch(url, @"iwara\.tv/video/[^/\?]+", RegexOptions.IgnoreCase);
        }

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
        /// 指定したパスでファイルを開く（関連付けられたアプリで）
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
        /// ダウンロードフォルダのパスを生成（購読DL用）
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
        /// ダウンロードフォルダのパスを生成（個別DL用）
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
    }
}
