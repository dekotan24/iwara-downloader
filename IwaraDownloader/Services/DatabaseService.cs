using Microsoft.Data.Sqlite;
using IwaraDownloader.Models;

namespace IwaraDownloader.Services
{
    /// <summary>
    /// SQLiteデータベースサービス
    /// </summary>
    public class DatabaseService : IDisposable
    {
        private readonly string _connectionString;
        private static DatabaseService? _instance;
        private static readonly object _lock = new();

        /// <summary>シングルトンインスタンス</summary>
        public static DatabaseService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new DatabaseService();
                    }
                }
                return _instance;
            }
        }

        private DatabaseService()
        {
            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "IwaraDownloader",
                "data.db");

            var directory = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Pooling=true で接続再利用、Cache=Shared で WAL の効果を最大化
            _connectionString = $"Data Source={dbPath};Pooling=True;Cache=Shared";
            InitializeDatabase();
        }

        /// <summary>
        /// データベースを初期化
        /// </summary>
        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // WAL モード有効化: 並行書込/読込で "database is locked" を減らす。
            // synchronous=NORMAL は WAL と組み合わせて電源断耐性と速度のバランスを取る。
            using (var pragma = connection.CreateCommand())
            {
                pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000;";
                pragma.ExecuteNonQuery();
            }

            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS SubscribedUsers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId TEXT NOT NULL UNIQUE,
                    Username TEXT NOT NULL,
                    ProfileUrl TEXT,
                    ThumbnailUrl TEXT,
                    LocalThumbnailPath TEXT,
                    CreatedAt TEXT NOT NULL,
                    LastCheckedAt TEXT,
                    DownloadedCount INTEGER DEFAULT 0,
                    TotalVideoCount INTEGER DEFAULT 0,
                    IsEnabled INTEGER DEFAULT 1,
                    CustomSavePath TEXT DEFAULT '',
                    DownloadExternalVideosOverride INTEGER NULL,
                    VideosLoaded INTEGER DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS Videos (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    VideoId TEXT NOT NULL UNIQUE,
                    Title TEXT NOT NULL,
                    Url TEXT NOT NULL,
                    ThumbnailUrl TEXT,
                    LocalThumbnailPath TEXT,
                    AuthorUserId TEXT,
                    AuthorUsername TEXT,
                    DurationSeconds INTEGER DEFAULT 0,
                    PostedAt TEXT,
                    LocalFilePath TEXT,
                    FileSize INTEGER DEFAULT 0,
                    Status INTEGER DEFAULT 0,
                    DownloadedAt TEXT,
                    SubscribedUserId INTEGER,
                    RetryCount INTEGER DEFAULT 0,
                    LastErrorMessage TEXT,
                    CreatedAt TEXT NOT NULL,
                    Tags TEXT DEFAULT '',
                    Memo TEXT DEFAULT '',
                    FileUuid TEXT DEFAULT '',
                    EmbedUrl TEXT DEFAULT '',
                    Rating TEXT DEFAULT '',
                    IsFavorite INTEGER DEFAULT 0,
                    FOREIGN KEY (SubscribedUserId) REFERENCES SubscribedUsers(Id) ON DELETE SET NULL
                );

                CREATE INDEX IF NOT EXISTS idx_videos_status ON Videos(Status);
                CREATE INDEX IF NOT EXISTS idx_videos_subscribed_user ON Videos(SubscribedUserId);
                CREATE INDEX IF NOT EXISTS idx_videos_video_id ON Videos(VideoId);
                CREATE INDEX IF NOT EXISTS idx_videos_file_uuid ON Videos(FileUuid);
                -- Rating カラムのインデックスは MigrateVideosTable 内 (ALTER TABLE 後) で作成する
            ";
            command.ExecuteNonQuery();

            // マイグレーション: CustomSavePathカラムを追加(既存DBの場合)
            MigrateDatabase(connection);

            // マイグレーション結果の最終検証: 期待するカラムが全て存在するか確認
            VerifyRequiredColumns(connection);
        }

        /// <summary>
        /// 期待するカラムが Videos テーブルに存在するかを検証し、
        /// 不足していれば強制的に ALTER TABLE を流す。
        /// MigrateVideosTable が何らかの理由で空振りした場合の保険。
        /// </summary>
        private void VerifyRequiredColumns(SqliteConnection connection)
        {
            try
            {
                // FileUuid カラムの存在を SELECT で確認 (最も確実な方法)
                var check = connection.CreateCommand();
                check.CommandText = "SELECT FileUuid FROM Videos LIMIT 0";
                check.ExecuteNonQuery();
            }
            catch (SqliteException ex) when (ex.Message.Contains("no such column"))
            {
                LoggingService.Instance.Warn("FileUuid カラムが不足しています。強制マイグレーションを実行します。");
                try
                {
                    var alterCmd = connection.CreateCommand();
                    alterCmd.CommandText = "ALTER TABLE Videos ADD COLUMN FileUuid TEXT DEFAULT ''";
                    alterCmd.ExecuteNonQuery();

                    var indexCmd = connection.CreateCommand();
                    indexCmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_videos_file_uuid ON Videos(FileUuid)";
                    indexCmd.ExecuteNonQuery();

                    LoggingService.Instance.Info("強制マイグレーション成功: FileUuid カラムを追加しました");
                }
                catch (Exception inner)
                {
                    LoggingService.Instance.Error($"強制マイグレーション失敗: {inner.Message}");
                    throw;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error($"マイグレーション検証で予期しないエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// データベースマイグレーション
        /// </summary>
        private void MigrateDatabase(SqliteConnection connection)
        {
            // SubscribedUsers の列をPRAGMAで一括検査
            bool hasCustomSavePath = false;
            bool hasDownloadExternalOverride = false;
            bool hasSubSite = false;
            bool hasVideosLoaded = false;

            var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "PRAGMA table_info(SubscribedUsers)";
            using (var reader = checkCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var columnName = reader.GetString(1);
                    if (columnName == "CustomSavePath") hasCustomSavePath = true;
                    if (columnName == "DownloadExternalVideosOverride") hasDownloadExternalOverride = true;
                    if (columnName == "Site") hasSubSite = true;
                    if (columnName == "VideosLoaded") hasVideosLoaded = true;
                }
            }

            AddColumnIfMissing(connection, hasCustomSavePath,
                "ALTER TABLE SubscribedUsers ADD COLUMN CustomSavePath TEXT DEFAULT ''", "CustomSavePath");
            AddColumnIfMissing(connection, hasDownloadExternalOverride,
                "ALTER TABLE SubscribedUsers ADD COLUMN DownloadExternalVideosOverride INTEGER NULL", "DownloadExternalVideosOverride");
            AddColumnIfMissing(connection, hasSubSite,
                "ALTER TABLE SubscribedUsers ADD COLUMN Site TEXT DEFAULT ''", "SubscribedUsers.Site");
            // 既存ユーザーは動画取得済みとみなす (DEFAULT 1)
            AddColumnIfMissing(connection, hasVideosLoaded,
                "ALTER TABLE SubscribedUsers ADD COLUMN VideosLoaded INTEGER DEFAULT 1", "VideosLoaded");

            // VideosテーブルのTags/Memo/FileUuid/EmbedUrl/Rating/Site カラムマイグレーション
            MigrateVideosTable(connection);
        }

        /// <summary>
        /// Videosテーブルのマイグレーション
        /// </summary>
        private void MigrateVideosTable(SqliteConnection connection)
        {
            bool hasTags = false;
            bool hasMemo = false;
            bool hasFileUuid = false;
            bool hasEmbedUrl = false;
            bool hasRating = false;
            bool hasSite = false;
            bool hasIsFavorite = false;
            bool hasThumbnailStatus = false;

            try
            {
                var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = "PRAGMA table_info(Videos)";
                using var reader = checkCmd.ExecuteReader();
                while (reader.Read())
                {
                    var columnName = reader.GetString(1);
                    if (columnName == "Tags") hasTags = true;
                    if (columnName == "Memo") hasMemo = true;
                    if (columnName == "FileUuid") hasFileUuid = true;
                    if (columnName == "EmbedUrl") hasEmbedUrl = true;
                    if (columnName == "Rating") hasRating = true;
                    if (columnName == "Site") hasSite = true;
                    if (columnName == "IsFavorite") hasIsFavorite = true;
                    if (columnName == "ThumbnailStatus") hasThumbnailStatus = true;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error($"Videosテーブルのスキーマ検査に失敗しました: {ex.Message}");
                throw;
            }

            AddColumnIfMissing(connection, hasTags, "ALTER TABLE Videos ADD COLUMN Tags TEXT DEFAULT ''", "Tags");
            AddColumnIfMissing(connection, hasMemo, "ALTER TABLE Videos ADD COLUMN Memo TEXT DEFAULT ''", "Memo");
            AddColumnIfMissing(connection, hasEmbedUrl, "ALTER TABLE Videos ADD COLUMN EmbedUrl TEXT DEFAULT ''", "EmbedUrl");

            if (!hasFileUuid)
            {
                AddColumnIfMissing(connection, false, "ALTER TABLE Videos ADD COLUMN FileUuid TEXT DEFAULT ''", "FileUuid");
                try
                {
                    var indexCmd = connection.CreateCommand();
                    indexCmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_videos_file_uuid ON Videos(FileUuid)";
                    indexCmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.Error($"FileUuid インデックスの作成に失敗しました: {ex.Message}");
                }
            }

            if (!hasRating)
            {
                AddColumnIfMissing(connection, false, "ALTER TABLE Videos ADD COLUMN Rating TEXT DEFAULT ''", "Rating");
                try
                {
                    var indexCmd = connection.CreateCommand();
                    indexCmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_videos_rating ON Videos(Rating)";
                    indexCmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.Error($"Rating インデックスの作成に失敗しました: {ex.Message}");
                }
            }

            // Site カラム (www.iwara.tv / www.iwara.ai 判別用)
            AddColumnIfMissing(connection, hasSite,
                "ALTER TABLE Videos ADD COLUMN Site TEXT DEFAULT ''", "Videos.Site");

            // IsFavorite カラム (お気に入りフラグ)
            if (!hasIsFavorite)
            {
                AddColumnIfMissing(connection, false, "ALTER TABLE Videos ADD COLUMN IsFavorite INTEGER DEFAULT 0", "IsFavorite");
                try
                {
                    var indexCmd = connection.CreateCommand();
                    indexCmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_videos_is_favorite ON Videos(IsFavorite)";
                    indexCmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.Error($"IsFavorite インデックスの作成に失敗しました: {ex.Message}");
                }
            }

            // ThumbnailStatus カラム (0=未試行, 1=キャッシュ済, 2=失敗)
            AddColumnIfMissing(connection, hasThumbnailStatus,
                "ALTER TABLE Videos ADD COLUMN ThumbnailStatus INTEGER DEFAULT 0", "ThumbnailStatus");
        }

        private static void AddColumnIfMissing(SqliteConnection connection, bool exists, string alterSql, string columnName)
        {
            if (exists) return;
            try
            {
                var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = alterSql;
                alterCmd.ExecuteNonQuery();
                LoggingService.Instance.Info($"DB マイグレーション: {columnName} カラムを追加しました");
            }
            catch (SqliteException ex) when (ex.Message.Contains("duplicate column"))
            {
                // 既に存在する場合は無視
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error($"{columnName} カラムの追加に失敗しました: {ex.Message}");
                throw;
            }
        }

        #region SubscribedUsers CRUD

        /// <summary>
        /// 購読ユーザーを追加
        /// </summary>
        public int AddSubscribedUser(SubscribedUser user)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO SubscribedUsers (UserId, Username, ProfileUrl, ThumbnailUrl, LocalThumbnailPath, CreatedAt, IsEnabled, CustomSavePath, DownloadExternalVideosOverride, Site, VideosLoaded)
                VALUES (@UserId, @Username, @ProfileUrl, @ThumbnailUrl, @LocalThumbnailPath, @CreatedAt, @IsEnabled, @CustomSavePath, @DownloadExternalVideosOverride, @Site, @VideosLoaded);
                SELECT last_insert_rowid();
            ";
            command.Parameters.AddWithValue("@UserId", user.UserId);
            command.Parameters.AddWithValue("@Username", user.Username);
            command.Parameters.AddWithValue("@ProfileUrl", user.ProfileUrl ?? "");
            command.Parameters.AddWithValue("@ThumbnailUrl", user.ThumbnailUrl ?? "");
            command.Parameters.AddWithValue("@LocalThumbnailPath", user.LocalThumbnailPath ?? "");
            command.Parameters.AddWithValue("@CreatedAt", user.CreatedAt.ToString("o"));
            command.Parameters.AddWithValue("@IsEnabled", user.IsEnabled ? 1 : 0);
            command.Parameters.AddWithValue("@CustomSavePath", user.CustomSavePath ?? "");
            command.Parameters.AddWithValue("@DownloadExternalVideosOverride",
                user.DownloadExternalVideosOverride.HasValue ? (object)(user.DownloadExternalVideosOverride.Value ? 1 : 0) : DBNull.Value);
            command.Parameters.AddWithValue("@Site", user.Site ?? "");
            command.Parameters.AddWithValue("@VideosLoaded", user.VideosLoaded ? 1 : 0);

            return Convert.ToInt32(command.ExecuteScalar());
        }

        /// <summary>
        /// 購読ユーザーを更新
        /// </summary>
        public void UpdateSubscribedUser(SubscribedUser user)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE SubscribedUsers SET
                    Username = @Username,
                    ProfileUrl = @ProfileUrl,
                    ThumbnailUrl = @ThumbnailUrl,
                    LocalThumbnailPath = @LocalThumbnailPath,
                    LastCheckedAt = @LastCheckedAt,
                    DownloadedCount = @DownloadedCount,
                    TotalVideoCount = @TotalVideoCount,
                    IsEnabled = @IsEnabled,
                    CustomSavePath = @CustomSavePath,
                    DownloadExternalVideosOverride = @DownloadExternalVideosOverride,
                    Site = @Site,
                    VideosLoaded = @VideosLoaded
                WHERE Id = @Id
            ";
            command.Parameters.AddWithValue("@Id", user.Id);
            command.Parameters.AddWithValue("@Username", user.Username);
            command.Parameters.AddWithValue("@ProfileUrl", user.ProfileUrl ?? "");
            command.Parameters.AddWithValue("@ThumbnailUrl", user.ThumbnailUrl ?? "");
            command.Parameters.AddWithValue("@LocalThumbnailPath", user.LocalThumbnailPath ?? "");
            command.Parameters.AddWithValue("@LastCheckedAt", user.LastCheckedAt?.ToString("o") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@DownloadedCount", user.DownloadedCount);
            command.Parameters.AddWithValue("@TotalVideoCount", user.TotalVideoCount);
            command.Parameters.AddWithValue("@IsEnabled", user.IsEnabled ? 1 : 0);
            command.Parameters.AddWithValue("@CustomSavePath", user.CustomSavePath ?? "");
            command.Parameters.AddWithValue("@DownloadExternalVideosOverride",
                user.DownloadExternalVideosOverride.HasValue ? (object)(user.DownloadExternalVideosOverride.Value ? 1 : 0) : DBNull.Value);
            command.Parameters.AddWithValue("@Site", user.Site ?? "");
            command.Parameters.AddWithValue("@VideosLoaded", user.VideosLoaded ? 1 : 0);

            command.ExecuteNonQuery();
        }

        /// <summary>
        /// 購読ユーザーを削除
        /// </summary>
        public void DeleteSubscribedUser(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM SubscribedUsers WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", id);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// 全ての購読ユーザーを取得
        /// </summary>
        public List<SubscribedUser> GetAllSubscribedUsers()
        {
            var users = new List<SubscribedUser>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM SubscribedUsers ORDER BY CreatedAt DESC";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                users.Add(ReadSubscribedUser(reader));
            }
            return users;
        }

        /// <summary>
        /// 有効な購読ユーザーを取得
        /// </summary>
        public List<SubscribedUser> GetEnabledSubscribedUsers()
        {
            var users = new List<SubscribedUser>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM SubscribedUsers WHERE IsEnabled = 1 ORDER BY CreatedAt DESC";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                users.Add(ReadSubscribedUser(reader));
            }
            return users;
        }

        /// <summary>
        /// 購読ユーザーをUserIdで取得
        /// </summary>
        public SubscribedUser? GetSubscribedUserByUserId(string userId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM SubscribedUsers WHERE UserId = @UserId";
            command.Parameters.AddWithValue("@UserId", userId);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return ReadSubscribedUser(reader);
            }
            return null;
        }

        /// <summary>
        /// 購読ユーザーをIDで取得
        /// </summary>
        public SubscribedUser? GetSubscribedUserById(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM SubscribedUsers WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", id);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return ReadSubscribedUser(reader);
            }
            return null;
        }

        private static SubscribedUser ReadSubscribedUser(SqliteDataReader reader)
        {
            var user = new SubscribedUser
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                UserId = reader.GetString(reader.GetOrdinal("UserId")),
                Username = reader.GetString(reader.GetOrdinal("Username")),
                ProfileUrl = reader.IsDBNull(reader.GetOrdinal("ProfileUrl")) ? "" : reader.GetString(reader.GetOrdinal("ProfileUrl")),
                ThumbnailUrl = reader.IsDBNull(reader.GetOrdinal("ThumbnailUrl")) ? "" : reader.GetString(reader.GetOrdinal("ThumbnailUrl")),
                LocalThumbnailPath = reader.IsDBNull(reader.GetOrdinal("LocalThumbnailPath")) ? "" : reader.GetString(reader.GetOrdinal("LocalThumbnailPath")),
                CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt"))),
                LastCheckedAt = reader.IsDBNull(reader.GetOrdinal("LastCheckedAt")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("LastCheckedAt"))),
                DownloadedCount = reader.GetInt32(reader.GetOrdinal("DownloadedCount")),
                TotalVideoCount = reader.GetInt32(reader.GetOrdinal("TotalVideoCount")),
                IsEnabled = reader.GetInt32(reader.GetOrdinal("IsEnabled")) == 1
            };

            // CustomSavePathカラムが存在する場合のみ読み取り(マイグレーション対応)
            try
            {
                var ordinal = reader.GetOrdinal("CustomSavePath");
                user.CustomSavePath = reader.IsDBNull(ordinal) ? "" : reader.GetString(ordinal);
            }
            catch { user.CustomSavePath = ""; }

            // DownloadExternalVideosOverride カラム(マイグレーション対応)
            try
            {
                var ordinal = reader.GetOrdinal("DownloadExternalVideosOverride");
                user.DownloadExternalVideosOverride = reader.IsDBNull(ordinal)
                    ? (bool?)null
                    : reader.GetInt32(ordinal) == 1;
            }
            catch { user.DownloadExternalVideosOverride = null; }

            // Site カラム (iwara.tv / iwara.ai 判別)
            try
            {
                var ordinal = reader.GetOrdinal("Site");
                user.Site = reader.IsDBNull(ordinal) ? "" : reader.GetString(ordinal);
            }
            catch { user.Site = ""; }

            // VideosLoaded カラム (仮登録フラグ)
            try
            {
                var ordinal = reader.GetOrdinal("VideosLoaded");
                user.VideosLoaded = !reader.IsDBNull(ordinal) && reader.GetInt32(ordinal) == 1;
            }
            catch { user.VideosLoaded = true; } // 旧DBは取得済みとみなす

            return user;
        }

        /// <summary>
        /// 動画一覧が未取得のユーザーを取得 (起動時の再キュー用)
        /// </summary>
        public List<SubscribedUser> GetUsersWithVideosNotLoaded()
        {
            var users = new List<SubscribedUser>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM SubscribedUsers WHERE VideosLoaded = 0 AND IsEnabled = 1 ORDER BY CreatedAt ASC";
            using var reader = command.ExecuteReader();
            while (reader.Read())
                users.Add(ReadSubscribedUser(reader));
            return users;
        }

        #endregion

        #region Videos CRUD

        /// <summary>
        /// 動画を追加
        /// </summary>
        public int AddVideo(VideoInfo video)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Videos (VideoId, Title, Url, ThumbnailUrl, LocalThumbnailPath, AuthorUserId, AuthorUsername,
                    DurationSeconds, PostedAt, LocalFilePath, FileSize, Status, DownloadedAt, SubscribedUserId,
                    RetryCount, LastErrorMessage, CreatedAt, Tags, Memo, FileUuid, EmbedUrl, Rating, Site, IsFavorite, ThumbnailStatus)
                VALUES (@VideoId, @Title, @Url, @ThumbnailUrl, @LocalThumbnailPath, @AuthorUserId, @AuthorUsername,
                    @DurationSeconds, @PostedAt, @LocalFilePath, @FileSize, @Status, @DownloadedAt, @SubscribedUserId,
                    @RetryCount, @LastErrorMessage, @CreatedAt, @Tags, @Memo, @FileUuid, @EmbedUrl, @Rating, @Site, @IsFavorite, @ThumbnailStatus);
                SELECT last_insert_rowid();
            ";
            AddVideoParameters(command, video);
            command.Parameters.AddWithValue("@CreatedAt", video.CreatedAt.ToString("o"));

            return Convert.ToInt32(command.ExecuteScalar());
        }

        /// <summary>
        /// 動画を更新
        /// </summary>
        public void UpdateVideo(VideoInfo video)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Videos SET
                    Title = @Title,
                    Url = @Url,
                    ThumbnailUrl = @ThumbnailUrl,
                    LocalThumbnailPath = @LocalThumbnailPath,
                    AuthorUserId = @AuthorUserId,
                    AuthorUsername = @AuthorUsername,
                    DurationSeconds = @DurationSeconds,
                    PostedAt = @PostedAt,
                    LocalFilePath = @LocalFilePath,
                    FileSize = @FileSize,
                    Status = @Status,
                    DownloadedAt = @DownloadedAt,
                    SubscribedUserId = @SubscribedUserId,
                    RetryCount = @RetryCount,
                    LastErrorMessage = @LastErrorMessage,
                    Tags = @Tags,
                    Memo = @Memo,
                    FileUuid = @FileUuid,
                    EmbedUrl = @EmbedUrl,
                    Rating = @Rating,
                    Site = @Site,
                    IsFavorite = @IsFavorite,
                    ThumbnailStatus = @ThumbnailStatus
                WHERE Id = @Id
            ";
            command.Parameters.AddWithValue("@Id", video.Id);
            AddVideoParameters(command, video);

            command.ExecuteNonQuery();
        }

        /// <summary>
        /// サムネイル取得ステータスだけを高速に更新する。
        /// </summary>
        public void UpdateThumbnailStatus(int videoId, int status)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "UPDATE Videos SET ThumbnailStatus = @Status WHERE Id = @Id";
            command.Parameters.AddWithValue("@Status", status);
            command.Parameters.AddWithValue("@Id", videoId);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// VideoId でサムネイル取得ステータスを更新する。
        /// </summary>
        public void UpdateThumbnailStatusByVideoId(string videoId, int status)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "UPDATE Videos SET ThumbnailStatus = @Status WHERE VideoId = @VideoId";
            command.Parameters.AddWithValue("@Status", status);
            command.Parameters.AddWithValue("@VideoId", videoId);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// お気に入りフラグだけを高速に更新する(他カラムを触らない単発 UPDATE)。
        /// </summary>
        public void SetVideoFavorite(int videoId, bool isFavorite)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "UPDATE Videos SET IsFavorite = @IsFavorite WHERE Id = @Id";
            command.Parameters.AddWithValue("@IsFavorite", isFavorite ? 1 : 0);
            command.Parameters.AddWithValue("@Id", videoId);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// FileUuid で動画を検索(ローカルに既に存在する動画の検出用)
        /// </summary>
        public VideoInfo? GetVideoByFileUuid(string fileUuid)
        {
            if (string.IsNullOrEmpty(fileUuid)) return null;

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Videos WHERE FileUuid = @FileUuid LIMIT 1";
            command.Parameters.AddWithValue("@FileUuid", fileUuid);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return ReadVideo(reader);
            }
            return null;
        }

        /// <summary>
        /// 動画を削除
        /// </summary>
        public void DeleteVideo(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Videos WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", id);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// 全ての動画を取得
        /// </summary>
        public List<VideoInfo> GetAllVideos()
        {
            var videos = new List<VideoInfo>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Videos ORDER BY CreatedAt DESC";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                videos.Add(ReadVideo(reader));
            }
            return videos;
        }

        /// <summary>
        /// 購読ユーザーの動画を取得
        /// </summary>
        public List<VideoInfo> GetVideosBySubscribedUser(int subscribedUserId)
        {
            var videos = new List<VideoInfo>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Videos WHERE SubscribedUserId = @SubscribedUserId ORDER BY PostedAt DESC";
            command.Parameters.AddWithValue("@SubscribedUserId", subscribedUserId);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                videos.Add(ReadVideo(reader));
            }
            return videos;
        }

        /// <summary>
        /// ステータスで動画を取得
        /// </summary>
        public List<VideoInfo> GetVideosByStatus(DownloadStatus status)
        {
            var videos = new List<VideoInfo>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Videos WHERE Status = @Status ORDER BY CreatedAt DESC";
            command.Parameters.AddWithValue("@Status", (int)status);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                videos.Add(ReadVideo(reader));
            }
            return videos;
        }

        /// <summary>
        /// Status 一括変更 (例: Downloading → Pending を再起動時にまとめて行う)。
        /// VideoInfo を1件ずつ UPDATE するより桁違いに速い。
        /// </summary>
        public int BulkUpdateStatus(DownloadStatus from, DownloadStatus to)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "UPDATE Videos SET Status = @to WHERE Status = @from";
            command.Parameters.AddWithValue("@to", (int)to);
            command.Parameters.AddWithValue("@from", (int)from);
            return command.ExecuteNonQuery();
        }

        /// <summary>
        /// 動画をVideoIdで取得
        /// </summary>
        public VideoInfo? GetVideoByVideoId(string videoId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Videos WHERE VideoId = @VideoId";
            command.Parameters.AddWithValue("@VideoId", videoId);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return ReadVideo(reader);
            }
            return null;
        }

        /// <summary>
        /// 動画をIDで取得
        /// </summary>
        public VideoInfo? GetVideoById(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Videos WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", id);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return ReadVideo(reader);
            }
            return null;
        }

        /// <summary>
        /// 動画が既に存在するか確認
        /// </summary>
        public bool VideoExists(string videoId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Videos WHERE VideoId = @VideoId";
            command.Parameters.AddWithValue("@VideoId", videoId);

            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }

        /// <summary>
        /// リトライ対象の動画を取得(失敗かつリトライ回数が上限未満)
        /// </summary>
        public List<VideoInfo> GetRetryableVideos(int maxRetryCount)
        {
            var videos = new List<VideoInfo>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT * FROM Videos 
                WHERE Status = @Status AND RetryCount < @MaxRetryCount 
                ORDER BY CreatedAt ASC";
            command.Parameters.AddWithValue("@Status", (int)DownloadStatus.Failed);
            command.Parameters.AddWithValue("@MaxRetryCount", maxRetryCount);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                videos.Add(ReadVideo(reader));
            }
            return videos;
        }

        private static void AddVideoParameters(SqliteCommand command, VideoInfo video)
        {
            command.Parameters.AddWithValue("@VideoId", video.VideoId);
            command.Parameters.AddWithValue("@Title", video.Title);
            command.Parameters.AddWithValue("@Url", video.Url);
            command.Parameters.AddWithValue("@ThumbnailUrl", video.ThumbnailUrl ?? "");
            command.Parameters.AddWithValue("@LocalThumbnailPath", video.LocalThumbnailPath ?? "");
            command.Parameters.AddWithValue("@AuthorUserId", video.AuthorUserId ?? "");
            command.Parameters.AddWithValue("@AuthorUsername", video.AuthorUsername ?? "");
            command.Parameters.AddWithValue("@DurationSeconds", video.DurationSeconds);
            command.Parameters.AddWithValue("@PostedAt", video.PostedAt?.ToString("o") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@LocalFilePath", video.LocalFilePath ?? "");
            command.Parameters.AddWithValue("@FileSize", video.FileSize);
            command.Parameters.AddWithValue("@Status", (int)video.Status);
            command.Parameters.AddWithValue("@DownloadedAt", video.DownloadedAt?.ToString("o") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@SubscribedUserId", video.SubscribedUserId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@RetryCount", video.RetryCount);
            command.Parameters.AddWithValue("@LastErrorMessage", video.LastErrorMessage ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Tags", video.Tags ?? "");
            command.Parameters.AddWithValue("@Memo", video.Memo ?? "");
            command.Parameters.AddWithValue("@FileUuid", video.FileUuid ?? "");
            command.Parameters.AddWithValue("@EmbedUrl", video.EmbedUrl ?? "");
            command.Parameters.AddWithValue("@Rating", video.Rating ?? "");
            command.Parameters.AddWithValue("@Site", video.Site ?? "");
            command.Parameters.AddWithValue("@IsFavorite", video.IsFavorite ? 1 : 0);
            command.Parameters.AddWithValue("@ThumbnailStatus", video.ThumbnailStatus);
        }

        private static VideoInfo ReadVideo(SqliteDataReader reader)
        {
            return new VideoInfo
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                VideoId = reader.GetString(reader.GetOrdinal("VideoId")),
                Title = reader.GetString(reader.GetOrdinal("Title")),
                Url = reader.GetString(reader.GetOrdinal("Url")),
                ThumbnailUrl = reader.IsDBNull(reader.GetOrdinal("ThumbnailUrl")) ? "" : reader.GetString(reader.GetOrdinal("ThumbnailUrl")),
                LocalThumbnailPath = reader.IsDBNull(reader.GetOrdinal("LocalThumbnailPath")) ? "" : reader.GetString(reader.GetOrdinal("LocalThumbnailPath")),
                AuthorUserId = reader.IsDBNull(reader.GetOrdinal("AuthorUserId")) ? "" : reader.GetString(reader.GetOrdinal("AuthorUserId")),
                AuthorUsername = reader.IsDBNull(reader.GetOrdinal("AuthorUsername")) ? "" : reader.GetString(reader.GetOrdinal("AuthorUsername")),
                DurationSeconds = reader.GetInt32(reader.GetOrdinal("DurationSeconds")),
                PostedAt = reader.IsDBNull(reader.GetOrdinal("PostedAt")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("PostedAt"))),
                LocalFilePath = reader.IsDBNull(reader.GetOrdinal("LocalFilePath")) ? "" : reader.GetString(reader.GetOrdinal("LocalFilePath")),
                FileSize = reader.GetInt64(reader.GetOrdinal("FileSize")),
                Status = (DownloadStatus)reader.GetInt32(reader.GetOrdinal("Status")),
                DownloadedAt = reader.IsDBNull(reader.GetOrdinal("DownloadedAt")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("DownloadedAt"))),
                SubscribedUserId = reader.IsDBNull(reader.GetOrdinal("SubscribedUserId")) ? null : reader.GetInt32(reader.GetOrdinal("SubscribedUserId")),
                RetryCount = reader.GetInt32(reader.GetOrdinal("RetryCount")),
                LastErrorMessage = reader.IsDBNull(reader.GetOrdinal("LastErrorMessage")) ? null : reader.GetString(reader.GetOrdinal("LastErrorMessage")),
                CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt"))),
                Tags = TryGetString(reader, "Tags"),
                Memo = TryGetString(reader, "Memo"),
                FileUuid = TryGetString(reader, "FileUuid"),
                EmbedUrl = TryGetString(reader, "EmbedUrl"),
                Rating = TryGetString(reader, "Rating"),
                Site = TryGetString(reader, "Site"),
                IsFavorite = TryGetInt(reader, "IsFavorite") == 1,
                ThumbnailStatus = TryGetInt(reader, "ThumbnailStatus")
            };
        }

        /// <summary>
        /// カラムが存在する場合のみ整数を取得(マイグレーション対応)。無ければ 0。
        /// </summary>
        private static int TryGetInt(SqliteDataReader reader, string columnName)
        {
            try
            {
                var ordinal = reader.GetOrdinal(columnName);
                return reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// カラムが存在する場合のみ文字列を取得(マイグレーション対応)
        /// </summary>
        private static string TryGetString(SqliteDataReader reader, string columnName)
        {
            try
            {
                var ordinal = reader.GetOrdinal(columnName);
                return reader.IsDBNull(ordinal) ? "" : reader.GetString(ordinal);
            }
            catch
            {
                return "";
            }
        }

        #endregion

        #region Batch Operations

        /// <summary>
        /// 複数の動画を一括追加(トランザクション使用で高速化)
        /// </summary>
        /// <param name="videos">追加する動画リスト</param>
        /// <returns>追加された動画数</returns>
        public int AddVideosBatch(IEnumerable<VideoInfo> videos)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();
            int addedCount = 0;

            try
            {
                var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"
                    INSERT OR IGNORE INTO Videos (VideoId, Title, Url, ThumbnailUrl, LocalThumbnailPath, AuthorUserId, AuthorUsername,
                        DurationSeconds, PostedAt, LocalFilePath, FileSize, Status, DownloadedAt, SubscribedUserId,
                        RetryCount, LastErrorMessage, CreatedAt, Tags, Memo, FileUuid, EmbedUrl, Rating, Site, IsFavorite, ThumbnailStatus)
                    VALUES (@VideoId, @Title, @Url, @ThumbnailUrl, @LocalThumbnailPath, @AuthorUserId, @AuthorUsername,
                        @DurationSeconds, @PostedAt, @LocalFilePath, @FileSize, @Status, @DownloadedAt, @SubscribedUserId,
                        @RetryCount, @LastErrorMessage, @CreatedAt, @Tags, @Memo, @FileUuid, @EmbedUrl, @Rating, @Site, @IsFavorite, @ThumbnailStatus)
                ";

                // パラメータを作成(再利用)
                var pVideoId = command.Parameters.Add("@VideoId", SqliteType.Text);
                var pTitle = command.Parameters.Add("@Title", SqliteType.Text);
                var pUrl = command.Parameters.Add("@Url", SqliteType.Text);
                var pThumbnailUrl = command.Parameters.Add("@ThumbnailUrl", SqliteType.Text);
                var pLocalThumbnailPath = command.Parameters.Add("@LocalThumbnailPath", SqliteType.Text);
                var pAuthorUserId = command.Parameters.Add("@AuthorUserId", SqliteType.Text);
                var pAuthorUsername = command.Parameters.Add("@AuthorUsername", SqliteType.Text);
                var pDurationSeconds = command.Parameters.Add("@DurationSeconds", SqliteType.Integer);
                var pPostedAt = command.Parameters.Add("@PostedAt", SqliteType.Text);
                var pLocalFilePath = command.Parameters.Add("@LocalFilePath", SqliteType.Text);
                var pFileSize = command.Parameters.Add("@FileSize", SqliteType.Integer);
                var pStatus = command.Parameters.Add("@Status", SqliteType.Integer);
                var pDownloadedAt = command.Parameters.Add("@DownloadedAt", SqliteType.Text);
                var pSubscribedUserId = command.Parameters.Add("@SubscribedUserId", SqliteType.Integer);
                var pRetryCount = command.Parameters.Add("@RetryCount", SqliteType.Integer);
                var pLastErrorMessage = command.Parameters.Add("@LastErrorMessage", SqliteType.Text);
                var pCreatedAt = command.Parameters.Add("@CreatedAt", SqliteType.Text);
                var pTags = command.Parameters.Add("@Tags", SqliteType.Text);
                var pMemo = command.Parameters.Add("@Memo", SqliteType.Text);
                var pFileUuid = command.Parameters.Add("@FileUuid", SqliteType.Text);
                var pEmbedUrl = command.Parameters.Add("@EmbedUrl", SqliteType.Text);
                var pRating = command.Parameters.Add("@Rating", SqliteType.Text);
                var pSite = command.Parameters.Add("@Site", SqliteType.Text);
                var pIsFavorite = command.Parameters.Add("@IsFavorite", SqliteType.Integer);
                var pThumbnailStatus = command.Parameters.Add("@ThumbnailStatus", SqliteType.Integer);

                foreach (var video in videos)
                {
                    pVideoId.Value = video.VideoId;
                    pTitle.Value = video.Title;
                    pUrl.Value = video.Url;
                    pThumbnailUrl.Value = video.ThumbnailUrl ?? "";
                    pLocalThumbnailPath.Value = video.LocalThumbnailPath ?? "";
                    pAuthorUserId.Value = video.AuthorUserId ?? "";
                    pAuthorUsername.Value = video.AuthorUsername ?? "";
                    pDurationSeconds.Value = video.DurationSeconds;
                    pPostedAt.Value = video.PostedAt?.ToString("o") ?? (object)DBNull.Value;
                    pLocalFilePath.Value = video.LocalFilePath ?? "";
                    pFileSize.Value = video.FileSize;
                    pStatus.Value = (int)video.Status;
                    pDownloadedAt.Value = video.DownloadedAt?.ToString("o") ?? (object)DBNull.Value;
                    pSubscribedUserId.Value = video.SubscribedUserId ?? (object)DBNull.Value;
                    pRetryCount.Value = video.RetryCount;
                    pLastErrorMessage.Value = video.LastErrorMessage ?? (object)DBNull.Value;
                    pCreatedAt.Value = video.CreatedAt.ToString("o");
                    pTags.Value = video.Tags ?? "";
                    pMemo.Value = video.Memo ?? "";
                    pFileUuid.Value = video.FileUuid ?? "";
                    pEmbedUrl.Value = video.EmbedUrl ?? "";
                    pRating.Value = video.Rating ?? "";
                    pSite.Value = video.Site ?? "";
                    pIsFavorite.Value = video.IsFavorite ? 1 : 0;
                    pThumbnailStatus.Value = video.ThumbnailStatus;

                    addedCount += command.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }

            return addedCount;
        }

        /// <summary>
        /// 複数の動画を一括更新(トランザクション使用で高速化)
        /// </summary>
        /// <param name="videos">更新する動画リスト</param>
        /// <returns>更新された動画数</returns>
        public int UpdateVideosBatch(IEnumerable<VideoInfo> videos)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();
            int updatedCount = 0;

            try
            {
                var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"
                    UPDATE Videos SET
                        Title = @Title,
                        Url = @Url,
                        ThumbnailUrl = @ThumbnailUrl,
                        LocalThumbnailPath = @LocalThumbnailPath,
                        AuthorUserId = @AuthorUserId,
                        AuthorUsername = @AuthorUsername,
                        DurationSeconds = @DurationSeconds,
                        PostedAt = @PostedAt,
                        LocalFilePath = @LocalFilePath,
                        FileSize = @FileSize,
                        Status = @Status,
                        DownloadedAt = @DownloadedAt,
                        SubscribedUserId = @SubscribedUserId,
                        RetryCount = @RetryCount,
                        LastErrorMessage = @LastErrorMessage,
                        Tags = @Tags,
                        Memo = @Memo,
                        FileUuid = @FileUuid,
                        EmbedUrl = @EmbedUrl,
                        Rating = @Rating,
                        Site = @Site,
                        IsFavorite = @IsFavorite,
                        ThumbnailStatus = @ThumbnailStatus
                    WHERE Id = @Id
                ";

                // パラメータを作成(再利用)
                var pId = command.Parameters.Add("@Id", SqliteType.Integer);
                var pTitle = command.Parameters.Add("@Title", SqliteType.Text);
                var pUrl = command.Parameters.Add("@Url", SqliteType.Text);
                var pThumbnailUrl = command.Parameters.Add("@ThumbnailUrl", SqliteType.Text);
                var pLocalThumbnailPath = command.Parameters.Add("@LocalThumbnailPath", SqliteType.Text);
                var pAuthorUserId = command.Parameters.Add("@AuthorUserId", SqliteType.Text);
                var pAuthorUsername = command.Parameters.Add("@AuthorUsername", SqliteType.Text);
                var pDurationSeconds = command.Parameters.Add("@DurationSeconds", SqliteType.Integer);
                var pPostedAt = command.Parameters.Add("@PostedAt", SqliteType.Text);
                var pLocalFilePath = command.Parameters.Add("@LocalFilePath", SqliteType.Text);
                var pFileSize = command.Parameters.Add("@FileSize", SqliteType.Integer);
                var pStatus = command.Parameters.Add("@Status", SqliteType.Integer);
                var pDownloadedAt = command.Parameters.Add("@DownloadedAt", SqliteType.Text);
                var pSubscribedUserId = command.Parameters.Add("@SubscribedUserId", SqliteType.Integer);
                var pRetryCount = command.Parameters.Add("@RetryCount", SqliteType.Integer);
                var pLastErrorMessage = command.Parameters.Add("@LastErrorMessage", SqliteType.Text);
                var pTags = command.Parameters.Add("@Tags", SqliteType.Text);
                var pMemo = command.Parameters.Add("@Memo", SqliteType.Text);
                var pFileUuid = command.Parameters.Add("@FileUuid", SqliteType.Text);
                var pEmbedUrl = command.Parameters.Add("@EmbedUrl", SqliteType.Text);
                var pRating = command.Parameters.Add("@Rating", SqliteType.Text);
                var pSite = command.Parameters.Add("@Site", SqliteType.Text);
                var pIsFavorite = command.Parameters.Add("@IsFavorite", SqliteType.Integer);
                var pThumbnailStatus = command.Parameters.Add("@ThumbnailStatus", SqliteType.Integer);

                foreach (var video in videos)
                {
                    pId.Value = video.Id;
                    pTitle.Value = video.Title;
                    pUrl.Value = video.Url;
                    pThumbnailUrl.Value = video.ThumbnailUrl ?? "";
                    pLocalThumbnailPath.Value = video.LocalThumbnailPath ?? "";
                    pAuthorUserId.Value = video.AuthorUserId ?? "";
                    pAuthorUsername.Value = video.AuthorUsername ?? "";
                    pDurationSeconds.Value = video.DurationSeconds;
                    pPostedAt.Value = video.PostedAt?.ToString("o") ?? (object)DBNull.Value;
                    pLocalFilePath.Value = video.LocalFilePath ?? "";
                    pFileSize.Value = video.FileSize;
                    pStatus.Value = (int)video.Status;
                    pDownloadedAt.Value = video.DownloadedAt?.ToString("o") ?? (object)DBNull.Value;
                    pSubscribedUserId.Value = video.SubscribedUserId ?? (object)DBNull.Value;
                    pRetryCount.Value = video.RetryCount;
                    pLastErrorMessage.Value = video.LastErrorMessage ?? (object)DBNull.Value;
                    pTags.Value = video.Tags ?? "";
                    pMemo.Value = video.Memo ?? "";
                    pFileUuid.Value = video.FileUuid ?? "";
                    pEmbedUrl.Value = video.EmbedUrl ?? "";
                    pRating.Value = video.Rating ?? "";
                    pSite.Value = video.Site ?? "";
                    pIsFavorite.Value = video.IsFavorite ? 1 : 0;
                    pThumbnailStatus.Value = video.ThumbnailStatus;

                    updatedCount += command.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }

            return updatedCount;
        }

        /// <summary>
        /// 複数のVideoIdの存在確認を一括で行う(高速化)
        /// </summary>
        /// <param name="videoIds">確認するVideoIdリスト</param>
        /// <returns>存在するVideoIdのHashSet</returns>
        public HashSet<string> GetExistingVideoIds(IEnumerable<string> videoIds)
        {
            var existingIds = new HashSet<string>();
            var videoIdList = videoIds.ToList();
            
            if (videoIdList.Count == 0)
                return existingIds;

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // SQLiteはINクエリのパラメータ数に制限があるため、バッチで処理
            const int batchSize = 500;
            
            for (int i = 0; i < videoIdList.Count; i += batchSize)
            {
                var batch = videoIdList.Skip(i).Take(batchSize).ToList();
                var placeholders = string.Join(",", batch.Select((_, idx) => $"@id{idx}"));
                
                var command = connection.CreateCommand();
                command.CommandText = $"SELECT VideoId FROM Videos WHERE VideoId IN ({placeholders})";
                
                for (int j = 0; j < batch.Count; j++)
                {
                    command.Parameters.AddWithValue($"@id{j}", batch[j]);
                }

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    existingIds.Add(reader.GetString(0));
                }
            }

            return existingIds;
        }

        /// <summary>
        /// 複数の動画を一括削除(トランザクション使用)
        /// </summary>
        /// <param name="ids">削除する動画IDリスト</param>
        /// <returns>削除された動画数</returns>
        public int DeleteVideosBatch(IEnumerable<int> ids)
        {
            var idList = ids.ToList();
            if (idList.Count == 0)
                return 0;

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();
            int deletedCount = 0;

            try
            {
                // バッチで削除
                const int batchSize = 500;
                
                for (int i = 0; i < idList.Count; i += batchSize)
                {
                    var batch = idList.Skip(i).Take(batchSize).ToList();
                    var placeholders = string.Join(",", batch.Select((_, idx) => $"@id{idx}"));
                    
                    var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = $"DELETE FROM Videos WHERE Id IN ({placeholders})";
                    
                    for (int j = 0; j < batch.Count; j++)
                    {
                        command.Parameters.AddWithValue($"@id{j}", batch[j]);
                    }

                    deletedCount += command.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }

            return deletedCount;
        }

        #endregion

        #region Statistics

        /// <summary>
        /// ダウンロード統計を取得
        /// </summary>
        public DownloadStatistics GetDownloadStatistics()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var stats = new DownloadStatistics();

            // 総動画数
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Videos";
            stats.TotalVideoCount = Convert.ToInt32(cmd.ExecuteScalar());

            // ステータス別カウント
            cmd.CommandText = "SELECT Status, COUNT(*) FROM Videos GROUP BY Status";
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var status = (DownloadStatus)reader.GetInt32(0);
                    var count = reader.GetInt32(1);
                    stats.StatusCounts[status] = count;
                }
            }

            // 総ファイルサイズ(完了分)
            cmd.CommandText = "SELECT COALESCE(SUM(FileSize), 0) FROM Videos WHERE Status = @Status";
            cmd.Parameters.AddWithValue("@Status", (int)DownloadStatus.Completed);
            stats.TotalDownloadedSize = Convert.ToInt64(cmd.ExecuteScalar());

            // チャンネル数
            cmd.CommandText = "SELECT COUNT(*) FROM SubscribedUsers";
            cmd.Parameters.Clear();
            stats.ChannelCount = Convert.ToInt32(cmd.ExecuteScalar());

            // 有効なチャンネル数
            cmd.CommandText = "SELECT COUNT(*) FROM SubscribedUsers WHERE IsEnabled = 1";
            stats.EnabledChannelCount = Convert.ToInt32(cmd.ExecuteScalar());

            return stats;
        }

        #endregion

        #region Export/Import

        /// <summary>
        /// 購読リストをJSONでエクスポート
        /// </summary>
        public string ExportSubscriptionsToJson()
        {
            var users = GetAllSubscribedUsers();
            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            return System.Text.Json.JsonSerializer.Serialize(users, options);
        }

        /// <summary>
        /// 購読リストをJSONからインポート
        /// </summary>
        public int ImportSubscriptionsFromJson(string json)
        {
            var users = System.Text.Json.JsonSerializer.Deserialize<List<SubscribedUser>>(json);
            if (users == null) return 0;

            int imported = 0;
            foreach (var user in users)
            {
                if (GetSubscribedUserByUserId(user.UserId) == null)
                {
                    user.Id = 0; // 新規登録用にIDをリセット
                    AddSubscribedUser(user);
                    imported++;
                }
            }
            return imported;
        }

        #endregion

        public void Dispose()
        {
            // SqliteConnectionは使い捨てなので特に何もしない
        }
    }

    /// <summary>
    /// ダウンロード統計情報
    /// </summary>
    public class DownloadStatistics
    {
        /// <summary>総動画数</summary>
        public int TotalVideoCount { get; set; }
        
        /// <summary>ステータス別カウント</summary>
        public Dictionary<DownloadStatus, int> StatusCounts { get; set; } = new();
        
        /// <summary>総ダウンロード済みサイズ(バイト)</summary>
        public long TotalDownloadedSize { get; set; }
        
        /// <summary>チャンネル数</summary>
        public int ChannelCount { get; set; }
        
        /// <summary>有効なチャンネル数</summary>
        public int EnabledChannelCount { get; set; }

        /// <summary>完了数</summary>
        public int CompletedCount => StatusCounts.GetValueOrDefault(DownloadStatus.Completed, 0);
        
        /// <summary>失敗数</summary>
        public int FailedCount => StatusCounts.GetValueOrDefault(DownloadStatus.Failed, 0);
        
        /// <summary>待機中数</summary>
        public int PendingCount => StatusCounts.GetValueOrDefault(DownloadStatus.Pending, 0);
        
        /// <summary>DL中数</summary>
        public int DownloadingCount => StatusCounts.GetValueOrDefault(DownloadStatus.Downloading, 0);

        /// <summary>総サイズを表示用にフォーマット</summary>
        public string TotalDownloadedSizeFormatted
        {
            get
            {
                if (TotalDownloadedSize <= 0) return "0 B";
                string[] sizes = { "B", "KB", "MB", "GB", "TB" };
                int order = 0;
                double size = TotalDownloadedSize;
                while (size >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    size /= 1024;
                }
                return $"{size:0.##} {sizes[order]}";
            }
        }
    }
}
