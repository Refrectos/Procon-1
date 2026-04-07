# Player List Enhancements Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Comprehensive visual and data upgrade to the player list page — team names, tickets, squad names, ping colors, score formatting, view switcher, micro-animations, player info panel redesign, action button polish, and EAGUID context menu.

**Architecture:** All changes build on existing `PlayerDisplayInfo`, `ServerEntry`, and `MainWindow` patterns. New computed properties on the model layer, AXAML template updates for visuals, and code-behind logic for team name resolution and view switching. No new dependencies.

**Tech Stack:** Avalonia UI, existing PRoCon.Core data (CServerInfo, TeamScore, PRoConClient.GetLocalizedTeamName)

---

### Task 1: PlayerDisplayInfo Model Enhancements

**Files:**
- Modify: `src/PRoCon.UI/Models/PlayerDisplayInfo.cs`

This task adds squad names, ping color, score formatting, GUID, and opacity support. All subsequent tasks depend on these properties.

- [ ] **Step 1: Replace the full PlayerDisplayInfo.cs**

Replace the entire file `src/PRoCon.UI/Models/PlayerDisplayInfo.cs` with the following. Key changes from current: `Ping` is now a notifying property; `SquadText` uses NATO names; `ScoreText` uses `N0` format; `PingBrush` is a computed color property; `GUID` field added; `RowOpacity` for dead dimming; `IsNewJoin` for join animation.

```csharp
using System.ComponentModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace PRoCon.UI.Models
{
    public class PlayerDisplayInfo : INotifyPropertyChanged
    {
        private static readonly string[] SquadNames =
        {
            "-", "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot",
            "Golf", "Hotel", "India", "Juliet", "Kilo", "Lima", "Mike",
            "November", "Oscar", "Papa", "Quebec", "Romeo", "Sierra",
            "Tango", "Uniform", "Victor", "Whiskey", "X-Ray", "Yankee", "Zulu"
        };

        public string Name { get; set; }
        public string ClanTag { get; set; }
        public int TeamID { get; set; }
        public string GUID { get; set; }

        private int _score;
        public int Score
        {
            get => _score;
            set { _score = value; OnPropertyChanged(nameof(Score)); OnPropertyChanged(nameof(ScoreText)); }
        }

        private int _kills;
        public int Kills
        {
            get => _kills;
            set { _kills = value; OnPropertyChanged(nameof(Kills)); OnPropertyChanged(nameof(KillsText)); }
        }

        private int _deaths;
        public int Deaths
        {
            get => _deaths;
            set { _deaths = value; OnPropertyChanged(nameof(Deaths)); OnPropertyChanged(nameof(DeathsText)); }
        }

        private int _ping;
        public int Ping
        {
            get => _ping;
            set { _ping = value; OnPropertyChanged(nameof(Ping)); OnPropertyChanged(nameof(PingText)); OnPropertyChanged(nameof(PingBrush)); }
        }

        public int Squad { get; set; }
        public string IP { get; set; }

        public int PlayerType { get; set; }
        public bool IsSpectator => PlayerType == 1;
        public bool IsCommander => PlayerType == 2;

        private bool _isAlive = true;
        public bool IsAlive
        {
            get => _isAlive;
            set { _isAlive = value; OnPropertyChanged(nameof(IsAlive)); OnPropertyChanged(nameof(StatusIcon)); OnPropertyChanged(nameof(RowOpacity)); }
        }

        private bool _isNewJoin;
        public bool IsNewJoin
        {
            get => _isNewJoin;
            set { _isNewJoin = value; OnPropertyChanged(nameof(IsNewJoin)); }
        }

        public string StatusIcon => IsAlive ? "" : "X";
        public double RowOpacity => IsAlive ? 1.0 : 0.5;

        private string _country = "";
        public string Country
        {
            get => _country;
            set { _country = value; OnPropertyChanged(nameof(Country)); OnPropertyChanged(nameof(CountryText)); OnPropertyChanged(nameof(FlagText)); }
        }

        private string _countryCode = "";
        public string CountryCode
        {
            get => _countryCode;
            set { _countryCode = value; OnPropertyChanged(nameof(CountryCode)); OnPropertyChanged(nameof(FlagText)); OnPropertyChanged(nameof(FlagImage)); }
        }

        private bool _isVPN;
        public bool IsVPN
        {
            get => _isVPN;
            set { _isVPN = value; OnPropertyChanged(nameof(IsVPN)); OnPropertyChanged(nameof(ThreatText)); }
        }

        private bool _isProxy;
        public bool IsProxy
        {
            get => _isProxy;
            set { _isProxy = value; OnPropertyChanged(nameof(IsProxy)); OnPropertyChanged(nameof(ThreatText)); }
        }

        public string ScoreText => Score.ToString("N0");
        public string KillsText => Kills.ToString();
        public string DeathsText => Deaths.ToString();
        public string PingText => Ping.ToString();
        public string SquadText => Squad > 0 && Squad < SquadNames.Length ? SquadNames[Squad] : Squad > 0 ? Squad.ToString() : "-";
        public string CountryText => !string.IsNullOrEmpty(Country) ? Country : "";
        public string FlagText => !string.IsNullOrEmpty(CountryCode) ? CountryCode.ToUpper() : "";
        public string ThreatText => IsVPN ? "VPN" : IsProxy ? "PROXY" : "";

        public IBrush PingBrush
        {
            get
            {
                if (Ping <= 0) return new SolidColorBrush(Color.Parse("#666666"));
                if (Ping <= 50) return new SolidColorBrush(Color.Parse("#81c784"));
                if (Ping <= 120) return new SolidColorBrush(Color.Parse("#ffd740"));
                return new SolidColorBrush(Color.Parse("#ef5350"));
            }
        }

        private Bitmap _flagImage;
        public Bitmap FlagImage
        {
            get => _flagImage;
            set { _flagImage = value; OnPropertyChanged(nameof(FlagImage)); OnPropertyChanged(nameof(HasFlagImage)); }
        }

        public bool HasFlagImage => _flagImage != null;

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
```

