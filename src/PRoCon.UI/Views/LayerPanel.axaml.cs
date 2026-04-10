using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using PRoCon.Core;
using PRoCon.Core.Remote;
using PRoCon.UI.Services;

namespace PRoCon.UI.Views
{
    public partial class LayerPanel : UserControl
    {
        private PRoConClient _client;

        private static IBrush ResolveBrush(string key)
        {
            if (Avalonia.Application.Current != null &&
                Avalonia.Application.Current.TryFindResource(key, Avalonia.Application.Current.ActualThemeVariant, out var value) &&
                value is IBrush brush)
                return brush;
            return Brushes.Transparent;
        }

        public LayerPanel()
        {
            InitializeComponent();
            ApplyLocalization();
        }

        internal void ApplyLocalization()
        {
            SetText("LayerTitleText", Loc.T("layer.title", "Layer Service"));
            SetText("LayerDescriptionText", Loc.T("layer.description", "The layer allows remote admin connections to manage this server through PRoCon."));
            SetText("LayerControlLabel", Loc.T("layer.control", "CONTROL"));
            SetCheckContent("LayerEnabledCheck", Loc.T("layer.enable", "Enable Layer Service"));
            SetText("LayerEnableDescText", Loc.T("layer.enable.desc", "When enabled, remote admin clients can connect via SignalR WebSocket to manage the game server."));
            SetText("LayerConfigLabel", Loc.T("layer.config", "CONFIGURATION"));
            SetText("LayerPortLabel", Loc.T("layer.config.port", "Port:"));
            SetText("LayerBindLabel", Loc.T("layer.config.bind", "Bind Address:"));
            SetButtonContent("LayerApplyButton", Loc.T("layer.config.apply", "Apply"));
            SetText("LayerClientsLabel", Loc.T("layer.clients", "CONNECTED CLIENTS"));
            SetButtonContent("LayerRefreshButton", Loc.T("layer.clients.refresh", "Refresh"));
            SetText("LayerConnInfoLabel", Loc.T("layer.conninfo", "CONNECTION INFO"));
            SetText("LayerConnectionInfo", Loc.T("layer.conninfo.notrunning", "Layer is not running."));
            SetText("LayerStatusText", Loc.T("layer.status.offline", "Offline"));
        }

        private void SetText(string name, string value)
        {
            var ctrl = this.FindControl<TextBlock>(name);
            if (ctrl != null) ctrl.Text = value;
        }

        private void SetButtonContent(string name, string value)
        {
            var ctrl = this.FindControl<Button>(name);
            if (ctrl != null) ctrl.Content = value;
        }

        private void SetCheckContent(string name, string value)
        {
            var ctrl = this.FindControl<CheckBox>(name);
            if (ctrl != null) ctrl.Content = value;
        }

        public void SetClient(PRoConClient client)
        {
            UnwireClient();
            _client = client;

            if (_client == null)
            {
                IsEnabled = false;
                return;
            }

            IsEnabled = true;
            WireClient();
            LoadCurrentState();
        }

        public void SetApplication(PRoConApplication app)
        {
            // Not needed for layer panel.
        }

        private void WireClient()
        {
            if (_client?.Layer == null) return;

            _client.Layer.LayerStarted += OnLayerStarted;
            _client.Layer.LayerShutdown += OnLayerShutdown;
            _client.Layer.ClientConnected += OnClientConnected;
        }

        private void UnwireClient()
        {
            if (_client?.Layer == null) return;

            _client.Layer.LayerStarted -= OnLayerStarted;
            _client.Layer.LayerShutdown -= OnLayerShutdown;
            _client.Layer.ClientConnected -= OnClientConnected;
        }

        private void LoadCurrentState()
        {
            if (_client?.Layer == null) return;

            var layer = _client.Layer;

            var enabledCheck = this.FindControl<CheckBox>("LayerEnabledCheck");
            if (enabledCheck != null) enabledCheck.IsChecked = layer.IsEnabled;

            var portInput = this.FindControl<TextBox>("LayerPortInput");
            if (portInput != null) portInput.Text = layer.ListeningPort.ToString();

            var bindingInput = this.FindControl<TextBox>("LayerBindingAddressInput");
            if (bindingInput != null) bindingInput.Text = layer.BindingAddress ?? "";

            UpdateStatusIndicator(layer.IsOnline);
            RefreshClientList();
        }

        // --- Events ---

        private void OnLayerStarted()
        {
            Dispatcher.UIThread.Post(() =>
            {
                UpdateStatusIndicator(true);
                SetStatus(Loc.T("layer.started", "Layer is now online."));
            });
        }

        private void OnLayerShutdown()
        {
            Dispatcher.UIThread.Post(() =>
            {
                UpdateStatusIndicator(false);
                SetStatus(Loc.T("layer.stopped", "Layer has gone offline."));
            });
        }

