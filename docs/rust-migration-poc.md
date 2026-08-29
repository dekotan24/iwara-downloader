# iwara_helper Python → Rust PoC

調査日: 2026-08-28 JST

調査対象はローカルcheckout `C:\Users\fanta\Documents\App\iwara-downloader` の `feature/wpf-migration` ブランチ、commit `8854fdde82d898adf231715240825670c01fa926` です。対象Python helperのgit blob hashは `fc061fb8b6c06809152d1fdcb9bbba42543cf0f4`、行数は1,320行でした。既存の未コミット変更は保持し、`IwaraDownloader/iwara_helper.py`、C#本体、Setup Wizard、download pipelineは変更していません。

この文書は正式移行の設計書ではなく、現行helperをRustで置換できるかを判定するための隔離PoCの結果です。

## Existing Python architecture

現在の実行経路は次の通りです。

```text
C# IwaraApiService / IwaraSearch
        │ ProcessStartInfo
        │ IWARA_TOKEN環境変数、action、引数
        ▼
python.exe iwara_helper.py <action>
        ├─ cloudscraper session
        ├─ api.iwara.tv / files.iwara.tv
        ├─ JSON stdout（C#がparse）
        └─ 進捗・診断 stderr
```

- `IwaraDownloader/Services/IwaraApiService.cs` がlogin、token確認、動画一覧、URL取得、動画DL、外部動画DLのPythonプロセスを起動する。
- `IwaraDownloader/Services/IwaraSearch.cs` はsearch時に別途Pythonプロセスを起動する。
- tokenはC#のコマンドラインではなく `IWARA_TOKEN` 環境変数で渡される。Python helperも `--token` と環境変数の両方を受け付ける。
- `IwaraDownloader/iwara_setup.bat` はpipを用意し、`cloudscraper` とPython版 `yt-dlp` をインストールし、`.python_setup_done`を作る。
- 現行CLIの正式なaction名は `login`、`verify-token`、`get-videos`、`download`、`get-url`、`search`、`download-external` であり、旧snake_case (`verify_token` 等) も互換受付する。`get-video` は検証用コマンドである。

## Python helper feature inventory

対象は `IwaraDownloader/iwara_helper.py` 全体です。行番号は調査時のローカルファイル基準です。

