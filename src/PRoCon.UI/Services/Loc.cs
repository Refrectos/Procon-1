using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace PRoCon.UI.Services
{
    /// <summary>
    /// JSON-based localization for the UI layer.
    /// Loads language files from embedded resources (*.json in PRoCon.Core.Resources.Localization).
    /// Call Loc.Initialize() at startup, then use Loc.T("key") throughout the UI.
    /// </summary>
    public static class Loc
    {
        private static volatile Dictionary<string, string> _strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, LanguageInfo> _languages = new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);
        private static string _currentLanguageCode = "au";

        public class LanguageInfo
        {
            public string Code { get; set; }
            public string DisplayName { get; set; }
            public string Author { get; set; }
            public string CountryCode { get; set; }
        }

        /// <summary>
        /// Initialize localization by loading all available JSON language files from embedded resources.
        /// </summary>
        public static void Initialize()
        {
            var assembly = typeof(PRoCon.Core.CMap).Assembly; // PRoCon.Core assembly
            string prefix = "PRoCon.Core.Resources.Localization.";

            foreach (string resourceName in assembly.GetManifestResourceNames())
            {
                if (!resourceName.StartsWith(prefix) || !resourceName.EndsWith(".json"))
                    continue;

                string code = resourceName.Substring(prefix.Length).Replace(".json", "");

                try
                {
                    using (var stream = assembly.GetManifestResourceStream(resourceName))
                    using (var reader = new StreamReader(stream))
                    {
                        var json = JObject.Parse(reader.ReadToEnd());
                        var meta = json["_meta"] as JObject;
                        _languages[code] = new LanguageInfo
                        {
                            Code = code,
                            DisplayName = meta?["Language"]?.ToString() ?? code,
                            Author = meta?["author"]?.ToString() ?? "",
                            CountryCode = meta?["countrycode"]?.ToString() ?? code,
                        };
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Localization: failed to load {resourceName}: {ex.Message}");
                }
            }

            // Load default language
            SetLanguage("au");
        }

        /// <summary>
        /// Switch to a different language by code (e.g., "au", "de", "es").
        /// </summary>
        public static void SetLanguage(string code)
        {
            var assembly = typeof(PRoCon.Core.CMap).Assembly;
            string resourceName = $"PRoCon.Core.Resources.Localization.{code}.json";

            try
            {
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) return;
                    using (var reader = new StreamReader(stream))
                    {
                        var json = JObject.Parse(reader.ReadToEnd());
                        var strings = json["strings"] as JObject;
                        if (strings != null)
                        {
                            _strings = strings.Properties()
                                .ToDictionary(p => p.Name, p => p.Value.ToString(), StringComparer.OrdinalIgnoreCase);
                            _currentLanguageCode = code;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Localization: failed to switch to '{code}': {ex.Message}");
            }
        }

        /// <summary>
        /// Get all available languages.
        /// </summary>
        public static IReadOnlyDictionary<string, LanguageInfo> Languages => _languages;

        /// <summary>
        /// Current language code.
        /// </summary>
        public static string CurrentCode => _currentLanguageCode;

        /// <summary>
        /// Get a localized string by key. Returns defaultText if not found.
        /// </summary>
        public static string T(string key, string defaultText = null)
        {
            if (string.IsNullOrEmpty(key))
                return defaultText ?? key ?? "";

            if (_strings.TryGetValue(key, out var value))
                return value;

            return defaultText ?? key;
        }

        /// <summary>
        /// Get a localized string with format arguments.
        /// </summary>
        public static string TF(string key, string defaultText, params object[] args)
        {
            string template = T(key, defaultText);
            try
            {
                return string.Format(template, args);
            }
            catch
            {
                return template;
            }
        }
    }
}
