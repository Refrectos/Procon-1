using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PRoCon.Core;
using PRoCon.Core.Localization;
using PRoCon.Core.Remote;

namespace PRoCon.UI.Views
{
    public partial class OptionsPanel : UserControl
    {
        private PRoConApplication _application;

        /// <summary>
        /// Callback set by MainWindow to trigger an update check.
        /// </summary>
        public Action OnForceUpdateCheck { get; set; }

        /// <summary>
        /// Callback set by MainWindow to open the What's New dialog.
        /// </summary>
        public Action OnOpenWhatsNewDialog { get; set; }

        public OptionsPanel()
        {
            InitializeComponent();
            ApplyLocalization();
        }

        private void ApplyLocalization()
        {
            var generalLabel = this.FindControl<TextBlock>("GeneralSectionLabel");
            if (generalLabel != null) generalLabel.Text = Services.Loc.T("options.general", "General");

            var showTrayCheck = this.FindControl<CheckBox>("ShowTrayIconCheck");
            if (showTrayCheck != null) showTrayCheck.Content = Services.Loc.T("options.showtray", "Show system tray icon");

            var closeToTrayCheck = this.FindControl<CheckBox>("CloseToTrayCheck");
            if (closeToTrayCheck != null) closeToTrayCheck.Content = Services.Loc.T("options.closetotray", "Close to system tray");

            var minimizeToTrayCheck = this.FindControl<CheckBox>("MinimizeToTrayCheck");
            if (minimizeToTrayCheck != null) minimizeToTrayCheck.Content = Services.Loc.T("options.minimizetotray", "Minimize to system tray");

            var animationsCheck = this.FindControl<CheckBox>("EnableAnimationsCheck");
            if (animationsCheck != null) animationsCheck.Content = Services.Loc.T("options.animations", "Enable animations");

            var languageLabel = this.FindControl<TextBlock>("LanguageSectionLabel");
            if (languageLabel != null) languageLabel.Text = Services.Loc.T("options.language", "Language");

            var languageFieldLabel = this.FindControl<TextBlock>("LanguageLabel");
            if (languageFieldLabel != null) languageFieldLabel.Text = Services.Loc.T("options.language.label", "Language:");

            var proxyCheckLabel = this.FindControl<TextBlock>("ProxyCheckSectionLabel");
            if (proxyCheckLabel != null) proxyCheckLabel.Text = Services.Loc.T("options.proxycheck", "IP Checking (ProxyCheck.io v3)");

            var apiKeyLabel = this.FindControl<TextBlock>("ApiKeyLabel");
            if (apiKeyLabel != null) apiKeyLabel.Text = Services.Loc.T("options.proxycheck.apikey", "API Key:");

            var saveApiKeyBtn = this.FindControl<Button>("SaveApiKeyButton");
            if (saveApiKeyBtn != null) saveApiKeyBtn.Content = Services.Loc.T("options.proxycheck.save", "Save API Key");

            var whatsNewLabel = this.FindControl<TextBlock>("WhatsNewSectionLabel");
            if (whatsNewLabel != null) whatsNewLabel.Text = Services.Loc.T("options.updates.whatsnew", "What's New");

            var releaseNotesBtn = this.FindControl<Button>("ViewReleaseNotesButton");
            if (releaseNotesBtn != null) releaseNotesBtn.Content = Services.Loc.T("options.updates.whatsnew", "VIEW RELEASE NOTES");

            var aboutLabel = this.FindControl<TextBlock>("AboutSectionLabel");
            if (aboutLabel != null) aboutLabel.Text = Services.Loc.T("options.about", "About PRoCon");

            var checkUpdateBtn = this.FindControl<Button>("CheckUpdateButton");
            if (checkUpdateBtn != null) checkUpdateBtn.Content = Services.Loc.T("options.updates.check", "CHECK FOR UPDATES");
        }

        public void SetClient(PRoConClient client)
        {
            // Options panel works at the application level, not per-client.
        }

        public void SetApplication(PRoConApplication app)
        {
            _application = app;

            if (_application == null)
            {
                IsEnabled = false;
                return;
            }

            IsEnabled = true;
            LoadCurrentState();
        }