| 機能 | Pythonライブラリ | Rust置換可能性 | 難易度 | 備考 |
|---|---|---|---|---|
| CLI action dispatch | `sys` | 可能 | easy | login、verify_token、get_videos、download、get_url、search、download_external |
| UTF-8 stdout/stderr | `sys` | 可能 | easy | Windows cp932でのJSON/日本語進捗を回避 |
| JSON入出力 | `json` | 可能 | easy | stdoutは最終JSON、stderrは進捗・診断 |
| JWT payload解析 | `base64`、`json` | 可能 | easy | payload decodeのみ。署名検証・refreshはしない（30-40行） |
| login | `cloudscraper`、JSON POST | 可能 | moderate | `POST /user/login`、tokenを取得しexp/id/typeをpayloadから読む |
| authentication | Bearer header | 可能 | easy | `Authorization: Bearer ...`。tokenの永続化はC#側 |
| token確認 | `time`、JSON GET | 可能 | easy | exp判定後、`GET /user`で200確認 |
| token refresh相当 | なし | 可能 | easy | 実refresh endpointはない。期限切れは`TOKEN_EXPIRED`として再login |
| X-Site生成 | 文字列 | 可能 | easy | `www.iwara.tv` / `www.iwara.ai`。認証headerにも付与 |
| API GET | `cloudscraper` | 可能 | easy | URL、query、headersをreqwestで再現可能 |
| API POST | `cloudscraper` | 可能 | easy | loginのJSON POST |
| search | `cloudscraper`、JSON | 可能 | easy | `GET /search?type=videos&query=&page=&limit=`、results/countを整形 |
| user情報 | `cloudscraper`、JSON | 可能 | easy | `/user`のusername、role、premiumを読む |
| user video一覧 | `cloudscraper`、JSON | 可能 | moderate | profile→user id→`/videos`の2段階 |
| pagination | `time`、ループ | 可能 | easy | user videoはpage 0から32件ずつ、最大100ページ、空resultsで停止 |
| video metadata | `cloudscraper`、JSON | 可能 | easy | `GET /video/{id}`の生JSONを返す |
| raw JSON pruning | `dict` | 可能 | easy | `siteId`とuserのavatar/status/follow関係等を除外してDB向けに縮小 |
| thumbnail URL | 文字列 | 可能 | easy | file idから`i.iwara.tv/image/thumbnail/...`を組み立てる |
| fileUrl取得 | URL parse、JSON | 可能 | easy | video metadataの`fileUrl`をfilesq入口として使用 |
| source URL取得 | JSON | 可能 | easy | file entryの`src.download`優先、なければ`src.view` |
| X-Version生成 | `hashlib` SHA-1 | 可能 | easy | `SHA1(file_id + "_" + expires + "_" + secret)` |
| secret取得 | `re`、`cloudscraper` | 可能 | moderate | homepage→`/main.<hash>.js`→`expires + "_<secret>"`抽出 |
| secret cache | `os`、`time`、file I/O | 可能 | easy | `%APPDATA%/IwaraDownloader/x_version_secret.txt`、TTL 30日、fallbackあり |
| secret更新 | `re`、HTTP | 可能 | moderate | Source/540が欠けたとき、セッション中1回だけmain.js再取得 |
| quality選択 | ループ | 可能 | easy | requested→Source→540→360→preview、未知指定は高画質順へfallback |
| rate limit間隔 | `time.sleep` | 可能 | easy | API間隔、pagination間隔を設定可能 |
| 429処理 | `time` | 可能 | easy | base×2^attempt、max上限までbackoff |
| 403処理 | JSON/text判定 | 可能 | moderate | rate limit/Cloudflare文言を含む403だけretry、権限403は即失敗 |
| API retry | `cloudscraper`、例外処理 | 可能 | easy | 429/判定済み403を最大3回。通常例外は線形待機 |
| cookies | `cloudscraper` session | 可能 | moderate | helperで明示保存はしないがsession cookie jarを利用。reqwest cookie_storeで通常sessionは再現可能 |
| User-Agent | `cloudscraper.create_scraper(browser=chrome/windows)` | 可能 | easy | Rust PoCはChrome風UAを設定 |
| Referer / Origin | 明示設定なし | 可能 | easy | helper自身は明示付与していない。APIは今回Originなしでも通った |
| Cloudflare challenge | `cloudscraper`内部 | 条件付き | blocker候補 | challengeを突破する独自機構はRust PoCに入れていない |
| CDN選択 | filesqの応答 | 可能 | moderate | filesqが返すURLを使用。接続/404/5xx時はfresh URLを最大6回取得 |
| Range download | `cloudscraper`、file I/O | 可能 | easy | `Range: bytes=N-`、206/200/416を処理 |
| resume | file I/O、JSON | 可能 | moderate | `.part`と`.part.meta`、file_id一致、末尾65,536 bytes rewind |
| `.part` | file I/O | 可能 | easy | resume中はr+bでtruncate/seek、新規はwb |
| ETag | response headers、JSON | 可能 | easy | metaとserver ETagが不一致ならpartialを破棄 |
| Content-Length | response headers | 可能 | easy | 200の総量、Range時のfallback計算に使用 |
| Content-Range | response headers | 可能 | easy | 206の`bytes start-end/total`から全体サイズを取得 |
| partial file validation | file I/O、metadata | 可能 | moderate | file id、size、ETagを照合。不一致時は初めから再取得 |
| rewind | file I/O | 可能 | easy | flush未完了の末尾ゴミを上書きする安全マージン |
| atomic rename | `os.replace` | 可能 | easy | `.part`→final。metaは成功後削除 |
| download retry | `time`、HTTP | 可能 | moderate | CDN errorだけfresh URL再取得、403はauth error、disk等はhard error |
| external video | `subprocess` | 可能 | moderate | embed URLをyt-dlpへ渡す。Rust `Command`で再現可能 |
| yt-dlp解決 | `shutil.which`、`subprocess` | 可能 | moderate | standalone command→PATH→`python -m yt_dlp`の順 |
| pip install | `subprocess` | 可能だが削除推奨 | moderate | Rust本体にpipを持ち込まずstandalone exeを配布する方針 |
| yt-dlp update | `subprocess` | 可能だが設計変更 | moderate | Python版は失敗後に`pip --upgrade`または`-U`。本番では暗黙更新を避ける |
| subprocess | `subprocess.Popen/run` | 可能 | easy | Rust標準ライブラリで可能。CancellationはKill/Job設計が必要 |
| stdout/stderr C#通信 | pipe、JSON | 可能 | moderate | 現行契約をRust CLIでも維持可能。stdoutに秘密を出さない設計が必要 |