- [ ] **Step 2: Build and verify**

Run: `~/.dotnet/dotnet build src/PRoCon.UI/PRoCon.UI.csproj`
Expected: 0 errors

- [ ] **Step 3: Commit**

```bash
git add src/PRoCon.UI/Models/PlayerDisplayInfo.cs
git commit -m "feat: enhance PlayerDisplayInfo — squad names, ping color, score format, GUID, opacity

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: ServerEntry — Team Names, Tickets, View Mode

**Files:**
- Modify: `src/PRoCon.UI/Models/ServerEntry.cs`

Add properties for resolved team names, ticket scores, and view mode toggle.

- [ ] **Step 1: Add new properties to ServerEntry**

In `src/PRoCon.UI/Models/ServerEntry.cs`, add after the `public string GameVersion` property (around line 123):

```csharp
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
```

- [ ] **Step 2: Build and verify**

Run: `~/.dotnet/dotnet build src/PRoCon.UI/PRoCon.UI.csproj`
Expected: 0 errors

- [ ] **Step 3: Commit**

```bash
git add src/PRoCon.UI/Models/ServerEntry.cs
git commit -m "feat: add TeamNames, TeamTickets, and GroupBySquad to ServerEntry

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: Team Name/Ticket Resolution in MainWindow

**Files:**
- Modify: `src/PRoCon.UI/Views/MainWindow.axaml.cs`

Wire team name resolution and ticket updates from the ServerInfo event.

- [ ] **Step 1: Update ServerInfo event handler**

In `src/PRoCon.UI/Views/MainWindow.axaml.cs`, find the `game.ServerInfo +=` handler (around line 1039). After the line `entry.GameVersion = sender.FriendlyVersionNumber ?? sender.VersionNumber ?? "";` add team name and ticket resolution:

```csharp
                // Resolve team names from game data
                var client = GetClient(entry.HostPort);
                if (client != null && !string.IsNullOrEmpty(info.Map))
                {
                    for (int t = 1; t <= 4; t++)
                    {
                        string teamName = client.GetLocalizedTeamName(t, info.Map, info.GameMode);
                        entry.TeamNames[t] = !string.IsNullOrEmpty(teamName) ? teamName : $"Team {t}";
                    }
                }

                // Update ticket scores
                if (info.TeamScores != null)
                {
                    foreach (var ts in info.TeamScores)
                    {
                        if (ts.TeamID >= 1 && ts.TeamID <= 4)
                            entry.TeamTickets[ts.TeamID] = ts.Score;
                    }
                    // Store target tickets if available
                    if (info.TeamScores.Count > 0)
                        entry.TargetTickets = info.TeamScores[0].WinningScore > 0
                            ? info.TeamScores[0].WinningScore : 1000;
                }
```

