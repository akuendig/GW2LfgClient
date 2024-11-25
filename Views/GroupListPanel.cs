#nullable enable

using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
            
            BuildLayout();
            RegisterEvents();
        }

        private void BuildLayout()
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
            BuildGroupsList(container);
            UpdateGroupsList();
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

        private void BuildGroupsList(Panel parent)
        {
            _groupsFlowPanel = new FlowPanel
            {
                Parent = parent,
                Top = 50,
                Height = parent.Height - 50,
                WidthSizingMode = SizingMode.Fill,
                FlowDirection = ControlFlowDirection.TopToBottom,
                ControlPadding = new Vector2(0, 5),
                ShowBorder = true,
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
                Visible = _viewModel.IsLoadingGroups,
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

        private void OnGroupsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            UpdateGroupsList();
        }

        private void OnIsLoadingChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_groupsLoadingSpinner != null)
            {
                _groupsLoadingSpinner.Visible = _viewModel.IsLoadingGroups;
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

        private void UpdateGroupsList()
        {
            var currentGroups = new HashSet<string>(_groupPanels.Keys);
            var newGroups = new HashSet<string>(_viewModel.Groups.Select(g => g.Id));

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
            foreach (var group in _viewModel.Groups)
            {
                if (_groupPanels.TryGetValue(group.Id, out var existingPanel))
                {
                    UpdateGroupPanel(existingPanel, group);
                }
                else
                {
                    AddGroupPanel(group);
                }
            }

            ApplyFilters();
        }

        private void AddGroupPanel(Proto.Group group)
        {
            var panel = CreateGroupPanel(group);
            _groupPanels[group.Id] = panel;
            panel.Parent = _groupsFlowPanel;
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

        private GroupListRowPanel CreateGroupPanel(Proto.Group group)
        {
            var panel = new GroupListRowPanel(group)
            {
                HeightSizingMode = SizingMode.AutoSize,
                Width = _groupsFlowPanel!.Width - 20,
                ShowBorder = true,
            };
            _groupsFlowPanel.Resized += (s, e) =>
            {
                panel.Width = _groupsFlowPanel.Width - 20;
            };

            var infoPanel = new Panel
            {
                Parent = panel,
                Left = PADDING,
                Top = 5,
                Width = panel.Width - 2 * 100 - 3 * PADDING,
                HeightSizingMode = SizingMode.AutoSize
            };
            panel.Resized += (s, e) =>
            {
                infoPanel.Width = panel.Width - 2 * 100 - 3 * PADDING;
            };

            var titleLabel = new Label
            {
                Parent = infoPanel,
                Text = group.Title,
                AutoSizeHeight = true,
                Width = infoPanel.Width,
                Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size16, ContentService.FontStyle.Regular)
            };
            infoPanel.Resized += (s, e) =>
            {
                titleLabel.Width = infoPanel.Width;
            };

            int height = titleLabel.Height;

            var kpRequirement = FormatKillProofRequirement(group);
            if (!string.IsNullOrEmpty(kpRequirement))
            {
                var requirementsLabel = new Label
                {
                    Parent = infoPanel,
                    Text = kpRequirement,
                    Top = height,
                    AutoSizeHeight = true,
                    Width = infoPanel.Width,
                    Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size12, ContentService.FontStyle.Regular)
                };
                infoPanel.Resized += (s, e) =>
                {
                    requirementsLabel.Width = infoPanel.Width;
                };
                height += requirementsLabel.Height;
            }

            infoPanel.Height = height + PADDING;

            var statusPanel = new Panel
            {
                Parent = panel,
                Top = 5,
                Left = infoPanel.Right + PADDING,
                Width = 100,
                Height = infoPanel.Height,
            };
            infoPanel.Resized += (s, e) =>
            {
                statusPanel.Left = infoPanel.Right + PADDING;
                statusPanel.Height = infoPanel.Height;
            };
            panel.StatusLabel = new Label
            {
                Parent = statusPanel,
                Width = statusPanel.Width,
                Height = 30,
                VerticalAlignment = VerticalAlignment.Middle,
                Top = (statusPanel.Height - 30) / 2,
                Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size12, ContentService.FontStyle.Regular)
            };
            infoPanel.Resized += (s, e) =>
            {
                panel.StatusLabel.Top = (statusPanel.Height - 30) / 2;
            };
            panel.UpdateStatus();

            var buttonPanel = new Panel
            {
                Parent = panel,
                Left = panel.Width - 110,
                Width = 100,
                HeightSizingMode = SizingMode.Fill,
            };
            panel.Resized += (s, e) =>
            {
                buttonPanel.Left = panel.Width - 110;
            };

            var isYourGroup = group.CreatorId == _viewModel.AccountName;
            var applyButton = new StandardButton
            {
                Parent = buttonPanel,
                Text = "Apply",
                Width = 100,
                Height = 30,
                Top = (buttonPanel.Height - 30) / 2,
                Visible = !isYourGroup,
            };
            buttonPanel.Resized += (s, e) =>
            {
                applyButton.Top = (buttonPanel.Height - 30) / 2;
            };

            var myGroupLabel = new Label
            {
                Parent = buttonPanel,
                Text = "My Group",
                Width = 100,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Center,
                Top = (buttonPanel.Height - 30) / 2,
                Visible = isYourGroup,
            };
            buttonPanel.Resized += (s, e) =>
            {
                myGroupLabel.Top = (buttonPanel.Height - 30) / 2;
            };

            applyButton.Click += async (s, e) =>
            {
                applyButton.Enabled = false;
                try
                {
                    await ApplyToGroupAsync(group.Id);
                    ScreenNotification.ShowNotification("Successfully applied", ScreenNotification.NotificationType.Info);
                }
                finally
                {
                    if (applyButton.Parent != null)
                    {
                        applyButton.Enabled = true;
                    }
                }
            };

            return panel;
        }

        private void UpdateGroupPanel(GroupListRowPanel panel, Proto.Group group)
        {
            panel.Group = group;
            var infoPanel = (Panel)panel.Children.First();
            var titleLabel = (Label)infoPanel.Children.First();
            titleLabel.Text = group.Title;

            var kpRequirement = FormatKillProofRequirement(group);
            if (infoPanel.Children.Count > 1)
            {
                if (string.IsNullOrEmpty(kpRequirement))
                {
                    infoPanel.Children[1].Dispose();
                }
                else
                {
                    ((Label)infoPanel.Children[1]).Text = kpRequirement;
                }
            }
            else if (!string.IsNullOrEmpty(kpRequirement))
            {
                var requirementsLabel = new Label
                {
                    Parent = infoPanel,
                    Text = kpRequirement,
                    Top = titleLabel.Height,
                    AutoSizeHeight = true,
                    Width = infoPanel.Width,
                    Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size12, ContentService.FontStyle.Regular)
                };
            }
            infoPanel.Height = infoPanel.Children.Sum(c => c.Height) + PADDING;

            var buttonPanel = (Panel)panel.Children.Last();
            buttonPanel.Height = infoPanel.Height;
            var applyButton = buttonPanel.GetChildrenOfType<StandardButton>().First();
            applyButton.Top = (buttonPanel.Height - 30) / 2;
            var myGroupLabel = buttonPanel.GetChildrenOfType<Label>().First();
            myGroupLabel.Top = (buttonPanel.Height - 30) / 2;
            var isYourGroup = group.CreatorId == _viewModel.AccountName;
            applyButton.Visible = !isYourGroup;
            myGroupLabel.Visible = isYourGroup;

            panel.UpdateStatus();
        }

        private static string FormatKillProofRequirement(Proto.Group group)
        {
            if (group.KillProofMinimum == 0 || group.KillProofId == Proto.KillProofId.KpUnknown)
            {
                return "";
            }
            return $"{group.KillProofMinimum} {KillProof.FormatId(group.KillProofId)}";
        }

        private async Task ApplyToGroupAsync(string groupId)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _lfgClient.CreateGroupApplication(groupId, cts.Token);
            }
            catch (Exception ex)
            {
                Notifications.ShowError($"Failed to apply to group: {ex.Message}");
            }
        }
    }
}