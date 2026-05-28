#!/usr/bin/env python3
"""
iwara_helper.py - Iwara.tv API helper with Cloudflare bypass
Usage:
    python iwara_helper.py login <email> <password>
    python iwara_helper.py get_videos <username> [--token <token>]
    python iwara_helper.py download <video_id> <output_path> [--token <token>]
"""

import base64
import cloudscraper
import json
import sys
import os
import hashlib
import re
import subprocess
import time
from urllib.parse import urlparse, parse_qs

# Windows コンソールで日本語タイトルを print(file=sys.stderr) するときの
# UnicodeEncodeError 防止。C# 側で UTF-8 を指定していない場合も死なないように。
try:
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    sys.stderr.reconfigure(encoding='utf-8', errors='replace')
except Exception:
    pass


def _decode_jwt_payload(token: str) -> dict:
    """JWT の payload を base64url デコードして辞書を返す。失敗時は空辞書。"""
    try:
        parts = token.split('.')
        if len(parts) != 3:
            return {}
        payload_b64 = parts[1] + '=' * (-len(parts[1]) % 4)
        return json.loads(base64.urlsafe_b64decode(payload_b64.encode()))
    except Exception:
        return {}


# ---------------------------------------------------------------------------
# X-Version secret 管理
# ---------------------------------------------------------------------------
# iwara は filesq に対する X-Version 検証用の secret を main.js にハードコード
# している。iwara 側でフロントエンドが更新されると secret が差し替わり、古い値
# を使うと filesq が 360/preview のみの劣化レスポンスを返すようになる。
# そのため以下の二段構えで対応する:
#   1. DEFAULT_SECRET を埋め込み (発掘された現行値)
#   2. 劣化レスポンスを検知した時のみ main.js を取りに行って新 secret を抽出
#      キャッシュファイルに永続化して 30 日間は再取得しない
# セッション中は一度再取得を試したらそれ以上リトライしない(過剰取得の抑制)

DEFAULT_X_VERSION_SECRET = 'mSvL05GfEmeEmsEYfGCnVpEjYgTJraJN'
SECRET_CACHE_TTL_SECONDS = 30 * 24 * 3600  # 30日

def _secret_cache_path() -> str:
    base = os.environ.get('APPDATA') or os.path.expanduser('~')
    return os.path.join(base, 'IwaraDownloader', 'x_version_secret.txt')

def _load_cached_secret() -> str:
    """キャッシュが 30 日以内なら返す。無効/古い場合は埋め込み値を返す。"""
    try:
        path = _secret_cache_path()
        st = os.stat(path)
        if time.time() - st.st_mtime < SECRET_CACHE_TTL_SECONDS:
            with open(path, 'r', encoding='utf-8') as f:
                s = f.read().strip()
                if s:
                    return s
    except Exception:
        pass
    return DEFAULT_X_VERSION_SECRET

def _save_cached_secret(secret: str) -> None:
    try:
        path = _secret_cache_path()
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, 'w', encoding='utf-8') as f:
            f.write(secret)
    except Exception as e:
        print(f"Failed to cache secret: {e}", file=sys.stderr)

def _extract_secret_from_main_js(scraper) -> str:
    """iwara.tv のトップページから main.js URL を割り出し、secret を抽出する。

    main.js のコードは以下のような形:
        (0,u.q4)(c+"_"+o.expires+"_mSvL05GfEmeEmsEYfGCnVpEjYgTJraJN")
    expires に続く "_<secret>" をキャプチャする。
    """
    try:
        r = scraper.get('https://www.iwara.tv/', timeout=20)
        if r.status_code != 200:
            return ''
        m = re.search(r'/main\.[a-f0-9]+\.js', r.text)
        if not m:
            return ''
        js_url = 'https://www.iwara.tv' + m.group(0)
        r = scraper.get(js_url, timeout=30)
        if r.status_code != 200:
            return ''
        # expires 直後の "_<20文字以上の識別子>"
        m = re.search(r'expires\s*\+\s*"_([A-Za-z0-9]{20,})"', r.text)
        if m:
            return m.group(1)
        return ''
    except Exception as e:
        print(f"Failed to extract secret from main.js: {e}", file=sys.stderr)
        return ''


