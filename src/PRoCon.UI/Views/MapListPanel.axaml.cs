using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PRoCon.Core;
using PRoCon.Core.Maps;
using PRoCon.Core.Remote;

namespace PRoCon.UI.Views
{
    /// <summary>
    /// Provides display-name lookups for map and mode engine IDs.
    /// Populated dynamically from the server's MapListPool at connection time.
    /// </summary>
    public static class GameData
    {
        private static readonly object _lock = new object();

        private static readonly Dictionary<string, string> MapNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> ModeNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static string GetMapName(string engineName)
        {
            if (string.IsNullOrEmpty(engineName)) return engineName;
            return MapNames.TryGetValue(engineName, out var name) ? name : engineName;
        }

        public static string GetModeName(string engineName)
        {
            if (string.IsNullOrEmpty(engineName)) return engineName;
            return ModeNames.TryGetValue(engineName, out var name) ? name : engineName;
        }

        /// <summary>
        /// Register a map display name from MapListPool data.
        /// Only adds if the key doesn't already exist.
        /// </summary>
        public static void RegisterMapName(string engineName, string displayName)
        {
            if (!string.IsNullOrEmpty(engineName) && !string.IsNullOrEmpty(displayName))
            {
                lock (_lock)
                {
                    if (!MapNames.ContainsKey(engineName))
                        MapNames[engineName] = displayName;
                }
            }
        }

        /// <summary>
        /// Register a mode display name from MapListPool data.
        /// Only adds if the key doesn't already exist.
        /// </summary>
        public static void RegisterModeName(string engineName, string displayName)
        {
            if (!string.IsNullOrEmpty(engineName) && !string.IsNullOrEmpty(displayName))
            {
                lock (_lock)
                {
                    if (!ModeNames.ContainsKey(engineName))
                        ModeNames[engineName] = displayName;
                }
            }
        }
    }

    public partial class MapListPanel : UserControl
    {
        private PRoConClient _client;
        private readonly List<MaplistEntry> _mapList = new List<MaplistEntry>();
        private string _selectedMapFileName;
        private List<(string modeId, string modeDisplayName)> _selectedMapModes;

        private bool _mapPoolRefreshPending;

