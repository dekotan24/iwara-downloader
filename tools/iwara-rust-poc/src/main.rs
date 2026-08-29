use base64::engine::general_purpose::{URL_SAFE, URL_SAFE_NO_PAD};
use base64::Engine;
use regex::Regex;
use reqwest::blocking::{Client, Response};
use reqwest::header::{
    HeaderMap, HeaderValue, AUTHORIZATION, CONTENT_LENGTH, CONTENT_RANGE, ETAG, RANGE,
};
use reqwest::Method;
use serde_json::{json, Map, Value};
use sha1::{Digest, Sha1};
use std::env;
use std::fs::{self, File, OpenOptions};
use std::io::{self, BufRead, BufReader, Read, Seek, SeekFrom, Write};
use std::path::{Path, PathBuf};
use std::sync::mpsc;
use std::thread;
use std::time::{Duration, Instant, SystemTime};
use url::Url;

const API_URL: &str = "https://api.iwara.tv";
const SITE_TV: &str = "www.iwara.tv";
const DEFAULT_X_VERSION_SECRET: &str = "mSvL05GfEmeEmsEYfGCnVpEjYgTJraJN";
const SECRET_TTL: Duration = Duration::from_secs(30 * 24 * 60 * 60);
const RESUME_REWIND_BYTES: u64 = 65_536;
const CDN_RETRIES: usize = 6;
const CHUNK_SIZE: usize = 65_536;

fn main() {
    let mut args: Vec<String> = env::args().skip(1).collect();
    let token = take_option(&mut args, "--token").or_else(|| env::var("IWARA_TOKEN").ok());
    let site = take_option(&mut args, "--site").unwrap_or_else(|| SITE_TV.to_string());
    let secret_override =
        take_option(&mut args, "--secret").or_else(|| env::var("IWARA_X_VERSION_SECRET").ok());
    let yt_dlp_path = take_option(&mut args, "--yt-dlp-path");
    let rate = RateConfig {
        api_delay: take_option(&mut args, "--api-delay")
            .and_then(|v| v.parse().ok())
            .unwrap_or(1.0),
        page_delay: take_option(&mut args, "--page-delay")
            .and_then(|v| v.parse().ok())
            .unwrap_or(0.5),
        rate_limit_base: take_option(&mut args, "--rate-limit-base")
            .and_then(|v| v.parse().ok())
            .unwrap_or(30.0),
        rate_limit_max: take_option(&mut args, "--rate-limit-max")
            .and_then(|v| v.parse().ok())
            .unwrap_or(300.0),
        enable_backoff: !take_flag(&mut args, "--no-backoff"),
    };

    let result = match args.first().map(String::as_str) {
        Some("login") => command_login(&args[1..], &site, &rate),
        Some("verify-token") | Some("verify_token") => command_verify_token(&token, &site, &rate),
        Some("get-video") => command_get_video(&args[1..], &token, &site, &rate),
        Some("search") => command_search(&args[1..], &token, &site, &rate),
        // C#側の正式名は kebab-case。snake_caseも受け付けて、旧ビルドや
        // 外部から直接呼び出す利用者との互換性を保つ。
        Some("user-videos") | Some("get-videos") | Some("get_videos") => {
            command_user_videos(&args[1..], &token, &site, &rate)
        }
        Some("get-url") | Some("get_url") => {
            command_get_url(&args[1..], &token, &site, &rate, secret_override.as_deref())
        }
        Some("download") => {
            command_download(&args[1..], &token, &site, &rate, secret_override.as_deref())
        }
        Some("download_external") | Some("download-external") => {
            command_download_external(&args[1..], yt_dlp_path.as_deref().unwrap_or("yt-dlp"))
        }
        Some("download-test") => command_download_test(&args[1..]),
        Some("download-test-video") => command_download_test_video(
            &args[1..],
            &token,
            &site,
            &rate,
            secret_override.as_deref(),
        ),
        Some("probe") => command_probe(&args[1..], &token, &site),
        Some(other) => Err(format!("Unknown action: {other}")),
        None => Err("No action specified".to_string()),
    };

    match result {
        Ok(value) => {
            let success = value.get("success") != Some(&Value::Bool(false));
            println!(
                "{}",
                serde_json::to_string(&value).unwrap_or_else(|_| {
                    "{\"success\":false,\"error\":\"JSON serialization failed\"}".to_string()
                })
            );
            if !success {
                std::process::exit(1);
            }
        }
        Err(error) => {
            println!("{}", json!({"success": false, "error": error}));
            std::process::exit(1);
        }
    }
}

fn take_option(args: &mut Vec<String>, name: &str) -> Option<String> {
    let position = args.iter().position(|arg| arg == name)?;
    args.remove(position);
    if position < args.len() {
        Some(args.remove(position))
    } else {
        None
    }
}

fn take_flag(args: &mut Vec<String>, name: &str) -> bool {
    if let Some(position) = args.iter().position(|arg| arg == name) {
        args.remove(position);
        true
    } else {
        false
    }
}

#[derive(Clone)]
struct RateConfig {
    api_delay: f64,
    page_delay: f64,
    rate_limit_base: f64,
    rate_limit_max: f64,
    enable_backoff: bool,
}

struct RateLimiter {
    config: RateConfig,
    last_request: Option<Instant>,
    consecutive_errors: u32,
}

impl RateLimiter {
    fn new(config: &RateConfig) -> Self {
        Self {
            config: config.clone(),
            last_request: None,
            consecutive_errors: 0,
        }
    }

    fn wait(&mut self, seconds: f64) {
        let delay = Duration::from_secs_f64(seconds.max(0.0));
        if let Some(last) = self.last_request {
            if let Some(remaining) = delay.checked_sub(last.elapsed()) {
                if !remaining.is_zero() {
                    eprintln!("RateLimit: waiting {:.1}s...", remaining.as_secs_f64());
                    thread::sleep(remaining);
                }
            }
        }
        self.last_request = Some(Instant::now());
    }

    fn api_wait(&mut self) {
        self.wait(self.config.api_delay);
    }

    fn page_wait(&mut self) {
        self.wait(self.config.page_delay);
    }

    fn backoff(&mut self, status: u16, detail: &str) {
        self.consecutive_errors = self.consecutive_errors.saturating_add(1);
        let exponent = self.consecutive_errors.saturating_sub(1).min(10);
        let multiplier = if self.config.enable_backoff {
            2_f64.powi(exponent as i32)
        } else {
            1.0
        };
        let delay = (self.config.rate_limit_base * multiplier).min(self.config.rate_limit_max);
        eprintln!(
            "RateLimit: HTTP {status}, backing off for {delay:.0}s (attempt {}) {detail}",
            self.consecutive_errors
        );
        thread::sleep(Duration::from_secs_f64(delay.max(0.0)));
    }

    fn reset_errors(&mut self) {
        self.consecutive_errors = 0;
    }
}

struct HttpResult {
    status: u16,
    body: String,
}

