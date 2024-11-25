using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Gw2Lfg
{
    public class GroupListPanel : Panel
    {
        private const int PADDING = 10;
        private readonly LfgViewModel _viewModel;
        private readonly LfgClient _lfgClient;
        private readonly Dictionary<string, GroupPanel> _groupPanels = [];
        private FlowPanel? _groupsFlowPanel;
        private LoadingSpinner? _groupsLoadingSpinner;
        private TextBox? _searchBox;
        private Dropdown? _contentTypeDropdown;

        public GroupListPanel(LfgViewModel viewModel, LfgClient lfgClient)
        {
            _viewModel = viewModel;
            _lfgClient = lfgClient;
            BuildUI();
        }

        private void BuildUI()
        {
            BuildFilterControls();
            BuildGroupsList();
        }

        private void BuildFilterControls()
        {
            var filterPanel = new Panel
            {
                Parent = this,
                Height = 40,
                WidthSizingMode = SizingMode.Fill,
            };

            _contentTypeDropdown = new Dropdown
            {
                Parent = filterPanel,
                Top = 5,
                Height = 30,
                Width = 120,
            };

            PopulateContentTypeDropdown();

            _searchBox = new TextBox
            {
                Parent = filterPanel,
                Left = _contentTypeDropdown.Right + PADDING,
                Top = 5,
                Height = 30,
                Width = filterPanel.Width - _contentTypeDropdown.Right - (PADDING * 2),
                PlaceholderText = "Search groups...",
            };

            var debounceTimer = new System.Timers.Timer(300);
            debounceTimer.Elapsed += (s, e) =>
            {
                debounceTimer.Stop();
                ApplyFilters();
            };

            _searchBox.TextChanged += (s, e) =>
            {
                debounceTimer.Stop();
                debounceTimer.Start();
            };

            _contentTypeDropdown.ValueChanged += (s, e) => ApplyFilters();
        }

        private void BuildGroupsList()
        {
            _groupsFlowPanel = new FlowPanel
            {
                Parent = this,
                Top = 50,
                Height = Height - 50,
                WidthSizingMode = SizingMode.Fill,
                FlowDirection = ControlFlowDirection.TopToBottom,
                ControlPadding = new Vector2(0, 5),
                ShowBorder = true,
            };

            _groupsLoadingSpinner = new LoadingSpinner
            {
                Parent = this,
                Location = new Point(
                    (Width - 64) / 2,
                    (Height - 64) / 2
                ),
                Visible = _viewModel.IsLoadingGroups,
            };
        }

        private void PopulateContentTypeDropdown()
        {
            _contentTypeDropdown!.Items.Add("All");
            _contentTypeDropdown.Items.Add("Fractals");
            _contentTypeDropdown.Items.Add("Raids");
            _contentTypeDropdown.Items.Add("Strike Missions");
            _contentTypeDropdown.Items.Add("Open World");
            _contentTypeDropdown.SelectedItem = "All";
        }

        public void UpdateGroupsList()
        {
            foreach (var panel in _groupPanels.Values)
            {
                panel.Dispose();
            }
            _groupPanels.Clear();

            foreach (var group in _viewModel.Groups)
            {
                var panel = new GroupPanel(group)
                {
                    Parent = _groupsFlowPanel,
                    Width = _groupsFlowPanel!.Width - 20,
                };
                _groupPanels[group.Id] = panel;
            }

            ApplyFilters();
        }

        private void ApplyFilters()
        {
            if (_searchBox == null || _contentTypeDropdown == null) return;
            var searchText = _searchBox.Text.Trim().ToLower();
            var contentType = _contentTypeDropdown.SelectedItem;

            foreach (var panel in _groupPanels.Values)
            {
                var group = panel.Group;
                var visible = true;

                if (!string.IsNullOrEmpty(searchText))
                {
                    visible = group.Title.ToLower().Contains(searchText);
                }

                if (visible && contentType != "All")
                {
                    // Add content type filtering logic here
                }

                panel.Visible = visible;
            }

            _groupsFlowPanel?.RecalculateLayout();
        }

        public async Task ApplyToGroupAsync(string groupId)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _lfgClient.CreateGroupApplication(groupId, cts.Token);
                ScreenNotification.ShowNotification("Successfully applied", ScreenNotification.NotificationType.Info);
            }
            catch (Exception ex)
            {
                ScreenNotification.ShowNotification($"Failed to apply to group: {ex.Message}", ScreenNotification.NotificationType.Error);
            }
        }
    }
}
