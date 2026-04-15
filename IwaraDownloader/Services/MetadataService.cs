using System.Diagnostics;
using TagLib;
using TagLib.Mpeg4;

namespace IwaraDownloader.Services
{
    /// <summary>
    /// mp4 ファイルに iwara 固有のメタ情報 (動画ID / ファイル UUID) を
    /// カスタムタグとして書き込み/読み出しするサービス。
    ///
    /// 使用する仕組み: MPEG-4 の QuickTime 互換 AppleTag で
    /// `----` アトム (Free-form) にキーと値のペアを格納する。
    /// 平均名前空間は "com.iwara"、キーは video_id / file_id。
    /// ファイル名に依存せず、移動・リネームに耐える。
    /// </summary>
    public static class MetadataService
    {
        private const string MeanNamespace = "com.iwara";
        public const string KeyVideoId = "video_id";
        public const string KeyFileId = "file_id";

        /// <summary>
        /// mp4 ファイルに iwara のメタ情報を書き込む。
        /// </summary>
        public static bool WriteIwaraTags(string filePath, string videoId, string fileUuid)
        {
            try
            {
                if (!System.IO.File.Exists(filePath)) return false;

                using var file = TagLib.File.Create(filePath);
                var appleTag = file.GetTag(TagTypes.Apple, true) as AppleTag;
                if (appleTag == null)
                {
                    Debug.WriteLine($"MetadataService: AppleTag not available for {filePath}");
                    return false;
                }

                SetDashBox(appleTag, KeyVideoId, videoId ?? "");
                SetDashBox(appleTag, KeyFileId, fileUuid ?? "");

                file.Save();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MetadataService.WriteIwaraTags failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// mp4 ファイルから iwara のメタ情報を読み出す。
        /// </summary>
        public static (string VideoId, string FileUuid) ReadIwaraTags(string filePath)
        {
            try
            {
                if (!System.IO.File.Exists(filePath)) return ("", "");

                using var file = TagLib.File.Create(filePath);
                var appleTag = file.GetTag(TagTypes.Apple, false) as AppleTag;
                if (appleTag == null) return ("", "");

                var videoId = appleTag.GetDashBox(MeanNamespace, KeyVideoId) ?? "";
                var fileUuid = appleTag.GetDashBox(MeanNamespace, KeyFileId) ?? "";
                return (videoId, fileUuid);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MetadataService.ReadIwaraTags failed: {ex.Message}");
                return ("", "");
            }
        }

        /// <summary>
        /// 指定フォルダ内の mp4 から FileUuid を読み取り、マップを返す。
        /// キャッシュ等は使わない単純スキャン。
        /// </summary>
        public static Dictionary<string, string> ScanFolderForFileUuids(string folderPath)
        {
            var result = new Dictionary<string, string>();
            if (!Directory.Exists(folderPath)) return result;

            foreach (var path in Directory.EnumerateFiles(folderPath, "*.mp4", SearchOption.TopDirectoryOnly))
            {
                var (_, fileUuid) = ReadIwaraTags(path);
                if (!string.IsNullOrEmpty(fileUuid))
                {
                    result[fileUuid] = path;
                }
            }
            return result;
        }

        private static void SetDashBox(AppleTag tag, string name, string value)
        {
            tag.SetDashBox(MeanNamespace, name, value);
        }
    }
}
