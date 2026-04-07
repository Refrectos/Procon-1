# Terminal.Gui TUI Console — Design Spec

**Date:** 2026-04-06
**Status:** Draft
**Scope:** Replace text-based `InteractiveConsole` with a Terminal.Gui v2 TUI for `PRoCon.Console`

## Overview

Build a full ncurses-style TUI admin interface for PRoCon's console mode using Terminal.Gui v2. The TUI provides a single-window multi-panel layout with live-updating server info, player list, chat, and kill feed — all driven by existing PRoConClient events with no polling. F-key shortcuts provide instant admin actions on selected players.

## Requirements Summary

- **Multi-server monitoring** with drill-down into single-server management
- **Equal prominence** for chat and player list panels
- **Visible kill feed** for spotting suspicious activity
- **F-key shortcuts** for kill, kick, ban, say, yell, raw RCON
- **Terminal.Gui v2** (latest NuGet `2.x`)
- **Default mode** — TUI replaces current InteractiveConsole; `--no-interactive` disables for Docker/headless

## Screen Layout

```
┌─────────────────────────────────────────────────────────────────────────┐
│ PRoCon TUI   [▸ Server 1: BF4 ★]  [  Server 2: BF3  ]   Connected 2/2│
├───────────────────────────────────┬─────────────────────────────────────┤
│ Players (32/64)                   │ Server Info                        │
│ ── Team 1: US (450 tickets) ──── │ Map: MP_Siege / ConquestLarge0     │
│  1. PlayerOne      K:12 D:3  25ms│ Round: 1/2                         │
│ >2. SuspiciousGuy  K:45 D:1  15ms│ Uptime: 2h 14m                    │
│  3. NewPlayer      K:0  D:5 120ms│ Ranked: Yes  PB: On               │
│ ── Team 2: RU (380 tickets) ──── │─────────────────────────────────────│
│  4. VeteranPro     K:8  D:6  30ms│ Kill Feed                          │
│  5. CamperDude     K:3  D:2  45ms│ PlayerOne [AEK-971] SuspiciousGuy  │
│  ...                              │ VeteranPro [M67 Grenade] NewPlayer │
│                                   │ CamperDude [headshot] VeteranPro   │
├───────────────────────────────────┴─────────────────────────────────────┤
│ Chat                                                                    │
│ [All] PlayerOne: anyone else getting lag?                               │
│ [Team1] SuspiciousGuy: rush B                                          │
│ [All] Admin: Server rules: no glitching                                │
├─────────────────────────────────────────────────────────────────────────┤
│ > say Hello everyone                                                    │
├─────────────────────────────────────────────────────────────────────────┤
│ F1:Kill F2:Kick F3:Ban F4:Say F5:Yell F6:Raw  F9:Dashboard  F10:Quit  │
└─────────────────────────────────────────────────────────────────────────┘
```

### Panel Proportions (80x24+ terminal)

| Panel | Position | Size |
|-------|----------|------|
| Server tab bar | Top | 1 row |
| Players (left) | Middle-left | ~50% width, fills middle height |
| Server Info (right-top) | Middle-right top | ~50% width, ~40% of middle height |
| Kill Feed (right-bottom) | Middle-right bottom | ~50% width, ~60% of middle height |
| Chat | Lower section | ~6 rows |
| Input bar | Below chat | 1 row |
| F-key status bar | Bottom | 1 row |

Layout uses `Pos.Percent()` / `Dim.Percent()` so panels scale with terminal size. Minimum usable: ~80x24.

## Navigation & Interaction

### Server Switching

- Tab bar at top shows all configured servers as labels
- `Ctrl+Left` / `Ctrl+Right` to cycle between servers
- Active server highlighted with `★` and bold — all panels update to that server's data
- Per-server connection indicator: green = connected, red = disconnected

### Player List

- `TableView` grouped by team with team headers showing team name + ticket count
- Arrow keys (`Up`/`Down`) to navigate; selected row highlighted
- Columns: `#`, `Name`, `K`, `D`, `Score`, `Ping`
- Selected player is the target for F-key actions

### F-Key Actions

| Key | Action | Behavior |
|-----|--------|----------|
| F1 | Kill | Immediate, no confirmation |
| F2 | Kick | Dialog for reason (pre-filled "Kicked by admin"), Enter/Esc |
| F3 | Ban | Dialog for reason (pre-filled "Banned by admin") + confirmation ("Ban PlayerName permanently? [Y/N]") |
| F4 | Say | Focuses input bar with `say ` pre-filled |
| F5 | Yell | Focuses input bar with `yell ` pre-filled |
| F6 | Raw | Focuses input bar for raw RCON |
| F9 | Dashboard | Toggle full-screen multi-server overview |
| F10 | Quit | Confirmation dialog, then clean shutdown |

