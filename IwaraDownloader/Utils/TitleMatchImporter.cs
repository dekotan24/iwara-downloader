using IwaraDownloader.Models;
using IwaraDownloader.Services;

namespace IwaraDownloader.Utils
{
    /// <summary>
    /// タイトル照合で見つかった (タグ無し) ファイルを、DB 上の既存動画に紐付けて
    /// ダウンロード完了扱いにする。ファイルは一切移動/コピー/削除しない。
    /// UI (ImportFromFolderWizard) から呼ばれるが、UI 非依存でテスト可能なように分離してある。
    /// </summary>
    public static class TitleMatchImporter
    {
        public class ImportOutcome
        {
            public bool TagWritten { get; init; }
            /// <summary>true なら FileUuid を iwara API から新規解決した (未DLで未取得だった場合)</summary>
            public bool ResolvedFileUuidFromApi { get; init; }
            /// <summary>API 解決を試みたが失敗した場合のエラー内容 (成否に関わらずマージ自体は続行する)</summary>
            public string? ApiError { get; init; }
        }

        /// <summary>
        /// 1件をマージする。
        /// video.Id が 0 (DB未登録。例: 未購読アーティストを iwara API から直接取得した場合) なら
        /// AddVideo で新規追加し、そうでなければ既存行を UpdateVideo で更新する。
        /// subUser を渡した場合はそれを優先して SubscribedUserId/AuthorUserId/AuthorUsername を補完する。
        /// 渡さなかった場合でも、動画の作者(AuthorUsername)が判明していれば
        /// EnsureChannelForAuthor で必ずチャンネルへ紐付ける(未購読アーティストのみ)。
        /// </summary>
        public static async Task<ImportOutcome> ImportOneAsync(
            VideoInfo existingVideo, string filePath, IwaraApiService api, DatabaseService database,
            SubscribedUser? subUser = null)
        {
            var resolvedFileUuid = existingVideo.FileUuid;
            bool resolvedFromApi = false;
            string? apiError = null;

            // 未DL動画は購読一覧取得APIの応答に file_id が含まれないため FileUuid が空のことが多い。
            // タグ書き込み・今後の再リンク照合のため、可能なら download-url API で解決しておく。
            if (string.IsNullOrEmpty(resolvedFileUuid))
            {
                try
                {
                    var site = string.IsNullOrEmpty(existingVideo.Site) ? null : existingVideo.Site;
                    var info = await api.GetDownloadUrlAsync(existingVideo.VideoId, site);
                    if (info.Success && !string.IsNullOrEmpty(info.FileUuid))
                    {
                        resolvedFileUuid = info.FileUuid;
                        resolvedFromApi = true;
                    }
                    else
                    {
                        apiError = info.Error ?? "unknown";
                    }
                }
                catch (Exception ex)
                {
                    // iwara から動画が削除済み (404) 等でも、ローカルファイルが唯一のコピーである
                    // 価値ある取り込み対象なので、API 失敗を理由にマージ自体は諦めない。
                    // タグ書き込みだけ諦めて DB 上のリンクは成立させる。
                    apiError = ex.Message;
                }
            }

            bool tagWritten = false;
            if (!string.IsNullOrEmpty(resolvedFileUuid))
            {
                // タグ書き込みでファイルサイズが変わるため、FileSize 取得より必ず先に行う
                tagWritten = MetadataService.WriteIwaraTags(filePath, existingVideo.VideoId, resolvedFileUuid);
            }

            try { existingVideo.FileSize = new FileInfo(filePath).Length; } catch { }
            existingVideo.LocalFilePath = filePath;
            existingVideo.Status = DownloadStatus.Completed;
            existingVideo.FileUuid = resolvedFileUuid;
            // 過去分をまとめて取り込んだ日を「今日」にすると統計ダッシュボードの日別グラフが
            // 偽のスパイクになるため、ファイルの実更新日時を使う
            existingVideo.DownloadedAt = SafeLastWriteTime(filePath);

            if (subUser != null)
            {
                if (!existingVideo.SubscribedUserId.HasValue) existingVideo.SubscribedUserId = subUser.Id;
                if (string.IsNullOrEmpty(existingVideo.AuthorUserId)) existingVideo.AuthorUserId = subUser.UserId;
                if (string.IsNullOrEmpty(existingVideo.AuthorUsername)) existingVideo.AuthorUsername = subUser.Username;
            }
            else if (!existingVideo.SubscribedUserId.HasValue && !string.IsNullOrEmpty(existingVideo.AuthorUsername))
            {
                // 呼び出し側が作者の SubscribedUser を解決していない場合(未購読アーティストの
                // タイトル照合等)も、作者が判明していれば必ずチャンネルへ紐付ける。
                // 既に購読済みならそこへ合流、未購読なら自動チェックOFFの新規チャンネルとして作成する。
                var channel = database.EnsureChannelForAuthor(existingVideo.AuthorUsername, existingVideo.Site);
                existingVideo.SubscribedUserId = channel.Id;
                if (string.IsNullOrEmpty(existingVideo.AuthorUserId)) existingVideo.AuthorUserId = channel.UserId;
            }

            if (existingVideo.Id == 0)
            {
                existingVideo.CreatedAt = DateTime.Now;
                existingVideo.Id = database.AddVideo(existingVideo);
            }
            else
            {
                database.UpdateVideo(existingVideo);
            }

            return new ImportOutcome
            {
                TagWritten = tagWritten,
                ResolvedFileUuidFromApi = resolvedFromApi,
                ApiError = apiError,
            };
        }

        private static DateTime SafeLastWriteTime(string path)
        {
            try { return File.GetLastWriteTime(path); }
            catch { return DateTime.Now; }
        }
    }
}
