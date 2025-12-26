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
from urllib.parse import urlparse, parse_qs

class IwaraAPI:
    def __init__(self, token=None):
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

    def login(self, email: str, password: str) -> dict:
        """ログインしてトークンを取得"""
        try:
            r = self.scraper.post(
                f"{self.api_url}/user/login",
                json={"email": email, "password": password}
            )
            
            if r.status_code != 200:
                return {"success": False, "error": f"Login failed: HTTP {r.status_code}"}
            
            data = r.json()
            self.token = data.get("token")
            
            if self.token:
                return {"success": True, "token": self.token}
            else:
                return {"success": False, "error": "No token in response"}
                
        except Exception as e:
            return {"success": False, "error": str(e)}

    def _auth_header(self) -> dict:
        """認証ヘッダーを返す"""
        if self.token:
            return {"Authorization": f"Bearer {self.token}"}
        return {}

    def get_user_videos(self, username: str) -> dict:
        """ユーザーの全動画リストを取得"""
        try:
            # 1. プロフィールからuser_idを取得
            profile_r = self.scraper.get(
                f"{self.api_url}/profile/{username}",
                headers=self._auth_header()
            )
            
            if profile_r.status_code != 200:
                return {"success": False, "error": f"Profile fetch failed: HTTP {profile_r.status_code}"}
            
            profile_data = profile_r.json()
            user_id = profile_data.get("user", {}).get("id")
            
            if not user_id:
                return {"success": False, "error": "User ID not found"}
            
            # 2. 動画リストを全ページ取得
            videos = []
            page = 0
            max_pages = 100  # 安全のため上限
            
            while page < max_pages:
                r = self.scraper.get(
                    f"{self.api_url}/videos",
                    params={
                        "page": page,
                        "sort": "date",
                        "user": user_id,
                        "limit": 32
                    },
                    headers=self._auth_header()
                )
                
                if r.status_code != 200:
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
            r = self.scraper.get(
                f"{self.api_url}/video/{video_id}",
                headers=self._auth_header()
            )
            
            if r.status_code != 200:
                return {"success": False, "error": f"Video info failed: HTTP {r.status_code}"}
            
            return {"success": True, "data": r.json()}
            
        except Exception as e:
            return {"success": False, "error": str(e)}

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
            
            r = self.scraper.get(file_url, headers=headers)
            
            if r.status_code != 200:
                return {"success": False, "error": f"File URL fetch failed: HTTP {r.status_code}"}
            
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
            
            if r.status_code != 200:
                return {"success": False, "error": f"Download failed: HTTP {r.status_code}"}
            
            # ファイルサイズ
            total_size = int(r.headers.get('content-length', 0))
            
            # 出力ディレクトリ作成
            os.makedirs(os.path.dirname(output_path) or '.', exist_ok=True)
            
            # ダウンロード（1%ごとに進捗出力）
            downloaded = 0
            last_percent = -1
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
            
            print("Progress: 100%", file=sys.stderr)  # 完了
            
            return {
                "success": True,
                "path": output_path,
                "size": downloaded,
                "quality": url_info["quality"]
            }
            
        except Exception as e:
            return {"success": False, "error": str(e)}


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
    
    api = IwaraAPI(token=token)
    
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
