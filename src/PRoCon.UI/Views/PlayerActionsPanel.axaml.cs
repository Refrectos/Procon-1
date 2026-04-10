using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PRoCon.Core;
using PRoCon.Core.Remote;
using PRoCon.UI.Models;
using PRoCon.UI.Services;

namespace PRoCon.UI.Views
{
    public partial class PlayerActionsPanel : UserControl
    {
        private PRoConClient _client;
        private readonly List<PlayerDisplayInfo> _selectedPlayers = new List<PlayerDisplayInfo>();

        public PlayerActionsPanel()
        {
            InitializeComponent();
            ApplyLocalization();
        }

        private void ApplyLocalization()
        {
            var messageReasonLabel = this.FindControl<TextBlock>("MessageReasonLabel");
            if (messageReasonLabel != null) messageReasonLabel.Text = Loc.T("player.reason", "MESSAGE / REASON");

            var actionsLabel = this.FindControl<TextBlock>("ActionsLabel");
            if (actionsLabel != null) actionsLabel.Text = Loc.T("player.actions", "ACTIONS");

            var sayBtn = this.FindControl<Button>("SayButton");
            if (sayBtn != null) sayBtn.Content = Loc.T("player.say", "SAY ALL");

            var yellBtn = this.FindControl<Button>("YellButton");
            if (yellBtn != null) yellBtn.Content = Loc.T("player.yell", "YELL ALL");

            var killBtn = this.FindControl<Button>("KillButton");
            if (killBtn != null) killBtn.Content = Loc.T("player.kill", "KILL");

            var kickBtn = this.FindControl<Button>("KickButton");
            if (kickBtn != null) kickBtn.Content = Loc.T("player.kick", "KICK");

            var moveLabel = this.FindControl<TextBlock>("MoveToTeamLabel");
            if (moveLabel != null) moveLabel.Text = Loc.T("player.move", "MOVE TO TEAM");

            var banLabel = this.FindControl<TextBlock>("BanLabel");
            if (banLabel != null) banLabel.Text = Loc.T("player.ban", "BAN");

            var banPlayerBtn = this.FindControl<Button>("BanPlayerButton");
            if (banPlayerBtn != null) banPlayerBtn.Content = Loc.T("player.ban.button", "BAN PLAYER");
        }

        public void SetClient(PRoConClient client)
        {
            _client = client;
            _selectedPlayers.Clear();
            ClearPlayerInfo();

            if (_client == null)
            {
                IsEnabled = false;
                return;
            }

            IsEnabled = true;
        }

        public void SetApplication(PRoConApplication app)
        {
            // Not needed for player actions.
        }

        /// <summary>
        /// Called when player selection changes. Accepts the full list of currently selected players.
        /// </summary>
        /// <summary>
        /// Callback to clear selection in the player ListBoxes.
        /// Set by MainWindow.
        /// </summary>
        public Action OnClearSelectionRequested { get; set; }

