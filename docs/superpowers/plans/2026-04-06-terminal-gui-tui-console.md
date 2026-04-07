# Terminal.Gui TUI Console Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the text-based `InteractiveConsole` with a full Terminal.Gui v2 TUI admin interface featuring multi-panel layout, live event-driven updates, and F-key admin actions.

**Architecture:** Single new file `TuiConsole.cs` built on Terminal.Gui v2's instance-based `IApplication` API. All panels are standard Terminal.Gui views (`ListView`, `TableView`, `Label`, `TextField`) arranged in a fixed multi-panel layout. PRoConClient events drive all UI updates via `App?.Invoke()` for thread safety. `Program.cs` swaps `InteractiveConsole` for `TuiConsole`.

**Tech Stack:** Terminal.Gui 2.x (NuGet), .NET 8.0, PRoCon.Core APIs

**Spec:** `docs/superpowers/specs/2026-04-06-terminal-gui-tui-console-design.md`

---

## File Structure

| File | Action | Responsibility |
|------|--------|---------------|
| `src/PRoCon.Console/TuiConsole.cs` | Create | Main TUI class — layout, panels, event wiring, F-key handlers, dashboard overlay |
| `src/PRoCon.Console/Program.cs` | Modify (lines 97-102) | Replace `InteractiveConsole` with `TuiConsole` |
| `src/PRoCon.Console/PRoCon.Console.csproj` | Modify | Add Terminal.Gui NuGet package |
| `src/PRoCon.Console/InteractiveConsole.cs` | No change | Kept as fallback for `--no-interactive` |

---

### Task 1: Add Terminal.Gui NuGet Package and Verify Build

**Files:**
- Modify: `src/PRoCon.Console/PRoCon.Console.csproj`

- [ ] **Step 1: Add Terminal.Gui package reference**

In `src/PRoCon.Console/PRoCon.Console.csproj`, add to the `<ItemGroup>` with other PackageReferences:

```xml
<PackageReference Include="Terminal.Gui" Version="2.*" />
```

- [ ] **Step 2: Restore and build**

Run:
```bash
dotnet restore src/PRoCon.Console/PRoCon.Console.csproj
dotnet build src/PRoCon.Console/PRoCon.Console.csproj
```
Expected: Build succeeds with no errors.

- [ ] **Step 3: Commit**

```bash
git add src/PRoCon.Console/PRoCon.Console.csproj
git commit -m "chore: add Terminal.Gui v2 NuGet package to PRoCon.Console"
```

---

### Task 2: Create TuiConsole Skeleton with Window and StatusBar

**Files:**
- Create: `src/PRoCon.Console/TuiConsole.cs`

- [ ] **Step 1: Create TuiConsole.cs with Application init, empty Window, StatusBar, and quit handler**

