#!/usr/bin/env python3
"""
iwara_helper.py - Iwara.tv API helper with Cloudflare bypass
Usage:
    python iwara_helper.py login <email> <password>
    python iwara_helper.py get_videos <username> [--token <token>]
    python iwara_helper.py download <video_id> <output_path> [--token <token>]
"""

import cloudscraper
import json
import sys
import os
import hashlib
import re
import time
from urllib.parse import urlparse, parse_qs

class IwaraAPI:
    def __init__(self, token=None, rate_limit_config=None):
        self.scraper = cloudscraper.create_scraper(
            browser={
                'browser': 'chrome',
                'platform': 'windows',
                'desktop': True
            }
        )
        self.api_url = "https://api.iwara.tv"
        self.file_url = "https://files.iwara.tv"
        self.token = token
        
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
                return {"success": True, "token": self.token}
            else:
                return {"success": False, "error": "No token in response"}
                
        except Exception as e:
            return {"success": False, "error": f"Exception: {str(e)}"}

    def _auth_header(self) -> dict:
        """認証ヘッダーを返す"""
        if self.token:
            return {"Authorization": f"Bearer {self.token}"}
        return {}

    def get_user_videos(self, username: str) -> dict:
        """ユーザーの全動画リストを取得"""
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
            user_id = profile_data.get("user", {}).get("id")
            
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
                    videos.append({
                        "id": video.get("id"),
                        "title": video.get("title"),
                        "slug": video.get("slug"),
                        "thumbnail": self._get_thumbnail_url(video),
                        "duration": video.get("file", {}).get("duration", 0),
                        "created_at": video.get("createdAt"),
                        "private": video.get("private", False)
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
        file_id = video.get("file", {}).get("id")
        if file_id:
            return f"https://i.iwara.tv/image/thumbnail/{file_id}/thumbnail-00.jpg"
        return ""

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
        try:
            # 動画情報を取得
            video_info = self.get_video_info(video_id)
            if not video_info["success"]:
                return video_info
            
            video_data = video_info["data"]
            file_url = video_data.get("fileUrl")
            
            if not file_url:
                return {"success": False, "error": "No fileUrl in video data"}
            
            # ファイルURLにアクセスしてダウンロードリンク取得
            # X-Versionヘッダーが必要
            parsed = urlparse(file_url)
            path_parts = parsed.path.rstrip('/').split('/')
            query = parse_qs(parsed.query)
            expires = query.get('expires', [''])[0]
            
            # X-Version計算（yt-dlpのロジックから）
            x_version = hashlib.sha1(
                '_'.join([path_parts[-1], expires, '5nFp9kmbNnHdAFhaqMvt']).encode()
            ).hexdigest()
            
            headers = self._auth_header()
            headers['X-Version'] = x_version
            
            r = self._request_with_retry('GET', file_url, headers=headers)
            
            if r is None:
                return {"success": False, "error": "No response from file server"}
            
            if r.status_code == 403:
                try:
                    body = r.json()
                    msg = body.get("message", "") or body.get("error", "")
                except:
                    msg = r.text[:100] if r.text else ""
                return {"success": False, "error": f"Access denied to download: {msg or 'Private video or login required'}"}
            
            if r.status_code != 200:
                try:
                    body = r.json()
                    msg = body.get("message", "") or body.get("error", "")
                except:
                    msg = r.text[:100] if r.text else ""
                return {"success": False, "error": f"File URL fetch failed: HTTP {r.status_code} - {msg}"}
            
            files = r.json()
            
            # 指定画質のURLを探す
            download_url = None
            for f in files:
                if f.get("name") == quality:
                    download_url = f.get("src", {}).get("download")
                    break
            
            # Sourceが見つからなければ最高画質を選択
            if not download_url and files:
                # 画質の優先順位
                quality_order = ["Source", "540", "360", "preview"]
                for q in quality_order:
                    for f in files:
                        if f.get("name") == q:
                            download_url = f.get("src", {}).get("download")
                            quality = q
                            break
                    if download_url:
                        break
            
            if not download_url:
                return {"success": False, "error": "No download URL found"}
            
            # URLが相対パスの場合
            if download_url.startswith("//"):
                download_url = "https:" + download_url
            
            return {
                "success": True,
                "url": download_url,
                "quality": quality,
                "title": video_data.get("title", video_id)
            }
            
        except Exception as e:
            return {"success": False, "error": str(e)}

    def download_video(self, video_id: str, output_path: str, quality: str = "Source") -> dict:
        """動画をダウンロード"""
        try:
            # ダウンロードURL取得
            url_info = self.get_download_url(video_id, quality)
            if not url_info["success"]:
                return url_info
            
            download_url = url_info["url"]
            
            # ダウンロード
            print(f"Downloading: {url_info['title']} ({url_info['quality']})", file=sys.stderr)
            
            r = self.scraper.get(download_url, stream=True)
            
            if r.status_code == 403:
                try:
                    # ストリームの場合はボディを読み取れないことがある
                    msg = "Access denied"
                except:
                    msg = "Access denied"
                return {"success": False, "error": f"Download blocked (403): {msg}"}
            
            if r.status_code == 404:
                return {"success": False, "error": "Download URL expired or not found (404)"}
            
            if r.status_code != 200:
                return {"success": False, "error": f"Download failed: HTTP {r.status_code}"}
            
            # ファイルサイズ
            total_size = int(r.headers.get('content-length', 0))
            
            # 出力ディレクトリ作成
            try:
                os.makedirs(os.path.dirname(output_path) or '.', exist_ok=True)
            except Exception as e:
                return {"success": False, "error": f"Cannot create output directory: {e}"}
            
            # ダウンロード（1%ごとに進捗出力）
            downloaded = 0
            last_percent = -1
            try:
                with open(output_path, 'wb') as f:
                    for chunk in r.iter_content(chunk_size=65536):  # 64KBチャンク
                        if chunk:
                            f.write(chunk)
                            downloaded += len(chunk)
                            if total_size > 0:
                                progress = (downloaded / total_size) * 100
                                current_percent = int(progress)
                                # 1%ごとに出力
                                if current_percent > last_percent:
                                    print(f"Progress: {current_percent}%", file=sys.stderr)
                                    last_percent = current_percent
            except IOError as e:
                return {"success": False, "error": f"File write error: {e}"}
            
            print("Progress: 100%", file=sys.stderr)  # 完了
            
            return {
                "success": True,
                "path": output_path,
                "size": downloaded,
                "quality": url_info["quality"]
            }
            
        except Exception as e:
            return {"success": False, "error": f"Download exception: {str(e)}"}


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
    
    # レート制限設定をパース
    rate_limit_config = parse_rate_limit_args()
    
    api = IwaraAPI(token=token, rate_limit_config=rate_limit_config)
    
    if action == "login":
        if len(sys.argv) < 4:
            print(json.dumps({"success": False, "error": "Usage: login <email> <password>"}))
            sys.exit(1)
        result = api.login(sys.argv[2], sys.argv[3])
        
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
        
    else:
        result = {"success": False, "error": f"Unknown action: {action}"}
    
    # Windows cp932対策: ASCIIエスケープして出力
    print(json.dumps(result, ensure_ascii=True))
    sys.exit(0 if result.get("success") else 1)


if __name__ == "__main__":
    main()