fn client() -> Result<Client, String> {
    Client::builder()
        .user_agent(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36",
        )
        .timeout(Duration::from_secs(30))
        .cookie_store(true)
        .redirect(reqwest::redirect::Policy::limited(5))
        .build()
        .map_err(|error| format!("HTTP client initialization failed: {error}"))
}

fn request_headers(token: &Option<String>, site: &str) -> HeaderMap {
    let mut headers = HeaderMap::new();
    if let Ok(value) = HeaderValue::from_str(site) {
        headers.insert("X-Site", value);
    }
    if let Some(token) = token {
        if let Ok(value) = HeaderValue::from_str(&format!("Bearer {token}")) {
            headers.insert(AUTHORIZATION, value);
        }
    }
    headers
}

fn request_with_retry(
    client: &Client,
    method: Method,
    url: &str,
    query: &[(&str, String)],
    headers: HeaderMap,
    body: Option<Value>,
    rate: &mut RateLimiter,
) -> Result<HttpResult, String> {
    let max_attempts = 3;
    let mut last_error = String::new();

    for attempt in 0..max_attempts {
        rate.api_wait();
        let mut request = client
            .request(method.clone(), url)
            .headers(headers.clone())
            .query(query);
        if let Some(body) = &body {
            request = request.json(body);
        }

        match request.send() {
            Ok(response) => {
                let result = read_http_result(response)?;
                if is_retryable_rate_limit(&result) && attempt + 1 < max_attempts {
                    last_error = safe_error_message_text(&result.body);
                    rate.backoff(result.status, &last_error);
                    eprintln!("Retrying... (attempt {}/{max_attempts})", attempt + 2);
                    continue;
                }
                rate.reset_errors();
                return Ok(result);
            }
            Err(error) => {
                last_error = error.to_string();
                eprintln!(
                    "Request error (attempt {}/{}): {}",
                    attempt + 1,
                    max_attempts,
                    last_error
                );
                if attempt + 1 < max_attempts {
                    thread::sleep(Duration::from_secs_f64(
                        rate.config.api_delay * (attempt + 1) as f64,
                    ));
                }
            }
        }
    }

    Err(format!(
        "Request failed after {max_attempts} attempts: {last_error}"
    ))
}

fn read_http_result(mut response: Response) -> Result<HttpResult, String> {
    let status = response.status().as_u16();
    let mut bytes = Vec::new();
    response
        .read_to_end(&mut bytes)
        .map_err(|error| format!("response read failed: {error}"))?;
    Ok(HttpResult {
        status,
        body: String::from_utf8_lossy(&bytes).into_owned(),
    })
}

fn api_get(
    client: &Client,
    path: &str,
    query: &[(&str, String)],
    token: &Option<String>,
    site: &str,
    rate: &mut RateLimiter,
) -> Result<HttpResult, String> {
    request_with_retry(
        client,
        Method::GET,
        &format!("{API_URL}{path}"),
        query,
        request_headers(token, site),
        None,
        rate,
    )
}

fn parse_body(result: &HttpResult) -> Value {
    serde_json::from_str(&result.body).unwrap_or(Value::Null)
}

fn command_login(args: &[String], site: &str, rate_config: &RateConfig) -> Result<Value, String> {
    let email = args
        .first()
        .cloned()
        .or_else(|| env::var("IWARA_EMAIL").ok())
        .ok_or("Usage: login <email> <password>")?;
    let password = args
        .get(1)
        .cloned()
        .or_else(|| env::var("IWARA_PASSWORD").ok())
        .ok_or("Usage: login <email> <password>")?;
    let http = client()?;
    let mut rate = RateLimiter::new(rate_config);
    let response = request_with_retry(
        &http,
        Method::POST,
        &format!("{API_URL}/user/login"),
        &[],
        request_headers(&None, site),
        Some(json!({"email": email, "password": password})),
        &mut rate,
    )?;
    let body = parse_body(&response);

    if response.status == 401 {
        return Ok(json!({"success": false, "error": "Invalid email or password"}));
    }
    if response.status == 403 {
        return Ok(json!({
            "success": false,
            "error": format!("Login blocked: {}", safe_error_message(&body, "Too many attempts or account issue"))
        }));
    }
    if response.status != 200 {
        return Ok(json!({
            "success": false,
            "error": format!("Login failed: HTTP {} - {}", response.status, safe_error_message(&body, ""))
        }));
    }

    let token = body
        .get("token")
        .and_then(Value::as_str)
        .unwrap_or_default();
    if token.is_empty() {
        return Ok(json!({"success": false, "error": "No token in response"}));
    }
    let payload = decode_jwt_payload(token).unwrap_or(Value::Null);
    Ok(json!({
        "success": true,
        "token": token,
        "expires_at": payload.get("exp").cloned().unwrap_or(Value::Null),
        "user_id": payload.get("id").cloned().unwrap_or(Value::Null),
        "token_type": payload.get("type").cloned().unwrap_or(Value::Null)
    }))
}

fn command_verify_token(
    token: &Option<String>,
    site: &str,
    rate_config: &RateConfig,
) -> Result<Value, String> {
    let Some(token) = token else {
        return Ok(json!({
            "success": false,
            "error": "No token",
            "code": "LOGIN_REQUIRED"
        }));
    };
    let payload = decode_jwt_payload(token).unwrap_or(Value::Null);
    if let Some(exp) = payload.get("exp").and_then(Value::as_i64) {
        if unix_now() >= exp {
            return Ok(json!({
                "success": false,
                "error": "Token expired",
                "code": "TOKEN_EXPIRED",
                "expires_at": exp
            }));
        }
    }

    let http = client()?;
    let mut rate = RateLimiter::new(rate_config);
    let response = api_get(&http, "/user", &[], &Some(token.clone()), site, &mut rate)?;
    let body = parse_body(&response);
    if response.status == 401 || response.status == 403 {
        return Ok(json!({
            "success": false,
            "error": format!("Token rejected: HTTP {}", response.status),
            "code": "TOKEN_INVALID"
        }));
    }
    if response.status != 200 {
        return Ok(json!({
            "success": false,
            "error": format!("HTTP {}", response.status),
            "code": "API_ERROR"
        }));
    }
    let user = body.get("user").cloned().unwrap_or(Value::Null);
    Ok(json!({
        "success": true,
        "expires_at": payload.get("exp").cloned().unwrap_or(Value::Null),
        "user_id": user.get("id").cloned().unwrap_or(Value::Null),
        "username": user.get("username").cloned().unwrap_or(Value::Null),
        "role": user.get("role").cloned().unwrap_or(Value::Null),
        "premium": user.get("premium").cloned().unwrap_or(Value::Bool(false))
    }))
}

fn command_get_video(
    args: &[String],
    token: &Option<String>,
    site: &str,
    rate_config: &RateConfig,
) -> Result<Value, String> {
    let video_id = args.first().ok_or("Usage: get-video <video_id>")?;
    let http = client()?;
    let mut rate = RateLimiter::new(rate_config);
    let response = api_get(
        &http,
        &format!("/video/{video_id}"),
        &[],
        token,
        site,
        &mut rate,
    )?;
    let body = parse_body(&response);
    if response.status != 200 {
        return Ok(video_error(response.status, &body, video_id));
    }
    Ok(json!({"success": true, "data": body}))
}