Create `src/PRoCon.Console/TuiConsole.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using PRoCon.Core;
using PRoCon.Core.Players;
using PRoCon.Core.Remote;
using Terminal.Gui;

namespace PRoCon.Console
{
    public class TuiConsole
    {
        private readonly PRoConApplication _application;
        private readonly ManualResetEvent _exitEvent;
        private IApplication _app;

        // Active server tracking
        private PRoConClient _activeClient;
        private readonly List<PRoConClient> _serverList = new();

        // UI panels
        private Window _mainWindow;
        private Label _serverTabBar;
        private ListView _playerListView;
        private Label _serverInfoLabel;
        private ListView _killFeedView;
        private ListView _chatView;
        private TextField _inputBar;
        private Label _fkeyBar;

        // Data buffers
        private List<CPlayerInfo> _playerCache = new();
        private readonly List<string> _playerDisplayLines = new();
        private readonly List<string> _chatBuffer = new();
        private readonly List<string> _killFeedBuffer = new();
        private const int MaxChatLines = 500;
        private const int MaxKillFeedLines = 100;

        // Track wired games to avoid double-wiring
        private readonly HashSet<string> _wiredGames = new();

        // Dashboard state
        private bool _dashboardVisible;
        private View _mainContent;
        private View _dashboardContent;

        public TuiConsole(PRoConApplication application, ManualResetEvent exitEvent)
        {
            _application = application;
            _exitEvent = exitEvent;
        }

        public void Start()
        {
            _app = Application.Create().Init();

            _mainWindow = new Window
            {
                Title = "PRoCon TUI",
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            BuildLayout();
            WireConnections();
            WireKeyHandlers();

            _app.Run(_mainWindow);
            _mainWindow.Dispose();
            _app.Dispose();

            // Signal exit to Program.cs
            _exitEvent.Set();
        }

        private void BuildLayout()
        {
            // Server tab bar (top row)
            _serverTabBar = new Label
            {
                Text = "No servers configured",
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = 1
            };

            // F-key bar (bottom row)
            _fkeyBar = new Label
            {
                Text = " F1:Kill F2:Kick F3:Ban F4:Say F5:Yell F6:Raw  F9:Dashboard  F10:Quit",
                X = 0,
                Y = Pos.AnchorEnd(1),
                Width = Dim.Fill(),
                Height = 1,
                ColorScheme = new ColorScheme
                {
                    Normal = new Attribute(Color.White, Color.Blue)
                }
            };

            // Input bar (above F-key bar)
            _inputBar = new TextField
            {
                X = 0,
                Y = Pos.AnchorEnd(2),
                Width = Dim.Fill(),
                Height = 1
            };

            // Build the main content area (between tab bar and input bar)
            _mainContent = BuildMainContent();

            _mainWindow.Add(_serverTabBar, _mainContent, _inputBar, _fkeyBar);
        }

        private View BuildMainContent()
        {
            var container = new View
            {
                X = 0,
                Y = 1, // below tab bar
                Width = Dim.Fill(),
                Height = Dim.Fill(3) // leave room for input + fkey bar
            };

            // Left panel: Player list (~50% width)
            var playerFrame = new FrameView
            {
                Title = "Players",
                X = 0,
                Y = 0,
                Width = Dim.Percent(50),
                Height = Dim.Fill()
            };
            _playerListView = new ListView
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                Source = new ListWrapper<string>(_playerDisplayLines)
            };
            playerFrame.Add(_playerListView);

            // Right-top panel: Server info (~40% of right height)
            var serverInfoFrame = new FrameView
            {
                Title = "Server Info",
                X = Pos.Percent(50),
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Percent(40)
            };
            _serverInfoLabel = new Label
            {
                Text = "No server selected",
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };
            serverInfoFrame.Add(_serverInfoLabel);

            // Right-bottom panel: Kill feed (~60% of right height)
            var killFeedFrame = new FrameView
            {
                Title = "Kill Feed",
                X = Pos.Percent(50),
                Y = Pos.Bottom(serverInfoFrame),
                Width = Dim.Fill(),
                Height = Dim.Fill(8) // leave room for chat
            };
            _killFeedView = new ListView
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                Source = new ListWrapper<string>(_killFeedBuffer)
            };
            killFeedFrame.Add(_killFeedView);

            // Bottom panel: Chat
            var chatFrame = new FrameView
            {
                Title = "Chat",
                X = 0,
                Y = Pos.AnchorEnd(8),
                Width = Dim.Fill(),
                Height = 8
            };
            _chatView = new ListView
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                Source = new ListWrapper<string>(_chatBuffer)
            };
            chatFrame.Add(_chatView);

            container.Add(playerFrame, serverInfoFrame, killFeedFrame, chatFrame);
            return container;
        }

        private void WireConnections()
        {
            _application.Connections.ConnectionAdded += client =>
            {
                App?.Invoke(() =>
                {
                    _serverList.Add(client);
                    if (_activeClient == null)
                        SetActiveServer(client);
                    UpdateServerTabBar();
                });
            };

            _application.Connections.ConnectionRemoved += client =>
            {
                App?.Invoke(() =>
                {
                    _serverList.Remove(client);
                    if (_activeClient == client)
                        SetActiveServer(_serverList.FirstOrDefault());
                    UpdateServerTabBar();
                });
            };

            // Wire existing connections
            foreach (PRoConClient client in _application.Connections)
            {
                _serverList.Add(client);
                WireClientEvents(client);
            }

            if (_serverList.Count > 0)
                SetActiveServer(_serverList[0]);

            UpdateServerTabBar();
        }

        private void WireClientEvents(PRoConClient client)
        {
            client.ConnectSuccess += sender =>
                App?.Invoke(() => UpdateServerTabBar());

            client.ConnectionClosed += sender =>
                App?.Invoke(() => UpdateServerTabBar());

            client.Login += sender =>
            {
                if (sender.Game != null)
                    WireGameEvents(sender, sender.Game);
            };

            client.GameTypeDiscovered += sender =>
            {
                if (sender.Game != null)
                    WireGameEvents(sender, sender.Game);
            };
        }

        private void WireGameEvents(PRoConClient client, FrostbiteClient game)
        {
            if (!_wiredGames.Add(client.HostNamePort)) return;

            game.ServerInfo += (sender, info) =>
            {
                App?.Invoke(() =>
                {
                    if (client == _activeClient)
                        UpdateServerInfoPanel(info);
                    if (_dashboardVisible)
                        UpdateDashboard();
                });
            };

            game.GlobalChat += (sender, playerName, message) =>
            {
                App?.Invoke(() =>
                {
                    if (client == _activeClient)
                        AppendChat($"[All] {playerName}: {message}");
                });
            };

            game.TeamChat += (sender, playerName, message, teamId) =>
            {
                App?.Invoke(() =>
                {
                    if (client == _activeClient)
                        AppendChat($"[Team{teamId}] {playerName}: {message}");
                });
            };

            game.SquadChat += (sender, playerName, message, teamId, squadId) =>
            {
                App?.Invoke(() =>
                {
                    if (client == _activeClient)
                        AppendChat($"[Squad{teamId}.{squadId}] {playerName}: {message}");
                });
            };

            game.PlayerKilled += (sender, killer, victim, weapon, headshot, kPos, vPos) =>
            {
                App?.Invoke(() =>
                {
                    if (client == _activeClient)
                    {
                        string hs = headshot ? " [HS]" : "";
                        AppendKillFeed($"{killer} [{weapon}] {victim}{hs}");
                    }
                });
            };

            game.ListPlayers += (sender, players, subset) =>
            {
                App?.Invoke(() =>
                {
                    if (client == _activeClient)
                    {
                        _playerCache = players;
                        RefreshPlayerList();
                    }
                });
            };

            // PlayerDictionary events for live updates
            if (client.PlayerList != null)
            {
                client.PlayerList.PlayerAdded += player =>
                {
                    App?.Invoke(() =>
                    {
                        if (client == _activeClient)
                        {
                            if (!_playerCache.Any(p => p.SoldierName == player.SoldierName))
                                _playerCache.Add(player);
                            RefreshPlayerList();
                        }
                    });
                };

                client.PlayerList.PlayerUpdated += player =>
                {
                    App?.Invoke(() =>
                    {
                        if (client == _activeClient)
                        {
                            int idx = _playerCache.FindIndex(p => p.SoldierName == player.SoldierName);
                            if (idx >= 0) _playerCache[idx] = player;
                            else _playerCache.Add(player);
                            RefreshPlayerList();
                        }
                    });
                };

                client.PlayerList.PlayerRemoved += player =>
                {
                    App?.Invoke(() =>
                    {
                        if (client == _activeClient)
                        {
                            _playerCache.RemoveAll(p => p.SoldierName == player.SoldierName);
                            RefreshPlayerList();
                        }
                    });
                };
            }
        }

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
                        _inputBar.SetFocus();
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
                }
            };

            // Ctrl+Left/Right for server switching
            _mainWindow.KeyDown += (s, key) =>
            {
                if (key.KeyCode == (KeyCode.CursorLeft | KeyCode.CtrlMask))
                {
                    CycleServer(-1);
                    key.Handled = true;
                }
                else if (key.KeyCode == (KeyCode.CursorRight | KeyCode.CtrlMask))
                {
                    CycleServer(1);
                    key.Handled = true;
                }
            };

            // Input bar Enter handler
            _inputBar.Accepting += (s, e) =>
            {
                string text = _inputBar.Text?.ToString()?.Trim() ?? "";
                if (!string.IsNullOrEmpty(text))
                {
                    ProcessInputCommand(text);
                    _inputBar.Text = "";
                }
                _playerListView.SetFocus();
            };

            // Esc from input bar returns to player list
            _inputBar.KeyDown += (s, key) =>
            {
                if (key.KeyCode == KeyCode.Esc)
                {
                    _inputBar.Text = "";
                    _playerListView.SetFocus();
                    key.Handled = true;
                }
            };
        }

        // ── Server Management ──

        private void SetActiveServer(PRoConClient client)
        {
            _activeClient = client;
            _playerCache.Clear();
            _playerDisplayLines.Clear();
            _chatBuffer.Clear();
            _killFeedBuffer.Clear();

            if (client != null)
            {
                WireClientEvents(client);
                if (client.Game != null)
                    WireGameEvents(client, client.Game);

                // Refresh from cached data
                if (client.CurrentServerInfo != null)
                    UpdateServerInfoPanel(client.CurrentServerInfo);

                // Request fresh player list
                client.Game?.SendAdminListPlayersPacket(
                    new CPlayerSubset(CPlayerSubset.PlayerSubsetType.All));
            }

            RefreshPlayerList();
            UpdateServerTabBar();
            RefreshChatView();
            RefreshKillFeedView();
        }

        private void CycleServer(int direction)
        {
            if (_serverList.Count <= 1) return;
            int idx = _serverList.IndexOf(_activeClient);
            idx = (idx + direction + _serverList.Count) % _serverList.Count;
            SetActiveServer(_serverList[idx]);
        }

        private void UpdateServerTabBar()
        {
            if (_serverList.Count == 0)
            {
                _serverTabBar.Text = " PRoCon TUI   No servers configured";
                return;
            }

            var parts = new List<string> { " PRoCon TUI  " };
            int connectedCount = 0;

            for (int i = 0; i < _serverList.Count; i++)
            {
                var client = _serverList[i];
                bool connected = client.Game?.IsLoggedIn == true;
                if (connected) connectedCount++;

                string name = client.CurrentServerInfo?.ServerName ?? client.HostNamePort;
                if (name.Length > 25) name = name.Substring(0, 25) + "..";

                string marker = client == _activeClient ? " \u2605" : "";
                string prefix = client == _activeClient ? "[\u25b8 " : "[  ";
                parts.Add($"{prefix}{name}{marker}]");
            }

            parts.Add($"   Connected {connectedCount}/{_serverList.Count}");
            _serverTabBar.Text = string.Join(" ", parts);
        }

        // ── Panel Updates ──

        private void UpdateServerInfoPanel(CServerInfo info)
        {
            if (info == null)
            {
                _serverInfoLabel.Text = "No server info available";
                return;
            }

            var lines = new List<string>
            {
                $"Map: {info.Map ?? "?"} / {info.GameMode ?? "?"}",
                $"Players: {info.PlayerCount}/{info.MaxPlayerCount}",
                $"Round: {(info.CurrentRound + 1)}/{info.TotalRounds}",
                $"Uptime: {FormatUptime(info.ServerUptime)}"
            };

            if (info.Ranked) lines.Add("Ranked: Yes");
            if (info.PunkBuster) lines.Add("PunkBuster: On");

            if (info.TeamScores != null)
            {
                foreach (var ts in info.TeamScores)
                {
                    string teamName = _activeClient?.GetLocalizedTeamName(
                        ts.TeamID, info.Map, info.GameMode) ?? $"Team {ts.TeamID}";
                    lines.Add($"  {teamName}: {ts.Score:F0} tickets");
                }
            }

            _serverInfoLabel.Text = string.Join("\n", lines);
        }

        private void RefreshPlayerList()
        {
            int selectedIdx = _playerListView.SelectedItem;
            _playerDisplayLines.Clear();

            if (_playerCache.Count == 0)
            {
                _playerDisplayLines.Add("No players");
                _playerListView.Source = new ListWrapper<string>(_playerDisplayLines);
                return;
            }

            var sorted = _playerCache
                .OrderBy(p => p.TeamID)
                .ThenBy(p => p.SquadID)
                .ThenBy(p => p.SoldierName)
                .ToList();
            _playerCache = sorted;

            int currentTeam = -999;
            int displayNum = 1;

            foreach (var p in sorted)
            {
                if (p.TeamID != currentTeam)
                {
                    currentTeam = p.TeamID;
                    string teamName = GetTeamHeader(p.TeamID);
                    _playerDisplayLines.Add($"── {teamName} ──");
                }

                string ping = p.Ping >= 0 ? $"{p.Ping}ms" : "?";
                _playerDisplayLines.Add(
                    $" {displayNum,2}. {p.SoldierName,-20} K:{p.Kills,-4} D:{p.Deaths,-4} " +
                    $"Sc:{p.Score,-5} {ping}");
                displayNum++;
            }

            _playerListView.Source = new ListWrapper<string>(_playerDisplayLines);

            // Restore selection
            if (selectedIdx >= 0 && selectedIdx < _playerDisplayLines.Count)
                _playerListView.SelectedItem = selectedIdx;
        }

        private string GetTeamHeader(int teamId)
        {
            if (_activeClient?.CurrentServerInfo == null)
                return $"Team {teamId}";

            var info = _activeClient.CurrentServerInfo;
            string name = _activeClient.GetLocalizedTeamName(teamId, info.Map, info.GameMode);
            if (string.IsNullOrEmpty(name)) name = $"Team {teamId}";

            // Add ticket count if available
            var ts = info.TeamScores?.FirstOrDefault(t => t.TeamID == teamId);
            if (ts != null)
                return $"{name} ({ts.Score:F0} tickets)";

            return name;
        }

        private void AppendChat(string message)
        {
            _chatBuffer.Add(message);
            while (_chatBuffer.Count > MaxChatLines)
                _chatBuffer.RemoveAt(0);
            RefreshChatView();
        }

        private void RefreshChatView()
        {
            _chatView.Source = new ListWrapper<string>(_chatBuffer);
            if (_chatBuffer.Count > 0)
                _chatView.SelectedItem = _chatBuffer.Count - 1; // auto-scroll
        }

        private void AppendKillFeed(string message)
        {
            _killFeedBuffer.Add(message);
            while (_killFeedBuffer.Count > MaxKillFeedLines)
                _killFeedBuffer.RemoveAt(0);
            RefreshKillFeedView();
        }

        private void RefreshKillFeedView()
        {
            _killFeedView.Source = new ListWrapper<string>(_killFeedBuffer);
            if (_killFeedBuffer.Count > 0)
                _killFeedView.SelectedItem = _killFeedBuffer.Count - 1; // auto-scroll
        }

        // ── Input Commands ──

        private void ProcessInputCommand(string input)
        {
            if (_activeClient == null)
            {
                AppendChat("[System] No server selected");
                return;
            }

            string[] parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            string cmd = parts[0].ToLowerInvariant();
            string arg = parts.Length > 1 ? parts[1] : "";

            switch (cmd)
            {
                case "say":
                    if (!string.IsNullOrWhiteSpace(arg))
                    {
                        _activeClient.SendRequest(new List<string> { "admin.say", arg, "all" });
                        AppendChat($"[Admin] {arg}");
                    }
                    break;
                case "yell":
                    if (!string.IsNullOrWhiteSpace(arg))
                    {
                        _activeClient.SendRequest(new List<string> { "admin.yell", arg, "10", "all" });
                        AppendChat($"[YELL] {arg}");
                    }
                    break;
                default:
                    // Raw RCON
                    var words = new List<string>(input.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                    _activeClient.SendRequest(words);
                    AppendChat($"[RCON] {input}");
                    break;
            }
        }

        private void FocusInputWithPrefix(string prefix)
        {
            _inputBar.Text = prefix;
            _inputBar.MoveEnd();
            _inputBar.SetFocus();
        }

        // ── F-Key Actions ──

        private CPlayerInfo GetSelectedPlayer()
        {
            int sel = _playerListView.SelectedItem;
            if (sel < 0 || _playerCache.Count == 0) return null;

            // Map display line index to player (skip team headers)
            string line = sel < _playerDisplayLines.Count ? _playerDisplayLines[sel] : null;
            if (line == null || line.StartsWith("──") || line == "No players") return null;

            // Extract player number from display line
            string trimmed = line.TrimStart();
            int dotIdx = trimmed.IndexOf('.');
            if (dotIdx > 0 && int.TryParse(trimmed.Substring(0, dotIdx), out int num)
                && num >= 1 && num <= _playerCache.Count)
            {
                return _playerCache[num - 1];
            }

            return null;
        }

        private void DoKillPlayer()
        {
            var player = GetSelectedPlayer();
            if (player == null || _activeClient == null) return;

            _activeClient.SendRequest(new List<string> { "admin.killPlayer", player.SoldierName });
            AppendChat($"[Action] Killed {player.SoldierName}");
        }

        private void DoKickPlayer()
        {
            var player = GetSelectedPlayer();
            if (player == null || _activeClient == null) return;

            var dialog = new Dialog
            {
                Title = $"Kick {player.SoldierName}",
                Width = 50,
                Height = 8
            };

            var reasonField = new TextField
            {
                Text = "Kicked by admin",
                X = 1,
                Y = 1,
                Width = Dim.Fill(2)
            };

            var kickBtn = new Button { Text = "Kick" };
            kickBtn.Accepting += (s, e) =>
            {
                string reason = reasonField.Text?.ToString() ?? "Kicked by admin";
                _activeClient.SendRequest(new List<string> { "admin.kickPlayer", player.SoldierName, reason });
                AppendChat($"[Action] Kicked {player.SoldierName} ({reason})");
                Application.RequestStop();
            };

            var cancelBtn = new Button { Text = "Cancel" };
            cancelBtn.Accepting += (s, e) => Application.RequestStop();

            dialog.Add(new Label { Text = "Reason:", X = 1, Y = 0 });
            dialog.Add(reasonField);
            dialog.AddButton(kickBtn);
            dialog.AddButton(cancelBtn);

            Application.Run(dialog);
            dialog.Dispose();
        }

        private void DoBanPlayer()
        {
            var player = GetSelectedPlayer();
            if (player == null || _activeClient == null) return;

            var dialog = new Dialog
            {
                Title = $"Ban {player.SoldierName}",
                Width = 50,
                Height = 10
            };

            var reasonField = new TextField
            {
                Text = "Banned by admin",
                X = 1,
                Y = 1,
                Width = Dim.Fill(2)
            };

            var banBtn = new Button { Text = "Ban Permanently" };
            banBtn.Accepting += (s, e) =>
            {
                int result = MessageBox.Query(
                    "Confirm Ban",
                    $"Ban {player.SoldierName} permanently?",
                    "Yes", "No");

                if (result == 0)
                {
                    string reason = reasonField.Text?.ToString() ?? "Banned by admin";
                    _activeClient.SendRequest(new List<string>
                        { "banList.add", "name", player.SoldierName, "perm", reason });
                    _activeClient.Game?.SendBanListSavePacket();
                    AppendChat($"[Action] Banned {player.SoldierName} ({reason})");
                    Application.RequestStop();
                }
            };

            var cancelBtn = new Button { Text = "Cancel" };
            cancelBtn.Accepting += (s, e) => Application.RequestStop();

            dialog.Add(new Label { Text = "Reason:", X = 1, Y = 0 });
            dialog.Add(reasonField);
            dialog.AddButton(banBtn);
            dialog.AddButton(cancelBtn);

            Application.Run(dialog);
            dialog.Dispose();
        }

        private void DoQuit()
        {
            int result = MessageBox.Query("Quit", "Shut down PRoCon?", "Yes", "No");
            if (result == 0)
            {
                _application.Shutdown();
                Application.RequestStop();
            }
        }

        // ── Dashboard Overlay ──

        private void ToggleDashboard()
        {
            if (_dashboardVisible)
            {
                // Return to main view
                _mainWindow.Remove(_dashboardContent);
                _dashboardContent?.Dispose();
                _dashboardContent = null;
                _mainWindow.Add(_mainContent);
                _dashboardVisible = false;
            }
            else
            {
                // Show dashboard
                _mainWindow.Remove(_mainContent);
                _dashboardContent = BuildDashboardView();
                _mainWindow.Add(_dashboardContent);
                _dashboardVisible = true;
            }
        }

        private View BuildDashboardView()
        {
            var container = new FrameView
            {
                Title = "PRoCon Dashboard  (F9/Esc: Back)",
                X = 0,
                Y = 1, // below tab bar
                Width = Dim.Fill(),
                Height = Dim.Fill(3) // leave room for input + fkey bar
            };

            var lines = new List<string>();
            lines.Add(string.Format(" {0,-3} {1,-8} {2,-35} {3,-9} {4,-15} {5}",
                "#", "Status", "Server Name", "Players", "Map", "Mode"));
            lines.Add(new string('\u2500', 80));

            int totalPlayers = 0;
            int connectedCount = 0;

            for (int i = 0; i < _serverList.Count; i++)
            {
                var client = _serverList[i];
                bool connected = client.Game?.IsLoggedIn == true;
                string status = connected ? "\u25cf ON " : "\u25cf OFF";
                string name = client.CurrentServerInfo?.ServerName ?? client.HostNamePort;
                if (name.Length > 33) name = name.Substring(0, 33) + "..";

                if (connected)
                {
                    connectedCount++;
                    int pc = client.CurrentServerInfo?.PlayerCount ?? 0;
                    int mc = client.CurrentServerInfo?.MaxPlayerCount ?? 0;
                    totalPlayers += pc;
                    string map = client.CurrentServerInfo?.Map ?? "--";
                    string mode = client.CurrentServerInfo?.GameMode ?? "--";

                    lines.Add(string.Format(" {0,-3} {1,-8} {2,-35} {3,3}/{4,-5} {5,-15} {6}",
                        i + 1, status, name, pc, mc, map, mode));
                }
                else
                {
                    lines.Add(string.Format(" {0,-3} {1,-8} {2,-35} {3,-9} {4,-15} {5}",
                        i + 1, status, name, "--", "--", "--"));
                }
            }

            lines.Add(new string('\u2500', 80));
            lines.Add($" Totals: {connectedCount}/{_serverList.Count} connected  |  " +
                       $"{totalPlayers} players  |  {DateTime.Now:HH:mm:ss}");

            var listView = new ListView
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                Source = new ListWrapper<string>(lines)
            };

            // Enter on a server row switches to it
            listView.Accepting += (s, e) =>
            {
                int sel = listView.SelectedItem;
                // Offset by 2 for header rows
                int serverIdx = sel - 2;
                if (serverIdx >= 0 && serverIdx < _serverList.Count)
                {
                    SetActiveServer(_serverList[serverIdx]);
                    ToggleDashboard(); // return to main view
                }
            };

            // Esc returns to main view
            listView.KeyDown += (s, key) =>
            {
                if (key.KeyCode == KeyCode.Esc)
                {
                    ToggleDashboard();
                    key.Handled = true;
                }
            };

            container.Add(listView);
            return container;
        }

        private void UpdateDashboard()
        {
            if (!_dashboardVisible) return;
            // Rebuild dashboard to refresh data
            _mainWindow.Remove(_dashboardContent);
            _dashboardContent?.Dispose();
            _dashboardContent = BuildDashboardView();
            _mainWindow.Add(_dashboardContent);
        }

        // ── Helpers ──

        private static string FormatUptime(int seconds)
        {
            if (seconds <= 0) return "?";
            var ts = TimeSpan.FromSeconds(seconds);
            return ts.Days > 0
                ? $"{ts.Days}d {ts.Hours}h {ts.Minutes}m"
                : $"{ts.Hours}h {ts.Minutes}m";
        }
    }
}
```