### 実装上の注意点

- Pythonの`_decode_jwt_payload`はJWTの署名を検証しない。Rustで同じ挙動は容易だが、認証の安全性を高める機能ではない。
- 実際のtoken refresh APIは存在しない。C#もexp切れ・API拒否でlogoutし、再loginを要求する設計である。
- `_DEAD_CDN_CONNECTION_TIMEOUT` は定義されているが、現行helper内で参照されていない。実際のCDN故障管理はHTTP結果によるretryである。
- Python helperの`get_download_url`はtokenなしで即座に`LOGIN_REQUIRED`を返すが、現行公開APIのvideo/filesqは匿名でも取得できた。これは移植時に「既存C#のログイン必須意味を維持するか」と「公開API機能を開放するか」を決める必要がある。

## Python dependency classification

### A. Rustへほぼ直接置換可能

`serde_json`によるJSON、JWT payloadのbase64url decode、SHA-1、URL/query parse、HTTP GET/POST、headers、file I/O、pagination、Range、Content-Length/Range、ETag、`.part`、seek/truncate、atomic rename、retry/backoff、stdout/stderr pipeはRust標準ライブラリと小規模crateで置換可能です。

実際のPoCは `reqwest`（blocking + rustls + cookie store）、`serde_json`、`base64`、`sha1`、`url`、`regex`だけを使用しています。tokioやCLI frameworkは使っていません。

### B. Rustで置換可能だが設計変更が必要

- `subprocess`はRust `std::process::Command`へ移せるが、C#からのcancel時にyt-dlp/ffmpegの子プロセスツリーを確実に終了する必要がある。
- `stdout JSON IPC`は維持できる。現行の1 action=1プロセスをそのまま置換する案と、JSON Linesで常駐させる案がある。
- `pip install`はRust化の対象に残すべきではない。standalone `yt-dlp.exe`を固定版で配布し、更新はアプリ更新経路に寄せる。
- secretのhomepage/main.js抽出はregexとHTTPで再現できるが、フロントエンドbundleの形に依存するため、抽出失敗時のcache/fallback/診断が必要。
- CDN retry、resume、partial validationはRustで再現可能だが、本番化時には非同期cancel、同時DL、ファイルロック、ディスク容量をC#との契約まで含めてテストする必要がある。

### C. Rust移行上の主要リスク

- `cloudscraper`の主な価値は、通常のHTTP clientではなくCloudflare JavaScript challengeやTLS/browser fingerprintに対するPython側の補助である。
- `reqwest + rustls`は通常APIを取得できたが、challengeが発生した場合の解決機能は持たない。
- 正規ブラウザのcookieやWebView2のsessionを移す設計は可能だが、cookieの安全な受け渡し、期限、ユーザー操作、利用規約への適合を別途設計する必要がある。
- `cloudscraper`のchallenge突破や、サイト側アクセス制御を回避する独自機構は実装しない。