fn command_search(
    args: &[String],
    token: &Option<String>,
    site: &str,
    rate_config: &RateConfig,
) -> Result<Value, String> {
    let query = args.first().ok_or("Usage: search <query> [page] [limit]")?;
    if token.is_none() {
        return Ok(login_required());
    }
    let page = args
        .get(1)
        .and_then(|value| value.parse::<u32>().ok())
        .unwrap_or(0);
    let limit = args
        .get(2)
        .and_then(|value| value.parse::<u32>().ok())
        .unwrap_or(32);
    let http = client()?;
    let mut rate = RateLimiter::new(rate_config);
    let response = api_get(
        &http,
        "/search",
        &[
            ("type", "videos".to_string()),
            ("query", query.to_string()),
            ("page", page.to_string()),
            ("limit", limit.to_string()),
        ],
        token,
        site,
        &mut rate,
    )?;
    let body = parse_body(&response);
    if response.status != 200 {
        return Ok(json!({
            "success": false,
            "error": format!("Search failed: HTTP {} - {}", response.status, safe_error_message(&body, ""))
        }));
    }

    let videos = body
        .get("results")
        .and_then(Value::as_array)
        .map(|items| items.iter().map(map_search_video).collect::<Vec<_>>())
        .unwrap_or_default();
    Ok(json!({
        "success": true,
        "count": body.get("count").cloned().unwrap_or(json!(videos.len())),
        "page": page,
        "limit": limit,
        "videos": videos
    }))
}

fn command_user_videos(
    args: &[String],
    token: &Option<String>,
    site: &str,
    rate_config: &RateConfig,
) -> Result<Value, String> {
    let username = args.first().ok_or("Usage: get_videos <username>")?;
    if token.is_none() {
        return Ok(login_required());
    }
    let http = client()?;
    let mut rate = RateLimiter::new(rate_config);
    let profile = api_get(
        &http,
        &format!("/profile/{username}"),
        &[],
        token,
        site,
        &mut rate,
    )?;
    let profile_body = parse_body(&profile);
    if profile.status == 404 {
        return Ok(json!({
            "success": false,
            "error": format!("User not found: {username}"),
            "code": "USER_NOT_FOUND"
        }));
    }
    if profile.status != 200 {
        return Ok(json!({
            "success": false,
            "error": format!("Profile fetch failed: HTTP {} - {}", profile.status, safe_error_message(&profile_body, ""))
        }));
    }
    let user_id = profile_body
        .get("user")
        .and_then(|user| user.get("id"))
        .and_then(Value::as_str)
        .ok_or("User ID not found")?;

    let mut videos = Vec::new();
    let mut page = 0_u32;
    while page < 100 {
        if page > 0 {
            rate.page_wait();
        }
        let response = api_get(
            &http,
            "/videos",
            &[
                ("page", page.to_string()),
                ("sort", "date".to_string()),
                ("user", user_id.to_string()),
                ("limit", "32".to_string()),
            ],
            token,
            site,
            &mut rate,
        )?;
        let body = parse_body(&response);
        if response.status != 200 {
            eprintln!("Page {page} fetch failed, stopping pagination");
            break;
        }
        let Some(items) = body.get("results").and_then(Value::as_array) else {
            break;
        };
        if items.is_empty() {
            break;
        }
        for video in items {
            videos.push(map_user_video(video));
        }
        eprintln!(
            "Fetched page {}, {} videos (total: {})",
            page + 1,
            items.len(),
            videos.len()
        );
        page += 1;
    }

    Ok(json!({
        "success": true,
        "username": username,
        "user_id": user_id,
        "count": videos.len(),
        "videos": videos
    }))
}

fn command_get_url(
    args: &[String],
    token: &Option<String>,
    site: &str,
    rate_config: &RateConfig,
    secret_override: Option<&str>,
) -> Result<Value, String> {
    let video_id = args.first().ok_or("Usage: get_url <video_id>")?;
    if token.is_none() {
        return Ok(login_required());
    }
    let quality = args.get(1).map(String::as_str).unwrap_or("Source");
    let http = client()?;
    let mut rate = RateLimiter::new(rate_config);
    let resolved = match resolve_download_url(
        &http,
        video_id,
        quality,
        token,
        site,
        &mut rate,
        secret_override,
    ) {
        Ok(resolved) => resolved,
        Err(error) => return Ok(json!({"success": false, "error": error})),
    };
    let video = &resolved.video;
    let user = video.get("user").cloned().unwrap_or(Value::Null);
    let file = video.get("file").cloned().unwrap_or(Value::Null);
    Ok(json!({
        "success": true,
        "url": resolved.url,
        "quality": resolved.quality,
        "title": video.get("title").cloned().unwrap_or(json!(video_id)),
        "file_id": file.get("id").cloned().unwrap_or(Value::Null),
        "author_username": user.get("username").cloned().unwrap_or(Value::Null),
        "author_name": user.get("name").cloned().unwrap_or(Value::Null),
        "rating": video.get("rating").cloned().unwrap_or(json!("")),
        "thumbnail": thumbnail_url(video),
        "created_at": video.get("createdAt").cloned().unwrap_or(Value::Null),
        "raw": prune_video_raw(video)
    }))
}

fn command_download(
    args: &[String],
    token: &Option<String>,
    site: &str,
    rate_config: &RateConfig,
    secret_override: Option<&str>,
) -> Result<Value, String> {
    let video_id = args
        .first()
        .ok_or("Usage: download <video_id> <output_path>")?;
    let output_path = args
        .get(1)
        .ok_or("Usage: download <video_id> <output_path>")?;
    if token.is_none() {
        return Ok(login_required());
    }
    let http = client()?;
    let mut rate = RateLimiter::new(rate_config);
    download_video(
        &http,
        video_id,
        output_path,
        token,
        site,
        &mut rate,
        secret_override,
    )
}

fn command_download_external(args: &[String], configured_path: &str) -> Result<Value, String> {
    let embed_url = args
        .first()
        .ok_or("Usage: download_external <embed_url> <output_path>")?;
    let output_path = args
        .get(1)
        .ok_or("Usage: download_external <embed_url> <output_path>")?;
    let path = resolve_yt_dlp(configured_path).ok_or_else(|| {
        format!(
            "yt-dlp executable was not found: {}. Install the standalone yt-dlp.exe or configure its path.",
            configured_path
        )
    })?;
    match run_yt_dlp(&path, embed_url, output_path) {
        Ok(file_path) => Ok(json!({
            "success": true,
            "file_path": file_path,
            "url": embed_url
        })),
        Err(error) => Ok(json!({"success": false, "error": error})),
    }
}

struct ResolvedDownload {
    video: Value,
    url: String,
    quality: String,
    secret_source: String,
}

