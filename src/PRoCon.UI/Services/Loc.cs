using System.Linq;
using PRoCon.Core;
using PRoCon.Core.Localization;

namespace PRoCon.UI.Services
{
    /// <summary>
    /// Static localization accessor for the UI layer.
    /// Call Loc.Initialize() at startup with the PRoConApplication instance.
    /// Use Loc.T("key") or Loc.T("key", "default") throughout the UI.
    /// </summary>
    public static class Loc
    {
        private static PRoConApplication _app;

        public static void Initialize(PRoConApplication app)
        {
            _app = app;
        }

        /// <summary>
        /// Get a localized string by key. Returns the key itself if not found.
        /// </summary>
        public static string T(string key)
        {
            if (_app?.CurrentLanguage == null || string.IsNullOrEmpty(key))
                return key;

            return _app.CurrentLanguage.GetDefaultLocalized(key, key);
        }

        /// <summary>
        /// Get a localized string by key with a fallback default.
        /// </summary>
        public static string T(string key, string defaultText)
        {
            if (_app?.CurrentLanguage == null || string.IsNullOrEmpty(key))
                return defaultText;

            return _app.CurrentLanguage.GetDefaultLocalized(defaultText, key);
        }

        /// <summary>
        /// Get a localized string with format arguments.
        /// </summary>
        public static string T(string key, string defaultText, params object[] args)
        {
            if (_app?.CurrentLanguage == null || string.IsNullOrEmpty(key))
                return string.Format(defaultText, args);

            return _app.CurrentLanguage.GetDefaultLocalized(defaultText, key, args.Select(a => a?.ToString() ?? "").ToArray());
        }

        /// <summary>
        /// Current language code (e.g., "au", "de", "es").
        /// </summary>
        public static string CurrentCode => _app?.CurrentLanguage?.FileName?.Replace(".loc", "") ?? "au";
    }
}