## PoC implementation

追加したファイル:

- `tools/iwara-rust-poc/Cargo.toml`
- `tools/iwara-rust-poc/Cargo.lock`
- `tools/iwara-rust-poc/src/main.rs`
- `tools/iwara-rust-poc/fixtures/parity.json`
- `tools/iwara-rust-poc/README.md`

実装したcommand:

| Command | 内容 |
|---|---|
| `login` | `IWARA_EMAIL`/`IWARA_PASSWORD`または引数でlogin。token本体は出さず、構造とexpの有無だけJSON出力 |
| `verify-token` | `IWARA_TOKEN`または`--token`を使い、ローカルexpと`GET /user`を確認 |
| `get-video` | `/video/{id}`を取得し、metadataのキーと公開情報のpresenceを出力 |
| `search` | `/search`のquery/page/limitを実行し、results/countを整形 |
| `user-videos` | profile→user id→`/videos` paginationを実行（最大100ページ） |
| `get-url` | video→filesq、secret cache/main.js抽出、X-Version、画質選択を実行。signed URL本体は出力しない |
| `download-test` | 直接URLに64KiB Rangeを送信し、status/Content-Range/ETagを出力 |
| `download-test-video` | video idからURL取得後、URL本体を出さずに64KiB Rangeを実行 |
| `probe` | status、redirect後URLのquery除去、header、body長/SHA-1、challenge指標を出力 |

PoCは正式なDLL、P/Invoke、C ABI、NativeAOT、C#統合を実装していません。`download-test`はRange通信の実証用であり、現行Pythonの本番用`.part.meta`全体、6回CDN再抽選、ffmpeg統合までは実装していません。

## Offline parity tests

`tools/iwara-rust-poc/fixtures/parity.json`は合成fixtureだけを含みます。実token、実secret、cookie、signed URLは含みません。

| 項目 | Python | Rust | 結果 |
|---|---|---|---|
| JWT payload decode | `{"exp":1700000000,"id":"fixture"}` | 同じ | PASS |
| X-Version入力 | `fixture-file_1700000000_fixture-secret` | 同じ | PASS |
| SHA-1結果 | `b38092089760701788e7011a7576d7881aff457b` | 同じ | PASS |
| secret regex形 | `expires + "_<20文字以上>"` | 同じ形を抽出 | PASS |
| quality fallback順 | Source→540→360→preview | 同じ | PASS |

実行結果:

```text
running 4 tests
test tests::jwt_payload_decodes_without_padding ... ok
test tests::quality_selection_order_prioritizes_requested_quality ... ok
test tests::x_version_matches_python_sha1_fixture ... ok
test tests::current_secret_pattern_is_extractable ... ok
test result: ok. 4 passed; 0 failed
```

Python標準`hashlib`とhelperの`_decode_jwt_payload`でも同じfixture結果を確認しました。

## Live API tests

テスト日時は2026-08-28 JSTです。認証情報を読み出したり保存したりせず、公開レスポンスと匿名セッションだけを使いました。

使用した公開video idは、`GET https://api.iwara.tv/videos?page=0&sort=date&limit=1` のレスポンスから取得したものです。レポートにはtoken、cookie、signed URL、secret値、個人アカウント情報を残していません。

### Python cloudscraper vs Rust reqwest probe

同じURLに対してPython `cloudscraper.create_scraper(browser=chrome/windows)`とRust `reqwest + rustls`を実行しました。

| Endpoint | Python | Rust | 観測 |
|---|---:|---:|---|
| `https://www.iwara.tv/` | 200 | 200 | redirect 0、Server=cloudflare、CF-RAYあり、Set-Cookieなし、challenge指標なし |
| `/videos?page=0&sort=date&limit=1` | 200 JSON | 200 JSON | redirect 0、CF-RAYあり、cookieなし、challenge指標なし |
| `/search?type=videos&query=test&page=0&limit=1` | 200 JSON | 200 JSON | redirect 0、CF-RAYあり、cookieなし、challenge指標なし |
| `/user` | 401 JSON | 401 JSON | anonymous tokenなしとして同じ。challenge指標なし |