fn resolve_download_url(
    client: &Client,
    video_id: &str,
    requested_quality: &str,
    token: &Option<String>,
    site: &str,
    rate: &mut RateLimiter,
    secret_override: Option<&str>,
) -> Result<ResolvedDownload, String> {
    let video_response = api_get(
        client,
        &format!("/video/{video_id}"),
        &[],
        token,
        site,
        rate,
    )?;
    let video = parse_body(&video_response);
    if video_response.status != 200 {
        return Err(video_error_message(video_response.status, &video, video_id));
    }

    let file_url = video
        .get("fileUrl")
        .and_then(Value::as_str)
        .ok_or("No fileUrl in video data")?;
    let file_url = normalize_download_url(file_url);
    let parsed = Url::parse(&file_url).map_err(|error| format!("invalid fileUrl: {error}"))?;
    let file_id = parsed
        .path_segments()
        .and_then(|segments| segments.last())
        .filter(|value| !value.is_empty())
        .ok_or("fileUrl has no file id")?;
    let expires = parsed
        .query_pairs()
        .find(|(key, _)| key == "expires")
        .map(|(_, value)| value.into_owned())
        .unwrap_or_default();

    let (mut secret, mut secret_source) = if let Some(value) = secret_override {
        (value.to_string(), "override".to_string())
    } else {
        resolve_secret(client)?
    };
    let mut files = fetch_files(
        client, &file_url, file_id, &expires, &secret, token, site, rate,
    )?;
    if !has_high_quality(&files) && secret_override.is_none() {
        eprintln!(
            "Low-quality only response detected. Refreshing X-Version secret from main.js..."
        );
        if let Ok((new_secret, _)) = extract_secret_from_main_js(client) {
            if new_secret != secret {
                let _ = save_cached_secret(&new_secret);
                secret = new_secret;
                secret_source = "main_js_refresh".to_string();
                files = fetch_files(
                    client, &file_url, file_id, &expires, &secret, token, site, rate,
                )?;
            }
        }
    }

    let quality_order = ["Source", "540", "360", "preview"];
    let mut search_order = Vec::new();
    if quality_order.contains(&requested_quality) {
        search_order.push(requested_quality);
    }
    for quality in quality_order {
        if !search_order.contains(&quality) {
            search_order.push(quality);
        }
    }

    for quality in search_order {
        for file in &files {
            if file.get("name").and_then(Value::as_str) != Some(quality) {
                continue;
            }
            let Some(src) = file.get("src").and_then(Value::as_object) else {
                continue;
            };
            let raw_url = src
                .get("download")
                .or_else(|| src.get("view"))
                .and_then(Value::as_str);
            if let Some(raw_url) = raw_url {
                return Ok(ResolvedDownload {
                    video,
                    url: normalize_download_url(raw_url),
                    quality: quality.to_string(),
                    secret_source,
                });
            }
        }
    }
    Err(format!(
        "No download URL found; available qualities={:?}",
        files
            .iter()
            .filter_map(|file| file.get("name").and_then(Value::as_str))
            .collect::<Vec<_>>()
    ))
}

fn fetch_files(
    client: &Client,
    file_url: &str,
    file_id: &str,
    expires: &str,
    secret: &str,
    token: &Option<String>,
    site: &str,
    rate: &mut RateLimiter,
) -> Result<Vec<Value>, String> {
    let x_version = sha1_hex(&format!("{file_id}_{expires}_{secret}"));
    let mut headers = request_headers(token, site);
    headers.insert(
        "X-Version",
        HeaderValue::from_str(&x_version).map_err(|error| error.to_string())?,
    );
    let response = request_with_retry(client, Method::GET, file_url, &[], headers, None, rate)?;
    let body = parse_body(&response);
    if response.status == 403 {
        return Err(format!(
            "Access denied to download: {}",
            safe_error_message(&body, "Private video or login required")
        ));
    }
    if response.status != 200 {
        return Err(format!(
            "File URL fetch failed: HTTP {} - {}",
            response.status,
            safe_error_message(&body, "")
        ));
    }
    if let Some(files) = body.as_array() {
        return Ok(files.clone());
    }
    if let Some(files) = body.get("files").and_then(Value::as_array) {
        return Ok(files.clone());
    }
    Err("Failed to parse filesq response: file list not found".to_string())
}

enum DownloadKind {
    Success(u64),
    CdnError(String),
    AuthError(String),
    HardError(String),
}

fn download_video(
    client: &Client,
    video_id: &str,
    output_path: &str,
    token: &Option<String>,
    site: &str,
    rate: &mut RateLimiter,
    secret_override: Option<&str>,
) -> Result<Value, String> {
    let mut last_error = String::new();
    let mut tried_hosts = Vec::new();

    for attempt in 0..CDN_RETRIES {
        let resolved = match resolve_download_url(
            client,
            video_id,
            "Source",
            token,
            site,
            rate,
            secret_override,
        ) {
            Ok(value) => value,
            Err(error) => return Err(error),
        };
        let quality = resolved.quality.clone();
        let video = &resolved.video;
        let file = video.get("file").cloned().unwrap_or(Value::Null);
        let user = video.get("user").cloned().unwrap_or(Value::Null);
        let file_id = file
            .get("id")
            .and_then(Value::as_str)
            .unwrap_or_default()
            .to_string();
        let author_username = user.get("username").cloned().unwrap_or(Value::Null);
        let author_name = user.get("name").cloned().unwrap_or(Value::Null);
        let title = video.get("title").cloned().unwrap_or(Value::Null);
        let host = Url::parse(&resolved.url)
            .ok()
            .and_then(|url| url.host_str().map(str::to_string))
            .unwrap_or_default();
        eprintln!(
            "Downloading: {} ({}) [attempt {}/{CDN_RETRIES}, host={host}]",
            title.as_str().unwrap_or(video_id),
            quality,
            attempt + 1
        );

        let meta_path = format!("{output_path}.part.meta");
        let resume_meta = read_resume_meta(&meta_path);
        match try_download_once(
            client,
            &resolved.url,
            output_path,
            &file_id,
            resume_meta.as_ref(),
            token,
            site,
        ) {
            DownloadKind::Success(size) => {
                eprintln!("Progress: 100%");
                return Ok(json!({
                    "success": true,
                    "path": output_path,
                    "size": size,
                    "quality": quality,
                    "file_id": file_id,
                    "author_username": author_username,
                    "author_name": author_name,
                    "title": title,
                    "cdn_retries": attempt
                }));
            }
            DownloadKind::AuthError(error) => {
                return Err(json_error_string(&error, "ACCESS_DENIED"));
            }
            DownloadKind::HardError(error) => return Err(error),
            DownloadKind::CdnError(error) => {
                last_error = error.clone();
                tried_hosts.push(format!("{host}={error}"));
                eprintln!("CDN error ({error}), retrying with fresh URL...");
                thread::sleep(Duration::from_secs_f64(
                    (1.0 + attempt as f64 * 0.5).min(3.0),
                ));
            }
        }
    }

    Err(format!(
        "All CDN candidates failed ({} retries): {}",
        tried_hosts.len(),
        if tried_hosts.is_empty() {
            last_error
        } else {
            tried_hosts.join(" | ")
        }
    ))
}

