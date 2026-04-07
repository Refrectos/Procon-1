using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using PRoCon.Core;
using PRoCon.Core.Players;
using PRoCon.Core.Remote;
using Terminal.Gui;
using TgAttribute = Terminal.Gui.Attribute;

namespace PRoCon.Console
{
    public class TuiConsole
    {
        private readonly PRoConApplication _application;
        private readonly ManualResetEvent _exitEvent;

        // Active server
        private PRoConClient _activeClient;
        private readonly List<PRoConClient> _servers = new List<PRoConClient>();
        private readonly HashSet<string> _wiredGames = new HashSet<string>();

        // Layout views
        private Window _mainWindow;
        private Label _serverTabBar;
        private FrameView _playerFrame;
        private ListView _playerListView;
        private FrameView _serverInfoFrame;
        private Label _serverInfoLabel;
        private FrameView _killFeedFrame;
        private ListView _killFeedListView;
        private FrameView _chatFrame;
        private ListView _chatListView;
        private TextField _inputField;
        private Label _fKeyBar;

        // Dashboard
        private bool _dashboardVisible;
        private FrameView _dashboardView;
        private ListView _dashboardListView;
        private View _mainContent;

        // Data (ObservableCollection required by ListView.SetSource<T>)
        private ObservableCollection<string> _playerDisplayList = new ObservableCollection<string>();
        private readonly List<CPlayerInfo> _playerInfoList = new List<CPlayerInfo>();
        private ObservableCollection<string> _chatBuffer = new ObservableCollection<string>();
        private ObservableCollection<string> _killFeedBuffer = new ObservableCollection<string>();
        private ObservableCollection<string> _dashboardItems = new ObservableCollection<string>();

        private const int MaxChatLines = 500;
        private const int MaxKillFeedLines = 100;

        public TuiConsole(PRoConApplication application, ManualResetEvent exitEvent)
        {
            _application = application;
            _exitEvent = exitEvent;
        }

        public void Start()
        {
            Application.Init();

            _mainWindow = new Window
            {
                Title = "PRoCon TUI Admin",
                BorderStyle = LineStyle.Single
            };

            BuildLayout();
            WireKeyHandlers();
            WireConnections();

            Application.Run(_mainWindow);
            _mainWindow.Dispose();
            Application.Shutdown();
        }

        // ── Layout ──────────────────────────────────────────────────────

        private void BuildLayout()
        {
            // Server tab bar (top row)
            _serverTabBar = new Label
            {
                Text = " No servers ",
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = 1,
                ColorScheme = new ColorScheme
                {
                    Normal = new TgAttribute(Color.White, Color.Blue)
                }
            };

            // Main content area
            _mainContent = new View
            {
                X = 0,
                Y = 1,
                Width = Dim.Fill(),
                Height = Dim.Fill(2) // leave room for input + F-key bar
            };

            BuildMainContent();

            // Input bar
            _inputField = new TextField
            {
                X = 0,
                Y = Pos.Bottom(_mainContent),
                Width = Dim.Fill(),
                Height = 1,
                Text = ""
            };

            _inputField.Accepting += (s, e) =>
            {
                string text = _inputField.Text?.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(text))
                {
                    ProcessInputCommand(text);
                    _inputField.Text = "";
                }
            };

            // F-key bar (bottom row)
            _fKeyBar = new Label
            {
                Text = " F1:Kill  F2:Kick  F3:Ban  F4:Say  F5:Yell  F6:Raw  F9:Dashboard  F10:Quit ",
                X = 0,
                Y = Pos.Bottom(_inputField),
                Width = Dim.Fill(),
                Height = 1,
                ColorScheme = new ColorScheme
                {
                    Normal = new TgAttribute(Color.White, Color.Blue)
                }
            };

            // Build dashboard (initially hidden)
            BuildDashboardView();

            _mainWindow.Add(_serverTabBar, _mainContent, _inputField, _fKeyBar);
        }