API `/videos`と`/search`では同一captureでbody SHA-1も一致しました。homepageはPython 2,301 bytes、Rust 1,948 bytesとなりbody SHA-1が異なりましたが、両方200でmain.jsの特定とsecret抽出はRust側でも成功しました。homepageのbodyはcache/encoding/レスポンスvariantの影響を受け得るため、body hash一致を一般条件にはしていません。

`Server: cloudflare`や`CF-RAY`が付くこと自体はchallenge発生を意味しません。今回の対象URLではchallenge HTML、captcha、Turnstile、`verify you are human`相当は検出されませんでした。

### Python helper vs Rust CLI

| Test | Python helper | Rust PoC | Result |
|---|---|---|---|
| Login | 未実行（環境に`IWARA_EMAIL`/`IWARA_PASSWORD`なし） | 未実行（同じ理由） | NOT RUN |
| verify-token | tokenなしで`LOGIN_REQUIRED` | tokenなしで`LOGIN_REQUIRED` | PASS（境界動作） |
| get-video | `/video/{id}`成功、data keys確認 | HTTP 200、id/title/rating/fileUrl/thumbnail presence確認 | PASS |
| search | tokenなしでHTTP送信前に`LOGIN_REQUIRED` | HTTP 200、page=0、limit=1、count=3,725を取得 | SEMANTIC DIFFERENCE |
| user-videos | tokenなしでHTTP送信前に`LOGIN_REQUIRED` | profile→videos成功、1件・1ページ | SEMANTIC DIFFERENCE |
| get-url | tokenなしでHTTP送信前に`LOGIN_REQUIRED` | filesq HTTP 200、`Source/540/360/preview`を取得 | SEMANTIC DIFFERENCE |
| quality Source | helper内部直接呼出でSource選択 | Source選択 | PASS |
| quality 360 | helper側のpriority logicと整合 | requested=360で360選択 | PASS |
| Range | 206、Content-Length=65,536、Content-Rangeあり、ETagあり | 同じ | PASS |

Pythonの`search/user-videos/get-url`は現行C#呼び出しのログイン必須契約に合わせたguardです。Rust PoCはAPIが匿名公開している事実を調査するため、そのguardを設けずにendpointを呼びました。したがって、これはCloudflareの差ではなく、helperとPoCの意図的な入口条件の差です。

### filesq / secret / CDN Range

公開videoに対して、Python helperの処理関数とRust PoCの両方で次を確認しました。

- video metadataに`fileUrl`が存在する。
- homepageからmain.js URLを特定し、現在のbundleからX-Version secret patternを抽出できる。
- filesq responseはHTTP 200で、`Source`、`540`、`360`、`preview`を返した。
- Rustのsecret cacheは1回目がmain.js由来、以後はTTL内cache由来として機能した。secret値は出力していない。
- Python側は同じ公開videoでfilesq、Source選択、CDN取得に成功した。
- Python側Rangeは206、65,536 bytes、`bytes 0-65535/262104033`、ETagあり。
- Rust側`download-test-video`も206、65,536 bytes、同じtotal sizeのContent-Range、ETagあり。
- CDN hostはfilesqの応答ごとに変動した。PoCはhost名だけを結果に残し、query付きURLは残していない。

Range responseは`Accept-Ranges`が空でも206/Content-Rangeで正常に返りました。従ってAccept-Ranges headerだけでRange対応可否を判定してはいけません。

## Cloudflare findings

判定は今回の検証範囲では **Case A** です。

> 通常のreqwestで通常API、公開video metadata、filesq、CDNの一部Rangeが利用でき、cloudscraper固有のchallenge solverは必要ありませんでした。

ただしこれは「すべての条件でcloudscraper不要」という意味ではありません。

