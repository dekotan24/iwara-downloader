using System.Text.Json;
using System.Text.Json.Serialization;

namespace IwaraDownloader.Services
{
    /// <summary>
    /// フォルダ取り込みのタグ読み取り結果を、ファイル単位のサイズ・更新日時とともに保存する。
    /// 書き込み権限が無いフォルダや壊れたキャッシュは、キャッシュ無しとして安全に扱う。
    /// </summary>
    public static class ImportScanCacheService
    {
        private const string CacheFileName = ".iwara_import_index.json";
        private const int CurrentVersion = 1;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
        };

        public sealed class CacheFile
        {
            [JsonPropertyName("version")]
            public int Version { get; set; } = CurrentVersion;

            [JsonPropertyName("entries")]
            public Dictionary<string, Entry> Entries { get; set; } =
                new(StringComparer.OrdinalIgnoreCase);
        }

        public sealed class Entry
        {
            [JsonPropertyName("size")]
            public long Size { get; set; }

            [JsonPropertyName("mtime")]
            public long LastWriteTimeUtcTicks { get; set; }

            [JsonPropertyName("video_id")]
            public string VideoId { get; set; } = "";

            [JsonPropertyName("file_uuid")]
            public string FileUuid { get; set; } = "";
        }

        public static CacheFile Load(string folderPath)
        {
            try
            {
                var path = GetCachePath(folderPath);
                if (!File.Exists(path)) return new CacheFile();

                var json = File.ReadAllText(path);
                var cache = JsonSerializer.Deserialize<CacheFile>(json, JsonOptions);
                if (cache?.Version != CurrentVersion || cache.Entries == null)
                    return new CacheFile();

                // JSON deserializationでは比較子が復元されないため、Windowsのファイル名比較に戻す。
                cache.Entries = new Dictionary<string, Entry>(cache.Entries, StringComparer.OrdinalIgnoreCase);
                return cache;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ImportScanCacheService.Load failed: {ex.Message}");
                return new CacheFile();
            }
        }

        public static bool TryGet(
            CacheFile cache, string rootPath, string filePath, long size, long mtimeTicks,
            out string videoId, out string fileUuid)
        {
            videoId = "";
            fileUuid = "";

            var key = GetRelativeKey(rootPath, filePath);
            if (!cache.Entries.TryGetValue(key, out var entry)
                || entry is null
                || entry.Size != size
                || entry.LastWriteTimeUtcTicks != mtimeTicks
                // ReadIwaraTags は読み取り例外をタグ無しとして返すため、空結果を
                // キャッシュから採用すると一時的な読み取り失敗を永久に固定してしまう。
                || string.IsNullOrEmpty(entry.VideoId))
            {
                return false;
            }

            videoId = entry.VideoId ?? "";
            fileUuid = entry.FileUuid ?? "";
            return true;
        }

        public static Entry CreateEntry(long size, long mtimeTicks, string? videoId, string? fileUuid)
            => new()
            {
                Size = size,
                LastWriteTimeUtcTicks = mtimeTicks,
                VideoId = videoId ?? "",
                FileUuid = fileUuid ?? "",
            };

        public static void Save(string folderPath, IReadOnlyDictionary<string, Entry> entries)
        {
            try
            {
                var path = GetCachePath(folderPath);
                var cache = new CacheFile
                {
                    Entries = new Dictionary<string, Entry>(entries, StringComparer.OrdinalIgnoreCase),
                };
                var json = JsonSerializer.Serialize(cache, JsonOptions);
                var tempPath = path + ".tmp";

                // 途中終了で本体を壊さないよう、同じフォルダ内の一時ファイルから差し替える。
                File.WriteAllText(tempPath, json);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tempPath, path);

                try
                {
                    var attributes = File.GetAttributes(path);
                    File.SetAttributes(path, attributes | FileAttributes.Hidden);
                }
                catch
                {
                    // 隠し属性は利便性のためだけなので、設定できなくても処理は成功扱いにする。
                }
            }
            catch (Exception ex)
            {
                // キャッシュは補助機能。保存失敗で取り込み自体を失敗させない。
                System.Diagnostics.Debug.WriteLine($"ImportScanCacheService.Save failed: {ex.Message}");
            }
        }

        public static string GetCachePath(string folderPath)
            => Path.Combine(folderPath, CacheFileName);

        public static string GetRelativeKey(string rootPath, string filePath)
        {
            var relative = Path.GetRelativePath(rootPath, filePath);
            return relative.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }
    }
}
