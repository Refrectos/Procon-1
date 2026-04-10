using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PRoCon.Core;
using PRoCon.Core.Remote;
using PRoCon.UI.Services;

namespace PRoCon.UI.Views
{
    public partial class PunkBusterPanel : UserControl
    {
        private PRoConClient _client;
        private readonly List<string> _logEntries = new List<string>();
        private const int MaxLogEntries = 500;

        public PunkBusterPanel()
        {
            InitializeComponent();
            ApplyLocalization();
        }

        private void ApplyLocalization()
        {
            var title = this.FindControl<TextBlock>("PunkBusterTitleText");
            if (title != null) title.Text = Loc.T("punkbuster.title", "PunkBuster");

            var cmdHeader = this.FindControl<TextBlock>("PunkBusterCommandHeader");
            if (cmdHeader != null) cmdHeader.Text = Loc.T("punkbuster.command.header", "Send PunkBuster Command");

            var cmdInput = this.FindControl<TextBox>("CommandInput");
            if (cmdInput != null) cmdInput.Watermark = Loc.T("punkbuster.command.watermark", "pb_sv_command (e.g. pb_sv_plist)");

            var sendBtn = this.FindControl<Button>("BtnSend");
            if (sendBtn != null) sendBtn.Content = Loc.T("punkbuster.command.send", "Send");
        }

        public void SetClient(PRoConClient client)
        {
            if (_client != null)
            {
                _client.PunkbusterPlayerInfo -= OnPunkbusterPlayerInfo;
            }

            _client = client;

            if (_client != null)
            {
                _client.PunkbusterPlayerInfo += OnPunkbusterPlayerInfo;
            }
        }

        private void OnPunkbusterPlayerInfo(PRoConClient sender, CPunkbusterInfo pbInfo)
        {
            Dispatcher.UIThread.Post(() =>
            {
                string entry = string.Format("[{0}] Slot:{1} Name:{2} GUID:{3} IP:{4} Country:{5}",
                    DateTime.Now.ToString("HH:mm:ss"),
                    pbInfo.SlotID,
                    pbInfo.SoldierName,
                    pbInfo.GUID,
                    pbInfo.Ip,
                    pbInfo.PlayerCountry);

                AppendLogEntry(entry);
            });
        }

        private void AppendLogEntry(string entry)
        {
            _logEntries.Add(entry);

            while (_logEntries.Count > MaxLogEntries)
            {
                _logEntries.RemoveAt(0);
            }

            LogListBox.ItemsSource = null;
            LogListBox.ItemsSource = new List<string>(_logEntries);
            LogListBox.ScrollIntoView(_logEntries[_logEntries.Count - 1]);
        }

        private void OnSendCommand(object sender, RoutedEventArgs e)
        {
            if (_client == null) return;

            string command = CommandInput?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(command)) return;

            _client.SendRequest(new List<string> { "punkBuster.pb_sv_command", command });

            Dispatcher.UIThread.Post(() =>
            {
                AppendLogEntry(string.Format("[{0}] > {1}", DateTime.Now.ToString("HH:mm:ss"), command));
            });

            if (CommandInput != null)
                CommandInput.Text = string.Empty;
        }
    }
}
