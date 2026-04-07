# Player List Enhancements — Design Spec

**Date:** 2026-04-06
**Status:** Approved

## Summary

Comprehensive visual and functional upgrade to the player list page. Improves readability, adds contextual data (team names, tickets, squad names, ping colors), enhances the player info panel, polishes action buttons, adds a squad-grouped view toggle, micro-animations for liveness, and context menu additions.

## Decisions

| Question | Answer |
|----------|--------|
| Audience | Both admin and casual — info density + good aesthetics |
| Real-time feel | Enhanced live feel — micro-animations on score change, join, death |
| Squad grouping | Secondary toggle — flat by-score default, squad-grouped optional |
| Actions | Current set is fine, just polish the look |
| Context menu | Add "Copy EAGUID" alongside existing "Copy Name" |

## Changes

### 1. Team Headers — Faction Names + Ticket Bars

**Current:** `Team 1 (24)` plain text header.

**New:** `US Marines (24)` with ticket count and a small visual ticket bar on the right side of the header.

**Implementation:**
- Use `PRoConClient.GetLocalizedTeamName(teamId, mapFilename, playlist)` to resolve faction names
- Falls back to `Team {N}` if localization not available
- Store resolved team names on `ServerEntry` (new property `TeamNames: Dictionary<int, string>`)
- Read `CServerInfo.TeamScores` for ticket counts, store on `ServerEntry` (new property `TeamTickets: Dictionary<int, int>`)
- Update team names when `ServerInfo` event fires (map/mode change)
- Team header AXAML: DockPanel with team name TextBlock left, ticket count + thin progress bar right
- Ticket bar: thin 4px `ProgressBar` or styled `Border` showing relative tickets (team tickets / max tickets)
- Team 1 accent: `#4fc3f7` (blue). Team 2 accent: `#ef5350` (red). Teams 3/4: existing PrimaryBrush.

**Data flow:**
1. `game.ServerInfo` event → update `entry.TeamTickets` and resolve team names from `client.GetLocalizedTeamName()`
2. `UpdateTeamPanels()` reads `entry.TeamNames[t]` and `entry.TeamTickets[t]` to populate headers

### 2. Squad Names

**Current:** Squad column shows `1`, `2`, `3` or `-`.

**New:** Shows `Alpha`, `Bravo`, `Charlie`, etc.

**Implementation:**
- Add static lookup in `PlayerDisplayInfo`:
  ```
  private static readonly string[] SquadNames = { "-", "Alpha", "Bravo", "Charlie", "Delta",
      "Echo", "Foxtrot", "Golf", "Hotel", "India", "Juliet", "Kilo", "Lima" };
  ```
- Change `SquadText` property: `Squad > 0 && Squad < SquadNames.Length ? SquadNames[Squad] : Squad.ToString()`
- Player info panel stat box also shows squad name instead of number

### 3. Ping Color Coding

**Current:** All pings same `TextSecondaryBrush` color.

**New:** Color-coded by latency range.

**Thresholds:**
- Green (`#81c784`): ≤ 50ms
- Yellow (`#ffd740`): 51–120ms
- Red (`#ef5350`): > 120ms

**Implementation:**
- Add `PingBrush` computed property to `PlayerDisplayInfo` returning a `SolidColorBrush`
- Bind the ping TextBlock's `Foreground` to `{Binding PingBrush}` instead of static brush
- Make `Ping` property notify `PingBrush` and `PingText` on change

### 4. Score Locale Formatting

**Current:** `12450` raw integer.

**New:** `12,450` formatted per system locale.