fn try_download_once(
    client: &Client,
    download_url: &str,
    output_path: &str,
    file_id: &str,
    resume_meta: Option<&Value>,
    token: &Option<String>,
    site: &str,
) -> DownloadKind {
    let part_path = format!("{output_path}.part");
    let meta_path = format!("{output_path}.part.meta");
    let mut resume_from = 0_u64;
    if let (Some(meta), Ok(part_size)) = (resume_meta, fs::metadata(&part_path).map(|m| m.len())) {
        let meta_file_id = meta
            .get("file_id")
            .and_then(Value::as_str)
            .unwrap_or_default();
        if meta_file_id == file_id && part_size > RESUME_REWIND_BYTES {
            resume_from = part_size - RESUME_REWIND_BYTES;
            eprintln!("Resuming from {resume_from} bytes (part size={part_size}, file_id match)");
        } else if meta_file_id != file_id {
            eprintln!("file_id mismatch; discarding .part");
            remove_if_exists(&part_path);
            remove_if_exists(&meta_path);
        }
    }

    let mut headers = request_headers(token, site);
    if resume_from > 0 {
        if let Ok(value) = HeaderValue::from_str(&format!("bytes={resume_from}-")) {
            headers.insert(RANGE, value);
        }
    }
    let response = match client.get(download_url).headers(headers).send() {
        Ok(response) => response,
        Err(error) => {
            return DownloadKind::CdnError(format!(
                "Connection failed: {}",
                truncate(&error.to_string(), 200)
            ))
        }
    };
    let status = response.status().as_u16();
    if status == 403 {
        return DownloadKind::AuthError("Download blocked (403)".to_string());
    }
    if matches!(status, 404 | 410 | 500 | 502 | 503 | 504) {
        return DownloadKind::CdnError(format!("CDN returned {status}"));
    }
    if resume_from > 0 && status == 200 {
        eprintln!("Server ignored Range header, restarting from 0");
        resume_from = 0;
    } else if resume_from > 0 && status == 416 {
        eprintln!("Range Not Satisfiable (416), discarding .part");
        remove_if_exists(&part_path);
        remove_if_exists(&meta_path);
        return DownloadKind::CdnError("Range not satisfiable; retry from 0".to_string());
    } else if resume_from > 0 && status != 206 {
        return DownloadKind::HardError(format!("Unexpected status {status} for Range request"));
    } else if resume_from == 0 && status != 200 && status != 206 {
        return DownloadKind::HardError(format!("Download failed: HTTP {status}"));
    }

    let headers = response.headers().clone();
    let total_size = content_range_total(&headers)
        .or_else(|| {
            headers
                .get(CONTENT_LENGTH)
                .and_then(|value| value.to_str().ok())
                .and_then(|value| value.parse::<u64>().ok())
                .map(|value| value + resume_from)
        })
        .unwrap_or(0);
    let server_etag = headers
        .get(ETAG)
        .and_then(|value| value.to_str().ok())
        .unwrap_or_default()
        .to_string();

    if let (Some(meta), true) = (resume_meta, resume_from > 0) {
        let meta_size = meta.get("size").and_then(Value::as_u64).unwrap_or(0);
        let meta_etag = meta.get("etag").and_then(Value::as_str).unwrap_or_default();
        if meta_size > 0 && total_size > 0 && meta_size != total_size {
            remove_if_exists(&part_path);
            remove_if_exists(&meta_path);
            return DownloadKind::CdnError("Resume size mismatch".to_string());
        }
        if !meta_etag.is_empty() && !server_etag.is_empty() && meta_etag != server_etag {
            remove_if_exists(&part_path);
            remove_if_exists(&meta_path);
            return DownloadKind::CdnError("Resume etag mismatch".to_string());
        }
    }

    if let Some(parent) = Path::new(&part_path).parent() {
        if let Err(error) = fs::create_dir_all(parent) {
            return DownloadKind::HardError(format!("Cannot create output directory: {error}"));
        }
    }
    if !file_id.is_empty() {
        let meta = json!({
            "file_id": file_id,
            "size": total_size,
            "etag": server_etag,
            "last_modified": headers.get("last-modified").and_then(|v| v.to_str().ok()).unwrap_or_default()
        });
        if let Err(error) = write_resume_meta(&meta_path, &meta) {
            eprintln!("Failed to write resume meta: {error}");
        }
    }

    let mut file = if resume_from > 0 {
        match OpenOptions::new().read(true).write(true).open(&part_path) {
            Ok(mut file) => {
                if let Err(error) = file
                    .set_len(resume_from)
                    .and_then(|_| file.seek(SeekFrom::Start(resume_from)))
                {
                    return DownloadKind::HardError(format!("Cannot prepare .part file: {error}"));
                }
                file
            }
            Err(error) => {
                return DownloadKind::HardError(format!("Cannot open .part file: {error}"))
            }
        }
    } else {
        match File::create(&part_path) {
            Ok(file) => file,
            Err(error) => {
                return DownloadKind::HardError(format!("Cannot create .part file: {error}"))
            }
        }
    };

    let mut response = response;
    let mut downloaded = resume_from;
    let mut last_percent = -1_i64;
    let mut buffer = vec![0_u8; CHUNK_SIZE];
    loop {
        match response.read(&mut buffer) {
            Ok(0) => break,
            Ok(read) => {
                if let Err(error) = file.write_all(&buffer[..read]) {
                    return DownloadKind::HardError(format!("Write error: {error}"));
                }
                downloaded += read as u64;
                if total_size > 0 {
                    let percent = (downloaded.saturating_mul(100) / total_size) as i64;
                    if percent > last_percent {
                        eprintln!("Progress: {percent}%");
                        last_percent = percent;
                    }
                }
            }
            Err(error) => {
                return DownloadKind::CdnError(format!(
                    "Stream error ({downloaded} bytes): {}",
                    truncate(&error.to_string(), 200)
                ));
            }
        }
    }

    if total_size > 0 && downloaded != total_size {
        return DownloadKind::CdnError(format!(
            "Size mismatch: got {downloaded}, expected {total_size}"
        ));
    }
    drop(file);
    if Path::new(output_path).exists() {
        if let Err(error) = fs::remove_file(output_path) {
            return DownloadKind::HardError(format!("Cannot replace existing output: {error}"));
        }
    }
    if let Err(error) = fs::rename(&part_path, output_path) {
        return DownloadKind::HardError(format!("Failed to finalize output: {error}"));
    }
    remove_if_exists(&meta_path);
    DownloadKind::Success(downloaded)
}

