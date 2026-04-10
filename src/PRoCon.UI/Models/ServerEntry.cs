using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using PRoCon.Core;
using PRoCon.Core.Logging;
using PRoCon.UI.Services;
using PRoCon.UI.Views;

namespace PRoCon.UI.Models
{
    public enum ServerConnectionState
    {
        Disconnected,
        Connecting,
        Connected
    }

    public class ConsoleLine
    {
        public string Text { get; set; }
        public string RawText { get; set; }
        public Avalonia.Media.IBrush ColorBrush { get; set; }
        public Avalonia.Media.FontWeight Weight { get; set; } = Avalonia.Media.FontWeight.Normal;
    }

    public class ServerEntry : INotifyPropertyChanged
    {
        public string HostPort { get; set; }
        public bool IsLayerConnection { get; set; }
        public string LayerUsername { get; set; }

        private string _serverName;
        public string ServerName
        {
            get => _serverName;
            set { _serverName = value; Notify(nameof(ServerName)); Notify(nameof(DisplayLabel)); Notify(nameof(DisplayName)); Notify(nameof(IsNameOverflow)); Notify(nameof(Initials)); Notify(nameof(TooltipLine1)); }
        }

        private string _gameType;
        public string GameType
        {
            get => _gameType;
            set { _gameType = value; Notify(nameof(GameType)); Notify(nameof(HasGameType)); Notify(nameof(DisplayLabel)); Notify(nameof(GameTypeLabel)); Notify(nameof(GameHeaderText)); }
        }

        public bool HasGameType => !string.IsNullOrEmpty(GameType);
        public string GameTypeLabel => !string.IsNullOrEmpty(GameType) ? $"[{GameType}]" : "";

        private ServerConnectionState _state = ServerConnectionState.Disconnected;
        public ServerConnectionState State
        {
            get => _state;
            set { _state = value; Notify(nameof(State)); Notify(nameof(IsConnected)); Notify(nameof(IsPulsing)); Notify(nameof(StatusColor)); Notify(nameof(DisplayName)); Notify(nameof(TooltipLine2)); }
        }

        public bool IsConnected => _state == ServerConnectionState.Connected;
        public bool IsPulsing => _state == ServerConnectionState.Connected || _state == ServerConnectionState.Connecting;

        // Display label: ServerName if available, otherwise HostPort
        public string DisplayLabel
        {
            get
            {
                string name = !string.IsNullOrEmpty(ServerName) ? ServerName : HostPort;
                if (!string.IsNullOrEmpty(GameType))
                    return $"[{GameType}] {name}";
                return name;
            }
        }

        // Status color brush for the indicator dot — resolved from theme resources
        private static Avalonia.Media.ISolidColorBrush ResolveBrush(string key, string fallback)
        {
            try
            {
                var app = Avalonia.Application.Current;
                if (app != null && app.Styles != null)
                {
                    if (app.TryGetResource(key, app.ActualThemeVariant, out var value) &&
                        value is Avalonia.Media.ISolidColorBrush brush)
                        return brush;
                }
            }
            catch { }
            return new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(fallback));
        }

        public Avalonia.Media.ISolidColorBrush StatusColor => _state switch
        {
            ServerConnectionState.Connected => ResolveBrush("ConnectedBrush", "#00ff88"),
            ServerConnectionState.Connecting => ResolveBrush("WarningBrush", "#ffaa00"),
            _ => ResolveBrush("DisconnectedBrush", "#ff3c3c"),
        };

