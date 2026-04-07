using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using PRoCon.Core.Updates;

namespace PRoCon.UI.Views
{
    public partial class WhatsNewDialog : Window
    {
        private readonly List<UpdateInfo> _releases;
        private readonly string _currentVersion;
        private Border _selectedVersionBorder;

        /// <summary>
        /// If true, clicking "Got it" fires OnDismiss to save the dismissed version.
        /// Set to true for auto-popup on upgrade, false for manual re-open.
        /// </summary>
        public bool IsFirstShow { get; set; }

        /// <summary>
        /// Callback to save dismissed version to config.
        /// </summary>
        public Action OnDismiss { get; set; }

        public WhatsNewDialog()
        {
            InitializeComponent();
            _releases = new List<UpdateInfo>();
            _currentVersion = "";
        }

        public WhatsNewDialog(List<UpdateInfo> releases, string currentVersion) : this()
        {
            _releases = releases ?? new List<UpdateInfo>();
            _currentVersion = currentVersion ?? "";
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            BuildVersionList();

            // Auto-select the first (newest) version
            if (_releases.Count > 0)
                SelectVersion(0);
        }

        private void BuildVersionList()
        {
            var versionList = this.FindControl<StackPanel>("VersionList");
            if (versionList == null) return;

            versionList.Children.Clear();

            for (int i = 0; i < _releases.Count; i++)
            {
                var release = _releases[i];
                int index = i;

                bool isCurrent = release.Version.ToString() == _currentVersion ||
                                 release.TagName?.TrimStart('v') == _currentVersion;

                var versionText = new TextBlock
                {
                    Text = $"v{release.Version}",
                    FontSize = 13,
                    FontWeight = isCurrent ? FontWeight.Bold : FontWeight.Normal,
                    Foreground = (IBrush)FindBrush(isCurrent ? "PrimaryBrush" : "TextPrimaryBrush"),
                };

                var dateText = new TextBlock
                {
                    Text = release.PublishedAt.ToString("MMM d, yyyy"),
                    FontSize = 10,
                    Foreground = (IBrush)FindBrush("TextDisabledBrush"),
                };

                var label = release.IsPreRelease ? " (pre-release)" : "";
                if (!string.IsNullOrEmpty(label))
                {
                    var labelText = new TextBlock
                    {
                        Text = label,
                        FontSize = 9,
                        Foreground = (IBrush)FindBrush("TextDisabledBrush"),
                    };
                    var stack = new StackPanel { Spacing = 2 };
                    stack.Children.Add(versionText);
                    stack.Children.Add(dateText);
                    stack.Children.Add(labelText);

                    var border = CreateVersionItem(stack, index);
                    versionList.Children.Add(border);
                }
                else
                {
                    var stack = new StackPanel { Spacing = 2 };
                    stack.Children.Add(versionText);
                    stack.Children.Add(dateText);

                    var border = CreateVersionItem(stack, index);
                    versionList.Children.Add(border);
                }
            }
        }

        private Border CreateVersionItem(StackPanel content, int index)
        {
            var border = new Border
            {
                Padding = new Thickness(16, 10),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(3, 0, 0, 0),
                BorderBrush = Brushes.Transparent,
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                Child = content,
            };

            border.PointerPressed += (s, e) => SelectVersion(index);
            border.PointerEntered += (s, e) =>
            {
                if (border != _selectedVersionBorder)
                    border.Background = (IBrush)FindBrush("NavHoverBackgroundBrush");
            };
            border.PointerExited += (s, e) =>
            {
                if (border != _selectedVersionBorder)
                    border.Background = Brushes.Transparent;
            };

            return border;
        }

        private void SelectVersion(int index)
        {
            if (index < 0 || index >= _releases.Count) return;

            var versionList = this.FindControl<StackPanel>("VersionList");
            if (versionList == null) return;

            // Deselect previous
            if (_selectedVersionBorder != null)
            {
                _selectedVersionBorder.BorderBrush = Brushes.Transparent;
                _selectedVersionBorder.Background = Brushes.Transparent;
            }

            // Select new
            if (index < versionList.Children.Count && versionList.Children[index] is Border border)
            {
                border.BorderBrush = (IBrush)FindBrush("PrimaryBrush");
                border.Background = (IBrush)FindBrush("NavActiveBackgroundBrush");
                _selectedVersionBorder = border;
            }

            // Render notes
            RenderReleaseNotes(_releases[index]);
        }