- [ ] **Step 2: Build to verify compilation**

Run:
```bash
dotnet build src/PRoCon.Console/PRoCon.Console.csproj
```
Expected: Build succeeds. `TuiConsole` is not yet wired into `Program.cs` so the app still uses `InteractiveConsole`.

- [ ] **Step 3: Commit**

```bash
git add src/PRoCon.Console/TuiConsole.cs
git commit -m "feat: add TuiConsole skeleton with Terminal.Gui v2 multi-panel layout"
```

---

### Task 3: Wire TuiConsole into Program.cs

**Files:**
- Modify: `src/PRoCon.Console/Program.cs:96-102`

- [ ] **Step 1: Replace InteractiveConsole with TuiConsole in Program.cs**

In `src/PRoCon.Console/Program.cs`, replace the interactive console block (around lines 96-102):

```csharp
// Old code:
bool interactive = !HasFlag(args, "--no-interactive");
if (interactive)
{
    var interactiveConsole = new InteractiveConsole(application, exitEvent);
    interactiveConsole.Start();
}
```

Replace with:

```csharp
// Interactive console mode (on by default, --no-interactive to disable)
bool interactive = !HasFlag(args, "--no-interactive");
if (interactive)
{
    var tuiConsole = new TuiConsole(application, exitEvent);
    tuiConsole.Start(); // Calls Application.Run(), blocks until quit
}
```