class IwaraAPI:
    def __init__(self, token=None, rate_limit_config=None, site=None):
        self.scraper = cloudscraper.create_scraper(
            browser={
                'browser': 'chrome',
                'platform': 'windows',
                'desktop': True
            }
        )
        # iwara.tv / iwara.ai いずれも同じ api.iwara.tv エンドポイントを使う。
        # 動画の所属サイト判別は X-Site ヘッダー (= ホスト名 www.iwara.tv / www.iwara.ai)。
        self.api_url = "https://api.iwara.tv"
        self.file_url = "https://files.iwara.tv"
        self.token = token
        # site が空文字 / None なら iwara.tv 扱い (旧データ互換)
        self.site = site if site else "www.iwara.tv"
        # X-Version secret の再取得はセッション中 1 回までに制限する
        self._secret_refreshed = False
        
        # Rate limiting configuration
        self.rate_limit_config = rate_limit_config or {}
        self.api_request_delay = self.rate_limit_config.get('api_delay', 1.0)  # seconds
        self.page_fetch_delay = self.rate_limit_config.get('page_delay', 0.5)  # seconds
        self.rate_limit_base_delay = self.rate_limit_config.get('rate_limit_base', 30)  # seconds
        self.rate_limit_max_delay = self.rate_limit_config.get('rate_limit_max', 300)  # seconds
        self.enable_backoff = self.rate_limit_config.get('enable_backoff', True)
        
        # Backoff state
        self._consecutive_errors = 0
        self._last_request_time = 0
    
    def _wait_for_rate_limit(self, delay_seconds=None):
        """リクエスト間隔を確保"""
        if delay_seconds is None:
            delay_seconds = self.api_request_delay
        
        elapsed = time.time() - self._last_request_time
        if elapsed < delay_seconds:
            wait_time = delay_seconds - elapsed
            print(f"RateLimit: waiting {wait_time:.1f}s...", file=sys.stderr)
            time.sleep(wait_time)
        
        self._last_request_time = time.time()
    
    def _handle_rate_limit_error(self, status_code, response=None):
        """429/403エラー時のバックオフ処理
        
        Returns:
            (should_retry, error_message)
            - should_retry: Trueならリトライすべき、Falseなら即座に失敗
            - error_message: エラーメッセージ
        """
        if status_code == 429:
            # 429 Too Many Requests - レート制限、リトライする
            self._consecutive_errors += 1
            
            if self.enable_backoff:
                delay = min(
                    self.rate_limit_base_delay * (2 ** (self._consecutive_errors - 1)),
                    self.rate_limit_max_delay
                )
            else:
                delay = self.rate_limit_base_delay
            
            print(f"RateLimit: HTTP 429 Too Many Requests, backing off for {delay:.0f}s (attempt {self._consecutive_errors})", file=sys.stderr)
            time.sleep(delay)
            return True, "Rate limited (429)"
        
        if status_code == 403:
            # 403 Forbidden - 原因を判別
            error_detail = ""
            is_rate_limit = False
            
            if response is not None:
                try:
                    # レスポンスボディを確認
                    try:
                        body = response.json()
                        error_detail = body.get("message", "") or body.get("error", "") or str(body)
                    except:
                        error_detail = response.text[:200] if response.text else ""
                    
                    # Cloudflareやレート制限の判定
                    text_lower = (error_detail + response.text).lower() if response.text else error_detail.lower()
                    if any(keyword in text_lower for keyword in ['rate limit', 'too many', 'cloudflare', 'blocked', 'captcha']):
                        is_rate_limit = True
                except Exception as e:
                    error_detail = f"Could not parse response: {e}"
            
            if is_rate_limit:
                # レート制限由来の403 - リトライする
                self._consecutive_errors += 1
                
                if self.enable_backoff:
                    delay = min(
                        self.rate_limit_base_delay * (2 ** (self._consecutive_errors - 1)),
                        self.rate_limit_max_delay
                    )
                else:
                    delay = self.rate_limit_base_delay
                
                print(f"RateLimit: HTTP 403 (rate limit), backing off for {delay:.0f}s (attempt {self._consecutive_errors})", file=sys.stderr)
                time.sleep(delay)
                return True, f"Rate limited (403): {error_detail}"
            else:
                # 権限不足の403 - リトライしない
                print(f"Permission denied: HTTP 403 - {error_detail}", file=sys.stderr)
                return False, f"Access denied (403): {error_detail or 'Private content or insufficient permissions'}"
        
        # その他のエラー
        self._consecutive_errors = 0
        return False, f"HTTP {status_code}"
    
    def _request_with_retry(self, method, url, max_retries=3, **kwargs):
        """リトライ機能付きリクエスト"""
        last_error = None
        
        for attempt in range(max_retries):
            self._wait_for_rate_limit()
            
            try:
                if method == 'GET':
                    r = self.scraper.get(url, **kwargs)
                elif method == 'POST':
                    r = self.scraper.post(url, **kwargs)
                else:
                    raise ValueError(f"Unknown method: {method}")
                
                # レート制限/権限エラーの処理
                if r.status_code in [429, 403]:
                    should_retry, error_msg = self._handle_rate_limit_error(r.status_code, r)
                    last_error = error_msg
                    
                    if should_retry and attempt < max_retries - 1:
                        print(f"Retrying... (attempt {attempt + 2}/{max_retries})", file=sys.stderr)
                        continue
                    else:
                        # リトライ不可または最後の試行
                        print(f"Failed after {attempt + 1} attempts: {error_msg}", file=sys.stderr)
                        return r  # エラーレスポンスを返す
                
                # 成功 - エラーカウントリセット
                self._consecutive_errors = 0
                return r
                
            except Exception as e:
                last_error = str(e)
                print(f"Request error (attempt {attempt + 1}/{max_retries}): {e}", file=sys.stderr)
                if attempt < max_retries - 1:
                    time.sleep(self.api_request_delay * (attempt + 1))
                else:
                    raise
        
        return None

    def login(self, email: str, password: str) -> dict:
        """ログインしてトークンを取得"""
        try:
            r = self._request_with_retry(
                'POST',
                f"{self.api_url}/user/login",
                json={"email": email, "password": password}
            )
            
            if r is None:
                return {"success": False, "error": "No response from server"}
            
            if r.status_code == 401:
                return {"success": False, "error": "Invalid email or password"}
            
            if r.status_code == 403:
                try:
                    body = r.json()
                    msg = body.get("message", "") or body.get("error", "")
                except:
                    msg = ""
                return {"success": False, "error": f"Login blocked: {msg or 'Too many attempts or account issue'}"}
            
            if r.status_code != 200:
                try:
                    body = r.json()
                    msg = body.get("message", "") or body.get("error", "")
                except:
                    msg = r.text[:100] if r.text else ""
                return {"success": False, "error": f"Login failed: HTTP {r.status_code} - {msg}"}
            
            data = r.json()
            self.token = data.get("token")

            if self.token:
                payload = _decode_jwt_payload(self.token)
                return {
                    "success": True,
                    "token": self.token,
                    "expires_at": payload.get("exp"),
                    "user_id": payload.get("id"),
                    "token_type": payload.get("type"),
                }
            else:
                return {"success": False, "error": "No token in response"}

        except Exception as e:
            return {"success": False, "error": f"Exception: {str(e)}"}

    def verify_token(self) -> dict:
        """保持しているトークンが API 的にまだ有効か検証する。

        - トークンが無ければ LOGIN_REQUIRED
        - JWT の exp が過ぎていれば TOKEN_EXPIRED
        - /user エンドポイントを叩いて 200 が返るか確認
        """
        if not self.token:
            return {"success": False, "error": "No token", "code": "LOGIN_REQUIRED"}

        payload = _decode_jwt_payload(self.token)
        exp = payload.get("exp")
        if exp and time.time() >= exp:
            return {"success": False, "error": "Token expired", "code": "TOKEN_EXPIRED", "expires_at": exp}

        try:
            r = self._request_with_retry(
                'GET',
                f"{self.api_url}/user",
                headers=self._auth_header()
            )
            if r is None:
                return {"success": False, "error": "No response from server", "code": "NETWORK_ERROR"}
            if r.status_code in (401, 403):
                return {"success": False, "error": f"Token rejected: HTTP {r.status_code}", "code": "TOKEN_INVALID"}
            if r.status_code != 200:
                return {"success": False, "error": f"HTTP {r.status_code}", "code": "API_ERROR"}

            body = r.json()
            user = body.get("user", {}) or {}
            return {
                "success": True,
                "expires_at": exp,
                "user_id": user.get("id"),
                "username": user.get("username"),
                "role": user.get("role"),
                "premium": user.get("premium", False),
            }
        except Exception as e:
            return {"success": False, "error": f"Exception: {str(e)}", "code": "EXCEPTION"}

    def _auth_header(self) -> dict:
        """認証 + X-Site ヘッダーを返す。
        iwara.tv 動画は X-Site=www.iwara.tv (なくても動くがフロントエンドが必ず送るので合わせる)。
        iwara.ai 動画は X-Site=www.iwara.ai が必須 (無いと errors.differentSite で 301)。"""
        h = {"X-Site": self.site}
        if self.token:
            h["Authorization"] = f"Bearer {self.token}"
        return h

    def search_videos(self, query: str, page: int = 0, limit: int = 32) -> dict:
        """iwara 検索 API で動画一覧を取得 (購読してないチャンネルからも検索可能)。
        iwara の /api/search は現在 500 を返す事があるため /api/videos?query= を使う。"""
        if not self.token:
            return {"success": False, "error": "Login required", "code": "LOGIN_REQUIRED"}
        try:
            r = self._request_with_retry(
                'GET',
                f"{self.api_url}/videos",
                params={'query': query, 'page': page, 'limit': limit, 'sort': 'date'},
                headers=self._auth_header()
            )
            if r is None:
                return {"success": False, "error": "No response from server"}
            if r.status_code == 403:
                return {"success": False, "error": "Search blocked (403): login may be required"}
            if r.status_code != 200:
                try:
                    body = r.json()
                    msg = body.get("message", "") or body.get("error", "")
                except Exception:
                    msg = r.text[:100] if r.text else ""
                return {"success": False, "error": f"Search failed: HTTP {r.status_code} - {msg}"}

            data = r.json()
            results = data.get("results", []) or []
            videos = []
            for v in results:
                user = v.get("user") or {}
                file_info = v.get("file") or {}
                videos.append({
                    "id": v.get("id"),
                    "title": v.get("title", ""),
                    "thumbnail": self._get_thumbnail_url(v),
                    "duration": file_info.get("duration", 0) or 0,
                    "rating": v.get("rating") or "",
                    "author_username": user.get("username", ""),
                    "author_name": user.get("name", ""),
                    "embed_url": v.get("embedUrl") or "",
                    "private": v.get("private", False),
                    "created_at": v.get("createdAt"),
                })
            return {
                "success": True,
                "count": data.get("count", len(videos)),
                "page": page,
                "limit": limit,
                "videos": videos,
            }
        except Exception as e:
            return {"success": False, "error": f"Exception: {e}"}

    def get_user_videos(self, username: str) -> dict:
        """ユーザーの全動画リストを取得"""
        if not self.token:
            return {"success": False, "error": "Login required", "code": "LOGIN_REQUIRED"}
        try:
            # 1. プロフィールからuser_idを取得
            profile_r = self._request_with_retry(
                'GET',
                f"{self.api_url}/profile/{username}",
                headers=self._auth_header()
            )
            
            if profile_r is None:
                return {"success": False, "error": "No response from server"}
            
            if profile_r.status_code == 404:
                return {"success": False, "error": f"User not found: {username}"}
            
            if profile_r.status_code == 403:
                try:
                    body = profile_r.json()
                    msg = body.get("message", "") or body.get("error", "")
                except:
                    msg = ""
                return {"success": False, "error": f"Access denied: {msg or 'Login may be required'}"}
            
            if profile_r.status_code != 200:
                try:
                    body = profile_r.json()
                    msg = body.get("message", "") or body.get("error", "")
                except:
                    msg = profile_r.text[:100] if profile_r.text else ""
                return {"success": False, "error": f"Profile fetch failed: HTTP {profile_r.status_code} - {msg}"}
            
            profile_data = profile_r.json()
            user_id = (profile_data.get("user") or {}).get("id")
            
            if not user_id:
                return {"success": False, "error": "User ID not found"}
            
            # 2. 動画リストを全ページ取得
            videos = []
            page = 0
            max_pages = 100  # 安全のため上限
            
            while page < max_pages:
                # ページ取得間のディレイ
                if page > 0:
                    self._wait_for_rate_limit(self.page_fetch_delay)
                
                r = self._request_with_retry(
                    'GET',
                    f"{self.api_url}/videos",
                    params={
                        "page": page,
                        "sort": "date",
                        "user": user_id,
                        "limit": 32
                    },
                    headers=self._auth_header()
                )
                
                if r is None or r.status_code != 200:
                    print(f"Page {page} fetch failed, stopping pagination", file=sys.stderr)
                    break
                
                data = r.json()
                results = data.get("results", [])
                
                if not results:
                    break
                
                for video in results:
                    file_info = video.get("file") or {}
                    videos.append({
                        "id": video.get("id"),
                        "title": video.get("title"),
                        "slug": video.get("slug"),
                        "thumbnail": self._get_thumbnail_url(video),
                        "duration": file_info.get("duration", 0),
                        "created_at": video.get("createdAt"),
                        "private": video.get("private", False),
                        "embed_url": video.get("embedUrl") or "",
                        "rating": video.get("rating") or ""
                    })
                
                print(f"Fetched page {page + 1}, {len(results)} videos (total: {len(videos)})", file=sys.stderr)
                page += 1
            
            return {
                "success": True,
                "username": username,
                "user_id": user_id,
                "count": len(videos),
                "videos": videos
            }
            
        except Exception as e:
            return {"success": False, "error": str(e)}

    def _get_thumbnail_url(self, video: dict) -> str:
        """サムネイルURLを生成"""
        file_info = video.get("file") or {}
        file_id = file_info.get("id")
        if file_id:
            return f"https://i.iwara.tv/image/thumbnail/{file_id}/thumbnail-00.jpg"
        return ""

    def _fetch_file_list(self, file_url: str, file_id: str, expires: str):
        """filesq へ X-Version 付きでアクセスしてファイル一覧を取得する。

        現在キャッシュされている secret で X-Version を計算する。
        Returns: (files_list, error_dict_or_None)
        """
        secret = _load_cached_secret()
        x_version = hashlib.sha1(
            '_'.join([file_id, expires, secret]).encode()
        ).hexdigest()
        headers = self._auth_header()
        headers['X-Version'] = x_version

        r = self._request_with_retry('GET', file_url, headers=headers)
        if r is None:
            return [], {"success": False, "error": "No response from file server"}
        if r.status_code == 403:
            try:
                body = r.json()
                msg = body.get("message", "") or body.get("error", "")
            except Exception:
                msg = r.text[:100] if r.text else ""
            return [], {"success": False, "error": f"Access denied to download: {msg or 'Private video or login required'}"}
        if r.status_code != 200:
            try:
                body = r.json()
                msg = body.get("message", "") or body.get("error", "")
            except Exception:
                msg = r.text[:100] if r.text else ""
            # 動画が削除された/存在しない: リトライしても永遠に同じ結果
            if r.status_code == 404 or (msg and "notFound" in msg):
                return [], {
                    "success": False,
                    "error": f"Video not found on iwara: HTTP {r.status_code} - {msg}",
                    "code": "VIDEO_NOT_FOUND",
                }
            return [], {"success": False, "error": f"File URL fetch failed: HTTP {r.status_code} - {msg}"}
        try:
            return r.json(), None
        except Exception as e:
            return [], {"success": False, "error": f"Failed to parse filesq response: {e}"}

    def get_video_info(self, video_id: str) -> dict:
        """動画情報を取得"""
        try:
            r = self._request_with_retry(
                'GET',
                f"{self.api_url}/video/{video_id}",
                headers=self._auth_header()
            )
            
            if r is None:
                return {"success": False, "error": "No response from server"}
            
            if r.status_code == 404:
                return {"success": False, "error": f"Video not found: {video_id}"}
            
            if r.status_code == 403:
                # 詳細なエラーメッセージを取得
                try:
                    body = r.json()
                    msg = body.get("message", "") or body.get("error", "")
                except:
                    msg = ""
                # フレンド限定動画: 相互承認してないと見れない。リトライしても無駄なので
                # 専用 code で明示する (C# 側で即座に Failed 確定 / リトライ抑制)
                if msg and "privateVideo" in msg:
                    return {
                        "success": False,
                        "error": f"Private video (friend-only): {msg}",
                        "code": "PRIVATE_VIDEO",
                    }
                return {"success": False, "error": f"Access denied: {msg or 'Private video or login required'}"}
            
            if r.status_code != 200:
                try:
                    body = r.json()
                    msg = body.get("message", "") or body.get("error", "")
                except:
                    msg = r.text[:100] if r.text else ""
                return {"success": False, "error": f"HTTP {r.status_code}: {msg}"}
            
            return {"success": True, "data": r.json()}
            
        except Exception as e:
            return {"success": False, "error": f"Exception: {str(e)}"}

    def get_download_url(self, video_id: str, quality: str = "Source") -> dict:
        """ダウンロードURLを取得"""
        if not self.token:
            return {"success": False, "error": "Login required", "code": "LOGIN_REQUIRED"}
        try:
            # 動画情報を取得
            video_info = self.get_video_info(video_id)
            if not video_info["success"]:
                return video_info
            
            video_data = video_info["data"]
            file_url = video_data.get("fileUrl")
            
            if not file_url:
                return {"success": False, "error": "No fileUrl in video data"}
            
            # ファイルURLにアクセスしてダウンロードリンク取得 (X-Version ヘッダー要)
            parsed = urlparse(file_url)
            path_parts = parsed.path.rstrip('/').split('/')
            query = parse_qs(parsed.query)
            expires = query.get('expires', [''])[0]
            file_id_part = path_parts[-1]

            files, err = self._fetch_file_list(file_url, file_id_part, expires)
            if err:
                return err

            # 劣化レスポンス判定: Source / 540 が欠けてて、かつ 360/preview しか無い場合、
            # secret が iwara 側で変更された可能性を疑う。セッション中 1 度だけ main.js を
            # 取りに行って新しい secret を抽出し、キャッシュ更新後に再試行する。
            names_set = {f.get('name') for f in files}
            high_q = {'Source', '540'}
            if not (high_q & names_set) and not self._secret_refreshed:
                self._secret_refreshed = True
                print("Low-quality only response detected. Refreshing X-Version secret from main.js...", file=sys.stderr)
                new_secret = _extract_secret_from_main_js(self.scraper)
                if new_secret and new_secret != _load_cached_secret():
                    _save_cached_secret(new_secret)
                    print(f"X-Version secret updated (cached). Retrying filesq...", file=sys.stderr)
                    files, err = self._fetch_file_list(file_url, file_id_part, expires)
                    if err:
                        return err
                elif new_secret:
                    print("Secret unchanged — likely the video itself has no high-quality version.", file=sys.stderr)
                else:
                    print("Failed to extract secret from main.js.", file=sys.stderr)

            def _extract_url(f):
                """src から利用可能な URL を取り出す (download 優先、なければ view)"""
                src = f.get("src", {}) or {}
                return src.get("download") or src.get("view")

            # 利用可能な画質一覧をログ出力(デバッグ用)
            available = [f.get("name") for f in files if f.get("name")]
            print(f"Available qualities for {video_id}: {available}", file=sys.stderr)

            # 画質の優先順位(高→低)
            quality_order = ["Source", "540", "360", "preview"]

            # 指定画質があれば最優先で探す
            search_order = []
            if quality and quality in quality_order:
                search_order.append(quality)
            for q in quality_order:
                if q not in search_order:
                    search_order.append(q)

            download_url = None
            for q in search_order:
                for f in files:
                    if f.get("name") == q:
                        url = _extract_url(f)
                        if url:
                            download_url = url
                            quality = q
                            break
                if download_url:
                    break

            if not download_url:
                return {"success": False, "error": "No download URL found"}
            
            # URLが相対パスの場合
            if download_url.startswith("//"):
                download_url = "https:" + download_url
            
            # 動画のメタ情報(file_id / author)を抽出して返す
            user_obj = video_data.get("user") or {}
            file_obj = video_data.get("file") or {}

            return {
                "success": True,
                "url": download_url,
                "quality": quality,
                "title": video_data.get("title", video_id),
                "file_id": file_obj.get("id"),
                "author_username": user_obj.get("username"),
                "author_name": user_obj.get("name"),
                "rating": video_data.get("rating") or "",
                "thumbnail": self._get_thumbnail_url(video_data),
            }

        except Exception as e:
            return {"success": False, "error": str(e)}

    # filesq が振り分けてくる CDN ホストのうち、現状 404/接続不能を返す群。
    # 完全な hardcode はせずに、セッション中に学習した死亡 CDN として参照する。
    # (起動時は空で始まる。コードレベルで除外せず、サーバー応答に基づいて判定する)
    _DEAD_CDN_CONNECTION_TIMEOUT = 8  # 接続テスト用の短いタイムアウト(秒)
    _CDN_REDIRECT_MAX_RETRIES = 6     # 別 CDN を引き当てるための get_download_url 再試行回数
    # リジューム時に末尾を再 DL するマージン。プロセス kill 等で flush 未完了のゴミバイトを上書きするため。
    _RESUME_REWIND_BYTES = 65536

    @staticmethod
    def _part_paths(output_path: str):
        """outputPath に対応する .part / .meta パスを返す"""
        return (output_path + ".part", output_path + ".part.meta")

    @staticmethod
    def _read_resume_meta(meta_path: str):
        try:
            with open(meta_path, 'r', encoding='utf-8') as f:
                return json.load(f)
        except Exception:
            return None

    @staticmethod
    def _write_resume_meta(meta_path: str, data: dict):
        try:
            with open(meta_path, 'w', encoding='utf-8') as f:
                json.dump(data, f, ensure_ascii=False)
        except Exception as e:
            print(f"Failed to write meta: {e}", file=sys.stderr)

    def _try_download_once(self, download_url: str, output_path: str,
                           file_id: str = "", resume_meta: dict = None):
        """単一の download_url で実際にダウンロードする。Range レジューム対応。
        戻り値: (kind, payload)
          kind='success' → payload は size(int)
          kind='cdn_error' → payload は error_str (404 / 接続不能 / 5xx — CDN 切替で回復可能)
          kind='auth_error' → payload は error_str (403 — リトライ無意味)
          kind='hard_error' → payload は error_str (致命的)
        """
        from urllib.parse import urlparse
        host = urlparse(download_url).netloc

        part_path, meta_path = self._part_paths(output_path)

        # レジューム判定: .part が存在し、メタ情報の file_id が一致するなら続きから
        resume_from = 0
        if file_id and os.path.exists(part_path) and resume_meta:
            meta_fid = resume_meta.get('file_id', '')
            current_part_size = os.path.getsize(part_path)
            if meta_fid == file_id and current_part_size > self._RESUME_REWIND_BYTES:
                # 末尾の数 KB は安全マージンで上書き再 DL (flush 未完了のゴミ対策)
                resume_from = current_part_size - self._RESUME_REWIND_BYTES
                print(f"Resuming from {resume_from} bytes (part size={current_part_size}, file_id match)",
                      file=sys.stderr)
            elif meta_fid != file_id:
                # ファイルが差し替わってる: .part 破棄
                print(f"file_id mismatch (meta={meta_fid[:8]}.., new={file_id[:8]}..), discarding .part",
                      file=sys.stderr)
                try: os.remove(part_path)
                except Exception: pass
                try: os.remove(meta_path)
                except Exception: pass

        req_headers = {}
        if resume_from > 0:
            req_headers['Range'] = f'bytes={resume_from}-'

        try:
            r = self.scraper.get(download_url, stream=True, timeout=30, headers=req_headers or None)
        except Exception as e:
            return ('cdn_error', f"Connection failed to {host}: {type(e).__name__}: {str(e)[:200]}")

        if r.status_code == 403:
            return ('auth_error', f"Download blocked (403) at {host}")
        if r.status_code in (404, 410):
            return ('cdn_error', f"CDN returned {r.status_code} at {host}")
        if r.status_code in (500, 502, 503, 504):
            return ('cdn_error', f"CDN returned {r.status_code} at {host}")
        # Range リクエストの正常応答は 206。サーバーが Range 非対応で 200 を返した場合は最初から扱い。
        if resume_from > 0 and r.status_code == 200:
            print(f"Server ignored Range header, restarting from 0", file=sys.stderr)
            resume_from = 0
        elif resume_from > 0 and r.status_code == 416:
            # サーバー側ファイルが縮んだ (再エンコード等) → .part 破棄して 0 からやり直し
            print(f"Range Not Satisfiable (416), discarding .part and retrying from 0", file=sys.stderr)
            try: os.remove(part_path)
            except Exception: pass
            try: os.remove(meta_path)
            except Exception: pass
            return ('cdn_error', f"Range not satisfiable at {host}, retry from 0")
        elif resume_from > 0 and r.status_code != 206:
            return ('hard_error', f"Unexpected status {r.status_code} for Range request at {host}")
        elif resume_from == 0 and r.status_code != 200:
            return ('hard_error', f"Download failed: HTTP {r.status_code} at {host}")

        # 整合性検証: Content-Range から元ファイル全体サイズを取得して .meta と照合
        # 206 の場合: "bytes 100-999/270593201" → total=270593201
        # 200 の場合: Content-Length が total
        content_range = r.headers.get('content-range', '')
        if content_range and '/' in content_range:
            try:
                total_size = int(content_range.rsplit('/', 1)[1])
            except Exception:
                total_size = 0
        else:
            total_size = int(r.headers.get('content-length', 0)) + resume_from

        server_etag = r.headers.get('etag', '')

        # メタの size/etag と矛盾していれば .part 破棄して最初からやり直し
        if resume_meta and resume_from > 0:
            meta_size = resume_meta.get('size', 0)
            meta_etag = resume_meta.get('etag', '')
            if meta_size and total_size and meta_size != total_size:
                print(f"size mismatch (meta={meta_size}, server={total_size}), discarding .part",
                      file=sys.stderr)
                try: os.remove(part_path)
                except Exception: pass
                try: os.remove(meta_path)
                except Exception: pass
                return ('cdn_error', f"Resume size mismatch at {host}")
            if meta_etag and server_etag and meta_etag != server_etag:
                print(f"etag mismatch (meta={meta_etag}, server={server_etag}), discarding .part",
                      file=sys.stderr)
                try: os.remove(part_path)
                except Exception: pass
                try: os.remove(meta_path)
                except Exception: pass
                return ('cdn_error', f"Resume etag mismatch at {host}")

        try:
            os.makedirs(os.path.dirname(part_path) or '.', exist_ok=True)
        except Exception as e:
            return ('hard_error', f"Cannot create output directory: {e}")

        # .meta を書き込む (file_id / size / etag を保存)
        if file_id:
            self._write_resume_meta(meta_path, {
                'file_id': file_id,
                'size': total_size,
                'etag': server_etag,
                'last_modified': r.headers.get('last-modified', ''),
            })

        # ファイルハンドル: resume 時は r+b で seek、新規は wb。
        # Windows の 'ab' モードは seek を無視して必ず末尾追記するので使わない
        # (truncate 後に他プロセスが書き込んだ場合に上書きできず巨大化するため)。
        downloaded = resume_from
        last_percent = -1
        try:
            if resume_from > 0:
                fh = open(part_path, 'r+b')
                fh.truncate(resume_from)
                fh.seek(resume_from)
            else:
                fh = open(part_path, 'wb')
        except Exception as e:
            return ('hard_error', f"Cannot prepare .part file: {e}")

        try:
            with fh as f:
                for chunk in r.iter_content(chunk_size=65536):
                    if chunk:
                        f.write(chunk)
                        downloaded += len(chunk)
                        if total_size > 0:
                            progress = (downloaded / total_size) * 100
                            current_percent = int(progress)
                            if current_percent > last_percent:
                                print(f"Progress: {current_percent}%", file=sys.stderr)
                                last_percent = current_percent
        except Exception as e:
            msg = str(e)
            is_network = any(s in msg for s in (
                "Connection broken", "Connection reset", "ConnectionReset",
                "Read timed out", "ReadTimeoutError", "ConnectionError",
                "Max retries exceeded", "RemoteDisconnected",
            ))
            kind2 = 'cdn_error' if is_network else 'hard_error'
            # .part / .meta は残しておく (次回リジューム)
            return (kind2, f"Stream error at {host} ({downloaded} bytes): {type(e).__name__}: {msg[:200]}")

        # 完了サイズ検証
        if total_size > 0 and downloaded != total_size:
            return ('cdn_error', f"Size mismatch at {host}: got {downloaded}, expected {total_size}")

        # アトミックリネーム: .part → final
        # os.replace は Windows でも上書き OK (POSIX 互換)。
        # 旧コードの os.remove(output_path) は他プロセスが開いてる場合に
        # PermissionError で破綻するため削除。
        try:
            os.replace(part_path, output_path)
            try: os.remove(meta_path)
            except Exception: pass
        except Exception as e:
            return ('hard_error', f"Failed to finalize {output_path}: {e}")

        print("Progress: 100%", file=sys.stderr)
        return ('success', downloaded)

    def download_video(self, video_id: str, output_path: str, quality: str = "Source") -> dict:
        """動画をダウンロード。CDN 404/接続不能時は別 CDN を引き当てるため
        get_download_url をリトライする (iwara の filesq がリクエスト毎に
        ランダム CDN を返すので、いずれ生きてる CDN が選ばれることを期待)。"""
        if not self.token:
            return {"success": False, "error": "Login required", "code": "LOGIN_REQUIRED"}

        from urllib.parse import urlparse
        last_error = None
        last_quality = None
        last_title = None
        last_file_id = None
        last_author_username = None
        last_author_name = None
        tried_hosts = []
        cdn_failure_count = 0

        for attempt in range(self._CDN_REDIRECT_MAX_RETRIES):
            url_info = self.get_download_url(video_id, quality)
            if not url_info["success"]:
                # filesq 段階で失敗 → CDN 切替では救えないので即時返す
                if attempt == 0:
                    return url_info
                # 既に何度か CDN 試行済みなら、最後のエラーを返す
                err = f"{url_info.get('error', 'unknown')} (after {attempt} CDN retries: {last_error})"
                return {"success": False, "error": err, "code": url_info.get("code")}

            download_url = url_info["url"]
            last_quality = url_info.get("quality")
            last_title = url_info.get("title")
            last_file_id = url_info.get("file_id") or ""
            last_author_username = url_info.get("author_username")
            last_author_name = url_info.get("author_name")
            host = urlparse(download_url).netloc

            # レジューム判定用: 既存 .part / .meta を読み込み
            _, meta_path = self._part_paths(output_path)
            resume_meta = self._read_resume_meta(meta_path)

            print(f"Downloading: {last_title} ({last_quality}) [attempt {attempt+1}/{self._CDN_REDIRECT_MAX_RETRIES}, host={host}]",
                  file=sys.stderr)

            try:
                kind, payload = self._try_download_once(
                    download_url, output_path,
                    file_id=last_file_id, resume_meta=resume_meta,
                )
            except Exception as e:
                kind, payload = ('hard_error', f"Download exception: {e}")

            if kind == 'success':
                return {
                    "success": True,
                    "path": output_path,
                    "size": payload,
                    "quality": last_quality,
                    "file_id": last_file_id,
                    "author_username": last_author_username,
                    "author_name": last_author_name,
                    "title": last_title,
                    "cdn_retries": attempt,
                }

            tried_hosts.append(f"{host}={payload}")
            last_error = payload

            if kind == 'auth_error':
                # 403 は権限問題なので CDN 切替では救えない
                return {"success": False, "error": payload, "code": "ACCESS_DENIED"}
            if kind == 'hard_error':
                # ディスク書込失敗等
                return {"success": False, "error": payload}

            # cdn_error: 別 CDN を引き当てるため再試行
            cdn_failure_count += 1
            print(f"CDN error ({payload}), retrying with fresh URL...", file=sys.stderr)
            # 連続失敗時は少し待ってからリトライ (filesq のキャッシュ更新を期待)
            time.sleep(min(1.0 + 0.5 * attempt, 3.0))

        # 全 CDN 失敗
        return {
            "success": False,
            "error": f"All CDN candidates failed ({cdn_failure_count} retries): {' | '.join(tried_hosts[-self._CDN_REDIRECT_MAX_RETRIES:])}",
            "code": "CDN_UNAVAILABLE",
        }