        public void SetSelectedPlayers(List<PlayerDisplayInfo> players)
        {
            _selectedPlayers.Clear();

            if (players == null || players.Count == 0)
            {
                ClearPlayerInfo();
                return;
            }

            _selectedPlayers.AddRange(players);

            if (_selectedPlayers.Count == 1)
            {
                var p = _selectedPlayers[0];
                SetText("PlayerNameText", p.Name ?? "--");
                SetText("PlayerScoreText", p.ScoreText);
                SetText("PlayerKDText", $"{p.Kills}/{p.Deaths}");
                SetText("PlayerPingText", p.PingText);
                // Color the ping stat value
                var pingStatValue = this.FindControl<TextBlock>("PlayerPingText");
                if (pingStatValue != null)
                {
                    if (p.Ping <= 50)
                        pingStatValue.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#81c784"));
                    else if (p.Ping <= 120)
                        pingStatValue.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#ffd740"));
                    else
                        pingStatValue.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#ef5350"));
                }
                SetText("PlayerSquadText", p.SquadText);
                SetText("PlayerIPText", p.IP ?? "");
                SetText("PlayerCountryText", p.CountryText);

                // Show location row if we have country data
                var locationRow = this.FindControl<StackPanel>("LocationRow");
                if (locationRow != null)
                    locationRow.IsVisible = !string.IsNullOrEmpty(p.CountryCode);

                // Flag image
                var flagImg = this.FindControl<Avalonia.Controls.Image>("PlayerFlagImage");
                if (flagImg != null)
                    flagImg.Source = p.FlagImage;

                // Threat badge
                var threatBadge = this.FindControl<Avalonia.Controls.Border>("ThreatBadge");
                string threat = p.ThreatText;
                SetText("PlayerThreatText", threat);
                if (threatBadge != null)
                    threatBadge.IsVisible = !string.IsNullOrEmpty(threat);
            }
            else
            {
                // Multi-select: show count and abbreviated name list
                string names = string.Join(", ", _selectedPlayers.Select(p => p.Name));
                if (names.Length > 60)
                    names = names.Substring(0, 57) + "...";
                SetText("PlayerNameText", $"{_selectedPlayers.Count} players selected");
                SetText("PlayerScoreText", _selectedPlayers.Sum(p => p.Score).ToString());
                SetText("PlayerKDText", $"{_selectedPlayers.Sum(p => p.Kills)}/{_selectedPlayers.Sum(p => p.Deaths)}");
                SetText("PlayerPingText", "--");
                SetText("PlayerSquadText", "--");
                SetText("PlayerIPText", "");
                SetText("PlayerCountryText", "");
                SetText("PlayerThreatText", "");
                var locationRow = this.FindControl<StackPanel>("LocationRow");
                if (locationRow != null) locationRow.IsVisible = false;
                var threatBadge = this.FindControl<Avalonia.Controls.Border>("ThreatBadge");
                if (threatBadge != null) threatBadge.IsVisible = false;
                SetStatus(names);
            }
        }

        private void ClearPlayerInfo()
        {
            SetText("PlayerNameText", "No player selected");
            SetText("PlayerScoreText", "--");
            SetText("PlayerKDText", "--");
            SetText("PlayerPingText", "--");
            SetText("PlayerSquadText", "--");
            SetText("PlayerIPText", "");
            SetText("PlayerCountryText", "");
            SetText("PlayerThreatText", "");
            SetText("ActionStatusText", "");
            var locationRow = this.FindControl<StackPanel>("LocationRow");
            if (locationRow != null) locationRow.IsVisible = false;
            var threatBadge = this.FindControl<Avalonia.Controls.Border>("ThreatBadge");
            if (threatBadge != null) threatBadge.IsVisible = false;
        }

        private void OnClearSelection(object sender, RoutedEventArgs e)
        {
            _selectedPlayers.Clear();
            ClearPlayerInfo();
            OnClearSelectionRequested?.Invoke();
        }

        // --- Actions ---

        private void OnKillPlayer(object sender, RoutedEventArgs e)
        {
            if (!ValidateSelection()) return;

            foreach (var player in _selectedPlayers)
                _client.SendRequest(new List<string> { "admin.killPlayer", player.Name });

            SetStatus($"Kill sent: {FormatNames()}");
        }

        private void OnKickPlayer(object sender, RoutedEventArgs e)
        {
            if (!ValidateSelection()) return;

            string reason = GetMessage();
            if (string.IsNullOrWhiteSpace(reason))
                reason = "Kicked by admin";

            foreach (var player in _selectedPlayers)
                _client.SendRequest(new List<string> { "admin.kickPlayer", player.Name, reason });

            SetStatus($"Kicked: {FormatNames()}");
        }

        private void OnSayPlayer(object sender, RoutedEventArgs e)
        {
            if (!ValidateClient()) return;

            string message = GetMessage();
            if (string.IsNullOrWhiteSpace(message))
            {
                SetStatus("Enter a message first.");
                return;
            }

            _client.SendRequest(new List<string> { "admin.say", message, "all" });
            SetStatus($"Said: {message}");
        }