        private void BuildMainContent()
        {
            // Left panel: Player list (~50% width)
            _playerFrame = new FrameView
            {
                Title = "Players",
                X = 0,
                Y = 0,
                Width = Dim.Percent(50),
                Height = Dim.Fill(),
                BorderStyle = LineStyle.Single
            };

            _playerListView = new ListView
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                CanFocus = true
            };
            _playerListView.SetSource(_playerDisplayList);
            _playerFrame.Add(_playerListView);

            // Right side
            var rightPanel = new View
            {
                X = Pos.Right(_playerFrame),
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            // Right-top: Server info (~40% of right height)
            _serverInfoFrame = new FrameView
            {
                Title = "Server Info",
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Percent(40),
                BorderStyle = LineStyle.Single
            };

            _serverInfoLabel = new Label
            {
                Text = "No server selected",
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };
            _serverInfoFrame.Add(_serverInfoLabel);

            // Right-bottom: Kill feed
            _killFeedFrame = new FrameView
            {
                Title = "Kill Feed",
                X = 0,
                Y = Pos.Bottom(_serverInfoFrame),
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                BorderStyle = LineStyle.Single
            };

            _killFeedListView = new ListView
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };
            _killFeedListView.SetSource(_killFeedBuffer);
            _killFeedFrame.Add(_killFeedListView);

            rightPanel.Add(_serverInfoFrame, _killFeedFrame);

            // Bottom: Chat panel (~8 rows)
            _chatFrame = new FrameView
            {
                Title = "Chat",
                X = 0,
                Y = Pos.AnchorEnd(10),
                Width = Dim.Fill(),
                Height = 10,
                BorderStyle = LineStyle.Single
            };