- [ ] **Step 2: Check that TeamScore has WinningScore**

Read `src/PRoCon.Core/TeamScore.cs` to confirm the `WinningScore` property exists. If it doesn't, use a fallback of 1000 or compute from max of ticket values. Adjust the code accordingly.

- [ ] **Step 3: Update GUID preservation in ListPlayers handler**

In the ListPlayers handler (around line 1120), in the section that creates new `PlayerDisplayInfo` objects, add GUID:

After `IsAlive = true`:
```csharp
                        GUID = player.GUID ?? "",
```

And in the "Preserve IP/country data from previous refresh" block, add:
```csharp
                        display.GUID = prev.GUID;
```
(after `display.FlagImage = prev.FlagImage;`)

- [ ] **Step 4: Detect new joins**

In the same ListPlayers handler, after preserving IP/country data, add join detection:

```csharp
                    // Mark new joins for fade-in animation
                    if (!previousLookup.ContainsKey(player.SoldierName))
                        display.IsNewJoin = true;
```

- [ ] **Step 5: Build and verify**

Run: `~/.dotnet/dotnet build src/PRoCon.UI/PRoCon.UI.csproj`
Expected: 0 errors

- [ ] **Step 6: Commit**

```bash
git add src/PRoCon.UI/Views/MainWindow.axaml.cs
git commit -m "feat: resolve team names/tickets from ServerInfo, add GUID and join detection

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

### Task 4: Team Headers with Tickets + View Toggle (AXAML)

**Files:**
- Modify: `src/PRoCon.UI/Views/MainWindow.axaml`

Update team header layout to show faction name + ticket bar. Add view toggle above team grid. Update player template for ping color, opacity, squad column header, and EAGUID context menu.

- [ ] **Step 1: Update column header — "Sq" → "Squad"**

In `src/PRoCon.UI/Views/MainWindow.axaml`, in the `PlayerColumnHeaders` DataTemplate, change:
```xml
        <TextBlock Grid.Column="7" Text="Sq" FontSize="8" FontWeight="SemiBold"
```
To:
```xml
        <TextBlock Grid.Column="7" Text="Squad" FontSize="8" FontWeight="SemiBold"
```

- [ ] **Step 2: Update player row template — ping color, opacity, EAGUID**

In the `PlayerItemTemplate` DataTemplate, make these changes:

a) Wrap the Grid in a Border for opacity binding. Replace the opening `<Grid` tag:
```xml
      <Grid ColumnDefinitions="16,22,*,45,35,40,30,35,40" Margin="2,1" Opacity="{Binding RowOpacity}">
```

b) Change the ping TextBlock Foreground from static to binding:
```xml
        <TextBlock Grid.Column="6" Text="{Binding PingText}" Foreground="{Binding PingBrush}" HorizontalAlignment="Right" />
```

c) Widen the squad column from 35 to 50 to fit "Alpha" etc. Update the Grid's ColumnDefinitions:
```
16,22,*,45,35,40,30,50,40
```
Also update the same ColumnDefinitions in the `PlayerColumnHeaders` template.

d) Add "Copy EAGUID" to the context menu after "Copy Name":
```xml
            <MenuItem Header="Copy EAGUID" Click="OnPlayerCopyGUID" />
```

- [ ] **Step 3: Replace each team header with DockPanel showing name + tickets**

For Team 1, replace the header Border content (currently just a TextBlock):

Replace:
```xml
                      <Border DockPanel.Dock="Top" Background="{DynamicResource GlassHeaderBrush}"
                              Padding="8,5" BorderBrush="{DynamicResource BorderBrush}" BorderThickness="0,0,0,1">
                        <TextBlock Name="TeamHeader1" Text="Team 1 (0)"
                                   FontWeight="SemiBold" FontSize="9"
                                   Foreground="{DynamicResource PrimaryBrush}" />
                      </Border>
