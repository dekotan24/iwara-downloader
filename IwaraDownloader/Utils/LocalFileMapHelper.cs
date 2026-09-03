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
            IWin32Window owner, VideoInfo video, IwaraApiService api, DatabaseService database,
            DownloadManager downloadManager, bool allowForeignTag = true,
            bool useAtomicExistingUpdate = false)
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

            // DB上で別動画が同じ実ファイルを参照している場合、ここで紐付けると
            // 片方の動画を修復したつもりで、もう片方の参照を壊してしまう。
            // ファイル移動やタグ上書きは行わず、先に選択を取り消す。
            var mappedVideo = database.GetVideoByLocalFilePath(selectedPath, video.Id);
            if (mappedVideo != null)
            {
                MessageBox.Show(owner,
                    L.T("SvcLocalFileMap_D012", selectedPath, mappedVideo.Title),
                    L.T("SvcLocalFileMap_D013"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return MapResult.Cancelled;
            }

            // 選択したファイルが既に別の動画の iwara タグを持っている場合、そのまま進むと
            // WriteIwaraTags で上書きされ、旧タグ先の動画の DB 行が「タグの無い実体」を
            // 指したまま迷子になる (再スキャンでも二度と自己修復できない)。書き込み前に検出して
            // 明示的に確認を取る。
            var (existingTagVideoId, existingTagUuid) = MetadataService.ReadIwaraTags(selectedPath);
            var taggedUuidOwner = !string.IsNullOrEmpty(existingTagUuid)
                ? database.GetVideoByFileUuid(existingTagUuid)
                : null;
            var foreignVideoId = !string.IsNullOrEmpty(existingTagVideoId)
                && !string.Equals(existingTagVideoId, video.VideoId, StringComparison.Ordinal);
            var foreignFileUuid = !string.IsNullOrEmpty(existingTagUuid)
                && ((!string.IsNullOrEmpty(video.FileUuid)
                        && !string.Equals(existingTagUuid, video.FileUuid, StringComparison.OrdinalIgnoreCase))
                    || (taggedUuidOwner != null && taggedUuidOwner.Id != video.Id));
            if (foreignVideoId || foreignFileUuid)
            {
                var otherVideo = !string.IsNullOrEmpty(existingTagVideoId)
                    ? database.GetVideoByVideoId(existingTagVideoId)
                    : taggedUuidOwner;
                var otherTitle = otherVideo?.Title
                    ?? (!string.IsNullOrEmpty(existingTagVideoId) ? existingTagVideoId : existingTagUuid);
                if (!allowForeignTag)
                {
                    MessageBox.Show(owner,
                        L.T("SvcLocalFileMap_D014", selectedPath, otherTitle),
                        L.T("SvcLocalFileMap_D015"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return MapResult.Cancelled;
                }

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

            if (useAtomicExistingUpdate && downloadManager.GetTask(video.VideoId) != null)
            {
                MessageBox.Show(owner, L.T("SvcLocalFileMap_D016"),
                    L.T("SvcLocalFileMap_D015"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return MapResult.Cancelled;
            }

            // 未DL/待機中の動画にはダウンロードキューへ投入済みの DownloadTask が残っている
            // ことがある。通常のマップでは、ImportOneAsync が Status=Completed を書き込む前に
            // キャンセルして最終的なDB書き込みがマップ側で決着するようにする。
            // 整合性チェック画面の安全経路(useAtomicExistingUpdate)では、上の再確認後に
            // タスクが無いことを前提にしてキャンセル処理自体を行わない。
            try
            {
                if (useAtomicExistingUpdate && video.Id > 0)
                {
                    var mapped = await MapExistingVideoSafelyAsync(
                        owner, video, selectedPath, existingTagVideoId, existingTagUuid, api, database);
                    if (!mapped) return MapResult.Cancelled;
                }
                else
                {
                    downloadManager.CancelTask(video.VideoId);
                    await TitleMatchImporter.ImportOneAsync(video, selectedPath, api, database);
                }
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

        /// <summary>
        /// 整合性チェック画面用の既存DB行マップ。
        /// TitleMatchImporterの全カラム更新を使わず、期待したDB状態とパス重複を
        /// 同一UPDATEで確認してからローカルファイル欄だけを更新する。
        /// </summary>
        private static async Task<bool> MapExistingVideoSafelyAsync(
            IWin32Window owner,
            VideoInfo video,
            string selectedPath,
            string existingTagVideoId,
            string existingTagUuid,
            IwaraApiService api,
            DatabaseService database)
        {
            var resolvedFileUuid = video.FileUuid;
            if (string.IsNullOrEmpty(resolvedFileUuid))
            {
                try
                {
                    var site = string.IsNullOrEmpty(video.Site) ? null : video.Site;
                    var info = await api.GetDownloadUrlAsync(video.VideoId, site);
                    if (info.Success && !string.IsNullOrEmpty(info.FileUuid))
                        resolvedFileUuid = info.FileUuid;
                }
                catch (Exception ex)
                {
                    // ローカルファイルのマップ自体はAPI障害で諦めない。UUIDが取れなければ
                    // タグ書き込みだけ行わず、DB上のローカルパスを安全に確定する。
                    LoggingService.Instance.Warn($"Local file map UUID resolve failed: {ex.Message}");
                }
            }

            // 対象動画のDB側UUIDが空だった場合でも、API解決後のUUIDとファイルタグが
            // 食い違っていれば別動画の実体なので、タグを上書きしない。
            if (!string.IsNullOrEmpty(existingTagUuid)
                && !string.IsNullOrEmpty(resolvedFileUuid)
                && !string.Equals(existingTagUuid, resolvedFileUuid, StringComparison.OrdinalIgnoreCase))
            {
                var otherVideo = database.GetVideoByFileUuid(existingTagUuid);
                var otherTitle = otherVideo?.Title
                    ?? (!string.IsNullOrEmpty(existingTagVideoId) ? existingTagVideoId : existingTagUuid);
                MessageBox.Show(owner,
                    L.T("SvcLocalFileMap_D014", selectedPath, otherTitle),
                    L.T("SvcLocalFileMap_D015"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!File.Exists(selectedPath))
            {
                MessageBox.Show(owner, L.T("SvcLocalFileMap_D016"),
                    L.T("SvcLocalFileMap_D015"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            var fileSize = new FileInfo(selectedPath).Length;
            var downloadedAt = SafeLastWriteTime(selectedPath);
            var mapped = database.TryUpdateVideoLocalFileFields(
                video.Id,
                selectedPath,
                fileSize,
                downloadedAt,
                DownloadStatus.Completed,
                resolvedFileUuid,
                video.LocalFilePath,
                video.Status,
                video.FileUuid);
            if (!mapped)
            {
                MessageBox.Show(owner, L.T("SvcLocalFileMap_D016"),
                    L.T("SvcLocalFileMap_D015"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            video.LocalFilePath = selectedPath;
            video.FileSize = fileSize;
            video.DownloadedAt = downloadedAt;
            video.Status = DownloadStatus.Completed;
            video.FileUuid = resolvedFileUuid;

            // DB上のマップを確保した後にタグを書き込む。タグ書き込みでファイルサイズが
            // 変わった場合だけ、同じ期待状態で限定更新する。
            if (!string.IsNullOrEmpty(resolvedFileUuid)
                && MetadataService.WriteIwaraTags(selectedPath, video.VideoId, resolvedFileUuid))
            {
                try
                {
                    var finalSize = new FileInfo(selectedPath).Length;
                    if (finalSize != fileSize
                        && database.TryUpdateVideoLocalFileFields(
                            video.Id,
                            selectedPath,
                            finalSize,
                            downloadedAt,
                            DownloadStatus.Completed,
                            resolvedFileUuid,
                            selectedPath,
                            DownloadStatus.Completed,
                            resolvedFileUuid))
                    {
                        video.FileSize = finalSize;
                    }
                }
                catch
                {
                    // タグ書き込み直後にファイルが移動/削除された場合でも、既に確定した
                    // DBマップを別の全カラム更新で巻き戻さない。
                }
            }

            return true;
        }

        private static DateTime SafeLastWriteTime(string path)
        {
            try { return File.GetLastWriteTime(path); }
            catch { return DateTime.Now; }
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
        public static MapResult Unmap(
            IWin32Window owner, VideoInfo video, DatabaseService database, DownloadManager downloadManager)
        {
            var oldPath = video.LocalFilePath;
            if (string.IsNullOrEmpty(oldPath))
                return MapResult.Cancelled;

            var confirm = MessageBox.Show(owner,
                L.T("SvcLocalFileMap_D010", video.Title, oldPath),
                L.T("SvcLocalFileMap_D011"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
                return MapResult.Cancelled;

            // マップ解除中にキュー投入済みのタスクが残っていると、直後に書き込む Status=Paused が
            // 古いタスクの完了/失敗ハンドラに上書きされることがあるため、先にキャンセルしておく。
            // ただしCancelTaskは非同期I/Oの中断を待たないため、この対策自体が完全ではない
            // (完了処理と競合する可能性が残る)。そのため下のUpdateVideoは全カラムUPDATEの
            // UpdateVideo(video)ではなく、Unmapが本来触るべき4カラムだけに絞ったスコープ付き
            // UPDATEを使う。これにより、たとえ競合してもTags/Memo/Priority等の無関係な
            // フィールドまで巻き戻されることはない(LocalFilePath/Status等の食い違いは
            // 後勝ちのまま残るが、影響範囲をUnmapの意図した4カラムだけに限定できる)。
            downloadManager.CancelTask(video.VideoId);

            video.LocalFilePath = string.Empty;
            video.FileSize = 0;
            video.DownloadedAt = null;
            video.Status = DownloadStatus.Paused;
            database.UpdateVideoUnmapFields(video.Id, video.LocalFilePath, video.FileSize, video.DownloadedAt, video.Status);

            InvalidateDir(oldPath);

            return MapResult.Mapped;
        }
    }
}