def _resolve_yt_dlp(yt_dlp_path: str) -> str | None:
    """yt-dlp 実行コマンドを解決。見つからなければ None"""
    import shutil
    if not yt_dlp_path:
        yt_dlp_path = "yt-dlp"
    # フルパス指定の場合
    if os.path.isabs(yt_dlp_path) and os.path.isfile(yt_dlp_path):
        return yt_dlp_path
    # PATH 検索
    found = shutil.which(yt_dlp_path)
    if found:
        return found
    # python -m yt_dlp も試す
    try:
        subprocess.run([sys.executable, "-m", "yt_dlp", "--version"],
                       capture_output=True, check=True, timeout=15)
        return f"{sys.executable} -m yt_dlp"
    except Exception:
        return None


def _install_yt_dlp() -> tuple[bool, str]:
    """pip install yt-dlp を実行"""
    try:
        print("yt-dlp が見つからないため pip でインストールを試行中...", file=sys.stderr)
        result = subprocess.run(
            [sys.executable, "-m", "pip", "install", "-U", "yt-dlp"],
            capture_output=True, text=True, timeout=300
        )
        if result.returncode == 0:
            print("yt-dlp インストール完了", file=sys.stderr)
            return True, ""
        return False, result.stderr or result.stdout
    except Exception as e:
        return False, str(e)