**Implementation:**
- Change `ScoreText` property: `Score.ToString("N0")` (uses current culture's thousands separator)
- Same for kills/deaths if desired, but they're usually small numbers so skip

### 5. View Switcher (Score / Squad Grouping)

**Current:** Players always sorted by score descending.

**New:** Toggle between "BY SCORE" (default) and "BY SQUAD" views.

**Implementation:**
- Add a small toggle bar above the team grid: two buttons "BY SCORE" and "BY SQUAD"
- Store view mode on `ServerEntry` (new property `ViewMode: PlayerViewMode` enum { ByScore, BySquad })
- In `UpdateTeamPanels()`:
  - ByScore: current behavior — flat list sorted by score
  - BySquad: group players by squad, each squad gets a header row (`StackPanel` with `TextBlock` "Alpha (5)"), players within squad sorted by score
- For BySquad view, don't use a `ListBox` with a single flat list. Instead, build the visual tree with squad separator `TextBlock` headers interleaved. Use a composite `ItemsSource` with a mix of header objects and player objects, distinguished by DataTemplate selector, OR simply build the StackPanel children programmatically.
- Simpler approach: use programmatic StackPanel children (TextBlock headers + ListBox-like items) since selection needs to work across squads.
- Simplest approach: keep the ListBox but insert lightweight separator items into the list. Add a `SquadSeparator` class with `SquadName` and `PlayerCount` properties. Use a `DataTemplateSelector` or check the item type in a single `DataTemplate` with visibility toggles. Actually, Avalonia doesn't have DataTemplateSelector built-in. Simplest: use `IDataTemplate` with a `Match` method.

**Recommended approach:** Create `PlayerListItem` base class. `PlayerDisplayInfo` and `SquadHeaderItem` both extend it. Use a custom `IDataTemplate` that returns different templates based on item type. The ListBox `ItemsSource` gets a mixed list. Selection only applies to `PlayerDisplayInfo` items.

### 6. Micro-Animations

**Current:** Player list rebuilds fully on refresh with no visual feedback.

**New:** Subtle visual cues for state changes.

**Implementation:**
- **Score flash:** When `Score` property changes, briefly flash the score text brighter. Use a 0.8s `Animation` on the `ScoreText` TextBlock's `Foreground` from white → normal color. Trigger via `PropertyChanged` handler or a CSS class toggle.
- **Join fade-in:** When a new player appears (name not in previous lookup), apply a brief background tint animation (#1a2a1a → transparent) over 0.5s. Can be done by setting a `IsNewJoin` flag on `PlayerDisplayInfo` and using a Styles trigger.
- **Dead player dimming:** Already partially implemented (StatusIcon="X"). Add `Opacity="0.5"` binding to the row when `IsAlive` is false. Bind player row opacity: `{Binding IsAlive, Converter={StaticResource BoolToOpacityConverter}}` where alive=1.0, dead=0.5.

**Practical notes:** Avalonia animations in `ItemsControl` templates can be tricky. Start with the static visual changes (opacity for dead, background tint for new) and score flash. If animations cause performance issues with 64 players, fall back to static-only.

### 7. Player Info Panel Redesign

**Current:** Flat grid of label:value pairs, dim text, small font.

**New:** Redesigned with visual hierarchy:

**Layout (top to bottom):**
1. **Player name** — large bold text, `PrimaryBrush`, with X close button (right-aligned)
2. **Location row** — flag image (20x14) + country name (11px, `#a0a8b8`) + threat badge (if VPN/Proxy)
3. **IP address** — monospace font (`Consolas, monospace`), `#8888aa`, 10px
4. **Stats grid** — 4 equal columns, each with:
   - Small uppercase label (7px, `TextDisabledBrush`): SCORE, K/D, PING, SQUAD
   - Large bold value (15px): colored per type (score=gold, k/d=white, ping=green/yellow/red, squad=gray)
   - Each stat in a subtle card (`#1a1a30` background, 4px border-radius, 6px padding)

**Multi-select:** Shows "{N} players selected" with summed score/K/D, no location/IP row.

### 8. Action Buttons Polish

**Current:** Flat uniform buttons with minimal contrast.

**New:** Color-coded buttons with better visual grouping:

- **SAY ALL:** dark blue-gray bg (`#2a2a45`), light text
- **YELL ALL:** dark amber bg (`#4a3a1a`), amber text (`#ffd740`)
- **KILL:** dark red bg (`#3a1a1a`), red text (`#ef5350`)
- **KICK:** dark amber bg (`#4a3a1a`), amber text
- **BAN PLAYER:** same as kill (dark red)
- **Move buttons:** subtle blue-gray, compact
- Section dividers (1px `BorderBrush` lines) between: message input, quick actions, move, ban
- Section labels: tiny uppercase text (8px, `TextDisabledBrush`)
- Button padding: 6px vertical for proper touch targets
- Button border-radius: 4px

### 9. Context Menu — Copy EAGUID

**Current:** Right-click menu has: Kill, Kick, Move (1-4), Ban, Copy Name.

**New:** Add "Copy EAGUID" menu item.

**Implementation:**
- Add `GUID` property to `PlayerDisplayInfo` (string)
- Populate from `CPlayerInfo.GUID` in the ListPlayers handler
- Preserve across refreshes like other properties
- Add `<MenuItem Header="Copy EAGUID" Click="OnPlayerCopyGUID" />` after "Copy Name" in the context menu
- Handler: `TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(player.GUID)`

### 10. Column Headers

**Already implemented** in previous commit. Keep as-is: Player, Score, K, D, Ping, Sq, Threat.

Update "Sq" → "Squad" now that we have more room with squad names.

## Files Modified

| File | Changes |
|------|---------|
| `PlayerDisplayInfo.cs` | SquadNames lookup, PingBrush, ScoreText N0 format, GUID, IsNewJoin flag, Ping notify |
| `ServerEntry.cs` | TeamNames dict, TeamTickets dict, ViewMode enum/property |
| `MainWindow.axaml` | Team header layout, view toggle, ping binding, context menu GUID, dead opacity, squad header template |
| `MainWindow.axaml.cs` | Team name/ticket resolution, view mode toggle, squad grouping logic, score flash, join detection, GUID copy handler |
| `PlayerActionsPanel.axaml` | Stat boxes layout, section dividers, button colors, location row |
| `PlayerActionsPanel.axaml.cs` | Populate redesigned fields, ping color in stat box |

## Out of Scope

- Rank display (data available but clutters the row)
- Player history/stats lookup
- Custom column ordering/visibility
- Full text search/filter (useful but separate feature)
- Drag-and-drop player moves between teams