            _chatListView = new ListView
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };
            _chatListView.SetSource(_chatBuffer);
            _chatFrame.Add(_chatListView);

            // Adjust player and right panels to leave room for chat
            _playerFrame.Height = Dim.Fill(10);
            rightPanel.Height = Dim.Fill(10);

            _mainContent.Add(_playerFrame, rightPanel, _chatFrame);
        }

        private void BuildDashboardView()
        {
            _dashboardView = new FrameView
            {
                Title = "Dashboard - Multi-Server Overview (Enter=Select, Esc/F9=Back)",
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                BorderStyle = LineStyle.Single,
                Visible = false
            };

            _dashboardListView = new ListView
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                CanFocus = true
            };
            _dashboardListView.SetSource(_dashboardItems);

            _dashboardListView.Accepting += (s, e) =>
            {
                // Offset by 2 header rows in the dashboard display
                int idx = _dashboardListView.SelectedItem - 2;
                if (idx >= 0 && idx < _servers.Count)
                {
                    SetActiveServer(_servers[idx]);
                    ToggleDashboard();
                }
            };

            _dashboardView.Add(_dashboardListView);
        }

        // ── Key Handlers ────────────────────────────────────────────────

        private void WireKeyHandlers()
        {
            _mainWindow.KeyDown += (s, key) =>
            {
                switch (key.KeyCode)
                {
                    case KeyCode.F1:
                        DoKillPlayer();
                        key.Handled = true;
                        break;
                    case KeyCode.F2:
                        DoKickPlayer();
                        key.Handled = true;
                        break;
                    case KeyCode.F3:
                        DoBanPlayer();
                        key.Handled = true;
                        break;
                    case KeyCode.F4:
                        FocusInputWithPrefix("say ");
                        key.Handled = true;
                        break;
                    case KeyCode.F5:
                        FocusInputWithPrefix("yell ");
                        key.Handled = true;
                        break;
                    case KeyCode.F6:
                        _inputField.Text = "";
                        _inputField.SetFocus();
                        key.Handled = true;
                        break;
                    case KeyCode.F9:
                        ToggleDashboard();
                        key.Handled = true;
                        break;
                    case KeyCode.F10:
                        DoQuit();
                        key.Handled = true;
                        break;
                    case KeyCode.CursorLeft | KeyCode.CtrlMask:
                        CycleServer(-1);
                        key.Handled = true;
                        break;
                    case KeyCode.CursorRight | KeyCode.CtrlMask:
                        CycleServer(1);
                        key.Handled = true;
                        break;
                    case KeyCode.Esc when _dashboardVisible:
                        ToggleDashboard();
                        key.Handled = true;
                        break;
                    case KeyCode.Esc when _inputField.HasFocus:
                        _inputField.Text = "";
                        _playerListView.SetFocus();
                        key.Handled = true;
                        break;
                }
            };
        }

        // ── Connection Wiring ───────────────────────────────────────────

        private void WireConnections()
        {
            _application.Connections.ConnectionAdded += client =>
            {
                Application.Invoke(() =>
                {
                    _servers.Add(client);
                    WireClientEvents(client);
                    if (_activeClient == null)
                        SetActiveServer(client);
                    UpdateServerTabBar();
                });
            };

            _application.Connections.ConnectionRemoved += client =>
            {
                Application.Invoke(() =>
                {
                    _servers.Remove(client);
                    _wiredGames.Remove(client.HostNamePort);
                    if (_activeClient == client)
                        SetActiveServer(_servers.FirstOrDefault());
                    UpdateServerTabBar();
                });
            };

            // Wire existing connections
            foreach (PRoConClient client in _application.Connections)
            {
                _servers.Add(client);
                WireClientEvents(client);
            }

            if (_servers.Count > 0)
                SetActiveServer(_servers[0]);

            UpdateServerTabBar();
        }

        private void WireClientEvents(PRoConClient client)
        {
            client.ConnectSuccess += sender =>
            {
                Application.Invoke(() =>
                {
                    AppendChat($"[{sender.HostNamePort}] Connected");
                    UpdateServerTabBar();
                });
            };

            client.ConnectionClosed += sender =>
            {
                Application.Invoke(() =>
                {
                    AppendChat($"[{sender.HostNamePort}] Disconnected");
                    UpdateServerTabBar();
                });
            };

            client.Login += sender =>
            {
                Application.Invoke(() =>
                {
                    AppendChat($"[{sender.HostNamePort}] Logged in");
                    if (sender.Game != null)
                        WireGameEvents(sender, sender.Game);
                    UpdateServerTabBar();
                });
            };

            client.GameTypeDiscovered += sender =>
            {
                Application.Invoke(() =>
                {
                    if (sender.Game != null)
                        WireGameEvents(sender, sender.Game);
                });
            };
        }

        private void WireGameEvents(PRoConClient client, FrostbiteClient game)
        {
            if (!_wiredGames.Add(client.HostNamePort)) return;

            game.ServerInfo += (sender, info) =>
            {
                Application.Invoke(() =>
                {
                    if (client == _activeClient)
                        UpdateServerInfoPanel();
                    if (_dashboardVisible)
                        UpdateDashboard();
                });
            };

            game.GlobalChat += (sender, playerName, message) =>
            {
                Application.Invoke(() =>
                {
                    if (client == _activeClient)
                        AppendChat($"[ALL] {playerName}: {message}");
                });
            };

            game.TeamChat += (sender, playerName, message, teamId) =>
            {
                Application.Invoke(() =>
                {
                    if (client == _activeClient)
                        AppendChat($"[TEAM] {playerName}: {message}");
                });
            };

            game.SquadChat += (sender, playerName, message, teamId, squadId) =>
            {
                Application.Invoke(() =>
                {
                    if (client == _activeClient)
                        AppendChat($"[SQUAD] {playerName}: {message}");
                });
            };

            game.PlayerKilled += (sender, killer, victim, weapon, headshot, killerPos, victimPos) =>
            {
                Application.Invoke(() =>
                {
                    string hs = headshot ? " [HS]" : "";
                    if (client == _activeClient)
                        AppendKillFeed($"{killer} [{weapon}] {victim}{hs}");
                });
            };

            game.ListPlayers += (sender, players, subset) =>
            {
                Application.Invoke(() =>
                {
                    if (client == _activeClient)
                        RefreshPlayerList(players);
                });
            };

            // Wire PlayerDictionary events for live updates
            if (client.PlayerList != null)
            {
                client.PlayerList.PlayerAdded += p =>
                {
                    Application.Invoke(() =>
                    {
                        if (client == _activeClient)
                            RequestPlayerListRefresh();
                    });
                };

                client.PlayerList.PlayerUpdated += p =>
                {
                    Application.Invoke(() =>
                    {
                        if (client == _activeClient)
                            RequestPlayerListRefresh();
                    });
                };

                client.PlayerList.PlayerRemoved += p =>
                {
                    Application.Invoke(() =>
                    {
                        if (client == _activeClient)
                            RequestPlayerListRefresh();
                    });
                };
            }
        }

        private void RequestPlayerListRefresh()
        {
            if (_activeClient?.Game != null)
                _activeClient.Game.SendAdminListPlayersPacket(new CPlayerSubset(CPlayerSubset.PlayerSubsetType.All));
        }

        // ── Server Management ───────────────────────────────────────────

        private void SetActiveServer(PRoConClient client)
        {
            _activeClient = client;
            UpdateServerTabBar();
            UpdateServerInfoPanel();

            _playerDisplayList.Clear();
            _playerInfoList.Clear();
            _chatBuffer.Clear();
            _killFeedBuffer.Clear();

            _playerListView?.SetSource(_playerDisplayList);
            _chatListView?.SetSource(_chatBuffer);
            _killFeedListView?.SetSource(_killFeedBuffer);

            if (client?.Game != null)
            {
                WireGameEvents(client, client.Game);
                client.Game.SendAdminListPlayersPacket(new CPlayerSubset(CPlayerSubset.PlayerSubsetType.All));
            }

            _mainWindow?.SetNeedsDraw();
        }

        private void CycleServer(int direction)
        {
            if (_servers.Count == 0) return;

            int idx = _activeClient != null ? _servers.IndexOf(_activeClient) : -1;
            idx = ((idx + direction) % _servers.Count + _servers.Count) % _servers.Count;
            SetActiveServer(_servers[idx]);
        }

        private void UpdateServerTabBar()
        {
            if (_serverTabBar == null) return;

            if (_servers.Count == 0)
            {
                _serverTabBar.Text = " No servers ";
                return;
            }

            var parts = new List<string>();
            for (int i = 0; i < _servers.Count; i++)
            {
                var s = _servers[i];
                string name = s.CurrentServerInfo?.ServerName ?? s.HostNamePort;
                if (name.Length > 30) name = name.Substring(0, 27) + "...";
                bool active = s == _activeClient;
                bool connected = s.Game?.IsLoggedIn == true;
                string status = connected ? "+" : "-";
                string prefix = active ? ">> " : "   ";
                parts.Add($"{prefix}[{status}] {name}");
            }

            _serverTabBar.Text = string.Join("  |  ", parts);
        }

        // ── Panel Updates ───────────────────────────────────────────────

        private void UpdateServerInfoPanel()
        {
            if (_serverInfoLabel == null) return;

            if (_activeClient == null)
            {
                _serverInfoLabel.Text = "No server selected";
                _serverInfoFrame.Title = "Server Info";
                return;
            }

            var info = _activeClient.CurrentServerInfo;

            if (info == null)
            {
                _serverInfoLabel.Text = $"Connecting to {_activeClient.HostNamePort}...";
                _serverInfoFrame.Title = "Server Info";
                return;
            }

            _serverInfoFrame.Title = $"Server Info - {info.ServerName ?? _activeClient.HostNamePort}";

            var lines = new List<string>
            {
                $"Map:     {info.Map ?? "?"} ({info.GameMode ?? "?"})",
                $"Players: {info.PlayerCount}/{info.MaxPlayerCount}",
                $"Round:   {(info.CurrentRound + 1)}/{info.TotalRounds}"
            };

            if (info.ServerUptime > 0)
                lines.Add($"Uptime:  {FormatUptime(info.ServerUptime)}");

            if (info.Ranked)
                lines.Add("Ranked:  Yes");

            if (info.TeamScores != null)
            {
                lines.Add("");
                foreach (var ts in info.TeamScores)
                {
                    string teamName = _activeClient.GetLocalizedTeamName(ts.TeamID, info.Map, info.GameMode);
                    if (string.IsNullOrEmpty(teamName)) teamName = $"Team {ts.TeamID}";
                    lines.Add($"  {teamName}: {ts.Score} tickets");
                }
            }

            _serverInfoLabel.Text = string.Join("\n", lines);
        }

        private void RefreshPlayerList(List<CPlayerInfo> players)
        {
            // Preserve selection
            string selectedName = null;
            int selIdx = _playerListView.SelectedItem;
            if (selIdx >= 0 && selIdx < _playerInfoList.Count)
                selectedName = _playerInfoList[selIdx]?.SoldierName;

            _playerInfoList.Clear();
            _playerDisplayList.Clear();

            if (players == null || players.Count == 0)
            {
                _playerDisplayList.Add("  (no players)");
                _playerListView.SetSource(_playerDisplayList);
                _playerFrame.Title = "Players (0)";
                return;
            }

            // Sort by team, squad, name
            var sorted = players
                .OrderBy(p => p.TeamID)
                .ThenBy(p => p.SquadID)
                .ThenBy(p => p.SoldierName)
                .ToList();

            int currentTeam = -1;

            foreach (var p in sorted)
            {
                if (p.TeamID != currentTeam)
                {
                    currentTeam = p.TeamID;
                    string teamName = GetTeamNameWithTickets(currentTeam);
                    _playerDisplayList.Add($"--- {teamName} ---");
                    _playerInfoList.Add(null); // placeholder for header
                }

                string clanTag = string.IsNullOrEmpty(p.ClanTag) ? "" : $"[{p.ClanTag}]";
                string line = $"  {clanTag}{p.SoldierName,-22} S:{p.Score,-5} K:{p.Kills}/{p.Deaths,-4} Sq:{p.SquadID} P:{p.Ping}ms";
                _playerDisplayList.Add(line);
                _playerInfoList.Add(p);
            }

            _playerListView.SetSource(_playerDisplayList);
            _playerFrame.Title = $"Players ({players.Count})";

            // Restore selection
            if (selectedName != null)
            {
                for (int i = 0; i < _playerInfoList.Count; i++)
                {
                    if (_playerInfoList[i]?.SoldierName == selectedName)
                    {
                        _playerListView.SelectedItem = i;
                        break;
                    }
                }
            }
        }

        private string GetTeamNameWithTickets(int teamId)
        {
            var info = _activeClient?.CurrentServerInfo;
            if (info == null) return $"Team {teamId}";

            string name = _activeClient.GetLocalizedTeamName(teamId, info.Map, info.GameMode);
            if (string.IsNullOrEmpty(name)) name = $"Team {teamId}";

            var ts = info.TeamScores?.FirstOrDefault(t => t.TeamID == teamId);
            if (ts != null)
                return $"{name} [{ts.Score} tickets]";

            return name;
        }

        private void AppendChat(string message)
        {
            string ts = DateTime.Now.ToString("HH:mm:ss");
            _chatBuffer.Add($"[{ts}] {message}");

            while (_chatBuffer.Count > MaxChatLines)
                _chatBuffer.RemoveAt(0);

            // Auto-scroll to bottom
            if (_chatBuffer.Count > 0)
                _chatListView.SelectedItem = _chatBuffer.Count - 1;
        }

        private void AppendKillFeed(string message)
        {
            string ts = DateTime.Now.ToString("HH:mm:ss");
            _killFeedBuffer.Add($"[{ts}] {message}");

            while (_killFeedBuffer.Count > MaxKillFeedLines)
                _killFeedBuffer.RemoveAt(0);

            // Auto-scroll to bottom
            if (_killFeedBuffer.Count > 0)
                _killFeedListView.SelectedItem = _killFeedBuffer.Count - 1;
        }

        // ── Input Command Processing ────────────────────────────────────

        private void ProcessInputCommand(string input)
        {
            string[] parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            string cmd = parts[0].ToLowerInvariant();
            string arg = parts.Length > 1 ? parts[1] : "";

            switch (cmd)
            {
                case "say":
                    if (_activeClient != null && !string.IsNullOrWhiteSpace(arg))
                    {
                        _activeClient.SendRequest(new List<string> { "admin.say", arg, "all" });
                        AppendChat($"[ADMIN] {arg}");
                    }
                    break;
                case "yell":
                    if (_activeClient != null && !string.IsNullOrWhiteSpace(arg))
                    {
                        _activeClient.SendRequest(new List<string> { "admin.yell", arg, "10", "all" });
                        AppendChat($"[YELL] {arg}");
                    }
                    break;
                case "raw":
                    if (_activeClient != null && !string.IsNullOrWhiteSpace(arg))
                    {
                        var words = new List<string>(arg.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                        _activeClient.SendRequest(words);
                        AppendChat($"[RAW] {arg}");
                    }
                    break;
                default:
                    // Treat as raw RCON
                    if (_activeClient != null)
                    {
                        var words = new List<string>(input.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                        _activeClient.SendRequest(words);
                        AppendChat($"[RAW] {input}");
                    }
                    break;
            }
        }

        private void FocusInputWithPrefix(string prefix)
        {
            _inputField.Text = prefix;
            _inputField.MoveEnd();
            _inputField.SetFocus();
        }

        // ── F-Key Actions ───────────────────────────────────────────────

        private CPlayerInfo GetSelectedPlayer()
        {
            int idx = _playerListView.SelectedItem;
            if (idx < 0 || idx >= _playerInfoList.Count) return null;
            return _playerInfoList[idx]; // may be null for team headers
        }

        private void DoKillPlayer()
        {
            var player = GetSelectedPlayer();
            if (player == null || _activeClient == null)
            {
                AppendChat("[!] Select a player first");
                return;
            }

            _activeClient.SendRequest(new List<string> { "admin.killPlayer", player.SoldierName });
            AppendChat($"[ADMIN] Killed {player.SoldierName}");
        }

        private void DoKickPlayer()
        {
            var player = GetSelectedPlayer();
            if (player == null || _activeClient == null)
            {
                AppendChat("[!] Select a player first");
                return;
            }

            var dialog = new Dialog
            {
                Title = $"Kick {player.SoldierName}",
                Width = 50,
                Height = 8
            };

            var reasonLabel = new Label
            {
                Text = "Reason:",
                X = 1,
                Y = 1
            };

            var reasonField = new TextField
            {
                X = Pos.Right(reasonLabel) + 1,
                Y = 1,
                Width = Dim.Fill(2),
                Text = "Kicked by admin"
            };

            var kickBtn = new Button { Text = "Kick" };
            kickBtn.Accepting += (s, e) =>
            {
                string reason = reasonField.Text?.ToString() ?? "Kicked by admin";
                _activeClient.SendRequest(new List<string> { "admin.kickPlayer", player.SoldierName, reason });
                AppendChat($"[ADMIN] Kicked {player.SoldierName}: {reason}");
                Application.RequestStop();
            };

            var cancelBtn = new Button { Text = "Cancel" };
            cancelBtn.Accepting += (s, e) =>
            {
                Application.RequestStop();
            };

            dialog.Add(reasonLabel, reasonField);
            dialog.AddButton(kickBtn);
            dialog.AddButton(cancelBtn);
            Application.Run(dialog);
            dialog.Dispose();
        }

        private void DoBanPlayer()
        {
            var player = GetSelectedPlayer();
            if (player == null || _activeClient == null)
            {
                AppendChat("[!] Select a player first");
                return;
            }

            // Step 1: Reason dialog
            var dialog = new Dialog
            {
                Title = $"Ban {player.SoldierName}",
                Width = 50,
                Height = 8
            };

            var reasonLabel = new Label
            {
                Text = "Reason:",
                X = 1,
                Y = 1
            };

            var reasonField = new TextField
            {
                X = Pos.Right(reasonLabel) + 1,
                Y = 1,
                Width = Dim.Fill(2),
                Text = "Banned by admin"
            };

            string capturedReason = null;

            var banBtn = new Button { Text = "Ban Permanently" };
            banBtn.Accepting += (s, e) =>
            {
                capturedReason = reasonField.Text?.ToString() ?? "Banned by admin";
                Application.RequestStop();
            };

            var cancelBtn = new Button { Text = "Cancel" };
            cancelBtn.Accepting += (s, e) =>
            {
                Application.RequestStop();
            };

            dialog.Add(reasonLabel, reasonField);
            dialog.AddButton(banBtn);
            dialog.AddButton(cancelBtn);
            Application.Run(dialog);
            dialog.Dispose();

            if (capturedReason == null) return;

            // Step 2: Confirmation
            int confirm = MessageBox.Query(
                "Confirm Ban",
                $"Permanently ban {player.SoldierName}?\nReason: {capturedReason}",
                "Yes", "No");

            if (confirm != 0) return;

            _activeClient.SendRequest(new List<string> { "banList.add", "name", player.SoldierName, "perm", capturedReason });
            _activeClient.Game?.SendBanListSavePacket();
            AppendChat($"[ADMIN] Banned {player.SoldierName}: {capturedReason}");
        }

        private void DoQuit()
        {
            int result = MessageBox.Query("Quit", "Shut down PRoCon?", "Yes", "No");
            if (result == 0)
            {
                _application.Shutdown();
                _exitEvent.Set();
                Application.RequestStop();
            }
        }

        // ── Dashboard ───────────────────────────────────────────────────

        private void ToggleDashboard()
        {
            _dashboardVisible = !_dashboardVisible;

            if (_dashboardVisible)
            {
                UpdateDashboard();
                _mainContent.Visible = false;
                _dashboardView.Visible = true;
                if (!_mainWindow.Subviews.Contains(_dashboardView))
                {
                    _dashboardView.X = 0;
                    _dashboardView.Y = 1;
                    _dashboardView.Width = Dim.Fill();
                    _dashboardView.Height = Dim.Fill(2);
                    _mainWindow.Add(_dashboardView);
                }
                _dashboardListView.SetFocus();
            }
            else
            {
                _dashboardView.Visible = false;
                _mainContent.Visible = true;
                _inputField.SetFocus();
            }

            _mainWindow.SetNeedsDraw();
        }

        private void UpdateDashboard()
        {
            _dashboardItems.Clear();

            if (_servers.Count == 0)
            {
                _dashboardItems.Add("  No servers configured.");
                return;
            }

            _dashboardItems.Add($"  {"Server",-40} {"Status",-10} {"Players",-10} {"Map",-25} {"Mode",-20}");
            _dashboardItems.Add($"  {new string('-', 40)} {new string('-', 10)} {new string('-', 10)} {new string('-', 25)} {new string('-', 20)}");

            foreach (var client in _servers)
            {
                bool connected = client.Game?.IsLoggedIn == true;
                var info = client.CurrentServerInfo;
                string serverName = info?.ServerName ?? client.HostNamePort;
                if (serverName.Length > 38) serverName = serverName.Substring(0, 35) + "...";
                string status = connected ? "ONLINE" : "OFFLINE";
                string players = info != null ? $"{info.PlayerCount}/{info.MaxPlayerCount}" : "--";
                string map = info?.Map ?? "--";
                if (map.Length > 23) map = map.Substring(0, 20) + "...";
                string mode = info?.GameMode ?? "--";
                string marker = client == _activeClient ? "* " : "  ";

                _dashboardItems.Add($"{marker}{serverName,-40} {status,-10} {players,-10} {map,-25} {mode,-20}");
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private static string FormatUptime(int seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalDays >= 1)
                return $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            return $"{ts.Minutes}m {ts.Seconds}s";
        }
    }
}
