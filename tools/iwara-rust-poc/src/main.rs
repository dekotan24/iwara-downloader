use base64::engine::general_purpose::{URL_SAFE, URL_SAFE_NO_PAD};
use base64::Engine;
use regex::Regex;
use reqwest::blocking::{Client, Response};
use reqwest::header::{
    HeaderMap, HeaderValue, ACCEPT_RANGES, AUTHORIZATION, CONTENT_LENGTH, CONTENT_RANGE, ETAG,
    RANGE,
};
use serde_json::{json, Value};
use sha1::{Digest, Sha1};
use std::env;
use std::fs;
use std::io::{Read, Write};
use std::path::PathBuf;
use std::time::{Duration, SystemTime};
use url::Url;

const API_URL: &str = "https://api.iwara.tv";
const SITE_TV: &str = "www.iwara.tv";
const SECRET_TTL: Duration = Duration::from_secs(30 * 24 * 60 * 60);
const RANGE_TEST_BYTES: u64 = 64 * 1024;

fn main() {
    let mut args: Vec<String> = env::args().skip(1).collect();
    let token = take_option(&mut args, "--token").or_else(|| env::var("IWARA_TOKEN").ok());
    let site = take_option(&mut args, "--site").unwrap_or_else(|| SITE_TV.to_string());
    let secret_override =
        take_option(&mut args, "--secret").or_else(|| env::var("IWARA_X_VERSION_SECRET").ok());

    let result = match args.first().map(String::as_str) {
        Some("login") => command_login(&args[1..]),
        Some("verify-token") | Some("verify_token") => command_verify_token(&token, &site),
        Some("get-video") => command_get_video(&args[1..], &token, &site),
        Some("search") => command_search(&args[1..], &token, &site),
        Some("user-videos") | Some("get-videos") => command_user_videos(&args[1..], &token, &site),
        Some("get-url") => command_get_url(&args[1..], &token, &site, secret_override.as_deref()),
        Some("download-test") => command_download_test(&args[1..]),
        Some("download-test-video") => command_download_test_video(&args[1..], &token, &site, secret_override.as_deref()),
        Some("probe") => command_probe(&args[1..], &token, &site),
        Some(other) => Err(format!("unknown action: {other}")),
        None => Err("usage: iwara-rust-poc <login|verify-token|get-video|search|user-videos|get-url|download-test|download-test-video|probe>".to_string()),
    };

    match result {
        Ok(value) => {
            println!(
                "{}",
                serde_json::to_string_pretty(&value)
                    .unwrap_or_else(|_| "{\"success\":false}".to_string())
            );
            if value.get("success") == Some(&Value::Bool(false)) {
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

fn client() -> Result<Client, String> {
    Client::builder()
        .user_agent("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36")
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

fn response_json(response: Response) -> Result<(u16, Value), String> {
    let status = response.status().as_u16();
    let text = response
        .text()
        .map_err(|error| format!("response read failed: {error}"))?;
    let value = serde_json::from_str(&text)
        .map_err(|error| format!("HTTP {status} returned non-JSON response: {error}"))?;
    Ok((status, value))
}

fn api_get(
    client: &Client,
    path: &str,
    query: &[(&str, String)],
    token: &Option<String>,
    site: &str,
) -> Result<(u16, Value), String> {
    let response = client
        .get(format!("{API_URL}{path}"))
        .query(query)
        .headers(request_headers(token, site))
        .send()
        .map_err(|error| format!("GET {path} failed: {error}"))?;
    response_json(response)
}

fn command_login(args: &[String]) -> Result<Value, String> {
    let email = args
        .first()
        .cloned()
        .or_else(|| env::var("IWARA_EMAIL").ok())
        .ok_or("login requires email or IWARA_EMAIL")?;
    let password = args
        .get(1)
        .cloned()
        .or_else(|| env::var("IWARA_PASSWORD").ok())
        .ok_or("login requires password or IWARA_PASSWORD")?;
    let http = client()?;
    let response = http
        .post(format!("{API_URL}/user/login"))
        .headers(request_headers(&None, SITE_TV))
        .json(&json!({"email": email, "password": password}))
        .send()
        .map_err(|error| format!("login request failed: {error}"))?;
    let (status, body) = response_json(response)?;
    if status != 200 {
        return Ok(json!({"success": false, "status": status, "error": safe_error_message(&body)}));
    }
    let token = body
        .get("token")
        .and_then(Value::as_str)
        .unwrap_or_default();
    if token.is_empty() {
        return Ok(
            json!({"success": false, "status": status, "error": "login response did not contain a token"}),
        );
    }
    let payload = decode_jwt_payload(token).unwrap_or_else(|| json!({}));
    Ok(json!({
        "success": true,
        "status": status,
        "token_present": true,
        "token_parts": token.split('.').count(),
        "payload_keys": object_keys(&payload),
        "expires_at": payload.get("exp").cloned().unwrap_or(Value::Null),
        "user_id_present": payload.get("id").is_some(),
        "token_type_present": payload.get("type").is_some()
    }))
}

fn command_verify_token(token: &Option<String>, site: &str) -> Result<Value, String> {
    let Some(token) = token else {
        return Ok(json!({"success": false, "code": "LOGIN_REQUIRED", "error": "No token"}));
    };
    let payload = decode_jwt_payload(token);
    if let Some(exp) = payload
        .as_ref()
        .and_then(|value| value.get("exp"))
        .and_then(Value::as_i64)
    {
        let now = SystemTime::now()
            .duration_since(SystemTime::UNIX_EPOCH)
            .map_err(|error| error.to_string())?
            .as_secs() as i64;
        if now >= exp {
            return Ok(
                json!({"success": false, "code": "TOKEN_EXPIRED", "expires_at": exp, "error": "Token expired"}),
            );
        }
    }
    let http = client()?;
    let (status, body) = api_get(&http, "/user", &[], &Some(token.clone()), site)?;
    if status != 200 {
        return Ok(
            json!({"success": false, "status": status, "code": if status == 401 || status == 403 { "TOKEN_INVALID" } else { "API_ERROR" }, "error": safe_error_message(&body)}),
        );
    }
    let user = body.get("user").unwrap_or(&Value::Null);
    Ok(json!({
        "success": true,
        "status": status,
        "expires_at": payload.as_ref().and_then(|value| value.get("exp")).cloned().unwrap_or(Value::Null),
        "user_present": user.is_object(),
        "username_present": user.get("username").is_some(),
        "role": user.get("role").cloned().unwrap_or(Value::Null),
        "premium": user.get("premium").cloned().unwrap_or(Value::Bool(false))
    }))
}

fn command_get_video(args: &[String], token: &Option<String>, site: &str) -> Result<Value, String> {
    let video_id = args.first().ok_or("get-video requires video id")?;
    let http = client()?;
    let (status, body) = api_get(&http, &format!("/video/{video_id}"), &[], token, site)?;
    if status != 200 {
        return Ok(json!({"success": false, "status": status, "error": safe_error_message(&body)}));
    }
    Ok(summarize_video(video_id, &body, status))
}

fn command_search(args: &[String], token: &Option<String>, site: &str) -> Result<Value, String> {
    let query = args.first().ok_or("search requires query")?;
    let page = args
        .get(1)
        .and_then(|value| value.parse::<u32>().ok())
        .unwrap_or(0);
    let limit = args
        .get(2)
        .and_then(|value| value.parse::<u32>().ok())
        .unwrap_or(32);
    let http = client()?;
    let (status, body) = api_get(
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
    )?;
    if status != 200 {
        return Ok(json!({"success": false, "status": status, "error": safe_error_message(&body)}));
    }
    let mut videos = Vec::new();
    if let Some(results) = body.get("results").and_then(Value::as_array) {
        for video in results {
            videos.push(json!({
                "id": video.get("id").cloned().unwrap_or(Value::Null),
                "title": video.get("title").cloned().unwrap_or(Value::String(String::new())),
                "rating": video.get("rating").cloned().unwrap_or(Value::String(String::new())),
                "author_present": video.get("user").and_then(Value::as_object).is_some(),
                "thumbnail_present": video.get("file").and_then(|file| file.get("id")).is_some(),
                "created_at": video.get("createdAt").cloned().unwrap_or(Value::Null)
            }));
        }
    }
    Ok(
        json!({"success": true, "status": status, "count": body.get("count").cloned().unwrap_or(json!(videos.len())), "page": page, "limit": limit, "videos": videos}),
    )
}

fn command_user_videos(
    args: &[String],
    token: &Option<String>,
    site: &str,
) -> Result<Value, String> {
    let username = args.first().ok_or("user-videos requires username")?;
    let http = client()?;
    let (profile_status, profile) =
        api_get(&http, &format!("/profile/{username}"), &[], token, site)?;
    if profile_status != 200 {
        return Ok(
            json!({"success": false, "status": profile_status, "code": if profile_status == 404 { "USER_NOT_FOUND" } else { "PROFILE_ERROR" }, "error": safe_error_message(&profile)}),
        );
    }
    let user_id = profile
        .get("user")
        .and_then(|user| user.get("id"))
        .and_then(Value::as_str)
        .ok_or("profile response did not contain user id")?;
    let mut videos = Vec::new();
    let mut page = 0u32;
    while page < 100 {
        let (status, body) = api_get(
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
        )?;
        if status != 200 {
            break;
        }
        let Some(results) = body.get("results").and_then(Value::as_array) else {
            break;
        };
        if results.is_empty() {
            break;
        }
        for video in results {
            videos.push(json!({"id": video.get("id").cloned().unwrap_or(Value::Null), "title": video.get("title").cloned().unwrap_or(Value::String(String::new())), "rating": video.get("rating").cloned().unwrap_or(Value::String(String::new())), "created_at": video.get("createdAt").cloned().unwrap_or(Value::Null)}));
        }
        page += 1;
    }
    Ok(
        json!({"success": true, "profile_status": profile_status, "user_id_present": true, "count": videos.len(), "pages_fetched": page, "videos": videos}),
    )
}

fn command_get_url(
    args: &[String],
    token: &Option<String>,
    site: &str,
    secret_override: Option<&str>,
) -> Result<Value, String> {
    let video_id = args.first().ok_or("get-url requires video id")?;
    let requested_quality = args.get(1).map(String::as_str).unwrap_or("Source");
    let http = client()?;
    let resolved = match resolve_download_url(
        &http,
        video_id,
        requested_quality,
        token,
        site,
        secret_override,
    ) {
        Ok(value) => value,
        Err(error) => return Ok(json!({"success": false, "status": 200, "error": error})),
    };
    let video = &resolved.video;
    let user = video.get("user").and_then(Value::as_object);
    let file = video.get("file").and_then(Value::as_object);
    Ok(json!({
        "success": true,
        "status": 200,
        "quality": resolved.quality,
        "available_qualities": resolved.available,
        "download_url_present": !resolved.url.is_empty(),
        "download_url_absolute": Url::parse(&resolved.url).is_ok(),
        "download_url_host": safe_host(&resolved.url),
        "title": video.get("title").cloned().unwrap_or(Value::String(video_id.to_string())),
        "file_id_present": file.and_then(|obj| obj.get("id")).is_some(),
        "author_present": user.is_some(),
        "rating": video.get("rating").cloned().unwrap_or(Value::String(String::new())),
        "thumbnail_present": file.and_then(|obj| obj.get("id")).is_some(),
        "secret_source": resolved.secret_source,
        "secret_refreshed": resolved.secret_refreshed
    }))
}

struct ResolvedDownload {
    video: Value,
    url: String,
    quality: String,
    available: Vec<Value>,
    secret_source: String,
    secret_refreshed: bool,
}

fn resolve_download_url(
    client: &Client,
    video_id: &str,
    requested_quality: &str,
    token: &Option<String>,
    site: &str,
    secret_override: Option<&str>,
) -> Result<ResolvedDownload, String> {
    let (status, video) = api_get(client, &format!("/video/{video_id}"), &[], token, site)?;
    if status != 200 {
        return Err(format!(
            "video HTTP {status}: {}",
            safe_error_message(&video)
        ));
    }
    let file_url = video
        .get("fileUrl")
        .and_then(Value::as_str)
        .ok_or("No fileUrl in video data")?;
    let file_url_parsed =
        Url::parse(file_url).map_err(|error| format!("invalid fileUrl: {error}"))?;
    let file_id = file_url_parsed
        .path_segments()
        .and_then(|segments| segments.last())
        .ok_or("fileUrl has no file id")?
        .to_string();
    let expires = file_url_parsed
        .query_pairs()
        .find(|(key, _)| key == "expires")
        .map(|(_, value)| value.to_string())
        .unwrap_or_default();
    let (secret, mut secret_source) = if let Some(value) = secret_override {
        (value.to_string(), "override".to_string())
    } else {
        let (value, source) = resolve_secret(client)?;
        (value, source.to_string())
    };
    let mut files = fetch_files(client, file_url, &file_id, &expires, &secret, token, site)?;
    let mut secret_refreshed = false;
    if !has_high_quality(&files) && secret_override.is_none() {
        if let Ok((new_secret, _)) = extract_secret_from_main_js(client) {
            if new_secret != secret {
                files = fetch_files(
                    client,
                    file_url,
                    &file_id,
                    &expires,
                    &new_secret,
                    token,
                    site,
                )?;
                secret_source = "main_js_refresh".to_string();
                secret_refreshed = true;
            }
        }
    }
    let available: Vec<Value> = files
        .iter()
        .filter_map(|file| file.get("name").cloned())
        .collect();
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
    let mut selected: Option<(String, String)> = None;
    for quality in search_order {
        for file in &files {
            if file.get("name").and_then(Value::as_str) == Some(quality) {
                let src = file.get("src").and_then(Value::as_object);
                let raw_url = src
                    .and_then(|source| source.get("download").or_else(|| source.get("view")))
                    .and_then(Value::as_str);
                if let Some(raw_url) = raw_url {
                    selected = Some((quality.to_string(), normalize_download_url(raw_url)));
                    break;
                }
            }
        }
        if selected.is_some() {
            break;
        }
    }
    let Some((quality, url)) = selected else {
        return Err(format!(
            "No download URL found; available qualities={available:?}"
        ));
    };
    Ok(ResolvedDownload {
        video,
        url,
        quality,
        available,
        secret_source,
        secret_refreshed,
    })
}

fn fetch_files(
    client: &Client,
    file_url: &str,
    file_id: &str,
    expires: &str,
    secret: &str,
    token: &Option<String>,
    site: &str,
) -> Result<Vec<Value>, String> {
    let x_version = sha1_hex(&format!("{file_id}_{expires}_{secret}"));
    let mut headers = request_headers(token, site);
    headers.insert(
        "X-Version",
        HeaderValue::from_str(&x_version).map_err(|error| error.to_string())?,
    );
    let response = client
        .get(file_url)
        .headers(headers)
        .send()
        .map_err(|error| format!("filesq request failed: {error}"))?;
    let (status, body) = response_json(response)?;
    if status != 200 {
        return Err(format!(
            "filesq HTTP {status}: {}",
            safe_error_message(&body)
        ));
    }
    if let Some(files) = body.as_array() {
        return Ok(files.clone());
    }
    if let Some(files) = body.get("files").and_then(Value::as_array) {
        return Ok(files.clone());
    }
    Err("filesq response did not contain a file list".to_string())
}

fn command_download_test(args: &[String]) -> Result<Value, String> {
    let url = args.first().ok_or("download-test requires a direct URL")?;
    let http = client()?;
    range_probe(&http, url, args.get(1).map(String::as_str))
}

fn command_download_test_video(
    args: &[String],
    token: &Option<String>,
    site: &str,
    secret_override: Option<&str>,
) -> Result<Value, String> {
    let video_id = args
        .first()
        .ok_or("download-test-video requires video id")?;
    let requested_quality = args.get(1).map(String::as_str).unwrap_or("Source");
    let http = client()?;
    let resolved = resolve_download_url(
        &http,
        video_id,
        requested_quality,
        token,
        site,
        secret_override,
    )?;
    let mut result = range_probe(&http, &resolved.url, None)?;
    if let Some(object) = result.as_object_mut() {
        object.insert("video_id".to_string(), Value::String(video_id.to_string()));
        object.insert("quality".to_string(), Value::String(resolved.quality));
        object.insert(
            "available_qualities".to_string(),
            Value::Array(resolved.available),
        );
        object.insert(
            "secret_source".to_string(),
            Value::String(resolved.secret_source),
        );
    }
    Ok(result)
}

fn range_probe(client: &Client, url: &str, output: Option<&str>) -> Result<Value, String> {
    let parsed = Url::parse(url).map_err(|error| format!("invalid URL: {error}"))?;
    let end = RANGE_TEST_BYTES - 1;
    let response = client
        .get(url)
        .header(RANGE, format!("bytes=0-{end}"))
        .send()
        .map_err(|error| format!("range request failed: {error}"))?;
    let status = response.status().as_u16();
    let headers = response.headers().clone();
    let mut body = Vec::new();
    response
        .take(RANGE_TEST_BYTES)
        .read_to_end(&mut body)
        .map_err(|error| format!("range body read failed: {error}"))?;
    if let Some(output) = output {
        let mut file = fs::File::create(output)
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
        "accept_ranges": headers.get(ACCEPT_RANGES).and_then(|value| value.to_str().ok()).unwrap_or_default(),
        "content_length": headers.get(CONTENT_LENGTH).and_then(|value| value.to_str().ok()).unwrap_or_default(),
        "content_range": headers.get(CONTENT_RANGE).and_then(|value| value.to_str().ok()).unwrap_or_default(),
        "etag_present": headers.get(ETAG).is_some(),
        "partial_output_written": output.is_some()
    }))
}

fn command_probe(args: &[String], token: &Option<String>, site: &str) -> Result<Value, String> {
    let url = args.first().ok_or("probe requires a URL")?;
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
        "success": status >= 200 && status < 400,
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

fn summarize_video(video_id: &str, video: &Value, status: u16) -> Value {
    let user = video.get("user").and_then(Value::as_object);
    let file = video.get("file").and_then(Value::as_object);
    let thumbnail_present = file.and_then(|obj| obj.get("id")).is_some();
    json!({
        "success": true,
        "status": status,
        "id": video.get("id").cloned().unwrap_or(Value::String(video_id.to_string())),
        "title": video.get("title").cloned().unwrap_or(Value::String(String::new())),
        "author_present": user.is_some(),
        "rating": video.get("rating").cloned().unwrap_or(Value::String(String::new())),
        "thumbnail_present": thumbnail_present,
        "file_url_present": video.get("fileUrl").and_then(Value::as_str).is_some(),
        "embed_url_present": video.get("embedUrl").and_then(Value::as_str).is_some(),
        "private": video.get("private").cloned().unwrap_or(Value::Bool(false)),
        "raw_keys": object_keys(video)
    })
}

fn resolve_secret(client: &Client) -> Result<(String, &'static str), String> {
    if let Ok(secret) = env::var("IWARA_X_VERSION_SECRET") {
        if !secret.is_empty() {
            return Ok((secret, "env"));
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
                        return Ok((secret.trim().to_string(), "cache"));
                    }
                }
            }
        }
    }
    let (secret, _) = extract_secret_from_main_js(client)?;
    let _ = fs::write(path, &secret);
    Ok((secret, "main_js"))
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
    env::temp_dir().join("iwara-rust-poc-x-version-secret.txt")
}

fn has_high_quality(files: &[Value]) -> bool {
    files.iter().any(|file| {
        matches!(
            file.get("name").and_then(Value::as_str),
            Some("Source") | Some("540")
        )
    })
}

fn safe_host(value: &str) -> Value {
    Url::parse(value)
        .ok()
        .and_then(|url| url.host_str().map(|host| Value::String(host.to_string())))
        .unwrap_or(Value::Null)
}

fn safe_url(url: &Url) -> String {
    let host = url.host_str().unwrap_or_default();
    format!("{}://{}{}", url.scheme(), host, url.path())
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

fn safe_error_message(value: &Value) -> String {
    value
        .get("message")
        .or_else(|| value.get("error"))
        .and_then(Value::as_str)
        .unwrap_or("request failed")
        .chars()
        .take(200)
        .collect()
}

fn object_keys(value: &Value) -> Vec<String> {
    value
        .as_object()
        .map(|object| object.keys().cloned().collect())
        .unwrap_or_default()
}

fn decode_jwt_payload(token: &str) -> Option<Value> {
    let segment = token.split('.').nth(1)?;
    let bytes = URL_SAFE_NO_PAD
        .decode(segment)
        .or_else(|_| URL_SAFE.decode(segment))
        .ok()?;
    serde_json::from_slice(&bytes).ok()
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
        let mut found = None;
        let order = [requested, "Source", "540", "360", "preview"];
        for quality in order {
            if found.is_some() {
                break;
            }
            for file in &files {
                if file["name"] == quality {
                    found = Some(quality);
                    break;
                }
            }
        }
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
}