        private void OnClientConnected(PRoCon.Core.Remote.Layer.ILayerClient layerClient)
        {
            // Wire sub-events for this client to auto-refresh on login/disconnect
            layerClient.Login += (client) => Dispatcher.UIThread.Post(() =>
            {
                RefreshClientList();
                SetStatus(Loc.TF("layer.client.login", "Client '{0}' logged in.", client.Username));
            });
            layerClient.Logout += (client) => Dispatcher.UIThread.Post(() =>
            {
                RefreshClientList();
                SetStatus(Loc.TF("layer.client.logout", "Client '{0}' logged out.", client.Username));
            });
            layerClient.Quit += (client) => Dispatcher.UIThread.Post(() =>
            {
                RefreshClientList();
                SetStatus(Loc.T("layer.client.disconnected", "A client disconnected."));
            });

            Dispatcher.UIThread.Post(() =>
            {
                RefreshClientList();
                SetStatus(Loc.TF("layer.client.connected", "New client connected from {0}", layerClient.IPPort));
            });
        }

        // --- UI Actions ---

        private void OnLayerEnabledToggle(object sender, RoutedEventArgs e)
        {
            if (_client?.Layer == null) return;

            var enabledCheck = this.FindControl<CheckBox>("LayerEnabledCheck");
            bool enabled = enabledCheck?.IsChecked == true;

            _client.Layer.IsEnabled = enabled;

            if (enabled)
            {
                _client.Layer.Start();
                SetStatus(Loc.T("layer.starting", "Starting layer..."));
            }
            else
            {
                _client.Layer.Shutdown();
                SetStatus(Loc.T("layer.stopping", "Stopping layer..."));
            }
        }

        private void OnApplyLayerConfig(object sender, RoutedEventArgs e)
        {
            if (_client?.Layer == null) return;

            var portInput = this.FindControl<TextBox>("LayerPortInput");
            if (portInput != null && ushort.TryParse(portInput.Text, out ushort port))
            {
                _client.Layer.ListeningPort = port;
            }

            var bindingInput = this.FindControl<TextBox>("LayerBindingAddressInput");
            if (bindingInput != null)
            {
                _client.Layer.BindingAddress = bindingInput.Text ?? "";
            }

            SetStatus(Loc.T("layer.config.applied", "Layer configuration applied. Restart the layer to take effect."));
        }

        private void OnRefreshClients(object sender, RoutedEventArgs e)
        {
            RefreshClientList();
        }

        // --- Helpers ---

        private void RefreshClientList()
        {
            var clientsList = this.FindControl<ListBox>("LayerClientsList");
            if (clientsList == null || _client?.Layer == null) return;

            var items = new List<string>();

            // Show all connected clients with their details
            try
            {
                var clients = _client.Layer.Clients;
                if (clients != null)
                {
                    foreach (var kvp in new Dictionary<string, PRoCon.Core.Remote.Layer.ILayerClient>(clients))
                    {
                        string name = !string.IsNullOrEmpty(kvp.Value.Username) ? kvp.Value.Username : "(authenticating)";
                        string ip = kvp.Value.IPPort ?? "?";
                        items.Add($"{name}  —  {ip}");
                    }
                }
            }
            catch { }

            // Fallback to logged-in usernames if Clients dict isn't accessible
            if (items.Count == 0)
            {
                var loggedIn = _client.Layer.GetLoggedInAccountUsernames();
                if (loggedIn != null)
                {
                    foreach (var username in loggedIn)
                    {
                        if (!string.IsNullOrEmpty(username))
                            items.Add(username);
                    }
                }
            }

            if (items.Count == 0)
                items.Add(Loc.T("layer.clients.none", "(No clients connected)"));

            clientsList.ItemsSource = items;

            // Update client count badge
            int count = items.Count > 0 && items[0].StartsWith("(") ? 0 : items.Count;
            var countText = this.FindControl<TextBlock>("ClientCountText");
            if (countText != null) countText.Text = count.ToString();

            // Update connection info
            UpdateConnectionInfo();
        }

        private void UpdateConnectionInfo()
        {
            var infoText = this.FindControl<TextBlock>("LayerConnectionInfo");
            if (infoText == null || _client?.Layer == null) return;

            var layer = _client.Layer;
            if (layer.IsOnline)
            {
                string bind = string.IsNullOrEmpty(layer.BindingAddress) ? "0.0.0.0" : layer.BindingAddress;
                infoText.Text = $"Protocol: SignalR WebSocket (ASP.NET Core)\n" +
                                $"Endpoint: ws://{bind}:{layer.ListeningPort}/layer\n" +
                                $"Auth: JWT Bearer Token\n" +
                                $"Status: Listening for connections";
            }
            else
            {
                infoText.Text = Loc.T("layer.conninfo.offline", "Layer is not running. Enable the layer to accept remote admin connections.");
            }
        }

        private void UpdateStatusIndicator(bool isOnline)
        {
            var indicator = this.FindControl<Ellipse>("LayerStatusIndicator");
            var statusText = this.FindControl<TextBlock>("LayerStatusText");

            if (indicator != null)
                indicator.Fill = ResolveBrush(isOnline ? "ConnectedBrush" : "DisconnectedBrush");
            if (statusText != null)
                statusText.Text = isOnline ? Loc.T("layer.status.online", "Online") : Loc.T("layer.status.offline", "Offline");

            UpdateConnectionInfo();
        }

        private void SetStatus(string message)
        {
            var status = this.FindControl<TextBlock>("LayerStatusMessage");
            if (status != null) status.Text = message;
        }
    }
}