def _update_yt_dlp(yt_dlp_cmd: str) -> tuple[bool, str]:
    """yt-dlp 自体を -U で更新(pip 経由インストールの場合は pip --upgrade)"""
    try:
        # python -m yt_dlp 形式の場合は pip で更新
        if yt_dlp_cmd.startswith(sys.executable):
            print("yt-dlp を pip で更新中...", file=sys.stderr)
            result = subprocess.run(
                [sys.executable, "-m", "pip", "install", "--upgrade", "yt-dlp"],
                capture_output=True, text=True, timeout=300
            )
        else:
            # スタンドアロン版は -U で自己更新
            cmd_parts = yt_dlp_cmd.split()
            print(f"yt-dlp を -U で更新中: {yt_dlp_cmd}", file=sys.stderr)
            result = subprocess.run(
                cmd_parts + ["-U"],
                capture_output=True, text=True, timeout=300
            )
        return result.returncode == 0, (result.stderr or result.stdout)
    except Exception as e:
        return False, str(e)


def _run_yt_dlp(yt_dlp_cmd: str, embed_url: str, output_path: str) -> tuple[bool, str]:
    """yt-dlp を実行して動画をDL。output_path はフルパス(拡張子抜きの場合は -o テンプレート扱い)"""
    try:
        # output_path はファイル名フォーマット(拡張子なしの想定)
        # 拡張子なしならテンプレートとして使用、ありなら % 不要
        if "." not in os.path.basename(output_path):
            out_template = output_path + ".%(ext)s"
        else:
            out_template = output_path

        cmd_parts = yt_dlp_cmd.split()
        full_cmd = cmd_parts + [
            "-o", out_template,
            "--no-playlist",
            "--no-warnings",
            "--newline",  # 進捗を行単位で
            "--merge-output-format", "mp4",
            embed_url,
        ]
        print(f"yt-dlp 実行: {' '.join(full_cmd)}", file=sys.stderr)

        process = subprocess.Popen(
            full_cmd,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            encoding='utf-8',
            errors='replace',
        )
        last_lines = []
        for line in process.stdout:
            line = line.rstrip()
            print(line, file=sys.stderr)
            last_lines.append(line)
            if len(last_lines) > 20:
                last_lines.pop(0)
            # Progress: XX% 形式に変換して進捗通知
            import re
            m = re.search(r'\[download\]\s+([\d.]+)%', line)
            if m:
                print(f"Progress: {m.group(1)}%", file=sys.stderr)

        rc = process.wait()
        if rc == 0:
            return True, ""
        return False, "\n".join(last_lines[-5:])
    except Exception as e:
        return False, str(e)