        // Expansion pack prefix → friendly group name, per game type
        private static readonly Dictionary<string, List<(string prefix, string groupName)>> ExpansionGroups =
            new Dictionary<string, List<(string, string)>>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "BF4", new List<(string, string)>
                    {
                        ("XP0_", "Second Assault"),
                        ("XP1_", "China Rising"),
                        ("XP2_", "Naval Strike"),
                        ("XP3_", "Dragon's Teeth"),
                        ("XP4_", "Final Stand"),
                        ("XP5_", "Night Operations"),
                        ("XP6_", "Community Operations"),
                        ("XP7_", "Legacy Operations"),
                        ("MP_", "Base Maps"),
                    }
                },
                {
                    "BF3", new List<(string, string)>
                    {
                        ("XP1_", "Back to Karkand"),
                        ("XP2_", "Close Quarters"),
                        ("XP3_", "Armored Kill"),
                        ("XP4_", "Aftermath"),
                        ("XP5_", "End Game"),
                        ("MP_", "Base Maps"),
                    }
                },
                {
                    // xp25_ must come before xp2_ so "xp25_bank" doesn't match "xp2_" prefix
                    "BFHL", new List<(string, string)>
                    {
                        ("XP1_", "Criminal Activity"),
                        ("xp25_", "Night Maps"),
                        ("xp2_", "Robbery"),
                        ("xp3_", "Getaway"),
                        ("xp4_", "Betrayal"),
                        ("MP_", "Base Maps"),
                    }
                },
                {
                    "BFBC2", new List<(string, string)>
                    {
                        ("levels/nam_", "Vietnam"),
                        ("levels/bc1_", "Classics"),
                        ("levels/mp_sp_", "Bonus Maps"),
                        ("levels/mp_", "Base Maps"),
                    }
                },
            };

        public MapListPanel()
        {
            InitializeComponent();
        }

        public void SetClient(PRoConClient client)
        {
            // Unwire previous client
            if (_client?.Game != null)
            {
                _client.Game.MapListListed -= OnMapListListed;
                _client.Game.MapListMapAppended -= OnMapListMapAppended;
                _client.Game.MapListMapInserted -= OnMapListMapInserted;
                _client.Game.MapListMapRemoved -= OnMapListMapRemoved;
                _client.Game.MapListCleared -= OnMapListCleared;
            }
            if (_client?.MapListPool != null)
            {
                _client.MapListPool.ItemAdded -= OnMapPoolItemAdded;
            }

            _client = client;

            if (_client?.Game != null)
            {
                _client.Game.MapListListed += OnMapListListed;
                _client.Game.MapListMapAppended += OnMapListMapAppended;
                _client.Game.MapListMapInserted += OnMapListMapInserted;
                _client.Game.MapListMapRemoved += OnMapListMapRemoved;
                _client.Game.MapListCleared += OnMapListCleared;

                // Listen for MapListPool population from .def file loading
                if (_client.MapListPool != null)
                    _client.MapListPool.ItemAdded += OnMapPoolItemAdded;

                // Populate GameData from the server's map pool so display names
                // are always complete for whatever game/DLC is connected.
                foreach (var map in _client.MapListPool)
                {
                    GameData.RegisterMapName(map.FileName, map.PublicLevelName);
                    GameData.RegisterModeName(map.PlayList, map.GameMode);
                }

                PopulateAvailableMaps();
            }
        }

        public void LoadData()
        {
            if (_client?.Game != null)
            {
                _client.Game.SendMapListListRoundsPacket();
            }
        }

        /// <summary>
        /// Called when MapListPool receives entries from .def file loading.
        /// Debounces to avoid rebuilding the tree for every single CMap entry.
        /// </summary>
        private void OnMapPoolItemAdded(int index, CMap item)
        {
            GameData.RegisterMapName(item.FileName, item.PublicLevelName);
            GameData.RegisterModeName(item.PlayList, item.GameMode);

            if (_mapPoolRefreshPending) return;
            _mapPoolRefreshPending = true;

            Dispatcher.UIThread.Post(() =>
            {
                _mapPoolRefreshPending = false;
                PopulateAvailableMaps();
            }, DispatcherPriority.Background);
        }

        /// <summary>
        /// Parses map entries from a .def file (embedded resource or on-disk fallback).
        /// </summary>
        private static List<CMap> LoadMapsFromDefFile(string gameType)
        {
            var maps = new List<CMap>();
            string defFileName = gameType + ".def";
            string[] lines = null;

            // Try embedded resource first (PRoCon.Core.Resources.Configs.{gameType}.def)
            var assembly = typeof(CMap).Assembly;
            string resourceName = $"PRoCon.Core.Resources.Configs.{defFileName}";
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream != null)
                {
                    using (var reader = new System.IO.StreamReader(stream))
                    {
                        lines = reader.ReadToEnd().Split('\n');
                    }
                }
            }

            // Fallback to on-disk file
            if (lines == null)
            {
                string defPath = System.IO.Path.Combine(ProConPaths.ConfigsDirectory, defFileName);
                if (System.IO.File.Exists(defPath))
                    lines = System.IO.File.ReadAllLines(defPath);
            }

            if (lines == null) return maps;

            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (!trimmed.StartsWith("procon.protected.maps.add", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Parse: procon.protected.maps.add "PlayList" "FileName" "GameMode" "PublicLevelName" DefaultSquadID
                var parts = new List<string>();
                int i = trimmed.IndexOf('"');
                while (i >= 0 && i < trimmed.Length)
                {
                    int end = trimmed.IndexOf('"', i + 1);
                    if (end < 0) break;
                    parts.Add(trimmed.Substring(i + 1, end - i - 1));
                    i = trimmed.IndexOf('"', end + 1);
                }

                if (parts.Count >= 4)
                {
                    int squadId = 0;
                    // Try to parse the trailing number after the last quote
                    string remainder = trimmed.Substring(trimmed.LastIndexOf('"') + 1).Trim();
                    int.TryParse(remainder, out squadId);
                    maps.Add(new CMap(parts[0], parts[1], parts[2], parts[3], squadId));
                }
            }

            return maps;
        }

        /// <summary>
        /// Builds the Available Maps tree from MapListPool, grouped by expansion pack.
        /// Falls back to parsing the .def file directly if MapListPool is empty.
        /// </summary>
        private void PopulateAvailableMaps()
        {
            // Determine the map source: live MapListPool or cached .def file
            IEnumerable<CMap> mapSource = null;
            string gameType = _client?.Game?.GameType ?? "";

            if (_client?.MapListPool != null && _client.MapListPool.Count > 0)
            {
                mapSource = _client.MapListPool;
            }
            else
            {
                // Try cached game type to load .def file directly
                if (string.IsNullOrEmpty(gameType) && _client != null)
                    gameType = _client.CachedGameType ?? "";

                if (!string.IsNullOrEmpty(gameType))
                {
                    var defMaps = LoadMapsFromDefFile(gameType);
                    if (defMaps.Count > 0)
                        mapSource = defMaps;
                }
            }

            if (mapSource == null)
            {
                AvailableMapsTree.ItemsSource = new List<TreeViewItem>
                {
                    new TreeViewItem { Header = "No maps available (not connected)" }
                };
                return;
            }

            // Use gameType determined above (from Game, CachedGameType, or .def fallback)
            if (string.IsNullOrEmpty(gameType) && _client != null)
                gameType = _client.CachedGameType ?? "";

            // Group CMap entries by FileName, collecting modes per map
            var mapsByFile = new Dictionary<string, (string displayName, List<(string modeId, string modeDisplay)> modes)>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var cmap in mapSource)
            {
                if (string.IsNullOrEmpty(cmap.FileName)) continue;

                // Register display names for rotation list lookups
                GameData.RegisterMapName(cmap.FileName, cmap.PublicLevelName);
                GameData.RegisterModeName(cmap.PlayList, cmap.GameMode);

                if (!mapsByFile.TryGetValue(cmap.FileName, out var entry))
                {
                    entry = (cmap.PublicLevelName ?? cmap.FileName, new List<(string, string)>());
                    mapsByFile[cmap.FileName] = entry;
                }

                // Avoid duplicate modes for the same map
                string modeId = cmap.PlayList ?? "";
                if (!string.IsNullOrEmpty(modeId) && !entry.modes.Any(m => string.Equals(m.modeId, modeId, StringComparison.OrdinalIgnoreCase)))
                {
                    entry.modes.Add((modeId, cmap.GameMode ?? modeId));
                }
            }

            // Get expansion group definitions for this game type
            var groups = ExpansionGroups.TryGetValue(gameType, out var g) ? g : null;

            // Build grouped structure: group name → list of (fileName, displayName, modes)
            var grouped = new List<(string groupName, List<(string fileName, string displayName, List<(string modeId, string modeDisplay)> modes)> maps)>();
            var assigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (groups != null)
            {
                foreach (var (prefix, groupName) in groups)
                {
                    var mapsInGroup = mapsByFile
                        .Where(kvp => kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !assigned.Contains(kvp.Key))
                        .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(kvp => (kvp.Key, kvp.Value.displayName, kvp.Value.modes))
                        .ToList();

                    if (mapsInGroup.Count > 0)
                    {
                        grouped.Add((groupName, mapsInGroup));
                        foreach (var m in mapsInGroup) assigned.Add(m.Key);
                    }
                }
            }

            // Any maps not matched by prefix go into "Other"
            var unmatched = mapsByFile
                .Where(kvp => !assigned.Contains(kvp.Key))
                .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kvp => (kvp.Key, kvp.Value.displayName, kvp.Value.modes))
                .ToList();

            if (unmatched.Count > 0)
            {
                grouped.Add(("Other", unmatched));
            }

            // Reorder so "Base Maps" is first
            var baseIdx = grouped.FindIndex(g2 => g2.groupName == "Base Maps");
            if (baseIdx > 0)
            {
                var baseGroup = grouped[baseIdx];
                grouped.RemoveAt(baseIdx);
                grouped.Insert(0, baseGroup);
            }

            // Build TreeView items
            var treeItems = new List<TreeViewItem>();
            foreach (var (groupName, maps) in grouped)
            {
                var groupNode = new TreeViewItem
                {
                    Header = groupName,
                    IsExpanded = true
                };

                foreach (var (fileName, displayName, modes) in maps)
                {
                    var mapNode = new TreeViewItem
                    {
                        Header = $"{displayName}  ({fileName})",
                        Tag = new MapNodeData { FileName = fileName, DisplayName = displayName, Modes = modes }
                    };
                    groupNode.Items.Add(mapNode);
                }

                treeItems.Add(groupNode);
            }

            AvailableMapsTree.ItemsSource = treeItems;
        }

        /// <summary>
        /// Tag data for map tree nodes.
        /// </summary>
        private class MapNodeData
        {
            public string FileName { get; set; }
            public string DisplayName { get; set; }
            public List<(string modeId, string modeDisplay)> Modes { get; set; }
        }

        private void OnAvailableMapSelected(object sender, SelectionChangedEventArgs e)
        {
            if (AvailableMapsTree.SelectedItem is TreeViewItem tvi && tvi.Tag is MapNodeData data)
            {
                _selectedMapFileName = data.FileName;
                _selectedMapModes = data.Modes;

                var modeItems = data.Modes
                    .Select(m => new ComboBoxItem
                    {
                        Content = m.modeDisplay,
                        Tag = m.modeId
                    })
                    .ToList();

                ModeCombo.ItemsSource = modeItems;
                if (modeItems.Count > 0)
                    ModeCombo.SelectedIndex = 0;
            }
            else
            {
                _selectedMapFileName = null;
                _selectedMapModes = null;
                ModeCombo.ItemsSource = null;
            }
        }

        private void OnAddToRotation(object sender, RoutedEventArgs e)
        {
            if (_client?.Game == null || _selectedMapFileName == null) return;

            string gamemode = string.Empty;
            if (ModeCombo.SelectedItem is ComboBoxItem cbi && cbi.Tag is string modeTag)
            {
                gamemode = modeTag;
            }

            int rounds = (int)(RoundsInput.Value ?? 1);
            if (rounds < 1) rounds = 1;

            var entry = new MaplistEntry(gamemode, _selectedMapFileName, rounds);
            _client.Game.SendMapListAppendPacket(entry);
            _client.Game.SendMapListSavePacket();
        }

        // --- Event handlers for server responses ---

        private void OnMapListListed(FrostbiteClient sender, List<MaplistEntry> lstMapList)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _mapList.Clear();
                _mapList.AddRange(lstMapList);
                RefreshRotationDisplay();
            });
        }

        private void OnMapListMapAppended(FrostbiteClient sender, MaplistEntry mapEntry)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _mapList.Add(mapEntry);
                RefreshRotationDisplay();
            });
        }

        private void OnMapListMapInserted(FrostbiteClient sender, MaplistEntry entry)
        {
            Dispatcher.UIThread.Post(() =>
            {
                LoadData();
            });
        }

        private void OnMapListMapRemoved(FrostbiteClient sender, int index)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (index >= 0 && index < _mapList.Count)
                {
                    _mapList.RemoveAt(index);
                }
                RefreshRotationDisplay();
            });
        }

        private void OnMapListCleared(FrostbiteClient sender)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _mapList.Clear();
                RefreshRotationDisplay();
            });
        }

        private void RefreshRotationDisplay()
        {
            var items = new List<string>();
            for (int i = 0; i < _mapList.Count; i++)
            {
                var entry = _mapList[i];
                string mapDisplay = GameData.GetMapName(entry.MapFileName);
                string gamemodeRaw = !string.IsNullOrEmpty(entry.Gamemode) ? entry.Gamemode : "default";
                string gamemodeDisplay = GameData.GetModeName(gamemodeRaw);
                items.Add($"{i + 1}.  {mapDisplay}  |  {gamemodeDisplay}  |  Rounds: {entry.Rounds}");
            }
            MapRotationList.ItemsSource = items;
        }

        // --- Right column actions ---

        private void OnRemoveMap(object sender, RoutedEventArgs e)
        {
            if (_client?.Game == null) return;

            int index = MapRotationList.SelectedIndex;
            if (index < 0 || index >= _mapList.Count) return;

            _client.Game.SendMapListRemovePacket(index);
            _client.Game.SendMapListSavePacket();

            // Immediate local feedback
            _mapList.RemoveAt(index);
            RefreshRotationDisplay();
        }

        private void OnMoveUp(object sender, RoutedEventArgs e)
        {
            if (_client?.Game == null) return;

            int index = MapRotationList.SelectedIndex;
            if (index <= 0 || index >= _mapList.Count) return;

            var entry = _mapList[index];
            _client.Game.SendMapListRemovePacket(index);
            var insertEntry = new MaplistEntry(
                entry.Gamemode ?? string.Empty,
                entry.MapFileName,
                entry.Rounds,
                index - 1);
            _client.Game.SendMapListInsertPacket(insertEntry);
            _client.Game.SendMapListSavePacket();

            // Immediate local feedback
            _mapList.RemoveAt(index);
            _mapList.Insert(index - 1, entry);
            RefreshRotationDisplay();
            MapRotationList.SelectedIndex = index - 1;
        }

        private void OnMoveDown(object sender, RoutedEventArgs e)
        {
            if (_client?.Game == null) return;

            int index = MapRotationList.SelectedIndex;
            if (index < 0 || index >= _mapList.Count - 1) return;

            var entry = _mapList[index];
            _client.Game.SendMapListRemovePacket(index);
            var insertEntry = new MaplistEntry(
                entry.Gamemode ?? string.Empty,
                entry.MapFileName,
                entry.Rounds,
                index + 1);
            _client.Game.SendMapListInsertPacket(insertEntry);
            _client.Game.SendMapListSavePacket();

            // Immediate local feedback
            _mapList.RemoveAt(index);
            _mapList.Insert(index + 1, entry);
            RefreshRotationDisplay();
            MapRotationList.SelectedIndex = index + 1;
        }

        private void OnSetNextMap(object sender, RoutedEventArgs e)
        {
            if (_client?.Game == null) return;

            int index = MapRotationList.SelectedIndex;
            if (index < 0 || index >= _mapList.Count) return;

            _client.Game.SendMapListNextLevelIndexPacket(index);
        }

        // --- Bottom bar actions ---

        private void OnRestartRound(object sender, RoutedEventArgs e)
        {
            _client?.Game?.SendAdminRestartRoundPacket();
        }

        private void OnRunNextRound(object sender, RoutedEventArgs e)
        {
            _client?.Game?.SendAdminRunNextRoundPacket();
        }

        private void OnEndRound(object sender, RoutedEventArgs e)
        {
            // End round by running next round - same practical effect
            _client?.Game?.SendAdminRunNextRoundPacket();
        }
    }
}