        private void RenderReleaseNotes(UpdateInfo release)
        {
            var panel = this.FindControl<StackPanel>("ReleaseNotesPanel");
            if (panel == null) return;

            panel.Children.Clear();

            // Version header
            panel.Children.Add(new TextBlock
            {
                Text = $"v{release.Version}",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                Foreground = (IBrush)FindBrush("PrimaryBrush"),
            });

            panel.Children.Add(new TextBlock
            {
                Text = $"Released {release.PublishedAt:MMMM d, yyyy}",
                FontSize = 11,
                Foreground = (IBrush)FindBrush("TextDisabledBrush"),
                Margin = new Thickness(0, 0, 0, 12),
            });

            // Parse and render sections
            var sections = ChangelogFormatter.Parse(release.Body);

            if (sections.Count == 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "No release notes available.",
                    Foreground = (IBrush)FindBrush("TextSecondaryBrush"),
                    FontSize = 12,
                    FontStyle = FontStyle.Italic,
                });
                return;
            }

            foreach (var section in sections)
            {
                // Section heading with badge
                if (!string.IsNullOrEmpty(section.Heading))
                {
                    var headingPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Margin = new Thickness(0, 8, 0, 4),
                    };

                    if (section.Category != ChangelogCategory.Other)
                    {
                        headingPanel.Children.Add(CreateBadge(section.Category));
                    }

                    headingPanel.Children.Add(new TextBlock
                    {
                        Text = section.Heading,
                        FontSize = 13,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = (IBrush)FindBrush("TextPrimaryBrush"),
                        VerticalAlignment = VerticalAlignment.Center,
                    });

                    panel.Children.Add(headingPanel);
                }

                // Items
                foreach (string item in section.Items)
                {
                    var itemPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Margin = new Thickness(8, 2, 0, 2),
                    };

                    // If no heading, show badge per-item
                    if (string.IsNullOrEmpty(section.Heading) && section.Category != ChangelogCategory.Other)
                    {
                        itemPanel.Children.Add(CreateBadge(section.Category));
                    }
                    else
                    {
                        itemPanel.Children.Add(new TextBlock
                        {
                            Text = "\u2022",
                            Foreground = (IBrush)FindBrush("TextDisabledBrush"),
                            FontSize = 12,
                            VerticalAlignment = VerticalAlignment.Top,
                        });
                    }

                    itemPanel.Children.Add(new TextBlock
                    {
                        Text = item,
                        FontSize = 12,
                        Foreground = (IBrush)FindBrush("TextSecondaryBrush"),
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 350,
                        VerticalAlignment = VerticalAlignment.Top,
                    });

                    panel.Children.Add(itemPanel);
                }
            }
        }

        private static Border CreateBadge(ChangelogCategory category)
        {
            string text;
            Color bg;
            Color fg;

            switch (category)
            {
                case ChangelogCategory.New:
                    text = "NEW";
                    bg = Color.Parse("#2d4a2d");
                    fg = Color.Parse("#81c784");
                    break;
                case ChangelogCategory.Fix:
                    text = "FIX";
                    bg = Color.Parse("#4a2d2d");
                    fg = Color.Parse("#e57373");
                    break;
                case ChangelogCategory.Enhancement:
                    text = "ENH";
                    bg = Color.Parse("#2d3a4a");
                    fg = Color.Parse("#64b5f6");
                    break;
                case ChangelogCategory.Breaking:
                    text = "BRK";
                    bg = Color.Parse("#4a3a2d");
                    fg = Color.Parse("#ffb74d");
                    break;
                default:
                    return new Border();
            }

            return new Border
            {
                Background = new SolidColorBrush(bg),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 1, 5, 1),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 9,
                    FontWeight = FontWeight.Bold,
                    Foreground = new SolidColorBrush(fg),
                }
            };
        }

        private object FindBrush(string name)
        {
            if (this.TryFindResource(name, this.ActualThemeVariant, out object brush))
                return brush;
            return Brushes.Gray;
        }

        private void OnGotIt(object sender, RoutedEventArgs e)
        {
            if (IsFirstShow)
                OnDismiss?.Invoke();
            Close();
        }
    }
}