```

With:
```xml
                      <Border DockPanel.Dock="Top" Background="{DynamicResource GlassHeaderBrush}"
                              Padding="8,4" BorderBrush="{DynamicResource BorderBrush}" BorderThickness="0,0,0,1">
                        <DockPanel>
                          <StackPanel DockPanel.Dock="Right" Orientation="Horizontal" Spacing="6" VerticalAlignment="Center">
                            <Border Width="50" Height="4" Background="#2a2a40" CornerRadius="2">
                              <Border Name="TicketBar1" Height="4" CornerRadius="2" HorizontalAlignment="Left"
                                      Background="{DynamicResource PrimaryBrush}" Width="0" />
                            </Border>
                            <TextBlock Name="TicketCount1" Text="" FontSize="11" FontWeight="Bold"
                                       Foreground="{DynamicResource PrimaryBrush}" />
                          </StackPanel>
                          <TextBlock Name="TeamHeader1" Text="Team 1 (0)"
                                     FontWeight="SemiBold" FontSize="9"
                                     Foreground="{DynamicResource PrimaryBrush}" VerticalAlignment="Center" />
                        </DockPanel>
                      </Border>
```

Repeat for Teams 2, 3, 4 with appropriate names (TicketBar2/TicketCount2, etc.). For Team 2, use `ErrorBrush` accent instead of `PrimaryBrush`.

- [ ] **Step 4: Add view toggle above the team grid**

Before the `<!-- Team grid -->` comment (around line 692), add:

```xml
                <!-- View toggle -->
                <DockPanel DockPanel.Dock="Top" Margin="4,4,4,0">
                  <TextBlock DockPanel.Dock="Right" Name="PlayerCountSummary" Text=""
                             FontSize="9" Foreground="{DynamicResource TextDisabledBrush}"
                             VerticalAlignment="Center" />
                  <StackPanel Orientation="Horizontal" Spacing="2">
                    <Button Name="ViewByScoreBtn" Content="BY SCORE" Padding="8,3" CornerRadius="3"
                            FontSize="9" FontWeight="SemiBold"
                            Background="{DynamicResource PrimaryBrush}" Foreground="{DynamicResource BackgroundBrush}"
                            Click="OnViewByScore" />
                    <Button Name="ViewBySquadBtn" Content="BY SQUAD" Padding="8,3" CornerRadius="3"
                            FontSize="9" FontWeight="SemiBold"
                            Background="{DynamicResource ButtonSecondaryBrush}" Foreground="{DynamicResource TextSecondaryBrush}"
                            Click="OnViewBySquad" />
                  </StackPanel>
                </DockPanel>
```

- [ ] **Step 5: Build and verify**

Run: `~/.dotnet/dotnet build src/PRoCon.UI/PRoCon.UI.csproj`
Expected: 0 errors (may have errors for missing event handlers — those come in Task 5)

- [ ] **Step 6: Commit**

```bash
git add src/PRoCon.UI/Views/MainWindow.axaml
git commit -m "feat: team headers with tickets, view toggle, ping color, opacity, EAGUID menu

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

### Task 5: MainWindow Code-Behind — UpdateTeamPanels, View Toggle, Copy GUID

**Files:**
- Modify: `src/PRoCon.UI/Views/MainWindow.axaml.cs`

Wire the new AXAML elements: update team headers with names/tickets, handle view toggle, implement squad grouping, add GUID copy handler, cache new controls.

- [ ] **Step 1: Cache new controls**

In the `CacheControls` method, add after existing control caching:

```csharp
            // Ticket/team name controls
            _ticketBars = new Border[4];
            _ticketCounts = new TextBlock[4];
            for (int t = 0; t < 4; t++)
            {
                _ticketBars[t] = this.FindControl<Border>($"TicketBar{t + 1}");
                _ticketCounts[t] = this.FindControl<TextBlock>($"TicketCount{t + 1}");
            }
            _viewByScoreBtn = this.FindControl<Button>("ViewByScoreBtn");
            _viewBySquadBtn = this.FindControl<Button>("ViewBySquadBtn");
            _playerCountSummary = this.FindControl<TextBlock>("PlayerCountSummary");
```

Add the field declarations near the other cached fields:

```csharp
        private Border[] _ticketBars;
        private TextBlock[] _ticketCounts;
        private Button _viewByScoreBtn;
        private Button _viewBySquadBtn;
        private TextBlock _playerCountSummary;
```

- [ ] **Step 2: Update UpdateTeamPanels to show team names and tickets**

In the `UpdateTeamPanels` method, in the loop that sets team headers, replace:
```csharp
                if (_teamHeaders[t] != null)
                    _teamHeaders[t].Text = $"Team {t + 1} ({players.Count})";
```