        private void OnYellPlayer(object sender, RoutedEventArgs e)
        {
            if (!ValidateClient()) return;

            string message = GetMessage();
            if (string.IsNullOrWhiteSpace(message))
            {
                SetStatus("Enter a message first.");
                return;
            }

            _client.SendRequest(new List<string> { "admin.yell", message, "10", "all" });
            SetStatus($"Yelled: {message}");
        }

        private void OnMoveTeam1(object sender, RoutedEventArgs e) => MoveToTeam(1);
        private void OnMoveTeam2(object sender, RoutedEventArgs e) => MoveToTeam(2);
        private void OnMoveTeam3(object sender, RoutedEventArgs e) => MoveToTeam(3);
        private void OnMoveTeam4(object sender, RoutedEventArgs e) => MoveToTeam(4);

        private void MoveToTeam(int teamId)
        {
            if (!ValidateSelection()) return;

            if (!int.TryParse(GetText("MoveSquadIdInput"), out int squadId))
                squadId = 0;

            foreach (var player in _selectedPlayers)
                _client.Game?.SendAdminMovePlayerPacket(player.Name, teamId, squadId, true);

            SetStatus($"Moved {FormatNames()} to Team {teamId}, Squad {squadId}");
        }

        private void OnBanPlayer(object sender, RoutedEventArgs e)
        {
            if (!ValidateSelection()) return;

            string reason = GetMessage();
            if (string.IsNullOrWhiteSpace(reason))
                reason = "Banned by admin";

            var banPerm = this.FindControl<RadioButton>("BanPermanentRadio");
            var banTemp = this.FindControl<RadioButton>("BanTemporaryRadio");
            var banRound = this.FindControl<RadioButton>("BanRoundRadio");

            foreach (var player in _selectedPlayers)
            {
                var words = new List<string> { "banList.add", "name", player.Name };

                if (banPerm?.IsChecked == true)
                {
                    words.Add("perm");
                }
                else if (banRound?.IsChecked == true)
                {
                    words.Add("rounds");
                    words.Add("1");
                }
                else if (banTemp?.IsChecked == true)
                {
                    if (!int.TryParse(GetText("BanDurationInput"), out int minutes))
                        minutes = 60;

                    words.Add("seconds");
                    words.Add((minutes * 60).ToString(CultureInfo.InvariantCulture));
                }

                words.Add(reason);
                _client.SendRequest(words);
            }

            _client.Game?.SendBanListSavePacket();

            SetStatus($"Banned: {FormatNames()}");
        }

        // --- Helpers ---

        private bool ValidateClient()
        {
            if (_client == null)
            {
                SetStatus("No connection.");
                return false;
            }
            return true;
        }

        private bool ValidateSelection()
        {
            if (_client == null || _selectedPlayers.Count == 0)
            {
                SetStatus("No player selected or no connection.");
                return false;
            }
            return true;
        }

        private string FormatNames()
        {
            if (_selectedPlayers.Count == 1)
                return _selectedPlayers[0].Name;
            return $"{_selectedPlayers.Count} players ({string.Join(", ", _selectedPlayers.Select(p => p.Name))})";
        }

        private string GetMessage()
        {
            var tb = this.FindControl<TextBox>("MessageInput");
            return tb?.Text ?? "";
        }

        private void SetText(string controlName, string value)
        {
            var tb = this.FindControl<TextBlock>(controlName);
            if (tb != null) tb.Text = value;
        }

        private string GetText(string controlName)
        {
            var tb = this.FindControl<TextBox>(controlName);
            return tb?.Text ?? "";
        }

        private void SetStatus(string message)
        {
            var status = this.FindControl<TextBlock>("ActionStatusText");
            if (status != null) status.Text = message;
        }
    }
}