- login、token付き`/user`、private/friend-only video、iwara.aiのdifferentSite、429/403発生時、IP reputation変化時は今回の認証情報なし検証では未確認です。
- Cloudflareが将来challengeを返す場合、reqwestだけで解決できるという証拠はありません。
- challengeが必要になった場合は、Cloudflare突破コードを作らず、WebView2/正規ブラウザのユーザーセッションcookieを正規の範囲で使う設計を検討します。
- Referer/OriginはPython helperが明示付与していません。今回のAPIはRust側でも明示Originなしで通りました。

## yt-dlp migration

Python helperのyt-dlp経路は次の通りです。

1. 指定pathまたはPATHの`yt-dlp`を探す。
2. なければ`python -m yt_dlp --version`を試す。
3. それでもなければ`python -m pip install -U yt-dlp`。
4. DL失敗後にpip upgradeまたは`yt-dlp -U`を実行して再試行する。
5. yt-dlpのstdout/stderrを読み、`[download] XX%`をC#向け進捗に変換する。

standalone実証:

- 公式GitHub release APIでWindows asset `yt-dlp.exe`を確認した。
- `yt-dlp 2026.08.19`のstandalone `yt-dlp.exe`を一時work領域へ取得した。
- Python helperの`_resolve_yt_dlp`にそのpathを渡し、Python runtime内のmoduleではなくexe自体で`--version`を実行してreturncode 0を確認した。

したがってPython runtimeとpipを残さず、standalone exeをRust/C#から`Command::new(path).args(...)`で起動する設計は可能です。外部動画そのもののDLは、第三者サイト側の仕様・ffmpeg要否・利用規約・ログイン条件が別問題のため今回実行していません。

本番では、yt-dlpの暗黙self-updateをDL失敗時に自動実行するより、アプリ配布物にバージョン固定したexeを同梱し、更新はアプリの更新経路で管理する方が再現性と監査性に優れます。yt-dlpがffmpegを必要とする形式をmergeする場合、yt-dlpをstandalone化してもffmpeg依存は別途残ります。

## Download portability

現行Python実装のdownload処理は、Rustでも標準HTTP/file I/Oで再現可能です。

```text
filesq URL
  ↓
既存 .part / .part.meta と file_id を照合
  ↓
末尾 65,536 bytes rewind
  ↓
Range bytes=N-
  ↓
206 Content-Range / 200 fallback / 416 discard
  ↓
Content-Length・全体size・ETag検証
  ↓
stream write
  ↓
os.replace相当のatomic rename
```

静的にはeasy〜moderateです。ただし今回のRust PoCが実装したのは64KiBのRange通信確認であり、次の本番機能は未移植です。

- `.part.meta`を使う長時間resume
- 読み書き中断後の再開と末尾rewind
- 6回までのCDN再取得
- 403/404/410/5xxをauth/CDN/hard errorに分類する完全な状態機械
- 同時ダウンロード、CancellationTokenと子プロセスkill

従って「Rustで不可能」ではありませんが、このPoCだけで本番downloadの完全互換を証明したことにはなりません。

## Python vs Rust results

| Test | Python | Rust | Result |
|---|---|---|---|
| JWT payload / SHA-1 fixture | 合成fixture一致 | 合成fixture一致 | PASS |
| Cloudflare homepage probe | 200、challengeなし | 200、challengeなし | PASS |
| Public API `/videos` | 200 JSON | 200 JSON | PASS |
| Public API `/search` | 200 JSON（helper入口はlogin guard） | 200 JSON | PASS / guard差 |
| Public API `/user` | 401 JSON | 401 JSON | PASS |
| Public get-video | 成功 | 成功 | PASS |
| Public filesq | helper内部呼出で成功 | 成功 | PASS |
| get-url Source | helper CLIはtoken guard、内部処理はSource成功 | Source成功 | PASS / guard差 |
| get-url 360 | internal priorityと整合 | 360成功 | PASS |
| CDN Range | 206、64KiB、Content-Range、ETag | 206、64KiB、Content-Range、ETag | PASS |
| Login | 認証情報なしで未実行 | 認証情報なしで未実行 | NOT RUN |
| Authenticated verify-token | 実tokenなしで未実行 | 実tokenなしで未実行 | NOT RUN |