### Input Bar

- Always visible above F-key status bar
- Typing + Enter sends command:
  - `say <msg>` → `admin.say`
  - `yell <msg>` → `admin.yell`
  - Otherwise → raw RCON
- `Esc` returns focus to player list

## F9 Dashboard Overlay

Full-screen multi-server overview replacing the main view:

```
┌─────────────────────────────────────────────────────────────────────────┐
│ PRoCon Dashboard                                          F9/Esc: Back │
├─────────────────────────────────────────────────────────────────────────┤
│ # │ Status │ Server Name                    │ Players │ Map       │Mode│
│───┼────────┼────────────────────────────────┼─────────┼───────────┼────│
│ 1 │  ● ON  │ My BF4 Server #1               │  32/64  │ MP_Siege  │ CQ │
│ 2 │  ● ON  │ BF3 Nostalgia Server            │  12/32  │ MP_Metro  │Rush│
│ 3 │  ● OFF │ BFH Test Server                 │   --    │    --     │ -- │
├─────────────────────────────────────────────────────────────────────────┤
│ Totals: 2/3 connected  |  44 players  |  12:34:56                      │
└─────────────────────────────────────────────────────────────────────────┘
```

- Arrow keys to select server row, `Enter` to switch and return to main view
- `F9` or `Esc` to return without changing server
- Live-updating from cached `CServerInfo` (event-driven, no polling)
- Implemented as a swappable `View`, not a modal dialog

## Data Flow & Thread Safety

### Event-Driven Updates (No Polling)

| Panel | Source Events |
|-------|-------------|
| Player list | `PlayerDictionary.PlayerAdded`, `PlayerUpdated`, `PlayerRemoved`, `ListPlayers` |
| Server info | `ServerInfo` (~30s from game server) |
| Chat | `GlobalChat`, `TeamChat`, `SquadChat` |
| Kill feed | `PlayerKilled` |
| Server tab bar | `ConnectSuccess`, `ConnectionClosed` |
| Dashboard | All `ServerInfo` + connection events |

### Thread Safety

All UI mutations go through `Application.Invoke()` to marshal onto Terminal.Gui's main loop thread. No direct UI writes from event callbacks.

### Server Switching

On server change: unwire old server's UI handlers, wire new server, refresh all panels from cached data.

### Data Caching

- **Player list**: `List<CPlayerInfo>` sorted by TeamID → SquadID → Name, rebuilt on `ListPlayers` event. Maintains selected row position across updates.
- **Chat**: `List<string>` ring buffer, max ~500 messages. Auto-scrolls unless user scrolled up.
- **Kill feed**: `List<string>` ring buffer, max ~100 entries. Always auto-scrolls.
- **Server info**: Latest `CServerInfo` reference per connection.

### Startup Sequence

1. `TuiConsole.Start()` called from `Program.cs`
2. `Application.Init()`
3. Wire `ConnectionDictionary` events (add/remove)
4. Wire existing connections' events
5. Auto-select first server
6. `Application.Run()` — blocks on main thread until quit

## Code Structure

### New Files

| File | Purpose |
|------|---------|
| `src/PRoCon.Console/TuiConsole.cs` | Main TUI class (~600-800 lines). Layout, panels, event wiring, F-key handlers. |

### Modified Files

| File | Change |
|------|--------|
| `src/PRoCon.Console/Program.cs` | Replace `InteractiveConsole` instantiation with `TuiConsole` (~5 lines) |
| `src/PRoCon.Console/PRoCon.Console.csproj` | Add `<PackageReference Include="Terminal.Gui" Version="2.*" />` |

### Preserved Files

| File | Reason |
|------|--------|
| `src/PRoCon.Console/InteractiveConsole.cs` | Kept as-is. Fallback for `--no-interactive` text-mode edge cases. No changes. |

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Terminal.Gui | 2.* (latest v2) | TUI framework |

No other new dependencies.

## Out of Scope

- Map management (rotation, switching)
- Move player to team/squad
- Temp bans with duration
- PunkBuster commands
- Plugin management from TUI
- Mouse interaction (keyboard-only for this iteration)
