using System.Collections.Concurrent;
using System.Net;

namespace IwaraDownloader.Utils
{
    /// <summary>
    /// メディアサーバー向けのリクエスト検証 (DNS リバインディング / CSRF 対策)。
    /// ASP.NET に依存しない純粋ロジックにしてあるので単体で検証できる。
    /// </summary>
    public static class WebRequestGuard
    {
        /// <summary>
        /// Host ヘッダーが許可されるか。
        /// DNS リバインディングは「攻撃者のドメイン名」を Host に乗せてくるため、
        /// IP アドレスリテラル / localhost / このPCのホスト名 だけを許可すれば防げる。
        /// </summary>
        public static bool IsAllowedHost(string? hostHeader, string? machineName)
        {
            var host = StripPort(hostHeader);
            if (string.IsNullOrEmpty(host)) return false;

            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;

            // IPv6 リテラルは [..] で来る
            var ipCandidate = host.StartsWith('[') && host.EndsWith(']') ? host[1..^1] : host;
            if (IPAddress.TryParse(ipCandidate, out _))
            {
                // "1" や "0x7f.1" のような省略表記も IPAddress は受け付けるが、
                // いずれもドメイン名ではないのでリバインディングには使えない
                return true;
            }

            if (!string.IsNullOrEmpty(machineName))
            {
                if (host.Equals(machineName, StringComparison.OrdinalIgnoreCase)) return true;
                if (host.Equals(machineName + ".local", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>
        /// 状態を変更するリクエスト (POST 等) がクロスサイトから来ていないか。
        /// Origin があれば Host と一致すること、無ければ Sec-Fetch-Site が cross-site/same-site でないこと。
        /// どちらも無い場合はブラウザ以外 (curl 等) からのアクセスとみなして許可する。
        /// </summary>
        public static bool IsSameOriginRequest(string? origin, string? secFetchSite, string? hostHeader)
        {
            if (!string.IsNullOrEmpty(origin))
            {
                if (origin == "null") return false;
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var o)) return false;
                if (string.IsNullOrEmpty(hostHeader)) return false;
                var originHost = o.IsDefaultPort ? o.Host : $"{o.Host}:{o.Port}";
                var reqHost = hostHeader.Trim();
                // Host が既定ポート(80)を明示している場合に合わせる
                if (o.IsDefaultPort && reqHost.EndsWith(":80", StringComparison.Ordinal))
                    reqHost = reqHost[..^3];
                return string.Equals(originHost, reqHost, StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrEmpty(secFetchSite))
            {
                return secFetchSite.Equals("same-origin", StringComparison.OrdinalIgnoreCase)
                    || secFetchSite.Equals("none", StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        private static string StripPort(string? hostHeader)
        {
            if (string.IsNullOrWhiteSpace(hostHeader)) return string.Empty;
            var h = hostHeader.Trim();
            if (h.StartsWith('['))
            {
                var end = h.IndexOf(']');
                return end < 0 ? string.Empty : h[..(end + 1)];
            }
            var colon = h.LastIndexOf(':');
            return colon >= 0 ? h[..colon] : h;
        }
    }

    /// <summary>
    /// ログイン失敗回数による IP 単位のロックアウト (総当たり対策)。
    /// 一定回数失敗するとロックし、以降の失敗ごとにロック時間を倍にする (上限あり)。
    /// </summary>
    public sealed class LoginThrottle
    {
        private readonly int _freeAttempts;
        private readonly TimeSpan _baseLockout;
        private readonly TimeSpan _maxLockout;
        private readonly Func<DateTime> _now;
        private readonly ConcurrentDictionary<string, Entry> _entries = new();

        private sealed class Entry
        {
            public int Failures;
            public DateTime LockedUntil;
            public DateTime LastFailure;
        }

        public LoginThrottle(int freeAttempts = 5, TimeSpan? baseLockout = null, TimeSpan? maxLockout = null, Func<DateTime>? now = null)
        {
            _freeAttempts = freeAttempts;
            _baseLockout = baseLockout ?? TimeSpan.FromSeconds(30);
            _maxLockout = maxLockout ?? TimeSpan.FromMinutes(15);
            _now = now ?? (() => DateTime.UtcNow);
        }

        /// <summary>ロック中なら残り時間を返す</summary>
        public bool IsLockedOut(string key, out TimeSpan retryAfter)
        {
            retryAfter = TimeSpan.Zero;
            if (!_entries.TryGetValue(key, out var e)) return false;
            lock (e)
            {
                var now = _now();
                if (e.LockedUntil > now)
                {
                    retryAfter = e.LockedUntil - now;
                    return true;
                }
                return false;
            }
        }

        public void RegisterFailure(string key)
        {
            var e = _entries.GetOrAdd(key, _ => new Entry());
            lock (e)
            {
                var now = _now();
                // 最後の失敗から十分時間が経っていればカウントをリセット
                if (e.Failures > 0 && now - e.LastFailure > _maxLockout)
                    e.Failures = 0;

                e.Failures++;
                e.LastFailure = now;
                if (e.Failures >= _freeAttempts)
                {
                    var exponent = Math.Min(e.Failures - _freeAttempts, 10);
                    var ticks = Math.Min(_baseLockout.Ticks * (1L << exponent), _maxLockout.Ticks);
                    e.LockedUntil = now + TimeSpan.FromTicks(ticks);
                }
            }

            // 放置エントリの掃除 (大量の IP から叩かれてもメモリが増え続けないように)
            if (_entries.Count > 1024)
            {
                var cutoff = _now() - _maxLockout;
                foreach (var pair in _entries)
                {
                    if (pair.Value.LastFailure < cutoff && pair.Value.LockedUntil < _now())
                        _entries.TryRemove(pair.Key, out _);
                }
            }
        }

        public void RegisterSuccess(string key) => _entries.TryRemove(key, out _);

        public void Clear() => _entries.Clear();
    }
}
