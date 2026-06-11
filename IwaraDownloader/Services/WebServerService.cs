using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IwaraDownloader.Models;
using IwaraDownloader.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace IwaraDownloader.Services
{
    public class WebServerService : IDisposable
    {
        private WebApplication? _app;
        private Task? _runTask;
        private CancellationTokenSource? _cts;
        private readonly LoggingService _logger = LoggingService.Instance;
        private readonly DatabaseService _database = DatabaseService.Instance;
        private DownloadManager? _downloadManager;

        private readonly ConcurrentDictionary<string, SessionInfo> _sessions = new();
        private static readonly TimeSpan SessionTimeout = TimeSpan.FromHours(24);
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public bool IsRunning => _app != null;
        public int Port { get; private set; }
        public string? BaseUrl { get; private set; }

        public void SetDownloadManager(DownloadManager dm) => _downloadManager = dm;

        public async Task StartAsync(int port, bool bindAll)
        {
            if (_app != null) return;

            Port = port;
            _cts = new CancellationTokenSource();

            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();

            var address = bindAll ? IPAddress.Any : IPAddress.Loopback;
            builder.WebHost.ConfigureKestrel(k =>
            {
                k.Listen(address, port);
            });

            _app = builder.Build();

            _app.Use(async (ctx, next) =>
            {
                ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
                ctx.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
                ctx.Response.Headers["Referrer-Policy"] = "same-origin";
                try
                {
                    await next();
                }
                catch (Exception ex)
                {
                    _logger.Error($"Web API unhandled error: {ex.Message}");
                    if (!ctx.Response.HasStarted)
                    {
                        ctx.Response.StatusCode = 500;
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.WriteAsync("{\"error\":\"Internal server error\"}");
                    }
                }
            });

            ConfigureApi(_app);
            ConfigureStaticFiles(_app);

            // 表示用 URL: 0.0.0.0 はアクセス先として使えないため、LAN バインド時は実際のローカル IP を出す
            BaseUrl = bindAll ? $"http://{GetLanDisplayHost()}:{port}" : $"http://127.0.0.1:{port}";
            _logger.Info($"Web media server starting on {BaseUrl}");

            _runTask = _app.RunAsync();
        }

        public async Task StopAsync()
        {
            if (_app == null) return;

            _logger.Info("Web media server stopping...");
            try
            {
                _cts?.Cancel();
                // 先にホストを止める。_runTask (RunAsync) はホスト停止までは完了しないため、
                // 先に _runTask を待つと必ず 5 秒タイムアウトまでブロックしていた
                // (アプリ終了が常に約5秒遅くなる原因だった)
                using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await _app.StopAsync(stopCts.Token);
                if (_runTask != null)
                {
                    try { await _runTask.WaitAsync(TimeSpan.FromSeconds(2)); }
                    catch (OperationCanceledException) { }
                    catch (TimeoutException) { }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"Web server stop error: {ex.Message}");
            }
            finally
            {
                _app.DisposeAsync().AsTask().Wait(3000);
                _app = null;
                _runTask = null;
                _cts?.Dispose();
                _cts = null;
                BaseUrl = null;
            }
        }

        public void Dispose()
        {
            StopAsync().Wait(5000);
        }

        /// <summary>
        /// LAN 内アクセス用に表示するローカル IPv4 を返す。
        /// プライベートアドレス (192.168 → 10 → 172.16-31 の優先順) を選び、無ければ最初の IPv4。
        /// </summary>
        private static string GetLanDisplayHost()
        {
            try
            {
                var candidates = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                              && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
                              && ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                    .Select(a => a.Address)
                    .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                             && !IPAddress.IsLoopback(a))
                    .Select(a => a.ToString())
                    .ToList();

                static bool Is172Private(string ip)
                {
                    var parts = ip.Split('.');
                    return parts.Length == 4 && parts[0] == "172"
                        && int.TryParse(parts[1], out var o) && o is >= 16 and <= 31;
                }

                return candidates.FirstOrDefault(ip => ip.StartsWith("192.168."))
                    ?? candidates.FirstOrDefault(ip => ip.StartsWith("10."))
                    ?? candidates.FirstOrDefault(Is172Private)
                    ?? candidates.FirstOrDefault()
                    ?? "127.0.0.1";
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        private void ConfigureStaticFiles(WebApplication app)
        {
            var webUiPath = Path.Combine(AppContext.BaseDirectory, "WebUI");
            if (Directory.Exists(webUiPath))
            {
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(webUiPath),
                    RequestPath = ""
                });

                app.MapFallback(async ctx =>
                {
                    if (ctx.Request.Path.StartsWithSegments("/api")) return;
                    var indexPath = Path.Combine(webUiPath, "index.html");
                    if (File.Exists(indexPath))
                    {
                        ctx.Response.ContentType = "text/html; charset=utf-8";
                        await ctx.Response.SendFileAsync(indexPath);
                    }
                    else
                    {
                        ctx.Response.StatusCode = 404;
                    }
                });
            }
        }

        private void ConfigureApi(WebApplication app)
        {
            // --- Auth ---
            app.MapPost("/api/auth/login", (Delegate)HandleLogin);
            app.MapPost("/api/auth/logout", HandleLogout);
            app.MapGet("/api/auth/status", HandleAuthStatus);

            // --- Videos ---
            app.MapGet("/api/videos", HandleGetVideos);
            app.MapGet("/api/videos/{id:int}", HandleGetVideo);
            app.MapGet("/api/videos/{id:int}/stream", (Delegate)HandleStreamVideo);
            app.MapGet("/api/videos/{id:int}/thumbnail", (Delegate)HandleGetThumbnail);
            app.MapPost("/api/videos/{id:int}/thumbnail/retry", HandleRetryThumbnail);
            app.MapPost("/api/videos/{id:int}/favorite", (Delegate)HandleSetFavorite);

            // --- Channels ---
            app.MapGet("/api/channels", HandleGetChannels);

            // --- Errors ---
            app.MapGet("/api/errors", HandleGetErrors);
            app.MapPost("/api/errors/{id:int}/retry", HandleRetryError);
            app.MapPost("/api/errors/retry-all", HandleRetryAllErrors);
            app.MapPost("/api/errors/delete-not-found", HandleDeleteNotFound);

            // --- Downloads ---
            app.MapPost("/api/downloads/{id:int}/queue", HandleQueueDownload);
            app.MapGet("/api/downloads/active", HandleGetActiveDownloads);

            // --- Stats ---
            app.MapGet("/api/stats", HandleGetStats);

            // --- Batch ---
            app.MapPost("/api/videos/delete-batch", (Delegate)HandleDeleteBatch);
        }

        #region Auth

        private bool IsAuthenticated(HttpContext ctx)
        {
            var settings = SettingsManager.Instance.Settings;
            if (string.IsNullOrEmpty(settings.WebServerPasswordEncrypted))
                return true;

            var token = ctx.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
            if (string.IsNullOrEmpty(token))
                token = ctx.Request.Cookies["iwdl_session"];

            if (string.IsNullOrEmpty(token)) return false;

            if (_sessions.TryGetValue(token, out var session))
            {
                if (DateTime.UtcNow - session.CreatedAt < SessionTimeout)
                    return true;
                _sessions.TryRemove(token, out _);
            }
            return false;
        }

        private IResult RequireAuth(HttpContext ctx)
        {
            if (!IsAuthenticated(ctx))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
            return null!;
        }

        private async Task<IResult> HandleLogin(HttpContext ctx)
        {
            var body = await ctx.Request.ReadFromJsonAsync<LoginRequest>();
            if (body == null)
                return Results.BadRequest(new { error = "Invalid request" });

            var settings = SettingsManager.Instance.Settings;
            var expectedUser = settings.WebServerUsername;
            var expectedPass = SettingsManager.Instance.GetWebServerPassword();

            if (string.IsNullOrEmpty(expectedPass))
                return Results.BadRequest(new { error = "Server password not configured" });

            if (body.Username != expectedUser ||
                body.Password != expectedPass)
                return Results.Json(new { error = "Invalid credentials" }, statusCode: 401);

            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            _sessions[token] = new SessionInfo { CreatedAt = DateTime.UtcNow, Username = body.Username };

            ctx.Response.Cookies.Append("iwdl_session", token, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                MaxAge = SessionTimeout,
                Path = "/"
            });

            return Results.Ok(new { token, username = body.Username });
        }

        private IResult HandleLogout(HttpContext ctx)
        {
            var token = ctx.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "")
                        ?? ctx.Request.Cookies["iwdl_session"];
            if (!string.IsNullOrEmpty(token))
                _sessions.TryRemove(token, out _);

            ctx.Response.Cookies.Delete("iwdl_session");
            return Results.Ok(new { success = true });
        }

        private IResult HandleAuthStatus(HttpContext ctx)
        {
            var settings = SettingsManager.Instance.Settings;
            bool needsAuth = !string.IsNullOrEmpty(settings.WebServerPasswordEncrypted);
            return Results.Ok(new
            {
                authenticated = IsAuthenticated(ctx),
                needsAuth
            });
        }

        #endregion

        #region Videos

        private IResult HandleGetVideos(HttpContext ctx)
        {
            var authResult = RequireAuth(ctx);
            if (authResult != null) return authResult;

            var q = ctx.Request.Query;
            int page = int.TryParse(q["page"], out var p) ? Math.Max(1, p) : 1;
            int limit = int.TryParse(q["limit"], out var l) ? Math.Clamp(l, 1, 200) : 50;
            string? channelId = q["channel"];
            string? status = q["status"];
            string? search = q["search"];
            string? sort = q["sort"];
            string? order = q["order"];
            string? rating = q["rating"];
            string? favOnly = q["favorite"];

            List<VideoInfo> videos;
            if (!string.IsNullOrEmpty(channelId) && int.TryParse(channelId, out var chId))
                videos = _database.GetVideosBySubscribedUser(chId);
            else if (!string.IsNullOrEmpty(status) && int.TryParse(status, out var st))
                videos = _database.GetVideosByStatus((DownloadStatus)st);
            else
                videos = _database.GetAllVideos();

            if (!string.IsNullOrEmpty(search))
            {
                // スペース区切りの AND 検索。各語がタイトル/アーティスト/タグ/VideoId のいずれかにマッチ
                var terms = search.Split(new[] { ' ', '　' }, StringSplitOptions.RemoveEmptyEntries);
                videos = videos.Where(v => terms.All(t =>
                    (v.Title?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (v.AuthorUsername?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (v.Tags?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (v.VideoId?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false)
                )).ToList();
            }

            if (!string.IsNullOrEmpty(rating))
            {
                videos = videos.Where(v => v.Rating == rating).ToList();
            }

            if (favOnly == "1" || favOnly == "true")
            {
                videos = videos.Where(v => v.IsFavorite).ToList();
            }

            // Downloaded only filter
            if (status == "downloaded")
            {
                videos = videos.Where(v => v.Status == DownloadStatus.Completed).ToList();
            }

            int total = videos.Count;

            videos = (sort?.ToLower()) switch
            {
                "title" => order == "asc" ? videos.OrderBy(v => v.Title).ToList() : videos.OrderByDescending(v => v.Title).ToList(),
                "author" => order == "asc" ? videos.OrderBy(v => v.AuthorUsername).ToList() : videos.OrderByDescending(v => v.AuthorUsername).ToList(),
                "date" => order == "asc" ? videos.OrderBy(v => v.PostedAt).ToList() : videos.OrderByDescending(v => v.PostedAt).ToList(),
                "size" => order == "asc" ? videos.OrderBy(v => v.FileSize).ToList() : videos.OrderByDescending(v => v.FileSize).ToList(),
                "duration" => order == "asc" ? videos.OrderBy(v => v.DurationSeconds).ToList() : videos.OrderByDescending(v => v.DurationSeconds).ToList(),
                "added" => order == "asc" ? videos.OrderBy(v => v.CreatedAt).ToList() : videos.OrderByDescending(v => v.CreatedAt).ToList(),
                _ => videos.OrderByDescending(v => v.CreatedAt).ToList()
            };

            var items = videos.Skip((page - 1) * limit).Take(limit).Select(MapVideoDto).ToList();

            return Results.Json(new
            {
                items,
                total,
                page,
                limit,
                totalPages = (int)Math.Ceiling((double)total / limit)
            }, JsonOpts);
        }

        private IResult HandleGetVideo(int id, HttpContext ctx)
        {
            var authResult = RequireAuth(ctx);
            if (authResult != null) return authResult;

            var video = _database.GetVideoById(id);
            if (video == null) return Results.NotFound(new { error = "Video not found" });

            return Results.Json(MapVideoDto(video), JsonOpts);
        }

        private async Task<IResult> HandleSetFavorite(int id, HttpContext ctx)
        {
            var authResult = RequireAuth(ctx);
            if (authResult != null) return authResult;

            var video = _database.GetVideoById(id);
            if (video == null) return Results.NotFound(new { error = "Video not found" });

            // body { "favorite": bool } で明示指定、省略時はトグル
            bool? requested = null;
            try
            {
                var body = await ctx.Request.ReadFromJsonAsync<FavoriteRequest>();
                requested = body?.Favorite;
            }
            catch { /* body 無し・不正 JSON はトグル扱い */ }

            bool fav = requested ?? !video.IsFavorite;
            _database.SetVideoFavorite(id, fav);
            return Results.Ok(new { id, favorite = fav });
        }

        private class FavoriteRequest
        {
            public bool? Favorite { get; set; }
        }

        private async Task<IResult> HandleStreamVideo(int id, HttpContext ctx)
        {
            var authResult = RequireAuth(ctx);
            if (authResult != null) return authResult;

            var video = _database.GetVideoById(id);
            if (video == null) return Results.NotFound(new { error = "Video not found" });
            if (string.IsNullOrEmpty(video.LocalFilePath) || !File.Exists(video.LocalFilePath))
                return Results.NotFound(new { error = "File not found on disk" });

            var ext = Path.GetExtension(video.LocalFilePath).ToLowerInvariant();
            var contentType = ext switch
            {
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                ".mkv" => "video/x-matroska",
                ".avi" => "video/x-msvideo",
                _ => "application/octet-stream"
            };

            return Results.File(video.LocalFilePath, contentType, enableRangeProcessing: true);
        }

        private async Task<IResult> HandleGetThumbnail(int id, HttpContext ctx)
        {
            var authResult = RequireAuth(ctx);
            if (authResult != null) return authResult;

            var video = _database.GetVideoById(id);
            if (video == null) return Results.NotFound();

            // Try local thumbnail first
            if (!string.IsNullOrEmpty(video.LocalThumbnailPath) && File.Exists(video.LocalThumbnailPath))
            {
                var thumbExt = Path.GetExtension(video.LocalThumbnailPath).ToLowerInvariant();
                var ct = thumbExt switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".webp" => "image/webp",
                    ".gif" => "image/gif",
                    _ => "image/jpeg"
                };
                return Results.File(video.LocalThumbnailPath, ct);
            }

            // Fallback to cached thumbnail (保存先は設定により Roaming / DL先フォルダ)
            var cached = ThumbnailCacheService.Instance.GetCachePath(video.VideoId);
            if (File.Exists(cached))
                return Results.File(cached, "image/jpeg");

            // Fire-and-forget: trigger async download if not yet attempted
            if (video.ThumbnailStatus == 0 && !string.IsNullOrEmpty(video.ThumbnailUrl) && !string.IsNullOrEmpty(video.VideoId))
            {
                ThumbnailCacheService.Instance.RequestAsync(video.VideoId, video.ThumbnailUrl);
            }

            return Results.NotFound();
        }

        private IResult HandleRetryThumbnail(int id, HttpContext ctx)
        {
            var authResult = RequireAuth(ctx);
            if (authResult != null) return authResult;

            var video = _database.GetVideoById(id);
            if (video == null) return Results.NotFound(new { error = "Video not found" });
            if (string.IsNullOrEmpty(video.ThumbnailUrl) || string.IsNullOrEmpty(video.VideoId))
                return Results.BadRequest(new { error = "No thumbnail URL available" });

            _database.UpdateThumbnailStatus(video.Id, 0);
            ThumbnailCacheService.Instance.RequestAsync(video.VideoId, video.ThumbnailUrl);
            return Results.Ok(new { success = true });
        }

        #endregion

        #region Channels

        private IResult HandleGetChannels(HttpContext ctx)
        {
            var authResult = RequireAuth(ctx);
            if (authResult != null) return authResult;

            var users = _database.GetAllSubscribedUsers();
            var channels = users.Select(u =>
            {
                var videos = _database.GetVideosBySubscribedUser(u.Id);
                return new
                {
                    u.Id,
                    u.UserId,
                    u.Username,
                    u.ProfileUrl,
                    u.IsEnabled,
                    u.Site,
                    totalVideos = videos.Count,
                    downloadedVideos = videos.Count(v => v.Status == DownloadStatus.Completed),
                    failedVideos = videos.Count(v => v.Status == DownloadStatus.Failed),
                    lastChecked = u.LastCheckedAt?.ToString("o")
                };
            }).ToList();

            return Results.Json(new { channels }, JsonOpts);
        }

        #endregion

        #region Errors

        private IResult HandleGetErrors(HttpContext ctx)
        {
            var authResult = RequireAuth(ctx);
            if (authResult != null) return authResult;

            var errors = _database.GetVideosByStatus(DownloadStatus.Failed);
            var items = errors.Select(MapVideoDto).ToList();

            int notFoundCount = errors.Count(v =>
                v.LastErrorMessage != null &&
                v.LastErrorMessage.Contains("not found", StringComparison.OrdinalIgnoreCase));

            return Results.Json(new
            {
                items,
                total = items.Count,
                notFoundCount
            }, JsonOpts);
        }

        private IResult HandleRetryError(int id, HttpContext ctx)
        {
            var authResult = RequireAuth(ctx);
            if (authResult != null) return authResult;

            var video = _database.GetVideoById(id);
            if (video == null) return Results.NotFound(new { error = "Video not found" });
            if (_downloadManager == null) return Results.StatusCode(503);

            SubscribedUser? user = null;
            if (video.SubscribedUserId.HasValue)
                user = _database.GetSubscribedUserById(video.SubscribedUserId.Value);

            _downloadManager.RetryFailedTask(video, user != null, user);
            return Results.Ok(new { success = true, videoId = video.VideoId });
        }

        private IResult HandleRetryAllErrors(HttpContext ctx)
        {
            var authResult = RequireAuth(ctx);
            if (authResult != null) return authResult;
            if (_downloadManager == null) return Results.StatusCode(503);

            var errors = _database.GetVideosByStatus(DownloadStatus.Failed);
            int count = 0;
            foreach (var video in errors)
            {
                SubscribedUser? user = null;
                if (video.SubscribedUserId.HasValue)
                    user = _database.GetSubscribedUserById(video.SubscribedUserId.Value);
                _downloadManager.RetryFailedTask(video, user != null, user);
                count++;
            }

            return Results.Ok(new { success = true, retriedCount = count });
        }

        private IResult HandleDeleteNotFound(HttpContext ctx)
        {
            var authResult = RequireAuth(ctx);
            if (authResult != null) return authResult;

            var errors = _database.GetVideosByStatus(DownloadStatus.Failed);
            var notFoundIds = errors
                .Where(v => v.LastErrorMessage != null &&
                           (v.LastErrorMessage.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                            v.LastErrorMessage.Contains("404", StringComparison.OrdinalIgnoreCase) ||
                            v.LastErrorMessage.Contains("deleted", StringComparison.OrdinalIgnoreCase)))
                .Select(v => v.Id)
                .ToList();

            int deleted = 0;
            if (notFoundIds.Count > 0)
                deleted = _database.DeleteVideosBatch(notFoundIds);

            _logger.Info($"Web API: Deleted {deleted} not-found videos");
            return Results.Ok(new { success = true, deletedCount = deleted });
        }

        #endregion

        #region Downloads

        private IResult HandleQueueDownload(int id, HttpContext ctx)
        {
            var authResult = RequireAuth(ctx);
            if (authResult != null) return authResult;
            if (_downloadManager == null) return Results.StatusCode(503);

            var video = _database.GetVideoById(id);
            if (video == null) return Results.NotFound(new { error = "Video not found" });

            SubscribedUser? user = null;
            if (video.SubscribedUserId.HasValue)
                user = _database.GetSubscribedUserById(video.SubscribedUserId.Value);

            _downloadManager.EnqueueDownload(video, user != null, user);
            return Results.Ok(new { success = true, videoId = video.VideoId });
        }

        private IResult HandleGetActiveDownloads(HttpContext ctx)
        {
            var authResult = RequireAuth(ctx);
            if (authResult != null) return authResult;

            if (_downloadManager == null)
                return Results.Json(new { active = 0, pending = 0, tasks = Array.Empty<object>() }, JsonOpts);

            return Results.Json(new
            {
                active = _downloadManager.ActiveTaskCount,
                downloading = _downloadManager.DownloadingCount,
                writingTags = _downloadManager.WritingTagsCount,
                pending = _downloadManager.PendingTaskCount,
                isRunning = _downloadManager.IsRunning
            }, JsonOpts);
        }

        #endregion

        #region Stats

        private IResult HandleGetStats(HttpContext ctx)
        {
            var authResult = RequireAuth(ctx);
            if (authResult != null) return authResult;

            var allVideos = _database.GetAllVideos();
            var channels = _database.GetAllSubscribedUsers();

            var completed = allVideos.Where(v => v.Status == DownloadStatus.Completed).ToList();
            long totalSize = completed.Sum(v => v.FileSize);

            return Results.Json(new
            {
                totalVideos = allVideos.Count,
                downloadedVideos = completed.Count,
                failedVideos = allVideos.Count(v => v.Status == DownloadStatus.Failed),
                pendingVideos = allVideos.Count(v => v.Status == DownloadStatus.Pending),
                skippedVideos = allVideos.Count(v => v.Status == DownloadStatus.Skipped),
                totalChannels = channels.Count,
                enabledChannels = channels.Count(c => c.IsEnabled),
                totalSizeBytes = totalSize,
                totalSizeFormatted = FormatFileSize(totalSize),
                favoriteCount = allVideos.Count(v => v.IsFavorite),
                recentDownloads = completed
                    .OrderByDescending(v => v.DownloadedAt)
                    .Take(10)
                    .Select(MapVideoDto)
                    .ToList()
            }, JsonOpts);
        }

        #endregion

        #region Batch

        private async Task<IResult> HandleDeleteBatch(HttpContext ctx)
        {
            var authResult = RequireAuth(ctx);
            if (authResult != null) return authResult;

            var body = await ctx.Request.ReadFromJsonAsync<DeleteBatchRequest>();
            if (body?.Ids == null || body.Ids.Length == 0)
                return Results.BadRequest(new { error = "No IDs specified" });

            int deleted = _database.DeleteVideosBatch(body.Ids);
            return Results.Ok(new { success = true, deletedCount = deleted });
        }

        #endregion

        #region Helpers

        private static object MapVideoDto(VideoInfo v) => new
        {
            v.Id,
            v.VideoId,
            v.Title,
            v.Url,
            v.AuthorUserId,
            v.AuthorUsername,
            v.DurationSeconds,
            durationFormatted = v.DurationSeconds > 0 ? FormatDuration(v.DurationSeconds) : (string?)null,
            postedAt = v.PostedAt?.ToString("o"),
            v.FileSize,
            fileSizeFormatted = FormatFileSize(v.FileSize),
            status = (int)v.Status,
            statusText = v.Status.ToString(),
            downloadedAt = v.DownloadedAt?.ToString("o"),
            v.SubscribedUserId,
            v.Tags,
            v.Rating,
            v.IsFavorite,
            lastErrorMessage = v.Status == DownloadStatus.Completed ? null : v.LastErrorMessage,
            v.RetryCount,
            hasFile = !string.IsNullOrEmpty(v.LocalFilePath) && File.Exists(v.LocalFilePath),
            hasThumbnail = HasThumbnailAvailable(v),
            thumbnailStatus = v.ThumbnailStatus,
            isExternal = v.IsExternal,
            v.Site,
            v.Memo,
            createdAt = v.CreatedAt.ToString("o")
        };

        private static bool HasThumbnailAvailable(VideoInfo v)
        {
            if (!string.IsNullOrEmpty(v.LocalThumbnailPath) && File.Exists(v.LocalThumbnailPath))
                return true;
            if (!string.IsNullOrEmpty(v.VideoId))
                return File.Exists(ThumbnailCacheService.Instance.GetCachePath(v.VideoId));
            return false;
        }

        private static string FormatDuration(int seconds)
        {
            if (seconds <= 0) return "0:00";
            var ts = TimeSpan.FromSeconds(seconds);
            return ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                : $"{ts.Minutes}:{ts.Seconds:D2}";
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes <= 0) return "0 B";
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            double size = bytes;
            while (size >= 1024 && i < units.Length - 1) { size /= 1024; i++; }
            return $"{size:0.##} {units[i]}";
        }

        #endregion

        #region DTOs

        private record LoginRequest(string Username, string Password);
        private record DeleteBatchRequest(int[] Ids);

        private class SessionInfo
        {
            public DateTime CreatedAt { get; set; }
            public string Username { get; set; } = "";
        }

        #endregion
    }
}
