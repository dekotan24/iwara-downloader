using IwaraDownloader.Models;
using IwaraDownloader.Services;

namespace IwaraDownloader.Utils
{
    /// <summary>
    /// 1本の動画に手動でローカルファイルを紐付ける (右クリックメニュー「ローカルファイルをマップ」/
    /// 詳細ダイアログ「ローカルファイルを再マップ」の共通処理)。
    /// 実体は TitleMatchImporter.ImportOneAsync に委譲する (タグ書き込み・FileUuid解決・DB反映を
    /// タイトル照合インポートと共通化するため)。ファイル選択・確認ダイアログの UI 部分だけここが持つ。
    /// </summary>
    public static class LocalFileMapHelper
    {
        public enum MapResult
        {
            Mapped,
            Cancelled,
            Failed,
        }

        /// <summary>
        /// ファイル選択 → 確認 → TitleMatchImporter.ImportOneAsync という一連の流れをまとめて行う。
        /// video は呼び出し側と共有の参照である前提で、成功時はその場でフィールドが更新される
        /// (LocalFilePath / Status / FileSize / FileUuid 等)。DBへの反映も内部で完了する。
        /// </summary>
        public static async Task<MapResult> MapAsync(
            IWin32Window owner, VideoInfo video, IwaraApiService api, DatabaseService database)
        {
            using var dialog = new OpenFileDialog
            {
                Title = L.T("SvcLocalFileMap_D001", video.Title),
                Filter = L.T("SvcLocalFileMap_D002"),
                CheckFileExists = true,
            };

            var existingDir = !string.IsNullOrEmpty(video.LocalFilePath)
                ? Path.GetDirectoryName(video.LocalFilePath)
                : null;
            if (!string.IsNullOrEmpty(existingDir) && Directory.Exists(existingDir))
                dialog.InitialDirectory = existingDir;

            if (dialog.ShowDialog(owner) != DialogResult.OK)
                return MapResult.Cancelled;

            var selectedPath = dialog.FileName;
            var oldPath = video.LocalFilePath;

            // 選択したファイルが既に別の動画の iwara タグを持っている場合、そのまま進むと
            // WriteIwaraTags で上書きされ、旧タグ先の動画の DB 行が「タグの無い実体」を
            // 指したまま迷子になる (再スキャンでも二度と自己修復できない)。書き込み前に検出して
            // 明示的に確認を取る。
            var (existingTagVideoId, _) = MetadataService.ReadIwaraTags(selectedPath);
            if (!string.IsNullOrEmpty(existingTagVideoId)
                && !string.Equals(existingTagVideoId, video.VideoId, StringComparison.Ordinal))
            {
                var otherTitle = database.GetVideoByVideoId(existingTagVideoId)?.Title ?? existingTagVideoId;
                var warnConfirm = MessageBox.Show(owner,
                    L.T("SvcLocalFileMap_D008", selectedPath, otherTitle),
                    L.T("SvcLocalFileMap_D009"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (warnConfirm != DialogResult.Yes)
                    return MapResult.Cancelled;
            }

            var confirmText = L.T("SvcLocalFileMap_D003", video.Title, selectedPath) +
                (!string.IsNullOrEmpty(oldPath) && !string.Equals(oldPath, selectedPath, StringComparison.OrdinalIgnoreCase)
                    ? L.T("SvcLocalFileMap_D004", oldPath)
                    : "");
            var confirm = MessageBox.Show(owner, confirmText, L.T("SvcLocalFileMap_D005"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
                return MapResult.Cancelled;

            try
            {
                await TitleMatchImporter.ImportOneAsync(video, selectedPath, api, database);
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, L.T("SvcLocalFileMap_D006", ex.Message),
                    L.T("SvcLocalFileMap_D007"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return MapResult.Failed;
            }

            // インデックスキャッシュ無効化 (旧パス・新パス双方のフォルダ)。
            // 旧ファイル自体は削除しない (ユーザーが誤マップに気付いて元に戻せるように)。
            InvalidateDir(oldPath);
            InvalidateDir(selectedPath);

            return MapResult.Mapped;
        }

        private static void InvalidateDir(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                IndexCacheService.Invalidate(dir);
        }

        /// <summary>
        /// 確認 → ローカルファイルとの紐付けを解除する (マップの逆操作)。
        /// 実ファイル・mp4 に書き込んだタグは一切触らない (ユーザーが後で元に戻せるように)。
        /// FileUuid は維持する (再マップ時に download-url API の再解決を避けるため)。
        /// Status は自動DLに拾われない Paused に戻す (Pending だと解除直後に自動DLが走ってしまう)。
        /// </summary>
        public static MapResult Unmap(IWin32Window owner, VideoInfo video, DatabaseService database)
        {
            var oldPath = video.LocalFilePath;
            if (string.IsNullOrEmpty(oldPath))
                return MapResult.Cancelled;

            var confirm = MessageBox.Show(owner,
                L.T("SvcLocalFileMap_D010", video.Title, oldPath),
                L.T("SvcLocalFileMap_D011"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
                return MapResult.Cancelled;

            video.LocalFilePath = string.Empty;
            video.FileSize = 0;
            video.DownloadedAt = null;
            video.Status = DownloadStatus.Paused;
            database.UpdateVideo(video);

            InvalidateDir(oldPath);

            return MapResult.Mapped;
        }
    }
}
