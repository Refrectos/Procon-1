using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace PRoCon.Core.Network
{
    /// <summary>
    /// Downloads and caches small country flag PNG images from flagcdn.com.
    /// Flags are cached permanently in Cache/Flags/{code}.png.
    /// </summary>
    public class FlagImageCache
    {
        private const string FlagCdnUrl = "https://flagcdn.com/24x18/{0}.png";
        private readonly string _cacheDir;
        private readonly HttpClient _http;
        private readonly ConcurrentDictionary<string, string> _pathCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, bool> _downloading = new(StringComparer.OrdinalIgnoreCase);

        public FlagImageCache(string cacheBaseDir)
        {
            _cacheDir = Path.Combine(cacheBaseDir, "Flags");
            Directory.CreateDirectory(_cacheDir);

            _http = new HttpClient();
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("PRoCon/2.0");
            _http.Timeout = TimeSpan.FromSeconds(10);
        }

        /// <summary>
        /// Gets the cached file path for a country flag, or null if not yet cached.
        /// Triggers a background download if not cached.
        /// </summary>
        public string GetFlagPath(string countryCode)
        {
            if (string.IsNullOrEmpty(countryCode) || countryCode.Length != 2)
                return null;

            string code = countryCode.ToLowerInvariant();
            string filePath = Path.Combine(_cacheDir, $"{code}.png");

            // Already cached on disk?
            if (_pathCache.TryGetValue(code, out string cached))
                return cached;

            if (File.Exists(filePath))
            {
                _pathCache[code] = filePath;
                return filePath;
            }

            // Not cached — trigger background download
            if (_downloading.TryAdd(code, true))
            {
                _ = DownloadFlagAsync(code, filePath);
            }

            return null;
        }

        private async Task DownloadFlagAsync(string code, string filePath)
        {
            try
            {
                string url = string.Format(FlagCdnUrl, code);
                byte[] data = await _http.GetByteArrayAsync(url).ConfigureAwait(false);

                if (data.Length > 0)
                {
                    await File.WriteAllBytesAsync(filePath, data).ConfigureAwait(false);
                    _pathCache[code] = filePath;
                }
            }
            catch
            {
                // Flag download failure is non-critical
            }
            finally
            {
                _downloading.TryRemove(code, out _);
            }
        }
    }
}