## Remaining Python dependencies

Rust完全移行後にPython helper由来として残るものはありません。ただし本体統合前の現checkoutでは、以下がまだ現行依存です。

- `cloudscraper`：`iwara_helper.py`のHTTP session。
- Python runtime：C#が`PythonPath`を起動するために必要。
- pip：`iwara_setup.bat`のcloudscraper/yt-dlpインストールに必要。
- Python `yt-dlp` module：standalone exeへ切り替えるまで必要。
- Python `subprocess`：helperからyt-dlpを起動するために必要。

標準ライブラリのJSON、base64、hashlib、urllib、file I/O、regex、timeはRustで置換できるため、Python runtime削除時には同時に消せます。

## C# integration options（推奨案のみ、未実装）

| 案 | 実装難易度 | 配布 | エラー処理 | 非同期/cancellation | debugging | panic対策 | C#変更量 |
|---|---|---|---|---|---|---|---|
| A. Rust DLL + P/Invoke | high | DLL/VC runtime/arch管理 | ABI境界を設計 | FFI callbackとhandle設計が必要 | native混在で難しい | `catch_unwind`必須 | large |
| B. Rust CLI + JSON stdin/stdout | low〜moderate | exe同梱だけ | JSON envelopeで明確 | 子プロセスkill、JSON Linesで対応 | exe単体を再現実行しやすい | panicをJSON化しexit code管理 | small〜moderate |
| C. Rust local helper service | moderate〜high | exe + service lifecycle | HTTP/RPCエラー | request cancel設計が必要 | serviceログ・port管理が必要 | process監視が必要 | moderate |

現行C#がすでにPython CLIとJSON stdout/stderrを使っているため、最初の正式移行では **案B** が最もリスクが低いです。action名とJSON schemaを維持し、`PythonPath`をRust exe pathへ置き換える移行が現実的です。長時間DLのプロセス起動コストや同時処理が問題になった場合だけ、JSON Lines常駐化またはlocal serviceを再評価します。

## Risks

1. **認証未検証**: 有効なlogin/tokenを使ったlogin、`/user`、private/friend-only video、site切替を今回実行していない。
2. **Cloudflare条件依存**: 現在の公開APIではchallengeなしだが、challengeが必要な将来・条件をreqwestだけで処理できる保証はない。
3. **secret bundle依存**: main.jsのbundle形や文字列が変わるとregexが壊れる。cache TTL、fallback、警告が必要。
4. **API入口条件の差**: 現行Python helperはsearch/user-videos/get-urlをlogin必須にしている一方、観測時の公開APIは匿名200だった。正式移行時に現行C#契約を維持するか決める必要がある。
5. **download完全互換未検証**: 64KiB Rangeは通ったが、本番のresume、ETag変更、416、CDN切替、cancel後再開はRustでまだ実装・比較していない。
6. **yt-dlp/ffmpeg**: standalone yt-dlpでPython削除は可能だが、外部サイトごとのextractorとffmpegは別依存である。
7. **rate limit挙動**: Pythonの429/判定済み403 backoffは静的に移植可能だが、実429を発生させる負荷試験はサイトに不要な負荷をかけるため実施していない。

## Recommended production architecture

現段階での推奨は次の分離です。