fn resolve_yt_dlp(configured_path: &str) -> Option<PathBuf> {
    let configured = if configured_path.trim().is_empty() {
        "yt-dlp"
    } else {
        configured_path
    };
    let configured_path = Path::new(configured);
    if configured_path.is_absolute() && configured_path.is_file() {
        return Some(configured_path.to_path_buf());
    }
    if configured_path.components().count() > 1 && configured_path.is_file() {
        return Some(configured_path.to_path_buf());
    }
    if let Ok(current_exe) = env::current_exe() {
        if let Some(parent) = current_exe.parent() {
            for name in ["yt-dlp.exe", "yt-dlp"] {
                let bundled = parent.join(name);
                if bundled.is_file() {
                    return Some(bundled);
                }
            }
        }
    }
    Some(PathBuf::from(configured))
}

fn run_yt_dlp(path: &Path, embed_url: &str, output_path: &str) -> Result<String, String> {
    let output_template = if Path::new(output_path).extension().is_none() {
        format!("{output_path}.%(ext)s")
    } else {
        output_path.to_string()
    };
    let mut command = std::process::Command::new(path);
    command
        .arg("-o")
        .arg(&output_template)
        .arg("--no-playlist")
        .arg("--no-warnings")
        .arg("--newline")
        .arg("--merge-output-format")
        .arg("mp4")
        .arg(embed_url)
        .stdout(std::process::Stdio::piped())
        .stderr(std::process::Stdio::piped());
    eprintln!("yt-dlp: {}", path.display());
    let mut child = command
        .spawn()
        .map_err(|error| format!("yt-dlp start failed: {error}"))?;
    let stdout = child.stdout.take();
    let stderr = child.stderr.take();
    let (sender, receiver) = mpsc::channel::<String>();
    for stream in [
        stdout.map(|s| Box::new(s) as Box<dyn Read + Send>),
        stderr.map(|s| Box::new(s) as Box<dyn Read + Send>),
    ] {
        if let Some(stream) = stream {
            let sender = sender.clone();
            thread::spawn(move || {
                let reader = BufReader::new(stream);
                for line in reader.lines().map_while(Result::ok) {
                    let _ = sender.send(line);
                }
            });
        }
    }
    drop(sender);
    let mut recent = Vec::new();
    for line in receiver {
        eprintln!("{line}");
        if let Some(percent) = parse_download_percent(&line) {
            eprintln!("Progress: {percent}%");
        }
        recent.push(line);
        if recent.len() > 20 {
            recent.remove(0);
        }
    }
    let status = child
        .wait()
        .map_err(|error| format!("yt-dlp wait failed: {error}"))?;
    if !status.success() {
        return Err(format!(
            "yt-dlp failed (exit={}): {}",
            status.code().unwrap_or(-1),
            recent
                .into_iter()
                .rev()
                .take(5)
                .collect::<Vec<_>>()
                .join("\n")
        ));
    }
    Ok(find_saved_file(output_path))
}

fn find_saved_file(base_path: &str) -> String {
    if Path::new(base_path).is_file() {
        return base_path.to_string();
    }
    let parent = Path::new(base_path)
        .parent()
        .unwrap_or_else(|| Path::new("."));
    let prefix = Path::new(base_path)
        .file_name()
        .and_then(|value| value.to_str())
        .unwrap_or_default();
    let mut candidates = Vec::new();
    if let Ok(entries) = fs::read_dir(parent) {
        for entry in entries.flatten() {
            let path = entry.path();
            let name = path
                .file_name()
                .and_then(|value| value.to_str())
                .unwrap_or_default();
            if name.starts_with(&format!("{prefix}.")) && path.is_file() {
                let modified = path
                    .metadata()
                    .and_then(|m| m.modified())
                    .unwrap_or(SystemTime::UNIX_EPOCH);
                candidates.push((modified, path));
            }
        }
    }
    candidates.sort_by_key(|(modified, _)| *modified);
    candidates
        .last()
        .map(|(_, path)| path.to_string_lossy().into_owned())
        .unwrap_or_else(|| base_path.to_string())
}

fn command_download_test(args: &[String]) -> Result<Value, String> {
    let url = args.first().ok_or("Usage: download-test <url> [output]")?;
    let http = client()?;
    range_probe(&http, url, args.get(1).map(String::as_str))
}

fn command_download_test_video(
    args: &[String],
    token: &Option<String>,
    site: &str,
    rate_config: &RateConfig,
    secret_override: Option<&str>,
) -> Result<Value, String> {
    let video_id = args
        .first()
        .ok_or("Usage: download-test-video <video_id> [quality]")?;
    if token.is_none() {
        return Ok(login_required());
    }
    let quality = args.get(1).map(String::as_str).unwrap_or("Source");
    let http = client()?;
    let mut rate = RateLimiter::new(rate_config);
    let resolved = resolve_download_url(
        &http,
        video_id,
        quality,
        token,
        site,
        &mut rate,
        secret_override,
    )?;
    let mut result = range_probe(&http, &resolved.url, None)?;
    if let Some(object) = result.as_object_mut() {
        object.insert("video_id".to_string(), json!(video_id));
        object.insert("quality".to_string(), json!(resolved.quality));
        object.insert("secret_source".to_string(), json!(resolved.secret_source));
    }
    Ok(result)
}

fn range_probe(client: &Client, url: &str, output: Option<&str>) -> Result<Value, String> {
    let parsed = Url::parse(url).map_err(|error| format!("invalid URL: {error}"))?;
    let end = RESUME_REWIND_BYTES - 1;
    let response = client
        .get(url)
        .header(RANGE, format!("bytes=0-{end}"))
        .send()
        .map_err(|error| format!("range request failed: {error}"))?;
    let status = response.status().as_u16();
    let headers = response.headers().clone();
    let mut body = Vec::new();
    response
        .take(RESUME_REWIND_BYTES)
        .read_to_end(&mut body)
        .map_err(|error| format!("range body read failed: {error}"))?;
    if let Some(output) = output {
        let mut file = File::create(output)
            .map_err(|error| format!("partial output create failed: {error}"))?;
        file.write_all(&body)
            .map_err(|error| format!("partial output write failed: {error}"))?;
    }
    Ok(json!({
        "success": status == 206 || status == 200,
        "status": status,
        "host": parsed.host_str().unwrap_or_default(),
        "range_requested": format!("bytes=0-{end}"),
        "bytes_read": body.len(),
        "content_length": headers.get(CONTENT_LENGTH).and_then(|value| value.to_str().ok()).unwrap_or_default(),
        "content_range": headers.get(CONTENT_RANGE).and_then(|value| value.to_str().ok()).unwrap_or_default(),
        "etag_present": headers.get(ETAG).is_some(),
        "partial_output_written": output.is_some()
    }))
}

