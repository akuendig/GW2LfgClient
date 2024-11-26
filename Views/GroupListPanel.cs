#nullable enable

using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Gw2Lfg
{
    public class GroupListPanel : Panel
    {
        private const int PADDING = 10;

        private readonly LfgViewModel _viewModel;
        private readonly LfgClient _lfgClient;

        private readonly Dictionary<string, GroupListRowPanel> _groupPanels = [];
        private FlowPanel? _groupsFlowPanel;
        private LoadingSpinner? _groupsLoadingSpinner;
        private TextBox? _searchBox;
        private Dropdown? _contentTypeDropdown;

        public GroupListPanel(LfgViewModel viewModel, LfgClient lfgClient)
        {
            _viewModel = viewModel;
            _lfgClient = lfgClient;

            HeightSizingMode = SizingMode.Fill;

            BuildLayout(viewModel.State);
            RegisterEvents();
        }

        private void BuildLayout(LfgModel state)
        {
            var container = new Panel
            {
                Parent = this,
                Width = Width - (PADDING * 2),
                Height = Height - PADDING,
                Left = PADDING,
                Top = PADDING,
            };

            Resized += (s, e) =>
            {
                container.Width = Width - (PADDING * 2);
                container.Height = Height - PADDING;
            };

            BuildFilterControls(container);
            BuildGroupsList(container, state.IsLoadingGroups);
            UpdateGroupsList(state.Groups, state.AccountName);
        }

        private void BuildFilterControls(Panel parent)
        {
            var filterPanel = new Panel
            {
                Parent = parent,
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

            filterPanel.Resized += (s, e) =>
            {
                _searchBox.Width = filterPanel.Width - _contentTypeDropdown.Right - (PADDING * 2);
            };

            // Debounce search to avoid excessive updates
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

        private void BuildGroupsList(Panel parent, bool isLoadingGroups)
        {
            _groupsFlowPanel = new FlowPanel
            {
                Parent = parent,
                Top = 50,
                Height = parent.Height - 50,
                WidthSizingMode = SizingMode.Fill,
                FlowDirection = ControlFlowDirection.TopToBottom,
                ShowBorder = true,
                CanScroll = true,
                HeightSizingMode = SizingMode.Fill,
            };

            parent.Resized += (s, e) =>
            {
                _groupsFlowPanel.Height = parent.Height - 50;
            };

            _groupPanels.Clear();
            _groupsLoadingSpinner = new LoadingSpinner
            {
                Parent = parent,
                Location = new Point(
                    (parent.Width - 64) / 2,
                    (parent.Height - 64) / 2
                ),
                Visible = isLoadingGroups,
                ZIndex = _groupsFlowPanel.ZIndex + 1,
            };

            parent.Resized += (s, e) =>
            {
                _groupsLoadingSpinner.Location = new Point(
                    (parent.Width - 64) / 2,
                    (parent.Height - 64) / 2
                );
            };
        }

        private void RegisterEvents()
        {
            _viewModel.GroupsChanged += OnGroupsChanged;
            _viewModel.IsLoadingGroupsChanged += OnIsLoadingChanged;
        }

        protected override void DisposeControl()
        {
            _viewModel.GroupsChanged -= OnGroupsChanged;
            _viewModel.IsLoadingGroupsChanged -= OnIsLoadingChanged;

            foreach (var panel in _groupPanels.Values)
            {
                panel.Dispose();
            }
            _groupPanels.Clear();

            base.DisposeControl();
        }

        private void OnGroupsChanged(object sender, LfgViewModelPropertyChangedEventArgs<ImmutableArray<Proto.Group>> e)
        {
            UpdateGroupsList(e.NewState.Groups, e.NewState.AccountName);
        }

        private void OnIsLoadingChanged(object sender, LfgViewModelPropertyChangedEventArgs<bool> e)
        {
            if (_groupsLoadingSpinner != null)
            {
                _groupsLoadingSpinner.Visible = e.NewValue;
            }
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

        private void UpdateGroupsList(IEnumerable<Proto.Group> groups, string accountName)
        {
            var currentGroups = new HashSet<string>(_groupPanels.Keys);
            var newGroups = new HashSet<string>(groups.Select(g => g.Id));

            // Remove panels for groups that no longer exist
            foreach (var groupId in currentGroups.Except(newGroups))
            {
                if (_groupPanels.TryGetValue(groupId, out var panel))
                {
                    panel.Dispose();
                    _groupPanels.Remove(groupId);
                }
            }

            // Add or update panels for current groups
            foreach (var group in groups)
            {
                if (_groupPanels.TryGetValue(group.Id, out var existingPanel))
                {
                    existingPanel.Update(accountName, group);
                }
                else
                {
                    var panel = new GroupListRowPanel(group, _viewModel, _lfgClient)
                    {
                        Parent = _groupsFlowPanel,
                        Width = _groupsFlowPanel.Width - 10,
                    };
                    _groupsFlowPanel.Resized += (s, e) =>
                    {
                        panel.Width = _groupsFlowPanel.Width - 10;
                    };
                    _groupPanels[group.Id] = panel;
                }
            }

            ApplyFilters();
        }

        private void ApplyFilters()
        {
            if (_searchBox == null || _contentTypeDropdown == null || _groupsFlowPanel == null) return;
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

                panel.Visible = visible;
            }

            _groupsFlowPanel.RecalculateLayout();
        }
    }
}