- [ ] **Step 2: Add Terminal.Gui using if needed**

The `using Terminal.Gui;` is only in `TuiConsole.cs`, so no change needed to `Program.cs` imports.

- [ ] **Step 3: Build and verify**

Run:
```bash
dotnet build src/PRoCon.Console/PRoCon.Console.csproj
```
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/PRoCon.Console/Program.cs
git commit -m "feat: wire TuiConsole as default interactive console mode"
```

---

### Task 4: Fix Compilation Issues and API Adjustments

This task exists as a buffer for any Terminal.Gui v2 API mismatches discovered during the build in Tasks 2-3. The v2 API is actively evolving and some property names, constructors, or event signatures may differ from documentation.

**Files:**
- Modify: `src/PRoCon.Console/TuiConsole.cs`

- [ ] **Step 1: Build the full solution and fix any compilation errors**

Run:
```bash
dotnet build src/PRoCon.Console/PRoCon.Console.csproj 2>&1
```

Common v2 API issues to watch for:
- `ListWrapper<T>` may need to be `ListWrapperSource<T>` or require implementing `IListDataSource`
- `Application.RequestStop()` may need `app.RequestStop()` in instance-based mode
- `ColorScheme` constructor syntax may differ
- `TextField.Text` may be `string` not `object`
- `Accepting` event may have different signature
- `key.Handled` may be `e.Handled` or need `return true` pattern

Fix each error based on the actual compiler output.

- [ ] **Step 2: Build clean**

Run:
```bash
dotnet build src/PRoCon.Console/PRoCon.Console.csproj
```
Expected: Build succeeds with no errors.

- [ ] **Step 3: Commit fixes**

```bash
git add src/PRoCon.Console/TuiConsole.cs
git commit -m "fix: resolve Terminal.Gui v2 API compilation issues"
```

---

### Task 5: Manual Smoke Test and Layout Tuning

**Files:**
- Modify: `src/PRoCon.Console/TuiConsole.cs` (layout adjustments as needed)

- [ ] **Step 1: Run the console app**

Run (on Linux with display):
```bash
DISPLAY=:0 dotnet run --project src/PRoCon.Console/PRoCon.Console.csproj -- --rcon-host <test-server-ip> --rcon-port <port> --rcon-pass <pass>
```

Or without a game server (just to verify TUI renders):
```bash
dotnet run --project src/PRoCon.Console/PRoCon.Console.csproj
```

- [ ] **Step 2: Verify layout renders correctly**

Check:
- Server tab bar visible at top
- Player list frame on left
- Server info frame on right-top
- Kill feed frame on right-bottom
- Chat frame at bottom
- Input bar below chat
- F-key bar at very bottom (white on blue)

- [ ] **Step 3: Verify keyboard navigation**

Check:
- Arrow keys move player list selection
- F4 focuses input bar with "say " prefix
- F5 focuses input bar with "yell " prefix
- F6 focuses input bar empty
- Esc from input bar returns to player list
- F9 toggles dashboard overlay
- F10 shows quit confirmation

- [ ] **Step 4: Adjust panel proportions if needed**

If the layout looks cramped or disproportionate on your terminal, adjust the `Dim.Percent()` and height values in `BuildMainContent()` and `BuildLayout()`. Common tuning:
- Chat frame height (currently 8 rows)
- Kill feed vs server info split (currently server info 40%, kill feed gets the rest)
- Left/right panel split (currently 50/50)

- [ ] **Step 5: Test with a live game server**

Connect to a test server and verify:
- Players appear in the player list grouped by team
- Chat messages appear in the chat panel
- Kill feed populates
- Server info shows map, mode, scores
- F1 on a selected player kills them
- F2 opens kick dialog
- F3 opens ban dialog with confirmation
- Say/yell commands work from input bar
- Ctrl+Left/Right cycles servers (if multiple configured)
- F9 dashboard shows all servers with status

- [ ] **Step 6: Commit any layout/tuning fixes**

```bash
git add src/PRoCon.Console/TuiConsole.cs
git commit -m "fix: tune TUI panel layout and proportions from smoke testing"
```

---

### Task 6: Final Cleanup and Integration Commit

**Files:**
- Possibly modify: `src/PRoCon.Console/TuiConsole.cs` (any remaining issues)

- [ ] **Step 1: Verify --no-interactive still works**

Run:
```bash
dotnet run --project src/PRoCon.Console/PRoCon.Console.csproj -- --no-interactive
```
Expected: No TUI, just the standard "Running... (Ctrl+C or SIGTERM to shutdown)" output. The old `InteractiveConsole` is not loaded either — it just runs headless.

- [ ] **Step 2: Verify the build is clean**

Run:
```bash
dotnet build src/PRoCon.Console/PRoCon.Console.csproj -c Release
```
Expected: No warnings or errors.

- [ ] **Step 3: Final commit if any changes**

```bash
git add -A src/PRoCon.Console/
git commit -m "feat: Terminal.Gui v2 TUI console — complete implementation"
```