        // Per-server state
        public const int MaxChatLines = 500;
        public ConcurrentQueue<string> ChatLines { get; } = new ConcurrentQueue<string>();
        internal readonly System.Text.StringBuilder ChatText = new System.Text.StringBuilder();
        public ObservableCollection<ConsoleLine> ConsoleLines { get; } = new ObservableCollection<ConsoleLine>();
        public ConsoleFileLogger ConsoleLogger { get; set; }
        public List<string> PlayerItems { get; set; } = new List<string>();
        public HashSet<string> SupportedCommands { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public ConcurrentDictionary<string, string> PlayerIPs { get; } = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        internal bool _pendingAdminHelp;
        public Dictionary<int, List<PlayerDisplayInfo>> TeamPlayers { get; set; } = new Dictionary<int, List<PlayerDisplayInfo>>
        {
            { 1, new List<PlayerDisplayInfo>() },
            { 2, new List<PlayerDisplayInfo>() },
            { 3, new List<PlayerDisplayInfo>() },
            { 4, new List<PlayerDisplayInfo>() }
        };
        public List<PlayerDisplayInfo> Spectators { get; set; } = new List<PlayerDisplayInfo>();
        public List<PlayerDisplayInfo> Commanders { get; set; } = new List<PlayerDisplayInfo>();
        // Fast lookup by soldier name for kill/spawn event updates
        public ConcurrentDictionary<string, PlayerDisplayInfo> PlayerLookup { get; } = new ConcurrentDictionary<string, PlayerDisplayInfo>(StringComparer.OrdinalIgnoreCase);
        public string ServerInfoText { get; set; } = "";

        private CServerInfo _lastServerInfo;
        public CServerInfo LastServerInfo
        {
            get => _lastServerInfo;
            set { _lastServerInfo = value; Notify(nameof(LastServerInfo)); Notify(nameof(TooltipLine2)); }
        }

        // Dashboard data
        public List<(DateTime Time, int Count)> PlayerHistory { get; } = new List<(DateTime, int)>();
        public ObservableCollection<string> KillFeed { get; } = new ObservableCollection<string>();
        public string GameVersion { get; set; } = "";

        // Team display data resolved from game server
        public Dictionary<int, string> TeamNames { get; } = new Dictionary<int, string>
        {
            { 1, "Team 1" }, { 2, "Team 2" }, { 3, "Team 3" }, { 4, "Team 4" }
        };
        public Dictionary<int, int> TeamTickets { get; } = new Dictionary<int, int>
        {
            { 1, 0 }, { 2, 0 }, { 3, 0 }, { 4, 0 }
        };
        public int TargetTickets { get; set; }

        // Player list view mode
        private bool _groupBySquad;
        public bool GroupBySquad
        {
            get => _groupBySquad;
            set { _groupBySquad = value; Notify(nameof(GroupBySquad)); }
        }

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(ServerName))
                    return ServerName;
                return HostPort;
            }
        }

        // Group header support
        private bool _showGameHeader;
        public bool ShowGameHeader
        {
            get => _showGameHeader;
            set { _showGameHeader = value; Notify(nameof(ShowGameHeader)); }
        }

        public string GameHeaderText => !string.IsNullOrEmpty(GameType) ? GameType : "Unknown";

        // Player count for sidebar display
        private int _playerCount;
        public int PlayerCount
        {
            get => _playerCount;
            set { _playerCount = value; Notify(nameof(PlayerCount)); Notify(nameof(PlayerCountText)); Notify(nameof(HasPlayerCount)); Notify(nameof(TooltipLine2)); }
        }

        private int _maxPlayerCount;
        public int MaxPlayerCount
        {
            get => _maxPlayerCount;
            set { _maxPlayerCount = value; Notify(nameof(MaxPlayerCount)); Notify(nameof(PlayerCountText)); Notify(nameof(HasPlayerCount)); Notify(nameof(TooltipLine2)); }
        }

        public string PlayerCountText => $"{PlayerCount}/{MaxPlayerCount}";
        public bool HasPlayerCount => MaxPlayerCount > 0;

        // Marquee for long names (> ~25 chars at 12px font in available sidebar width)
        public bool IsNameOverflow => (DisplayName?.Length ?? 0) > 25;

        // Sidebar icon initials (2-3 chars)
        public string Initials
        {
            get
            {
                string name = !string.IsNullOrEmpty(ServerName) ? ServerName : HostPort;
                if (string.IsNullOrEmpty(name))
                    return "?";

                var words = name.Split(new[] { ' ', '-', '_', '|', '#' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length >= 2)
                    return string.Concat(words.Take(3).Select(w => char.ToUpper(w[0])));

                return name.Length <= 3 ? name.ToUpper() : name.Substring(0, 3).ToUpper();
            }
        }

        // Tooltip properties for sidebar server icon
        public string TooltipLine1 => !string.IsNullOrEmpty(ServerName) ? ServerName : HostPort;

        public string TooltipLine2
        {
            get
            {
                if (!IsConnected)
                    return _state == ServerConnectionState.Connecting ? "Connecting..." : "Disconnected";

                var parts = new List<string>();
                if (MaxPlayerCount > 0)
                    parts.Add($"{PlayerCount}/{MaxPlayerCount}");

                if (LastServerInfo != null)
                {
                    if (!string.IsNullOrEmpty(LastServerInfo.Map))
                        parts.Add(GameData.GetMapName(LastServerInfo.Map));
                    if (!string.IsNullOrEmpty(LastServerInfo.GameMode))
                        parts.Add(GameData.GetModeName(LastServerInfo.GameMode));
                }

                return parts.Count > 0 ? string.Join(" | ", parts) : "Connected";
            }
        }

        public string TooltipLayerInfo => IsLayerConnection ? $"Layer via {LayerUsername}@{HostPort}" : null;
        public bool HasTooltipLayerInfo => !string.IsNullOrEmpty(TooltipLayerInfo);

        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        public override string ToString() => DisplayName;
    }
}