def download_external_video(embed_url: str, output_path: str, yt_dlp_path: str = "yt-dlp") -> dict:
    """yt-dlp で外部動画をDL。未インストールなら pip 自動DL、失敗時は -U して再試行"""
    if not embed_url:
        return {"success": False, "error": "embed_url is empty"}

    yt_dlp_cmd = _resolve_yt_dlp(yt_dlp_path)

    # yt-dlp 不在 → pip install
    if yt_dlp_cmd is None:
        ok, err = _install_yt_dlp()
        if not ok:
            return {"success": False, "error": f"yt-dlp のインストールに失敗しました: {err[:300]}"}
        yt_dlp_cmd = _resolve_yt_dlp(yt_dlp_path)
        if yt_dlp_cmd is None:
            return {"success": False, "error": "yt-dlp インストール後も実行コマンドを解決できませんでした"}

    # DL 試行
    ok, err = _run_yt_dlp(yt_dlp_cmd, embed_url, output_path)
    if ok:
        # 実際に保存されたファイルを探す
        saved = _find_saved_file(output_path)
        return {"success": True, "file_path": saved, "url": embed_url}

    # 失敗 → yt-dlp 更新して再試行
    print(f"yt-dlp DL 失敗、-U 後に再試行: {err[:200]}", file=sys.stderr)
    upd_ok, upd_err = _update_yt_dlp(yt_dlp_cmd)
    if not upd_ok:
        return {"success": False, "error": f"yt-dlp DL失敗かつ更新も失敗: {err[:200]} / 更新エラー: {upd_err[:200]}"}

    yt_dlp_cmd = _resolve_yt_dlp(yt_dlp_path) or yt_dlp_cmd
    ok2, err2 = _run_yt_dlp(yt_dlp_cmd, embed_url, output_path)
    if ok2:
        saved = _find_saved_file(output_path)
        return {"success": True, "file_path": saved, "url": embed_url, "updated": True}
    return {"success": False, "error": f"yt-dlp DL失敗(更新後も失敗): {err2[:300]}"}