With:
```csharp
                if (_teamHeaders[t] != null)
                {
                    string teamName = entry.TeamNames.TryGetValue(t + 1, out var tn) ? tn : $"Team {t + 1}";
                    _teamHeaders[t].Text = $"{teamName} ({players.Count})";
                }

                // Update ticket display
                if (_ticketCounts[t] != null)
                {
                    int tickets = entry.TeamTickets.TryGetValue(t + 1, out var tk) ? tk : 0;
                    _ticketCounts[t].Text = tickets > 0 ? tickets.ToString("N0") : "";
                }
                if (_ticketBars[t] != null && entry.TargetTickets > 0)
                {
                    int tickets = entry.TeamTickets.TryGetValue(t + 1, out var tk2) ? tk2 : 0;
                    double pct = System.Math.Clamp((double)tickets / entry.TargetTickets, 0, 1);
                    _ticketBars[t].Width = pct * 50;
                }
```

Also update the player count summary:
```csharp
            // Player count summary
            if (_playerCountSummary != null)
            {
                int total = 0;
                for (int t = 1; t <= 4; t++)
                    total += entry.TeamPlayers.ContainsKey(t) ? entry.TeamPlayers[t].Count : 0;
                total += entry.Spectators.Count + entry.Commanders.Count;
                _playerCountSummary.Text = $"{total}/{entry.MaxPlayerCount} players";
            }
```

- [ ] **Step 3: Add view toggle handlers**

Add after the existing `ShowEditButton` method:

```csharp
        private void OnViewByScore(object sender, RoutedEventArgs e)
        {
            if (_selectedServer == null) return;
            _selectedServer.GroupBySquad = false;
            UpdateViewToggleButtons();
            UpdateTeamPanels(_selectedServer);
        }

        private void OnViewBySquad(object sender, RoutedEventArgs e)
        {
            if (_selectedServer == null) return;
            _selectedServer.GroupBySquad = true;
            UpdateViewToggleButtons();
            UpdateTeamPanels(_selectedServer);
        }

        private void UpdateViewToggleButtons()
        {
            bool bySquad = _selectedServer?.GroupBySquad ?? false;
            if (_viewByScoreBtn != null)
            {
                _viewByScoreBtn.Background = (Avalonia.Media.IBrush)(bySquad
                    ? FindThemeBrush("ButtonSecondaryBrush") : FindThemeBrush("PrimaryBrush"));
                _viewByScoreBtn.Foreground = (Avalonia.Media.IBrush)(bySquad
                    ? FindThemeBrush("TextSecondaryBrush") : FindThemeBrush("BackgroundBrush"));
            }
            if (_viewBySquadBtn != null)
            {
                _viewBySquadBtn.Background = (Avalonia.Media.IBrush)(bySquad
                    ? FindThemeBrush("PrimaryBrush") : FindThemeBrush("ButtonSecondaryBrush"));
                _viewBySquadBtn.Foreground = (Avalonia.Media.IBrush)(bySquad
                    ? FindThemeBrush("BackgroundBrush") : FindThemeBrush("TextSecondaryBrush"));
            }
        }

        private object FindThemeBrush(string name)
        {
            if (this.TryFindResource(name, this.ActualThemeVariant, out object brush))
                return brush;
            return Avalonia.Media.Brushes.Gray;
        }
```

- [ ] **Step 4: Implement squad grouping in UpdateTeamPanels**

In `UpdateTeamPanels`, modify the section where `ItemsSource` is set. Before assigning `ItemsSource`, check `GroupBySquad`:

Replace the `_teamLists[t].ItemsSource = null; _teamLists[t].ItemsSource = players;` block with:

```csharp
                    if (entry.GroupBySquad)
                    {
                        // Sort by squad then by score within squad
                        var grouped = players.OrderBy(p => p.Squad).ThenByDescending(p => p.Score).ToList();
                        _teamLists[t].ItemsSource = null;
                        _teamLists[t].ItemsSource = grouped;
                    }
                    else
                    {
                        _teamLists[t].ItemsSource = null;
                        _teamLists[t].ItemsSource = players;
                    }
```

Note: This is a simplified grouping — players sorted by squad number with score within squad, rather than full visual squad headers. Full squad headers with separator items would require a DataTemplateSelector which is complex in Avalonia. This sorted view achieves the goal of seeing squads clustered together.

- [ ] **Step 5: Add Copy GUID handler**

After the existing `OnPlayerCopyName` handler, add:

```csharp
        private async void OnPlayerCopyGUID(object sender, RoutedEventArgs e)
        {
            var player = GetPlayerFromMenuContext(sender);
            if (player == null || string.IsNullOrEmpty(player.GUID)) return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard != null)
                await topLevel.Clipboard.SetTextAsync(player.GUID);
        }
```

- [ ] **Step 6: Build and verify**

Run: `~/.dotnet/dotnet build src/PRoCon.UI/PRoCon.UI.csproj`
Expected: 0 errors

- [ ] **Step 7: Commit**

```bash
git add src/PRoCon.UI/Views/MainWindow.axaml.cs
git commit -m "feat: team name/ticket display, view toggle, squad grouping, copy EAGUID

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

### Task 6: Player Actions Panel Polish

**Files:**
- Modify: `src/PRoCon.UI/Views/PlayerActionsPanel.axaml`
- Modify: `src/PRoCon.UI/Views/PlayerActionsPanel.axaml.cs`

Redesign the actions panel with color-coded buttons, section dividers, better spacing, and improved player info display.

- [ ] **Step 1: Replace the actions section in PlayerActionsPanel.axaml**

Replace the entire `<!-- Quick Actions -->` Border and everything below it (from `<!-- Message / Reason Input -->` through the end of `<!-- Ban Section -->`) with redesigned content using color-coded buttons and section dividers.

Key changes to the actions card:
- Section labels: 8px uppercase, `TextDisabledBrush`
- SAY ALL button: `Background="#2a2a45"` `Foreground="#a0a8b8"`
- YELL ALL button: `Background="#4a3a1a"` `Foreground="#ffd740"`
- KILL button: `Background="#3a1a1a"` `Foreground="#ef5350"`
- KICK button: `Background="#4a3a1a"` `Foreground="#ffd740"`
- BAN PLAYER button: `Background="#3a1a1a"` `Foreground="#ef5350"`
- 1px `BorderBrush` separator lines between sections
- Button padding: `6px` vertical, `CornerRadius="4"`
- Move buttons: compact `28px` width

- [ ] **Step 2: Update IP/country text readability**

In the player info header section:
- Country text: change Foreground to `#a0a8b8` (brighter than `TextSecondaryBrush`)
- IP text: change to `#8888aa` with `FontFamily="Consolas,monospace"`
- Threat badge: darker red background with bright text

- [ ] **Step 3: Update code-behind for ping color in stat box**

In `PlayerActionsPanel.axaml.cs`, in the `SetSelectedPlayers` single-player branch, after setting PingText, also set the ping stat value color:

```csharp
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
```

- [ ] **Step 4: Build and verify**

Run: `~/.dotnet/dotnet build src/PRoCon.UI/PRoCon.UI.csproj`
Expected: 0 errors

- [ ] **Step 5: Commit**

```bash
git add src/PRoCon.UI/Views/PlayerActionsPanel.axaml src/PRoCon.UI/Views/PlayerActionsPanel.axaml.cs
git commit -m "feat: polish player actions panel — color buttons, sections, ping color, readability

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

### Task 7: Manual Test

- [ ] **Step 1: Build and launch**

```bash
~/.dotnet/dotnet build src/PRoCon.UI/PRoCon.UI.csproj
DISPLAY=:0 ~/.dotnet/dotnet run --project src/PRoCon.UI/PRoCon.UI.csproj
```

- [ ] **Step 2: Verify team headers**

Connect to a server. Team headers should show faction names (e.g., "US Marines (24)") with ticket count and bar on the right.

- [ ] **Step 3: Verify squad names**

Squad column should show "Alpha", "Bravo", etc. instead of numbers.

- [ ] **Step 4: Verify ping colors**

Green for low ping, yellow for medium, red for high.

- [ ] **Step 5: Verify score formatting**

Scores should show thousands separator (e.g., "12,450").

- [ ] **Step 6: Verify view toggle**

Click "BY SQUAD" — players should reorder by squad number. Click "BY SCORE" — back to score order.

- [ ] **Step 7: Verify dead player dimming**

Players who die should appear at 50% opacity until they respawn.

- [ ] **Step 8: Verify context menu**

Right-click a player. "Copy EAGUID" should appear. Click it — GUID should be on clipboard.

- [ ] **Step 9: Verify action buttons**

Select a player. Action buttons should have color coding (kill=red, yell=amber, etc.) with section dividers.

- [ ] **Step 10: Verify player info panel**

Country name and IP should be readable. Ping stat should be color-coded.
