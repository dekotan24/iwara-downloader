using System.Data;
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
        private readonly string _dbPath;
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
            _dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "IwaraDownloader",
                "data.db");

            var directory = Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Pooling=true で接続再利用、Cache=Shared で WAL の効果を最大化
            _connectionString = $"Data Source={_dbPath};Pooling=True;Cache=Shared";
            InitializeDatabase();
            BackupDatabaseIfNeeded();
        }

        private const int MaxBackupCount = 7;

        private void BackupDatabaseIfNeeded()
        {
            try
            {
                if (!File.Exists(_dbPath)) return;

                var backupDir = Path.Combine(Path.GetDirectoryName(_dbPath)!, "backups");
                Directory.CreateDirectory(backupDir);

                // data_backup_manual_... 等の命名違いのファイルが混じると文字列ソートで
                // 先頭に来てしまい (例: 'm' > '2') 24時間スロットルが常に無効化されるバグがあった。
                // 自動生成パターン (data_backup_yyyyMMdd_HHmmss.db) のみに絞り、実際の更新日時でソートする。
                var existing = Directory.GetFiles(backupDir, "data_backup_*.db")
                    .Where(f => System.Text.RegularExpressions.Regex.IsMatch(
                        Path.GetFileName(f), @"^data_backup_\d{8}_\d{6}\.db$"))
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .ToArray();

                if (existing.Length > 0)
                {
                    var latestWriteTime = File.GetLastWriteTime(existing[0]);
                    if ((DateTime.Now - latestWriteTime).TotalHours < 24)
                        return;
                }

                var backupPath = Path.Combine(backupDir, $"data_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db");

                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = $"VACUUM INTO '{backupPath.Replace("'", "''")}'";
                    cmd.ExecuteNonQuery();
                }

                LoggingService.Instance.Info($"データベースバックアップ作成: {Path.GetFileName(backupPath)}");

                // 古いバックアップを削除
                foreach (var old in existing.Skip(MaxBackupCount - 1))
                {
                    try { File.Delete(old); }
                    catch { /* 削除失敗は無視 */ }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Warn($"データベースバックアップ失敗: {ex.Message}", ex);
            }
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
                    VideosLoaded INTEGER DEFAULT 0,
                    DefaultPriority INTEGER NULL,
                    IsAccountDeleted INTEGER DEFAULT 0
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
                    ApiRawJson TEXT DEFAULT '',
                    Priority INTEGER NULL,
                    FOREIGN KEY (SubscribedUserId) REFERENCES SubscribedUsers(Id) ON DELETE SET NULL
                );

                CREATE INDEX IF NOT EXISTS idx_videos_status ON Videos(Status);
                CREATE INDEX IF NOT EXISTS idx_videos_subscribed_user ON Videos(SubscribedUserId);
                CREATE INDEX IF NOT EXISTS idx_videos_video_id ON Videos(VideoId);
                CREATE INDEX IF NOT EXISTS idx_videos_file_uuid ON Videos(FileUuid);
                -- Rating カラムのインデックスは MigrateVideosTable 内 (ALTER TABLE 後) で作成する

                -- 除外(ゴミ箱)テーブル: Videos と同じ列構成 + ExcludedAt。
                -- 不変条件: ある VideoId は Videos か ExcludedVideos の「片方だけ」に存在する。
                -- 削除 = Videos → ExcludedVideos へ移動、復元 = 逆に移動。
                -- これにより自動取得のガードは ProcessFetchQueueAsync の1箇所で済む。
                CREATE TABLE IF NOT EXISTS ExcludedVideos (
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
                    Site TEXT DEFAULT '',
                    IsFavorite INTEGER DEFAULT 0,
                    ThumbnailStatus INTEGER DEFAULT 0,
                    ApiRawJson TEXT DEFAULT '',
                    Priority INTEGER NULL,
                    ExcludedAt TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_excluded_video_id ON ExcludedVideos(VideoId);
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
            bool hasDefaultPriority = false;
            bool hasIsAccountDeleted = false;

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
                    if (columnName == "DefaultPriority") hasDefaultPriority = true;
                    if (columnName == "IsAccountDeleted") hasIsAccountDeleted = true;
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
            AddColumnIfMissing(connection, hasDefaultPriority,
                "ALTER TABLE SubscribedUsers ADD COLUMN DefaultPriority INTEGER NULL", "DefaultPriority");
            AddColumnIfMissing(connection, hasIsAccountDeleted,
                "ALTER TABLE SubscribedUsers ADD COLUMN IsAccountDeleted INTEGER DEFAULT 0", "IsAccountDeleted");

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
            bool hasApiRawJson = false;
            bool hasPriority = false;

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
                    if (columnName == "ApiRawJson") hasApiRawJson = true;
                    if (columnName == "Priority") hasPriority = true;
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

            // ApiRawJson カラム: iwara API の生レスポンス(著者アカウントの揮発的情報のみ間引いたもの)を
            // そのまま保存。将来 numLikes/numViews/tags/body 等を使いたくなった時に再取得なしで
            // json_extract できるようにするための保険。
            AddColumnIfMissing(connection, hasApiRawJson,
                "ALTER TABLE Videos ADD COLUMN ApiRawJson TEXT DEFAULT ''", "ApiRawJson");

            // Priority カラム: DLキューの優先度(手動設定時のみ非NULL、未設定はチャンネル既定/Normalへ解決)
            AddColumnIfMissing(connection, hasPriority,
                "ALTER TABLE Videos ADD COLUMN Priority INTEGER NULL", "Priority");

            // ExcludedVideos は CREATE TABLE IF NOT EXISTS のみで既存DBには新規列が反映されないため、
            // ここで個別にマイグレーションする。無いと GetSharedVideoColumnList の交差から Priority が
            // 漏れ、削除→復元の往復で優先度が黙って消える。
            bool hasExcludedPriority = GetTableColumns(connection, "ExcludedVideos")
                .Contains("Priority", StringComparer.OrdinalIgnoreCase);
            AddColumnIfMissing(connection, hasExcludedPriority,
                "ALTER TABLE ExcludedVideos ADD COLUMN Priority INTEGER NULL", "ExcludedVideos.Priority");
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
                INSERT INTO SubscribedUsers (UserId, Username, ProfileUrl, ThumbnailUrl, LocalThumbnailPath, CreatedAt, IsEnabled, CustomSavePath, DownloadExternalVideosOverride, Site, VideosLoaded, DefaultPriority, IsAccountDeleted)
                VALUES (@UserId, @Username, @ProfileUrl, @ThumbnailUrl, @LocalThumbnailPath, @CreatedAt, @IsEnabled, @CustomSavePath, @DownloadExternalVideosOverride, @Site, @VideosLoaded, @DefaultPriority, @IsAccountDeleted);
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
            command.Parameters.AddWithValue("@DefaultPriority",
                user.DefaultPriority.HasValue ? (object)(int)user.DefaultPriority.Value : DBNull.Value);
            command.Parameters.AddWithValue("@IsAccountDeleted", user.IsAccountDeleted ? 1 : 0);

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
                    VideosLoaded = @VideosLoaded,
                    DefaultPriority = @DefaultPriority,
                    IsAccountDeleted = @IsAccountDeleted
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
            command.Parameters.AddWithValue("@DefaultPriority",
                user.DefaultPriority.HasValue ? (object)(int)user.DefaultPriority.Value : DBNull.Value);
            command.Parameters.AddWithValue("@IsAccountDeleted", user.IsAccountDeleted ? 1 : 0);

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
            // チャンネルツリー / Web のチャンネル一覧の表示順 (名前昇順)
            command.CommandText = "SELECT * FROM SubscribedUsers ORDER BY Username COLLATE NOCASE ASC";

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

            // DefaultPriority カラム (マイグレーション対応)
            try
            {
                var ordinal = reader.GetOrdinal("DefaultPriority");
                var rawPriority = reader.IsDBNull(ordinal) ? (int?)null : reader.GetInt32(ordinal);
                user.DefaultPriority = rawPriority is >= 0 and <= 3 ? (DownloadPriority)rawPriority.Value : null;
            }
            catch { user.DefaultPriority = null; }

            // IsAccountDeleted カラム(マイグレーション対応)
            try
            {
                var ordinal = reader.GetOrdinal("IsAccountDeleted");
                user.IsAccountDeleted = !reader.IsDBNull(ordinal) && reader.GetInt32(ordinal) == 1;
            }
            catch { user.IsAccountDeleted = false; }

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
            // 明示的に追加/インポートされた動画は除外(ゴミ箱)から出す = 不変条件を維持。
            // 間違えて削除しても、URL 貼り直しやインポートで普通に戻せる。
            command.CommandText = @"
                DELETE FROM ExcludedVideos WHERE VideoId = @VideoId;
                INSERT INTO Videos (VideoId, Title, Url, ThumbnailUrl, LocalThumbnailPath, AuthorUserId, AuthorUsername,
                    DurationSeconds, PostedAt, LocalFilePath, FileSize, Status, DownloadedAt, SubscribedUserId,
                    RetryCount, LastErrorMessage, CreatedAt, Tags, Memo, FileUuid, EmbedUrl, Rating, Site, IsFavorite, ThumbnailStatus, ApiRawJson, Priority)
                VALUES (@VideoId, @Title, @Url, @ThumbnailUrl, @LocalThumbnailPath, @AuthorUserId, @AuthorUsername,
                    @DurationSeconds, @PostedAt, @LocalFilePath, @FileSize, @Status, @DownloadedAt, @SubscribedUserId,
                    @RetryCount, @LastErrorMessage, @CreatedAt, @Tags, @Memo, @FileUuid, @EmbedUrl, @Rating, @Site, @IsFavorite, @ThumbnailStatus, @ApiRawJson, @Priority);
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
                    ThumbnailStatus = @ThumbnailStatus,
                    ApiRawJson = @ApiRawJson,
                    Priority = @Priority
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
        /// 複数動画の優先度だけを高速に更新する(他カラムを触らない一括UPDATE、500件バッチ)。
        /// </summary>
        public void SetVideoPriority(IEnumerable<string> videoIds, DownloadPriority priority)
        {
            var idList = videoIds.Distinct().ToList();
            if (idList.Count == 0) return;

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try
            {
                const int batchSize = 500;
                for (int i = 0; i < idList.Count; i += batchSize)
                {
                    var batch = idList.Skip(i).Take(batchSize).ToList();
                    var placeholders = string.Join(",", batch.Select((_, idx) => $"@vid{idx}"));

                    var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = $"UPDATE Videos SET Priority = @Priority WHERE VideoId IN ({placeholders})";
                    command.Parameters.AddWithValue("@Priority", (int)priority);
                    for (int j = 0; j < batch.Count; j++)
                        command.Parameters.AddWithValue($"@vid{j}", batch[j]);
                    command.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// チャンネル新着チェックで既存動画に再度遭遇した際、まだ未取得(NULL/空)なPostedAt・ApiRawJsonだけ
        /// まとめて埋める。どちらも既に値が入っている行は上書きしないバックフィル専用の更新で、
        /// 継続的な鮮度更新はしない。WHERE 句に未取得条件を入れているので、既に両方埋まっている行は
        /// 該当0件でUPDATE自体がスキップされる。取得失敗などで空のままなら次回以降のチェックでも
        /// 自然に対象になり続ける。
        /// 1件ずつ接続を開いてUPDATEすると新着チェックのたびに数千件規模の個別コミットで
        /// DB書き込みロックを奪い合うため、1接続・1トランザクションで処理する。
        /// </summary>
        public void BackfillExistingVideoMetadata(IEnumerable<(string VideoId, DateTime? PostedAt, string? ApiRawJson)> items)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try
            {
                var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"
                    UPDATE Videos SET
                        PostedAt = COALESCE(PostedAt, @PostedAt),
                        ApiRawJson = COALESCE(NULLIF(ApiRawJson, ''), @ApiRawJson)
                    WHERE VideoId = @VideoId
                      AND (PostedAt IS NULL OR ApiRawJson IS NULL OR ApiRawJson = '')";
                var pPostedAt = command.Parameters.Add("@PostedAt", SqliteType.Text);
                var pApiRawJson = command.Parameters.Add("@ApiRawJson", SqliteType.Text);
                var pVideoId = command.Parameters.Add("@VideoId", SqliteType.Text);

                foreach (var (videoId, postedAt, apiRawJson) in items)
                {
                    pPostedAt.Value = postedAt?.ToString("o") ?? (object)DBNull.Value;
                    pApiRawJson.Value = string.IsNullOrEmpty(apiRawJson) ? (object)DBNull.Value : apiRawJson;
                    pVideoId.Value = videoId;
                    command.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
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
            command.CommandText = "SELECT * FROM Videos ORDER BY COALESCE(PostedAt, CreatedAt) DESC";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                videos.Add(ReadVideo(reader));
            }
            return videos;
        }

        /// <summary>
        /// チャンネルツリー表示用の件数集計を SQL 側の GROUP BY で取得する。
        /// GetAllVideos() の全件ロード + LINQ 集計 (動画数万件でRefreshChannelTreeのたびに重くなる) の代替。
        /// </summary>
        public VideoTreeCounts GetVideoTreeCounts()
        {
            var result = new VideoTreeCounts();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT SubscribedUserId, Status, IsFavorite, COUNT(*)
                FROM Videos
                GROUP BY SubscribedUserId, Status, IsFavorite";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                int? subId = reader.IsDBNull(0) ? null : reader.GetInt32(0);
                // Status/IsFavorite は NOT NULL 制約が無く ALTER TABLE ADD COLUMN の DEFAULT 0 のみで保護されている列。
                // NULL 行が万一あっても GetInt32 で例外を投げてツリー更新が永久に止まらないようにガードする。
                var status = (DownloadStatus)(reader.IsDBNull(1) ? 0 : reader.GetInt32(1));
                bool isFavorite = !reader.IsDBNull(2) && reader.GetInt32(2) != 0;
                int count = reader.GetInt32(3);

                result.Total += count;
                if (isFavorite) result.Favorite += count;

                switch (status)
                {
                    case DownloadStatus.Completed: result.Completed += count; break;
                    case DownloadStatus.Failed: result.Failed += count; break;
                    case DownloadStatus.Skipped: result.Skipped += count; break;
                }
                if (status != DownloadStatus.Completed && status != DownloadStatus.Skipped)
                    result.NotDownloaded += count;

                if (subId == null)
                {
                    result.SingleVideos += count;
                    continue;
                }

                if (!result.ByChannel.TryGetValue(subId.Value, out var ch))
                {
                    ch = new ChannelVideoCounts();
                    result.ByChannel[subId.Value] = ch;
                }
                ch.Total += count;
                switch (status)
                {
                    case DownloadStatus.Completed: ch.Completed += count; break;
                    case DownloadStatus.Downloading: ch.Downloading += count; break;
                    case DownloadStatus.Pending: ch.Pending += count; break;
                    case DownloadStatus.Paused: ch.Paused += count; break;
                }
            }
            return result;
        }

        /// <summary>
        /// ステータスバー表示用の件数集計をSQL側で取得する。
        /// GetAllVideos()の全件ロード + LINQ集計の代替(動画数万件で頻繁に呼ばれると重い)。
        /// </summary>
        public (int Downloading, int Pending, int Completed, long CompletedSize) GetDownloadCountSummary()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT
                    SUM(CASE WHEN Status = @Downloading THEN 1 ELSE 0 END),
                    SUM(CASE WHEN Status = @Pending THEN 1 ELSE 0 END),
                    SUM(CASE WHEN Status = @Completed THEN 1 ELSE 0 END),
                    COALESCE(SUM(CASE WHEN Status = @Completed THEN FileSize ELSE 0 END), 0)
                FROM Videos";
            command.Parameters.AddWithValue("@Downloading", (int)DownloadStatus.Downloading);
            command.Parameters.AddWithValue("@Pending", (int)DownloadStatus.Pending);
            command.Parameters.AddWithValue("@Completed", (int)DownloadStatus.Completed);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                int downloading = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                int pending = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                int completed = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                long completedSize = reader.IsDBNull(3) ? 0L : reader.GetInt64(3);
                return (downloading, pending, completed, completedSize);
            }
            return (0, 0, 0, 0L);
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
            command.CommandText = "SELECT * FROM Videos WHERE SubscribedUserId = @SubscribedUserId ORDER BY COALESCE(PostedAt, CreatedAt) DESC";
            command.Parameters.AddWithValue("@SubscribedUserId", subscribedUserId);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                videos.Add(ReadVideo(reader));
            }
            return videos;
        }

        /// <summary>
        /// 未DL (Completed / Skipped を除く) の動画を取得
        /// </summary>
        public List<VideoInfo> GetNotDownloadedVideos()
        {
            var videos = new List<VideoInfo>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Videos WHERE Status != @Completed AND Status != @Skipped ORDER BY COALESCE(PostedAt, CreatedAt) DESC";
            command.Parameters.AddWithValue("@Completed", (int)DownloadStatus.Completed);
            command.Parameters.AddWithValue("@Skipped", (int)DownloadStatus.Skipped);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                videos.Add(ReadVideo(reader));
            }
            return videos;
        }

        /// <summary>
        /// 購読外(単発追加)の動画を取得
        /// </summary>
        public List<VideoInfo> GetSingleVideos()
        {
            var videos = new List<VideoInfo>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Videos WHERE SubscribedUserId IS NULL ORDER BY COALESCE(PostedAt, CreatedAt) DESC";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                videos.Add(ReadVideo(reader));
            }
            return videos;
        }

        /// <summary>
        /// お気に入り動画を取得
        /// </summary>
        public List<VideoInfo> GetFavoriteVideos()
        {
            var videos = new List<VideoInfo>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Videos WHERE IsFavorite = 1 ORDER BY COALESCE(PostedAt, CreatedAt) DESC";

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
            command.CommandText = "SELECT * FROM Videos WHERE Status = @Status ORDER BY COALESCE(PostedAt, CreatedAt) DESC";
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
            command.Parameters.AddWithValue("@ApiRawJson", video.ApiRawJson ?? "");
            command.Parameters.AddWithValue("@Priority", video.Priority.HasValue ? (int)video.Priority.Value : (object)DBNull.Value);
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
                ThumbnailStatus = TryGetInt(reader, "ThumbnailStatus"),
                ApiRawJson = TryGetString(reader, "ApiRawJson"),
                // 範囲外の値(DB破損等)はNormal相当としてフォールバックする。
                // ここでガードしないと DownloadManager 側の _pendingQueueByPriority[(int)priority] が
                // 配列範囲外アクセスで例外を投げ、fire-and-forget の ProcessQueueAsync 内で
                // 観測されないままキュー処理が永久に止まる。
                Priority = TryGetNullableInt(reader, "Priority") is int p && p is >= 0 and <= 3 ? (DownloadPriority)p : null
            };
        }

        /// <summary>
        /// カラムが存在する場合のみnullable整数を取得(マイグレーション対応)。無い/NULLならnull。
        /// TryGetIntと違い「未設定(NULL)」と「0」を区別する必要がある列(Priority等)用。
        /// </summary>
        private static int? TryGetNullableInt(SqliteDataReader reader, string columnName)
        {
            try
            {
                var ordinal = reader.GetOrdinal(columnName);
                return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
            }
            catch
            {
                return null;
            }
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
                // インポートされた動画は除外(ゴミ箱)から出す = 不変条件を維持。
                command.CommandText = @"
                    DELETE FROM ExcludedVideos WHERE VideoId = @VideoId;
                    INSERT OR IGNORE INTO Videos (VideoId, Title, Url, ThumbnailUrl, LocalThumbnailPath, AuthorUserId, AuthorUsername,
                        DurationSeconds, PostedAt, LocalFilePath, FileSize, Status, DownloadedAt, SubscribedUserId,
                        RetryCount, LastErrorMessage, CreatedAt, Tags, Memo, FileUuid, EmbedUrl, Rating, Site, IsFavorite, ThumbnailStatus, ApiRawJson, Priority)
                    VALUES (@VideoId, @Title, @Url, @ThumbnailUrl, @LocalThumbnailPath, @AuthorUserId, @AuthorUsername,
                        @DurationSeconds, @PostedAt, @LocalFilePath, @FileSize, @Status, @DownloadedAt, @SubscribedUserId,
                        @RetryCount, @LastErrorMessage, @CreatedAt, @Tags, @Memo, @FileUuid, @EmbedUrl, @Rating, @Site, @IsFavorite, @ThumbnailStatus, @ApiRawJson, @Priority)
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
                var pApiRawJson = command.Parameters.Add("@ApiRawJson", SqliteType.Text);
                var pPriority = command.Parameters.Add("@Priority", SqliteType.Integer);

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
                    pApiRawJson.Value = video.ApiRawJson ?? "";
                    pPriority.Value = video.Priority.HasValue ? (int)video.Priority.Value : (object)DBNull.Value;

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
                        ThumbnailStatus = @ThumbnailStatus,
                        ApiRawJson = @ApiRawJson,
                        Priority = @Priority
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
                var pApiRawJson = command.Parameters.Add("@ApiRawJson", SqliteType.Text);
                var pPriority = command.Parameters.Add("@Priority", SqliteType.Integer);

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
                    pApiRawJson.Value = video.ApiRawJson ?? "";
                    pPriority.Value = video.Priority.HasValue ? (int)video.Priority.Value : (object)DBNull.Value;

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

        #region Exclusion (bin)

        /// <summary>
        /// テーブルの列名一覧を取得 (PRAGMA table_info)。
        /// </summary>
        private static List<string> GetTableColumns(SqliteConnection connection, string table)
        {
            var cols = new List<string>();
            var cmd = connection.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info({table})";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                cols.Add(reader.GetString(1));
            return cols;
        }

        /// <summary>
        /// Videos と ExcludedVideos の共通データ列 (Id を除く) をカンマ区切りで返す。
        /// INSERT ... SELECT で丸ごとコピーするために使い、手動フィールドマッピングの取りこぼしを防ぐ。
        /// </summary>
        private static string GetSharedVideoColumnList(SqliteConnection connection)
        {
            var excludedCols = new HashSet<string>(
                GetTableColumns(connection, "ExcludedVideos"), StringComparer.OrdinalIgnoreCase);
            var shared = GetTableColumns(connection, "Videos")
                .Where(c => !string.Equals(c, "Id", StringComparison.OrdinalIgnoreCase) && excludedCols.Contains(c));
            return string.Join(", ", shared);
        }

        /// <summary>
        /// 指定した動画を Videos → ExcludedVideos へ移動する (削除の実体)。1トランザクションで原子的に行う。
        /// 途中で落ちても「Videos から消えたのに Excluded に無い」= 再取得で復活する穴を作らない。
        /// </summary>
        /// <returns>除外した件数</returns>
        public int MoveVideosToExcluded(IEnumerable<int> ids)
        {
            var idList = ids.Distinct().ToList();
            if (idList.Count == 0) return 0;

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var cols = GetSharedVideoColumnList(connection);
            var now = DateTime.Now.ToString("o");

            using var transaction = connection.BeginTransaction();
            int moved = 0;
            try
            {
                const int batchSize = 500;
                for (int i = 0; i < idList.Count; i += batchSize)
                {
                    var batch = idList.Skip(i).Take(batchSize).ToList();
                    var placeholders = string.Join(",", batch.Select((_, idx) => $"@id{idx}"));

                    // 1) スナップショットを ExcludedVideos へ。
                    //    INSERT OR REPLACE で「削除→復元→再削除」サイクルの VideoId UNIQUE 衝突を回避。
                    var insertCmd = connection.CreateCommand();
                    insertCmd.Transaction = transaction;
                    insertCmd.CommandText =
                        $"INSERT OR REPLACE INTO ExcludedVideos ({cols}, ExcludedAt) " +
                        $"SELECT {cols}, @now FROM Videos WHERE Id IN ({placeholders})";
                    insertCmd.Parameters.AddWithValue("@now", now);
                    for (int j = 0; j < batch.Count; j++)
                        insertCmd.Parameters.AddWithValue($"@id{j}", batch[j]);
                    insertCmd.ExecuteNonQuery();

                    // 2) Videos から削除。
                    var deleteCmd = connection.CreateCommand();
                    deleteCmd.Transaction = transaction;
                    deleteCmd.CommandText = $"DELETE FROM Videos WHERE Id IN ({placeholders})";
                    for (int j = 0; j < batch.Count; j++)
                        deleteCmd.Parameters.AddWithValue($"@id{j}", batch[j]);
                    moved += deleteCmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
            return moved;
        }

        /// <summary>
        /// 指定した VideoId を ExcludedVideos → Videos へ戻す (復元)。1トランザクションで原子的に行う。
        /// ローカルファイルは除外時に削除済みのため、呼び出し側が復元後にファイル欠損を正規化する。
        /// </summary>
        /// <returns>復元した件数</returns>
        public int RestoreVideosFromExcluded(IEnumerable<string> videoIds)
        {
            var idList = videoIds.Distinct().ToList();
            if (idList.Count == 0) return 0;

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var cols = GetSharedVideoColumnList(connection);

            using var transaction = connection.BeginTransaction();
            int restored = 0;
            try
            {
                const int batchSize = 500;
                for (int i = 0; i < idList.Count; i += batchSize)
                {
                    var batch = idList.Skip(i).Take(batchSize).ToList();
                    var placeholders = string.Join(",", batch.Select((_, idx) => $"@vid{idx}"));

                    // OR IGNORE: 万一同 VideoId が既に Videos にあっても不変条件を壊さない。
                    var insertCmd = connection.CreateCommand();
                    insertCmd.Transaction = transaction;
                    insertCmd.CommandText =
                        $"INSERT OR IGNORE INTO Videos ({cols}) " +
                        $"SELECT {cols} FROM ExcludedVideos WHERE VideoId IN ({placeholders})";
                    for (int j = 0; j < batch.Count; j++)
                        insertCmd.Parameters.AddWithValue($"@vid{j}", batch[j]);
                    restored += insertCmd.ExecuteNonQuery();

                    var deleteCmd = connection.CreateCommand();
                    deleteCmd.Transaction = transaction;
                    deleteCmd.CommandText = $"DELETE FROM ExcludedVideos WHERE VideoId IN ({placeholders})";
                    for (int j = 0; j < batch.Count; j++)
                        deleteCmd.Parameters.AddWithValue($"@vid{j}", batch[j]);
                    deleteCmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
            return restored;
        }

        /// <summary>
        /// ある VideoId が除外(ゴミ箱)に入っているか。自動取得のガードで使う。
        /// </summary>
        public bool IsVideoExcluded(string videoId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM ExcludedVideos WHERE VideoId = @VideoId";
            command.Parameters.AddWithValue("@VideoId", videoId);
            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }

        /// <summary>
        /// 除外(ゴミ箱)に入っている全動画を取得 (除外日時の新しい順)。
        /// </summary>
        public List<VideoInfo> GetExcludedVideos()
        {
            var videos = new List<VideoInfo>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM ExcludedVideos ORDER BY ExcludedAt DESC";
            using var reader = command.ExecuteReader();
            while (reader.Read())
                videos.Add(ReadVideo(reader));
            return videos;
        }

        /// <summary>
        /// 除外(ゴミ箱)の件数。
        /// </summary>
        public int GetExcludedCount()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM ExcludedVideos";
            return Convert.ToInt32(command.ExecuteScalar());
        }

        /// <summary>
        /// 除外(ゴミ箱)から完全に削除する (復元不可)。
        /// </summary>
        /// <returns>削除した件数</returns>
        public int DeleteExcludedPermanent(IEnumerable<string> videoIds)
        {
            var idList = videoIds.Distinct().ToList();
            if (idList.Count == 0) return 0;

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            int deleted = 0;
            try
            {
                const int batchSize = 500;
                for (int i = 0; i < idList.Count; i += batchSize)
                {
                    var batch = idList.Skip(i).Take(batchSize).ToList();
                    var placeholders = string.Join(",", batch.Select((_, idx) => $"@vid{idx}"));
                    var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = $"DELETE FROM ExcludedVideos WHERE VideoId IN ({placeholders})";
                    for (int j = 0; j < batch.Count; j++)
                        command.Parameters.AddWithValue($"@vid{j}", batch[j]);
                    deleted += command.ExecuteNonQuery();
                }
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
            return deleted;
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

        #region Advanced DB Tool (上級者向け: SQLエディタ/テーブルブラウザ)
        // DatabaseToolForm 専用の管理者API。通常のアプリ機能からは呼ばない。
        // 読み取りは常に Mode=ReadOnly の別接続で行い、書き込みは呼び出し元 (DatabaseToolForm) が
        // 明示的な書き込みモードのときのみ ExecuteAdminNonQuery を呼ぶ想定。
        // トランザクションは保持しない (Cache=Shared 環境で開きっぱなしにすると
        // DownloadManager 側の書き込みが database is locked で失敗するため)。

        /// <summary>DBファイルの絶対パス。DatabaseToolForm のバックアップ表示・確認用。</summary>
        public string DbPath => _dbPath;

        /// <summary>
        /// 24時間スロットルの対象外・7世代ローテーションの対象外となる強制バックアップを作成する。
        /// ファイル名は data_backup_manual_ プレフィックス (BackupDatabaseIfNeeded の自動生成
        /// パターン ^data_backup_\d{8}_\d{6}\.db$ に一致しないため、既存のスロットル/削除ロジックの
        /// 対象から自然に除外される)。DatabaseToolForm が書き込みモードへ切り替える直前に必ず呼ぶ。
        /// </summary>
        public string CreateForcedBackup()
        {
            var backupDir = Path.Combine(Path.GetDirectoryName(_dbPath)!, "backups");
            Directory.CreateDirectory(backupDir);
            var backupPath = Path.Combine(backupDir, $"data_backup_manual_{DateTime.Now:yyyyMMdd_HHmmss}.db");

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"VACUUM INTO '{backupPath.Replace("'", "''")}'";
            cmd.ExecuteNonQuery();

            LoggingService.Instance.Info($"DB操作ツール: 書き込みモード移行前の強制バックアップ作成: {Path.GetFileName(backupPath)}");
            return backupPath;
        }

        /// <summary>
        /// 任意のSQLを1文実行する。writable=false なら Mode=ReadOnly の別接続を使うため、
        /// UPDATE/DELETE/INSERT/DDL 等は SQLite 自体が "attempt to write a readonly database" で
        /// 拒否する(先頭キーワードでのSQL文字列判定は行わない。WITH句付きDELETE等の回避策があり
        /// 信頼できないため、可否の判定は必ず接続の権限そのものに委ねる)。
        /// SELECT等の結果セットを持つ文は DataTable を、UPDATE/DELETE/INSERT等は
        /// RecordsAffected のみを返す。ExecuteReaderはどちらの文でも実行できるため、
        /// 呼び出し前にSQL種別を判定する必要が無い。
        /// maxRows で結果行数をキャップする(大テーブルの全件フェッチによるUIフリーズ防止)。
        /// </summary>
        public (DataTable? Result, int RecordsAffected) ExecuteAdminSql(string sql, bool writable, int maxRows = 1000)
        {
            var connStr = writable ? _connectionString : $"Data Source={_dbPath};Mode=ReadOnly";
            using var connection = new SqliteConnection(connStr);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            using var reader = cmd.ExecuteReader();

            if (reader.FieldCount == 0)
                return (null, reader.RecordsAffected);

            var table = new DataTable();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                // JOINで同名列(例: 複数テーブルのId)が並ぶとDataTable.Columns.Addが
                // DuplicateNameExceptionを投げるため、重複時は連番を付けて回避する。
                var name = reader.GetName(i);
                var uniqueName = name;
                var suffix = 1;
                while (table.Columns.Contains(uniqueName))
                    uniqueName = $"{name}{suffix++}";
                table.Columns.Add(uniqueName, typeof(object));
            }

            var rowCount = 0;
            while (reader.Read())
            {
                if (rowCount >= maxRows) { table.ExtendedProperties["Truncated"] = true; break; }
                var row = table.NewRow();
                for (var i = 0; i < reader.FieldCount; i++)
                    row[i] = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
                table.Rows.Add(row);
                rowCount++;
            }
            return (table, reader.RecordsAffected);
        }

        /// <summary>テーブルブラウザ用: ユーザーテーブル名の一覧 (sqlite_内部テーブルは除外)。</summary>
        public List<string> GetAdminTableNames()
        {
            var names = new List<string>();
            using var connection = new SqliteConnection($"Data Source={_dbPath};Mode=ReadOnly");
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                names.Add(reader.GetString(0));
            return names;
        }

        /// <summary>テーブルブラウザ用: 指定テーブルの主キー列名 (複合主キー対応)。無ければ空リスト。</summary>
        public List<string> GetAdminPrimaryKeyColumns(string tableName)
        {
            var pk = new List<(int Order, string Name)>();
            using var connection = new SqliteConnection($"Data Source={_dbPath};Mode=ReadOnly");
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info('{tableName.Replace("'", "''")}')";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var pkOrder = reader.GetInt32(reader.GetOrdinal("pk"));
                if (pkOrder > 0)
                    pk.Add((pkOrder, reader.GetString(reader.GetOrdinal("name"))));
            }
            return pk.OrderBy(p => p.Order).Select(p => p.Name).ToList();
        }

        /// <summary>テーブルブラウザ用: 指定テーブルの行を取得 (limit/offset付き)。</summary>
        public DataTable GetAdminTableRows(string tableName, int limit, int offset)
        {
            // テーブル名はコンボボックスの選択肢 (GetAdminTableNames の戻り値) のみを渡す前提。
            // クォートで囲むことで予約語/記号を含むテーブル名にも安全に対応する。
            var (result, _) = ExecuteAdminSql(
                $"SELECT * FROM \"{tableName.Replace("\"", "\"\"")}\" LIMIT {limit} OFFSET {offset}",
                writable: false,
                maxRows: limit);
            return result ?? new DataTable();
        }

        /// <summary>テーブルブラウザ用: 1セルを更新する。主キー列の値でWHERE句を組み立てる。</summary>
        public void UpdateAdminCell(string tableName, IReadOnlyDictionary<string, object?> primaryKeyValues, string columnName, object? newValue)
        {
            if (primaryKeyValues.Count == 0)
                throw new InvalidOperationException("主キーが無いテーブルはセル編集できません。");

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            var where = string.Join(" AND ", primaryKeyValues.Keys.Select((k, i) => $"\"{k.Replace("\"", "\"\"")}\" = @pk{i}"));
            cmd.CommandText = $"UPDATE \"{tableName.Replace("\"", "\"\"")}\" SET \"{columnName.Replace("\"", "\"\"")}\" = @value WHERE {where}";
            cmd.Parameters.AddWithValue("@value", (object?)newValue ?? DBNull.Value);
            var i2 = 0;
            foreach (var kv in primaryKeyValues)
                cmd.Parameters.AddWithValue($"@pk{i2++}", kv.Value ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        /// <summary>テーブルブラウザ用: 主キーの値で1行削除する。</summary>
        public void DeleteAdminRow(string tableName, IReadOnlyDictionary<string, object?> primaryKeyValues)
        {
            if (primaryKeyValues.Count == 0)
                throw new InvalidOperationException("主キーが無いテーブルは行削除できません。");

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            var where = string.Join(" AND ", primaryKeyValues.Keys.Select((k, i) => $"\"{k.Replace("\"", "\"\"")}\" = @pk{i}"));
            cmd.CommandText = $"DELETE FROM \"{tableName.Replace("\"", "\"\"")}\" WHERE {where}";
            var i2 = 0;
            foreach (var kv in primaryKeyValues)
                cmd.Parameters.AddWithValue($"@pk{i2++}", kv.Value ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Videos / ExcludedVideos の XOR不変条件 (同じVideoIdが両方に存在してはいけない) を検査する。
        /// 生SQLでの直接編集はこの不変条件を無警告で破りうるため、DatabaseToolForm が
        /// 書き込み実行のたびに呼び、違反があればバナーで警告する。
        /// </summary>
        public int CheckAdminXorViolationCount()
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath};Mode=ReadOnly");
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Videos v INNER JOIN ExcludedVideos e ON v.VideoId = e.VideoId";
            return Convert.ToInt32(cmd.ExecuteScalar());
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