fn command_probe(args: &[String], token: &Option<String>, site: &str) -> Result<Value, String> {
    let url = args.first().ok_or("Usage: probe <url>")?;
    let http = client()?;
    let response = http
        .get(url)
        .headers(request_headers(token, site))
        .send()
        .map_err(|error| format!("probe request failed: {error}"))?;
    let status = response.status().as_u16();
    let final_url = safe_url(response.url());
    let headers = response.headers().clone();
    let body = response
        .bytes()
        .map_err(|error| format!("probe body read failed: {error}"))?;
    let lower = String::from_utf8_lossy(&body).to_ascii_lowercase();
    Ok(json!({
        "success": (200..400).contains(&status),
        "status": status,
        "final_url": final_url,
        "body_len": body.len(),
        "body_sha1": sha1_bytes(&body),
        "content_type": headers.get("content-type").and_then(|value| value.to_str().ok()).unwrap_or_default(),
        "server": headers.get("server").and_then(|value| value.to_str().ok()).unwrap_or_default(),
        "cf_ray_present": headers.get("cf-ray").is_some(),
        "cf_cache_status": headers.get("cf-cache-status").and_then(|value| value.to_str().ok()).unwrap_or_default(),
        "set_cookie_present": headers.get("set-cookie").is_some(),
        "challenge_indicators_present": lower.contains("cf-chl") || lower.contains("captcha") || lower.contains("turnstile") || lower.contains("verify you are human")
    }))
}

fn map_search_video(video: &Value) -> Value {
    let user = video.get("user").cloned().unwrap_or(Value::Null);
    let file = video.get("file").cloned().unwrap_or(Value::Null);
    json!({
        "id": video.get("id").cloned().unwrap_or(Value::Null),
        "title": video.get("title").cloned().unwrap_or(json!("")),
        "thumbnail": thumbnail_url(video),
        "duration": file.get("duration").cloned().unwrap_or(json!(0)),
        "rating": video.get("rating").cloned().unwrap_or(json!("")),
        "author_username": user.get("username").cloned().unwrap_or(json!("")),
        "author_name": user.get("name").cloned().unwrap_or(json!("")),
        "embed_url": video.get("embedUrl").cloned().unwrap_or(json!("")),
        "private": video.get("private").cloned().unwrap_or(json!(false)),
        "created_at": video.get("createdAt").cloned().unwrap_or(Value::Null)
    })
}

fn map_user_video(video: &Value) -> Value {
    let file = video.get("file").cloned().unwrap_or(Value::Null);
    json!({
        "id": video.get("id").cloned().unwrap_or(Value::Null),
        "title": video.get("title").cloned().unwrap_or(Value::Null),
        "slug": video.get("slug").cloned().unwrap_or(Value::Null),
        "thumbnail": thumbnail_url(video),
        "duration": file.get("duration").cloned().unwrap_or(json!(0)),
        "created_at": video.get("createdAt").cloned().unwrap_or(Value::Null),
        "private": video.get("private").cloned().unwrap_or(json!(false)),
        "embed_url": video.get("embedUrl").cloned().unwrap_or(json!("")),
        "rating": video.get("rating").cloned().unwrap_or(json!("")),
        "raw": prune_video_raw(video)
    })
}

fn thumbnail_url(video: &Value) -> Value {
    video
        .get("file")
        .and_then(|file| file.get("id"))
        .and_then(Value::as_str)
        .map(|id| {
            Value::String(format!(
                "https://i.iwara.tv/image/thumbnail/{id}/thumbnail-00.jpg"
            ))
        })
        .unwrap_or_else(|| json!(""))
}

fn prune_video_raw(video: &Value) -> Value {
    let Some(object) = video.as_object() else {
        return video.clone();
    };
    let mut pruned = object.clone();
    pruned.remove("siteId");
    if let Some(user) = pruned.get("user").and_then(Value::as_object) {
        let mut reduced = Map::new();
        for key in ["id", "name", "username"] {
            if let Some(value) = user.get(key) {
                reduced.insert(key.to_string(), value.clone());
            }
        }
        pruned.insert("user".to_string(), Value::Object(reduced));
    }
    Value::Object(pruned)
}

fn resolve_secret(client: &Client) -> Result<(String, String), String> {
    if let Ok(secret) = env::var("IWARA_X_VERSION_SECRET") {
        if !secret.trim().is_empty() {
            return Ok((secret.trim().to_string(), "env".to_string()));
        }
    }
    let path = secret_cache_path();
    if let Ok(metadata) = fs::metadata(&path) {
        if let Ok(modified) = metadata.modified() {
            if SystemTime::now()
                .duration_since(modified)
                .unwrap_or(SECRET_TTL + Duration::from_secs(1))
                < SECRET_TTL
            {
                if let Ok(secret) = fs::read_to_string(&path) {
                    if !secret.trim().is_empty() {
                        return Ok((secret.trim().to_string(), "cache".to_string()));
                    }
                }
            }
        }
    }
    match extract_secret_from_main_js(client) {
        Ok((secret, _)) => {
            let _ = save_cached_secret(&secret);
            Ok((secret, "main_js".to_string()))
        }
        Err(error) => {
            eprintln!("Failed to extract X-Version secret, using bundled fallback: {error}");
            Ok((DEFAULT_X_VERSION_SECRET.to_string(), "bundled".to_string()))
        }
    }
}

