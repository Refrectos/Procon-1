using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PRoCon.Core.Remote;
using PRoCon.UI.Services;

namespace PRoCon.UI.Views
{
    public partial class ReservedSlotsPanel : UserControl
    {
        private PRoConClient _client;
        private readonly List<string> _reservedPlayers = new List<string>();

        public ReservedSlotsPanel()
        {
            InitializeComponent();
            ApplyLocalization();
        }

        private void ApplyLocalization()
        {
            var title = this.FindControl<TextBlock>("ReservedTitleText");
            if (title != null) title.Text = Loc.T("reserved.title", "Reserved Slots");

            var refreshBtn = this.FindControl<Button>("BtnRefresh");
            if (refreshBtn != null) refreshBtn.Content = Loc.T("reserved.refresh", "Refresh");

            var removeBtn = this.FindControl<Button>("BtnRemovePlayer");
            if (removeBtn != null) removeBtn.Content = Loc.T("reserved.remove", "Remove Selected");

            var addHeader = this.FindControl<TextBlock>("ReservedAddHeader");
            if (addHeader != null) addHeader.Text = Loc.T("reserved.add.header", "Add Player to Reserved Slots");

            var addBtn = this.FindControl<Button>("BtnAddPlayer");
            if (addBtn != null) addBtn.Content = Loc.T("reserved.add.button", "Add Player");

            var nameInput = this.FindControl<TextBox>("PlayerNameInput");
            if (nameInput != null) nameInput.Watermark = Loc.T("reserved.add.watermark", "Player name");
        }

        public void SetClient(PRoConClient client)
        {
            if (_client?.Game != null)
            {
                _client.Game.ReservedSlotsList -= OnReservedSlotsList;
                _client.Game.ReservedSlotsPlayerAdded -= OnReservedSlotsPlayerAdded;
                _client.Game.ReservedSlotsPlayerRemoved -= OnReservedSlotsPlayerRemoved;
            }

            _client = client;

            if (_client?.Game != null)
            {
                _client.Game.ReservedSlotsList += OnReservedSlotsList;
                _client.Game.ReservedSlotsPlayerAdded += OnReservedSlotsPlayerAdded;
                _client.Game.ReservedSlotsPlayerRemoved += OnReservedSlotsPlayerRemoved;
            }
        }

        public void LoadData()
        {
            if (_client?.Game != null)
            {
                _client.Game.SendReservedSlotsListPacket();
            }
        }

        private void OnReservedSlotsList(FrostbiteClient sender, List<string> soldierNames)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _reservedPlayers.Clear();
                _reservedPlayers.AddRange(soldierNames);
                RefreshListDisplay();
            });
        }

        private void OnReservedSlotsPlayerAdded(FrostbiteClient sender, string strSoldierName)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!_reservedPlayers.Contains(strSoldierName))
                {
                    _reservedPlayers.Add(strSoldierName);
                    RefreshListDisplay();
                }
            });
        }

        private void OnReservedSlotsPlayerRemoved(FrostbiteClient sender, string strSoldierName)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _reservedPlayers.Remove(strSoldierName);
                RefreshListDisplay();
            });
        }

        private void RefreshListDisplay()
        {
            ReservedSlotsList.ItemsSource = null;
            ReservedSlotsList.ItemsSource = new List<string>(_reservedPlayers);
        }

        private void OnAddPlayer(object sender, RoutedEventArgs e)
        {
            if (_client?.Game == null) return;

            string playerName = PlayerNameInput?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(playerName)) return;

            _client.Game.SendReservedSlotsAddPlayerPacket(playerName);
            _client.Game.SendReservedSlotsSavePacket();

            if (PlayerNameInput != null)
                PlayerNameInput.Text = string.Empty;
        }

        private void OnRemovePlayer(object sender, RoutedEventArgs e)
        {
            if (_client?.Game == null) return;

            string selected = ReservedSlotsList.SelectedItem as string;
            if (string.IsNullOrEmpty(selected)) return;

            _client.Game.SendReservedSlotsRemovePlayerPacket(selected);
            _client.Game.SendReservedSlotsSavePacket();
        }

        private void OnRefresh(object sender, RoutedEventArgs e)
        {
            LoadData();
        }
    }
}