1. Rustをstandalone CLIとして正式化し、現行Python action/JSON schemaを互換維持する。
2. HTTPはまずreqwest + rustls + cookie storeで実装する。通常APIではcloudscraper相当のchallenge solverを持ち込まない。
3. tokenは引き続きコマンドラインへ出さず、C#→子プロセス環境変数または標準入力の安全な制御メッセージで渡す。stdoutにはtoken/cookie/signed URL/secretを出さない。
4. secretはcache→main.js再取得の順序を維持し、抽出失敗を明示的なエラーにする。実secretはfixture・ログ・レポートに保存しない。
5. downloaderは本実装時に`.part.meta`、ETag、Content-Range、rewind、CDN retry、cancellationをRust unit/integration testで個別に検証する。
6. yt-dlpはstandalone exeを固定版で同梱し、Rust `Command`で引数を配列として渡す。外部exe更新をDL失敗時に暗黙実行しない。
7. Cloudflare challengeが実際に発生したendpointだけを対象に、正規ブラウザ/WebView2 session利用の可否を別設計する。challenge突破コードは作らない。

## Final decision

### CONDITIONAL GO

基本的なPython helper機能はRustへ置換可能です。今回の実測では、通常のreqwestでCloudflare challengeなしに公開API、video metadata、filesq、画質選択、CDN Rangeが動作し、Python cloudscraper固有の突破処理は確認できませんでした。SHA-1/X-Version fixtureもPythonとRustで一致し、yt-dlp standalone exeもPython runtimeなしで起動できました。

ただし、次の理由で現時点の判定はGOではありません。

- 実アカウントのlogin/token verifyを実行していない。
- private/friend-only、429/403、iwara.aiのsite差、Cloudflare challenge発生条件を未検証。
- Rust PoCは本番の`.part.meta` resume/CDN retry/cancellationまで実装していない。
- Python helperのlogin guardと、現行APIの匿名公開挙動に入口条件の差がある。

したがって、正式移行を開始する条件は「安全なテスト用tokenでlogin/verify/get-video/search/user-videos/get-urlをPython/Rust同条件で比較」「full download state machineをfixture serverで検証」「private/403/429/site差を正規セッション範囲で確認」です。これらを満たし、challengeが発生しない、またはWebView2等の正規session設計が受け入れられるなら、Python runtime、pip、cloudscraper、Python subprocess依存を段階的に削除できます。

## Follow-up: Rust migration completed in the application (2026-08-28)

The production integration described above was completed after the initial PoC review.

- Restore point created first: commit `d3a43f7` (`chore: checkpoint before Rust migration`).
- The Rust CLI in `tools/iwara-rust-poc` is now packaged as `IwaraDownloader/iwara-helper.exe`.
- `IwaraApiService` now invokes the helper directly with `ProcessStartInfo.ArgumentList`; the token is passed through `IWARA_TOKEN`, and login credentials are passed through child-process environment variables rather than command-line arguments.
- The Rust helper implements the production actions used by the application: login, token verification, public video metadata, search, user videos, URL resolution, full resumable download, and standalone yt-dlp execution. It includes `.part`/`.part.meta`, ETag and Content-Range checks, 64 KiB rewind, CDN retry, atomic finalization, cancellation-compatible process ownership, and bounded API/CDN backoff.
- Python helper/setup files were removed from the application project. No Python runtime, pip, or cloudscraper is required by the application anymore. The old `PythonPath` setting is retained only so older settings JSON can still be deserialized; it is not read or exported by the new runtime.
- The setup wizard and localized settings strings now refer to the Rust helper and standalone yt-dlp. The project copies `iwara-helper.exe` to the application output directory during build.

Validation performed after integration:

- Rust GNU release build: PASS; the helper imports only Windows system DLLs.
- Rust unit tests: 6 passed.
- .NET solution Release build: PASS, 0 warnings, 0 errors.
- .NET tests: 13 passed.
- Bundled output hash matched the source helper binary.
- Live public `get-video`: PASS.
- Live authenticated-path smoke using a synthetic non-secret token: `get-url` resolved Source and `download-test-video` received HTTP 206, 65,536 bytes, `Content-Range: bytes 0-65535/262104033`, with an ETag.

Real account login/token verification, private-content download, and a full 262 MB download were intentionally not run because no credentials were supplied and a large file transfer was unnecessary for this migration check. The migration commit should therefore be treated as production code with those environment-specific checks still pending.