fn extract_secret_from_main_js(client: &Client) -> Result<(String, String), String> {
    let html = client
        .get("https://www.iwara.tv/")
        .send()
        .map_err(|error| format!("homepage request failed: {error}"))?
        .text()
        .map_err(|error| format!("homepage read failed: {error}"))?;
    let script_re = Regex::new(r"/main\.[a-f0-9]+\.js").map_err(|error| error.to_string())?;
    let script_path = script_re
        .find(&html)
        .ok_or("main.js URL not found")?
        .as_str();
    let script_url = format!("https://www.iwara.tv{script_path}");
    let js = client
        .get(&script_url)
        .send()
        .map_err(|error| format!("main.js request failed: {error}"))?
        .text()
        .map_err(|error| format!("main.js read failed: {error}"))?;
    let secret_re =
        Regex::new(r#"expires\s*\+\s*"_([A-Za-z0-9]{20,})""#).map_err(|error| error.to_string())?;
    let secret = secret_re
        .captures(&js)
        .and_then(|captures| captures.get(1))
        .map(|value| value.as_str().to_string())
        .ok_or("X-Version secret pattern not found")?;
    Ok((secret, script_url))
}

fn secret_cache_path() -> PathBuf {
    if let Ok(path) = env::var("IWARA_RUST_SECRET_CACHE") {
        return PathBuf::from(path);
    }
    if let Ok(appdata) = env::var("APPDATA") {
        return PathBuf::from(appdata)
            .join("IwaraDownloader")
            .join("x_version_secret.txt");
    }
    env::temp_dir()
        .join("IwaraDownloader")
        .join("x_version_secret.txt")
}

fn save_cached_secret(secret: &str) -> io::Result<()> {
    let path = secret_cache_path();
    if let Some(parent) = path.parent() {
        fs::create_dir_all(parent)?;
    }
    fs::write(path, secret)
}

fn has_high_quality(files: &[Value]) -> bool {
    files.iter().any(|file| {
        matches!(
            file.get("name").and_then(Value::as_str),
            Some("Source") | Some("540")
        )
    })
}

fn video_error(status: u16, body: &Value, video_id: &str) -> Value {
    let mut error = json!({
        "success": false,
        "error": video_error_message(status, body, video_id)
    });
    if status == 403 && safe_error_message(body, "").contains("privateVideo") {
        error["code"] = json!("PRIVATE_VIDEO");
    }
    error
}

fn video_error_message(status: u16, body: &Value, video_id: &str) -> String {
    match status {
        404 => format!("Video not found: {video_id}"),
        403 => format!(
            "Access denied: {}",
            safe_error_message(body, "Private video or login required")
        ),
        _ => format!("HTTP {status}: {}", safe_error_message(body, "")),
    }
}

fn login_required() -> Value {
    json!({"success": false, "error": "Login required", "code": "LOGIN_REQUIRED"})
}

fn json_error_string(error: &str, code: &str) -> String {
    format!("{error} [{code}]")
}

fn is_retryable_rate_limit(result: &HttpResult) -> bool {
    if result.status == 429 {
        return true;
    }
    if result.status != 403 {
        return false;
    }
    let text = result.body.to_ascii_lowercase();
    ["rate limit", "too many", "cloudflare", "blocked", "captcha"]
        .iter()
        .any(|keyword| text.contains(keyword))
}

fn safe_error_message(value: &Value, fallback: &str) -> String {
    value
        .get("message")
        .or_else(|| value.get("error"))
        .and_then(Value::as_str)
        .map(|value| truncate(value, 200))
        .unwrap_or_else(|| {
            if value.is_null() {
                fallback.to_string()
            } else {
                truncate(&value.to_string(), 200)
            }
        })
}

fn safe_error_message_text(text: &str) -> String {
    let body: Value = serde_json::from_str(text).unwrap_or(Value::Null);
    let fallback = truncate(text, 200);
    safe_error_message(&body, &fallback)
}

fn normalize_download_url(value: &str) -> String {
    if value.starts_with("//") {
        return format!("https:{value}");
    }
    if Url::parse(value).is_ok() {
        return value.to_string();
    }
    Url::parse("https://www.iwara.tv/")
        .and_then(|base| base.join(value))
        .map(|url| url.to_string())
        .unwrap_or_else(|_| value.to_string())
}

fn safe_url(url: &Url) -> String {
    let host = url.host_str().unwrap_or_default();
    format!("{}://{}{}", url.scheme(), host, url.path())
}

fn decode_jwt_payload(token: &str) -> Option<Value> {
    let parts: Vec<&str> = token.split('.').collect();
    if parts.len() != 3 {
        return None;
    }
    let bytes = URL_SAFE_NO_PAD
        .decode(parts[1])
        .or_else(|_| URL_SAFE.decode(parts[1]))
        .ok()?;
    serde_json::from_slice(&bytes).ok()
}

fn unix_now() -> i64 {
    SystemTime::now()
        .duration_since(SystemTime::UNIX_EPOCH)
        .unwrap_or_default()
        .as_secs() as i64
}

fn sha1_hex(value: &str) -> String {
    let mut hasher = Sha1::new();
    hasher.update(value.as_bytes());
    format!("{:x}", hasher.finalize())
}

fn sha1_bytes(value: &[u8]) -> String {
    let mut hasher = Sha1::new();
    hasher.update(value);
    format!("{:x}", hasher.finalize())
}

fn content_range_total(headers: &HeaderMap) -> Option<u64> {
    let value = headers.get(CONTENT_RANGE)?.to_str().ok()?;
    let total = value.rsplit('/').next()?;
    if total == "*" {
        None
    } else {
        total.parse().ok()
    }
}

fn read_resume_meta(path: &str) -> Option<Value> {
    let text = fs::read_to_string(path).ok()?;
    serde_json::from_str(&text).ok()
}

fn write_resume_meta(path: &str, value: &Value) -> io::Result<()> {
    let temp_path = format!("{path}.tmp");
    fs::write(&temp_path, serde_json::to_vec(value).unwrap_or_default())?;
    if Path::new(path).exists() {
        let _ = fs::remove_file(path);
    }
    fs::rename(temp_path, path)
}

fn remove_if_exists(path: &str) {
    if Path::new(path).exists() {
        let _ = fs::remove_file(path);
    }
}

fn parse_download_percent(line: &str) -> Option<String> {
    let regex = Regex::new(r"\[download\]\s+([0-9]+(?:\.[0-9]+)?)%").ok()?;
    regex
        .captures(line)
        .and_then(|captures| captures.get(1).map(|value| value.as_str().to_string()))
}

fn truncate(value: &str, max_chars: usize) -> String {
    value.chars().take(max_chars).collect()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn jwt_payload_decodes_without_padding() {
        let fixture: Value = serde_json::from_str(include_str!("../fixtures/parity.json")).unwrap();
        let payload = decode_jwt_payload(fixture["token"].as_str().unwrap()).unwrap();
        assert_eq!(payload, fixture["expected_payload"]);
    }

    #[test]
    fn x_version_matches_python_sha1_fixture() {
        let fixture: Value = serde_json::from_str(include_str!("../fixtures/parity.json")).unwrap();
        let input = format!(
            "{}_{}_{}",
            fixture["file_id"].as_str().unwrap(),
            fixture["expires"].as_str().unwrap(),
            fixture["secret"].as_str().unwrap()
        );
        assert_eq!(sha1_hex(&input), fixture["expected_x_version"]);
    }

    #[test]
    fn quality_selection_order_prioritizes_requested_quality() {
        let files = vec![
            json!({"name": "360", "src": {"download": "https://cdn/360"}}),
            json!({"name": "Source", "src": {"view": "https://cdn/source"}}),
        ];
        let requested = "360";
        let order = [requested, "Source", "540", "360", "preview"];
        let found = order
            .into_iter()
            .find(|quality| files.iter().any(|file| file["name"] == *quality));
        assert_eq!(found, Some("360"));
    }

    #[test]
    fn current_secret_pattern_is_extractable() {
        let js = r#"(0,u.q4)(c+"_"+o.expires+"_fixtureSecret123456789012345678")"#;
        let re = Regex::new(r#"expires\s*\+\s*"_([A-Za-z0-9]{20,})""#).unwrap();
        assert_eq!(
            re.captures(js).unwrap().get(1).unwrap().as_str(),
            "fixtureSecret123456789012345678"
        );
    }

    #[test]
    fn content_range_total_is_parsed() {
        let mut headers = HeaderMap::new();
        headers.insert(
            CONTENT_RANGE,
            HeaderValue::from_static("bytes 0-65535/262104033"),
        );
        assert_eq!(content_range_total(&headers), Some(262104033));
    }

    #[test]
    fn resume_meta_round_trip_uses_json() {
        let path = env::temp_dir().join(format!("iwara-rust-test-{}.meta", std::process::id()));
        let path_string = path.to_string_lossy().into_owned();
        let value = json!({"file_id": "fixture", "size": 42, "etag": "abc"});
        write_resume_meta(&path_string, &value).unwrap();
        assert_eq!(read_resume_meta(&path_string).unwrap(), value);
        remove_if_exists(&path_string);
        remove_if_exists(&format!("{path_string}.tmp"));
    }
}