        private void LoadCurrentState()
        {
            if (_application == null) return;

            var options = _application.OptionsSettings;
            if (options != null)
            {
                SetCheck("ShowTrayIconCheck", options.ShowTrayIcon);
                SetCheck("CloseToTrayCheck", options.CloseToTray);
                SetCheck("MinimizeToTrayCheck", options.MinimizeToTray);
            }

            // ProxyCheck API key
            var apiKeyInput = this.FindControl<TextBox>("ProxyCheckApiKeyInput");
            if (apiKeyInput != null && options != null)
                apiKeyInput.Text = options.ProxyCheckApiKey ?? "";

            // Language dropdown — populated from JSON localization system
            var langCombo = this.FindControl<ComboBox>("LanguageComboBox");
            if (langCombo != null)
            {
                var langItems = new List<ComboBoxItem>();
                foreach (var kvp in Services.Loc.Languages.OrderBy(l => l.Value.DisplayName))
                {
                    langItems.Add(new ComboBoxItem { Content = kvp.Value.DisplayName, Tag = kvp.Key });
                }
                langCombo.ItemsSource = langItems;

                // Select current language
                string currentCode = Services.Loc.CurrentCode;
                for (int i = 0; i < langItems.Count; i++)
                {
                    if ((string)langItems[i].Tag == currentCode)
                    {
                        langCombo.SelectedIndex = i;
                        break;
                    }
                }
            }

            // Version from assembly — prefer InformationalVersion (includes pre-release suffix)
            var versionText = this.FindControl<TextBlock>("VersionText");
            if (versionText != null)
            {
                string vStr = Assembly.GetEntryAssembly()?
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion;
                // Strip build metadata (+commit hash) that .NET appends
                if (!string.IsNullOrEmpty(vStr))
                {
                    int plusIdx = vStr.IndexOf('+');
                    if (plusIdx >= 0) vStr = vStr.Substring(0, plusIdx);
                }
                else
                {
                    var ver = Assembly.GetEntryAssembly()?.GetName().Version;
                    vStr = ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "2.0.0";
                }
                versionText.Text = $"PRoCon Frostbite v{vStr}";
            }

            // Runtime info
            var runtimeInfo = this.FindControl<TextBlock>("RuntimeInfoText");
            if (runtimeInfo != null)
            {
                runtimeInfo.Text = $"Runtime: {RuntimeInformation.FrameworkDescription} | " +
                                   $"OS: {RuntimeInformation.OSDescription}";
            }

            // Data directory
            var dataDirText = this.FindControl<TextBlock>("DataDirText");
            if (dataDirText != null)
            {
                dataDirText.Text = $"Data: {ProConPaths.DataDirectory}";
            }

        }

        // --- UI Actions ---

        private void OnShowTrayIconToggle(object sender, RoutedEventArgs e)
        {
            if (_application?.OptionsSettings == null) return;
            _application.OptionsSettings.ShowTrayIcon = (sender as CheckBox)?.IsChecked == true;
        }

        private void OnCloseToTrayToggle(object sender, RoutedEventArgs e)
        {
            if (_application?.OptionsSettings == null) return;
            _application.OptionsSettings.CloseToTray = (sender as CheckBox)?.IsChecked == true;
        }

        private void OnMinimizeToTrayToggle(object sender, RoutedEventArgs e)
        {
            if (_application?.OptionsSettings == null) return;
            _application.OptionsSettings.MinimizeToTray = (sender as CheckBox)?.IsChecked == true;
        }

        private void OnEnableAnimationsToggle(object sender, RoutedEventArgs e)
        {
            bool enabled = (sender as CheckBox)?.IsChecked == true;
            if (Avalonia.Application.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow != null)
            {
                if (enabled)
                    desktop.MainWindow.Classes.Remove("no-animations");
                else
                    desktop.MainWindow.Classes.Add("no-animations");
            }
        }

        private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = this.FindControl<ComboBox>("LanguageComboBox");
            if (combo?.SelectedItem is ComboBoxItem item && item.Tag is string langCode)
            {
                Services.Loc.SetLanguage(langCode);

                // Also update the legacy loc system for backward compat
                string locFileName = langCode + ".loc";
                if (_application?.Languages != null && _application.Languages.Contains(locFileName))
                {
                    _application.CurrentLanguage = _application.Languages[locFileName];
                    _application.SaveMainConfig();
                }

                SetStatus($"Language changed to: {item.Content}");
            }
        }

        // --- Helpers ---

        private void SetCheck(string controlName, bool value)
        {
            var cb = this.FindControl<CheckBox>(controlName);
            if (cb != null) cb.IsChecked = value;
        }

        private void SetStatus(string message)
        {
            var status = this.FindControl<TextBlock>("OptionsStatusText");
            if (status != null) status.Text = message;
        }

        private void OnSaveProxyCheckKey(object sender, RoutedEventArgs e)
        {
            if (_application?.OptionsSettings == null) return;

            var input = this.FindControl<TextBox>("ProxyCheckApiKeyInput");
            var status = this.FindControl<TextBlock>("ProxyCheckStatus");

            string key = input?.Text?.Trim() ?? "";
            _application.OptionsSettings.ProxyCheckApiKey = key;

            if (status != null)
                status.Text = string.IsNullOrEmpty(key)
                    ? "Using free tier (1,000 queries/day). Key cleared."
                    : "API key saved. Using paid tier.";
        }

        private void OnOpenWhatsNew(object sender, RoutedEventArgs e)
        {
            OnOpenWhatsNewDialog?.Invoke();
        }

        private void OnCheckForUpdates(object sender, RoutedEventArgs e)
        {
            var statusText = this.FindControl<TextBlock>("UpdateCheckStatusText");
            if (statusText != null) statusText.Text = "Checking...";

            if (OnForceUpdateCheck != null)
            {
                OnForceUpdateCheck();
                if (statusText != null) statusText.Text = "Check triggered — see banner if update found.";
            }
            else
            {
                if (statusText != null) statusText.Text = "Update checker not available.";
            }
        }
    }
}
