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

            _connectionString = $"Data Source={dbPath}";
            InitializeDatabase();
        }

        /// <summary>
        /// データベースを初期化
        /// </summary>
        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

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
                    CustomSavePath TEXT DEFAULT ''
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
                    FOREIGN KEY (SubscribedUserId) REFERENCES SubscribedUsers(Id) ON DELETE SET NULL
                );

                CREATE INDEX IF NOT EXISTS idx_videos_status ON Videos(Status);
                CREATE INDEX IF NOT EXISTS idx_videos_subscribed_user ON Videos(SubscribedUserId);
                CREATE INDEX IF NOT EXISTS idx_videos_video_id ON Videos(VideoId);
            ";
            command.ExecuteNonQuery();

            // マイグレーション: CustomSavePathカラムを追加（既存DBの場合）
            MigrateDatabase(connection);
        }

        /// <summary>
        /// データベースマイグレーション
        /// </summary>
        private void MigrateDatabase(SqliteConnection connection)
        {
            // CustomSavePathカラムが存在しない場合は追加
            var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "PRAGMA table_info(SubscribedUsers)";
            bool hasCustomSavePath = false;
            using (var reader = checkCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (reader.GetString(1) == "CustomSavePath")
                    {
                        hasCustomSavePath = true;
                        break;
                    }
                }
            }

            if (!hasCustomSavePath)
            {
                var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE SubscribedUsers ADD COLUMN CustomSavePath TEXT DEFAULT ''";
                alterCmd.ExecuteNonQuery();
            }

            // VideosテーブルのTags/Memoカラムマイグレーション
            MigrateVideosTable(connection);
        }

        /// <summary>
        /// Videosテーブルのマイグレーション
        /// </summary>
        private void MigrateVideosTable(SqliteConnection connection)
        {
            var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "PRAGMA table_info(Videos)";
            bool hasTags = false;
            bool hasMemo = false;
            
            using (var reader = checkCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var columnName = reader.GetString(1);
                    if (columnName == "Tags") hasTags = true;
                    if (columnName == "Memo") hasMemo = true;
                }
            }

            if (!hasTags)
            {
                var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE Videos ADD COLUMN Tags TEXT DEFAULT ''";
                alterCmd.ExecuteNonQuery();
            }

            if (!hasMemo)
            {
                var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE Videos ADD COLUMN Memo TEXT DEFAULT ''";
                alterCmd.ExecuteNonQuery();
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
                INSERT INTO SubscribedUsers (UserId, Username, ProfileUrl, ThumbnailUrl, LocalThumbnailPath, CreatedAt, IsEnabled, CustomSavePath)
                VALUES (@UserId, @Username, @ProfileUrl, @ThumbnailUrl, @LocalThumbnailPath, @CreatedAt, @IsEnabled, @CustomSavePath);
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
                    CustomSavePath = @CustomSavePath
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

            // CustomSavePathカラムが存在する場合のみ読み取り（マイグレーション対応）
            try
            {
                var ordinal = reader.GetOrdinal("CustomSavePath");
                user.CustomSavePath = reader.IsDBNull(ordinal) ? "" : reader.GetString(ordinal);
            }
            catch { user.CustomSavePath = ""; }

            return user;
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
                    RetryCount, LastErrorMessage, CreatedAt, Tags, Memo)
                VALUES (@VideoId, @Title, @Url, @ThumbnailUrl, @LocalThumbnailPath, @AuthorUserId, @AuthorUsername,
                    @DurationSeconds, @PostedAt, @LocalFilePath, @FileSize, @Status, @DownloadedAt, @SubscribedUserId,
                    @RetryCount, @LastErrorMessage, @CreatedAt, @Tags, @Memo);
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
                    Memo = @Memo
                WHERE Id = @Id
            ";
            command.Parameters.AddWithValue("@Id", video.Id);
            AddVideoParameters(command, video);

            command.ExecuteNonQuery();
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
        /// リトライ対象の動画を取得（失敗かつリトライ回数が上限未満）
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
                Memo = TryGetString(reader, "Memo")
            };
        }

        /// <summary>
        /// カラムが存在する場合のみ文字列を取得（マイグレーション対応）
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
        /// 複数の動画を一括追加（トランザクション使用で高速化）
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
                        RetryCount, LastErrorMessage, CreatedAt, Tags, Memo)
                    VALUES (@VideoId, @Title, @Url, @ThumbnailUrl, @LocalThumbnailPath, @AuthorUserId, @AuthorUsername,
                        @DurationSeconds, @PostedAt, @LocalFilePath, @FileSize, @Status, @DownloadedAt, @SubscribedUserId,
                        @RetryCount, @LastErrorMessage, @CreatedAt, @Tags, @Memo)
                ";

                // パラメータを作成（再利用）
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
        /// 複数の動画を一括更新（トランザクション使用で高速化）
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
                        Memo = @Memo
                    WHERE Id = @Id
                ";

                // パラメータを作成（再利用）
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
        /// 複数のVideoIdの存在確認を一括で行う（高速化）
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
        /// 複数の動画を一括削除（トランザクション使用）
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

            // 総ファイルサイズ（完了分）
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
        
        /// <summary>総ダウンロード済みサイズ（バイト）</summary>
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