def _find_saved_file(base_path: str) -> str:
    """yt-dlp が出力したファイルを探す(拡張子を補完)"""
    if os.path.isfile(base_path):
        return base_path
    import glob
    # base_path + .xxx を探す
    candidates = glob.glob(base_path + ".*")
    if candidates:
        # 最新のファイル
        return max(candidates, key=os.path.getmtime)
    # 拡張子付きで探した結果
    return base_path


def parse_rate_limit_args():
    """レート制限設定をコマンドライン引数からパース"""
    config = {}
    
    for i, arg in enumerate(sys.argv):
        if arg == "--api-delay" and i + 1 < len(sys.argv):
            config['api_delay'] = float(sys.argv[i + 1])
        elif arg == "--page-delay" and i + 1 < len(sys.argv):
            config['page_delay'] = float(sys.argv[i + 1])
        elif arg == "--rate-limit-base" and i + 1 < len(sys.argv):
            config['rate_limit_base'] = float(sys.argv[i + 1])
        elif arg == "--rate-limit-max" and i + 1 < len(sys.argv):
            config['rate_limit_max'] = float(sys.argv[i + 1])
        elif arg == "--no-backoff":
            config['enable_backoff'] = False
    
    return config


def main():
    if len(sys.argv) < 2:
        print(json.dumps({"success": False, "error": "No action specified"}))
        sys.exit(1)
    
    action = sys.argv[1]
    
    # トークンを探す
    token = None
    for i, arg in enumerate(sys.argv):
        if arg == "--token" and i + 1 < len(sys.argv):
            token = sys.argv[i + 1]
            break
    
    # 環境変数からも取得可能
    if not token:
        token = os.environ.get("IWARA_TOKEN")

    # --site で iwara.tv / iwara.ai を切替 (デフォルト www.iwara.tv)
    site = None
    for i, arg in enumerate(sys.argv):
        if arg == "--site" and i + 1 < len(sys.argv):
            site = sys.argv[i + 1]
            break

    # レート制限設定をパース
    rate_limit_config = parse_rate_limit_args()

    api = IwaraAPI(token=token, rate_limit_config=rate_limit_config, site=site)
    
    if action == "login":
        if len(sys.argv) < 4:
            print(json.dumps({"success": False, "error": "Usage: login <email> <password>"}))
            sys.exit(1)
        result = api.login(sys.argv[2], sys.argv[3])

    elif action == "verify_token":
        result = api.verify_token()

    elif action == "get_videos":
        if len(sys.argv) < 3:
            print(json.dumps({"success": False, "error": "Usage: get_videos <username>"}))
            sys.exit(1)
        result = api.get_user_videos(sys.argv[2])
        
    elif action == "download":
        if len(sys.argv) < 4:
            print(json.dumps({"success": False, "error": "Usage: download <video_id> <output_path>"}))
            sys.exit(1)
        result = api.download_video(sys.argv[2], sys.argv[3])
        
    elif action == "get_url":
        if len(sys.argv) < 3:
            print(json.dumps({"success": False, "error": "Usage: get_url <video_id>"}))
            sys.exit(1)
        result = api.get_download_url(sys.argv[2])

    elif action == "search":
        if len(sys.argv) < 3:
            print(json.dumps({"success": False, "error": "Usage: search <query> [page] [limit]"}))
            sys.exit(1)
        query = sys.argv[2]
        page = 0
        limit = 32
        # オプション位置引数 (page, limit)
        try:
            if len(sys.argv) >= 4 and not sys.argv[3].startswith("--"):
                page = int(sys.argv[3])
            if len(sys.argv) >= 5 and not sys.argv[4].startswith("--"):
                limit = int(sys.argv[4])
        except ValueError:
            pass
        result = api.search_videos(query, page=page, limit=limit)

    elif action == "download_external":
        if len(sys.argv) < 4:
            print(json.dumps({"success": False, "error": "Usage: download_external <embed_url> <output_path> [--yt-dlp-path <path>]"}))
            sys.exit(1)
        embed_url = sys.argv[2]
        output_path = sys.argv[3]
        yt_dlp_path = "yt-dlp"
        for i, arg in enumerate(sys.argv):
            if arg == "--yt-dlp-path" and i + 1 < len(sys.argv):
                yt_dlp_path = sys.argv[i + 1]
                break
        result = download_external_video(embed_url, output_path, yt_dlp_path)

    else:
        result = {"success": False, "error": f"Unknown action: {action}"}
    
    # Windows cp932対策: ASCIIエスケープして出力
    print(json.dumps(result, ensure_ascii=True))
    sys.exit(0 if result.get("success") else 1)


if __name__ == "__main__":
    try:
        main()
    except SystemExit:
        raise
    except BaseException as _e:
        # トップレベル例外を JSON にして stdout に返す。
        # これがないと Python のトレースバックが stderr に出るだけで stdout が空になり、
        # 呼び出し元 C# 側の json.parse が "Python 応答なし" になる。
        try:
            print(json.dumps({"success": False, "error": f"{type(_e).__name__}: {_e}"}, ensure_ascii=False))
        except Exception:
            pass
        sys.exit(1)
