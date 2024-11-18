#nullable enable

using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Blish_HUD;
using System.Net.Http;
using System.Threading;

namespace Gw2Lfg
{
    public class GroupPanel : Panel
    {
        public Proto.Group Group { get; set; }

        public GroupPanel(Proto.Group group)
        {
            Group = group;
            HeightSizingMode = SizingMode.AutoSize;
            ShowBorder = true;
        }
    }

    public class ApplicationPanel : Panel
    {
        public Proto.GroupApplication Application { get; set; }

        public ApplicationPanel(Proto.GroupApplication application)
        {
            Application = application;
            Height = 60;
            ShowBorder = true;
        }
    }
    public class LfgView : View, IDisposable
    {
        private const int PADDING = 10;
        private bool _disposed;
        private CancellationTokenSource _cancellationTokenSource;
        private HttpClient _httpClient;
        private SimpleGrpcWebClient _grpcClient = null!;
        private LfgClient _lfgClient = null!;
        private readonly Dictionary<string, GroupPanel> _groupPanels = [];
        private readonly Dictionary<string, ApplicationPanel> _applicationPanels = [];
        private readonly LfgViewModel _viewModel;

        private FlowPanel? _groupsFlowPanel;
        private FlowPanel? _applicationsList;
        private TextBox? _searchBox;
        private Dropdown? _contentTypeDropdown;
        private Panel? _groupManagementPanel;
        private StandardButton? _createButton;
        private TextBox? _descriptionBox;
        private Panel? _requirementsPanel;
        private TextBox? _requirementsNumber;
        private Dropdown? _requirementsDropdown;
        
        public LfgView(HttpClient httpClient, LfgViewModel viewModel)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _httpClient = httpClient;
            _viewModel = viewModel;
        }

        protected override void Build(Container buildPanel)
        {
            BuildMainLayout(buildPanel);
            ReinitializeClients();
            RegisterEventHandlers();
        }

        protected override void Unload()
        {
            UnregisterEventHandlers();
            base.Unload();
        }

        private void ReinitializeClients()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            _grpcClient = new SimpleGrpcWebClient(
                _httpClient, _viewModel.ApiKey, _cancellationTokenSource.Token);
            _lfgClient = new LfgClient(_grpcClient);
        }

        private void BuildMainLayout(Container buildPanel)
        {
            var leftPanel = new Panel
            {
                Parent = buildPanel,
                Width = (int)(buildPanel.ContentRegion.Width * 0.6f),
                Height = buildPanel.ContentRegion.Height,
            };

            var rightPanel = new Panel
            {
                Parent = buildPanel,
                Left = leftPanel.Right + PADDING,
                Width = buildPanel.ContentRegion.Width - leftPanel.Width - PADDING,
                Height = buildPanel.ContentRegion.Height,
                ShowBorder = true,
            };

            BuildGroupListPanel(leftPanel);
            BuildManagementPanel(rightPanel);
        }

        private void BuildGroupListPanel(Panel parent)
        {
            var container = new Panel
            {
                Parent = parent,
                Height = parent.Height - PADDING,
                Width = parent.Width - (PADDING * 2),
                Left = PADDING,
                Top = PADDING,
            };

            BuildFilterControls(container);
            BuildGroupsList(container);
        }

        private void BuildFilterControls(Panel parent)
        {
            var filterPanel = new Panel
            {
                Parent = parent,
                Height = 40,
                Width = parent.Width,
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
                Width = parent.Width,
                FlowDirection = ControlFlowDirection.TopToBottom,
                ControlPadding = new Vector2(0, 5),
                ShowBorder = true,
                HeightSizingMode = SizingMode.Fill,
            };
            _groupPanels.Clear();
        }

        private void BuildManagementPanel(Panel parent)
        {
            _groupManagementPanel = new Panel
            {
                Parent = parent,
                Width = parent.Width - PADDING,
                Height = parent.Height - (PADDING * 2),
            };

            RefreshManagementPanel();
        }

        private void RefreshManagementPanel()
        {
            _groupManagementPanel.ClearChildren();

            if (_viewModel.MyGroup == null)
            {
                BuildCreateGroupPanel();
            }
            else
            {
                BuildGroupManagementPanel();
            }
        }

        private void BuildCreateGroupPanel()
        {
            var panel = new Panel
            {
                Parent = _groupManagementPanel,
                Width = _groupManagementPanel.Width,
                Height = _groupManagementPanel.Height,
                Title = "Create Group",
            };

            BuildGroupInputs(panel);

            _createButton = new StandardButton
            {
                Parent = panel,
                Text = "Create Group",
                Width = 120,
                Height = 30,
                Top = _requirementsPanel.Bottom + PADDING,
                Left = (panel.Width - 120) / 2,
            };

            _createButton.Click += async (s, e) => await CreateGroupAsync();
        }

        private void BuildGroupManagementPanel()
        {
            var panel = new Panel
            {
                Parent = _groupManagementPanel,
                Width = _groupManagementPanel.Width,
                Height = _groupManagementPanel.Height,
                Title = "Manage Group",
            };

            BuildGroupInputs(panel);

            var buttonPanel = new Panel
            {
                Parent = panel,
                Width = panel.Width,
                Height = 40,
                Top = _requirementsPanel.Bottom + PADDING,
            };

            var updateButton = new StandardButton
            {
                Parent = buttonPanel,
                Text = "Update",
                Width = 100,
                Left = (buttonPanel.Width - 210) / 2,
            };

            var closeButton = new StandardButton
            {
                Parent = buttonPanel,
                Text = "Close Group",
                Width = 100,
                Left = updateButton.Right + PADDING,
            };

            updateButton.Click += async (s, e) => await UpdateGroupAsync();
            closeButton.Click += async (s, e) => await CloseGroupAsync();

            BuildApplicationsList(panel, buttonPanel.Bottom + PADDING);
        }

        private void BuildGroupInputs(Panel parent)
        {
            _descriptionBox = new TextBox
            {
                Parent = parent,
                Width = parent.Width - (PADDING * 2),
                Height = 30,
                Left = PADDING,
                Top = PADDING,
                PlaceholderText = "Group Description",
                Text = _viewModel.MyGroup?.Title ?? "",
            };

            _requirementsPanel = new Panel
            {
                Parent = parent,
                Width = parent.Width - (PADDING * 2),
                Height = 30,
                Left = PADDING,
                Top = _descriptionBox.Bottom + PADDING,
            };

            new Label
            {
                Parent = _requirementsPanel,
                Text = "Required KP :",
                AutoSizeWidth = true,
                Height = 30,
            };

            _requirementsNumber = new TextBox
            {
                Parent = _requirementsPanel,
                Width = 50,
                Height = 30,
                Left = _requirementsPanel.Width - 150,
                PlaceholderText = "0",
                Text = _viewModel.MyGroup?.KillProofMinimum.ToString() ?? "",
            };

            _requirementsDropdown = new Dropdown
            {
                Parent = _requirementsPanel,
                Width = 90,
                Height = 30,
                Left = _requirementsPanel.Width - 90,
            };

            PopulateKillProofDropdown();
        }

        private void BuildApplicationsList(Panel parent, int topOffset)
        {
            var applicationsLabel = new Label
            {
                Parent = parent,
                Text = "Applications",
                Top = topOffset,
                Width = parent.Width - (PADDING * 2),
                Left = PADDING,
                Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size16, ContentService.FontStyle.Regular),
            };

            _applicationsList = new FlowPanel
            {
                Parent = parent,
                Top = applicationsLabel.Bottom + PADDING,
                Height = parent.Height - applicationsLabel.Bottom - (PADDING * 2),
                Width = parent.Width - (PADDING * 2),
                Left = PADDING,
                FlowDirection = ControlFlowDirection.TopToBottom,
                ControlPadding = new Vector2(0, 5),
                ShowBorder = true,
            };
            _applicationPanels.Clear();

            foreach (var application in _viewModel.GroupApplications)
            {
                CreateApplicationPanel(_applicationsList, application);
            }
        }

        private async Task CreateGroupAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_descriptionBox.Text))
                {
                    ShowError("Please enter a group description");
                    return;
                }

                uint.TryParse(_requirementsNumber.Text, out uint minKp);
                var kpId = ParseKillProofId(_requirementsDropdown.SelectedItem);

                await _lfgClient.CreateGroup(
                    _descriptionBox.Text.Trim(),
                    minKp,
                    kpId
                );
            }
            catch (Exception ex)
            {
                ShowError($"Failed to create group: {ex.Message}");
            }
        }

        private async Task UpdateGroupAsync()
        {
            try
            {
                if (_viewModel.MyGroup == null || string.IsNullOrWhiteSpace(_descriptionBox.Text))
                {
                    return;
                }

                uint.TryParse(_requirementsNumber.Text, out uint minKp);
                var kpId = ParseKillProofId(_requirementsDropdown.SelectedItem);

                var updatedGroup = new Proto.Group
                {
                    Id = _viewModel.MyGroup.Id,
                    Title = _descriptionBox.Text.Trim(),
                    KillProofMinimum = minKp,
                    KillProofId = kpId,
                };

                await _lfgClient.UpdateGroup(updatedGroup);
            }
            catch (Exception ex)
            {
                ShowError($"Failed to update group: {ex.Message}");
            }
        }

        private async Task CloseGroupAsync()
        {
            try
            {
                if (_viewModel.MyGroup == null)
                {
                    return;
                }

                await _lfgClient.DeleteGroup(_viewModel.MyGroup.Id);
            }
            catch (Exception ex)
            {
                ShowError($"Failed to close group: {ex.Message}");
            }
        }

        private void RegisterEventHandlers()
        {
            _viewModel.ApiKeyChanged += OnApiKeyChanged;
            _viewModel.GroupsChanged += OnGroupsChanged;
            _viewModel.MyGroupChanged += OnMyGroupChanged;
            _viewModel.GroupApplicationsChanged += OnGroupApplicationsChanged;
        }

        private void UnregisterEventHandlers()
        {
            _viewModel.ApiKeyChanged -= OnApiKeyChanged;
            _viewModel.GroupsChanged -= OnGroupsChanged;
            _viewModel.MyGroupChanged -= OnMyGroupChanged;
            _viewModel.GroupApplicationsChanged -= OnGroupApplicationsChanged;
        }

        private void OnApiKeyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            ReinitializeClients();
        }

        private void OnGroupsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            UpdateGroupsList();
        }

        private void OnMyGroupChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            RefreshManagementPanel();
        }

        private void OnGroupApplicationsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            UpdateGroupsList(); // For the Apply button
            RefreshApplicationsList();
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

        private void RefreshApplicationsList()
        {
            if (_viewModel.MyGroup == null)
            {
                return;
            }

            var currentApplications = new HashSet<string>(_applicationPanels.Keys);
            var newApplications = new HashSet<string>(_viewModel.GroupApplications.Select(a => a.Id));

            foreach (var applicationId in currentApplications.Except(newApplications))
            {
                if (_applicationPanels.TryGetValue(applicationId, out var panel))
                {
                    panel.Dispose();
                    _applicationPanels.Remove(applicationId);
                }
            }

            foreach (var application in _viewModel.GroupApplications)
            {
                if (!_applicationPanels.ContainsKey(application.Id))
                {
                    var panel = CreateApplicationPanel(_applicationsList, application);
                    _applicationPanels[application.Id] = panel;
                }
            }
        }

        private void ApplyFilters()
        {
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
                    // visible = MatchesContentType(group, contentType);
                }

                panel.Visible = visible;
            }

            _groupsFlowPanel.RecalculateLayout();
        }

        private void ShowError(string message)
        {
            GameService.Content.PlaySoundEffectByName("error");
            ScreenNotification.ShowNotification(message, ScreenNotification.NotificationType.Error);
        }

        private void PopulateContentTypeDropdown()
        {
            _contentTypeDropdown.Items.Add("All");
            _contentTypeDropdown.Items.Add("Fractals");
            _contentTypeDropdown.Items.Add("Raids");
            _contentTypeDropdown.Items.Add("Strike Missions");
            _contentTypeDropdown.Items.Add("Open World");
            _contentTypeDropdown.SelectedItem = "All";
        }

        private void PopulateKillProofDropdown()
        {
            _requirementsDropdown.Items.Add("");
            _requirementsDropdown.Items.Add("LI");
            _requirementsDropdown.Items.Add("UFE");
            _requirementsDropdown.Items.Add("BSKP");
            _requirementsDropdown.SelectedItem = FormatKillProofId(
                _viewModel.MyGroup?.KillProofId ?? Proto.KillProofId.KpUnknown
            );
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var panel in _groupPanels.Values)
                {
                    panel.Dispose();
                }
                _groupPanels.Clear();

                foreach (var panel in _applicationPanels.Values)
                {
                    panel.Dispose();
                }
                _applicationPanels.Clear();

                _disposed = true;
            }
        }

        // Helper methods for
        // Helper methods for KillProof handling
        private static Proto.KillProofId ParseKillProofId(string value) => value switch
        {
            "LI" => Proto.KillProofId.KpLi,
            "UFE" => Proto.KillProofId.KpUfe,
            "BSKP" => Proto.KillProofId.KpBskp,
            _ => Proto.KillProofId.KpUnknown
        };

        private static string FormatKillProofId(Proto.KillProofId id) => id switch
        {
            Proto.KillProofId.KpLi => "LI",
            Proto.KillProofId.KpUfe => "UFE",
            Proto.KillProofId.KpBskp => "BSKP",
            _ => ""
        };

        private static string FormatKillProofRequirement(Proto.Group group)
        {
            if (group.KillProofMinimum == 0 || group.KillProofId == Proto.KillProofId.KpUnknown)
            {
                return "";
            }
            return $"{group.KillProofMinimum} {FormatKillProofId(group.KillProofId)}";
        }

        // Panel creation and update methods
        private void AddGroupPanel(Proto.Group group)
        {
            var panel = CreateGroupPanel(group);
            _groupPanels[group.Id] = panel;
            panel.Parent = _groupsFlowPanel;
        }

        private GroupPanel CreateGroupPanel(Proto.Group group)
        {
            var panel = new GroupPanel(group)
            {
                HeightSizingMode = SizingMode.AutoSize,
                Width = _groupsFlowPanel.Width - 20,
                ShowBorder = true,
            };

            var infoPanel = new Panel
            {
                Parent = panel,
                Left = PADDING,
                Top = 5,
                Width = panel.Width - 120,
                HeightSizingMode = SizingMode.AutoSize
            };

            var titleLabel = new Label
            {
                Parent = infoPanel,
                Text = group.Title,
                AutoSizeHeight = true,
                Width = infoPanel.Width,
                Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size16, ContentService.FontStyle.Regular)
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
                height += requirementsLabel.Height;
            }

            infoPanel.Height = height + PADDING;

            var buttonPanel = new Panel
            {
                Parent = panel,
                Left = panel.Width - 110,
                Width = 100,
                Height = infoPanel.Height
            };

            var applyButton = new StandardButton
            {
                Parent = buttonPanel,
                Text = "Apply",
                Width = 100,
                Height = 30,
                Top = (buttonPanel.Height - 30) / 2,
                Enabled = group.CreatorId != _viewModel.AccountName
            };

            applyButton.Click += async (s, e) => await ApplyToGroupAsync(group.Id);

            return panel;
        }

        private void UpdateGroupPanel(GroupPanel panel, Proto.Group group)
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

            var buttonPanel = (Panel)panel.Children.Last();
            var applyButton = (StandardButton)buttonPanel.Children.First();
            applyButton.Enabled = group.CreatorId != _viewModel.AccountName;
        }

        private ApplicationPanel CreateApplicationPanel(FlowPanel parent, Proto.GroupApplication application)
        {
            var panel = new ApplicationPanel(application)
            {
                Parent = parent,
                Height = 60,
                Width = parent.Width - PADDING,
                ShowBorder = true,
            };

            var applicantInfo = new Panel
            {
                Parent = panel,
                Left = PADDING,
                Top = 5,
                HeightSizingMode = SizingMode.AutoSize,
                Width = panel.Width - 120
            };

            var nameLabel = new Label
            {
                Parent = applicantInfo,
                Text = application.AccountName,
                AutoSizeHeight = true,
                Width = applicantInfo.Width,
                Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size16, ContentService.FontStyle.Regular)
            };

            var buttonPanel = new Panel
            {
                Parent = panel,
                Left = panel.Width - 110,
                Top = 5,
                Width = 100,
                Height = panel.Height - 10
            };

            var inviteButton = new StandardButton
            {
                Parent = buttonPanel,
                Text = "Invite",
                Width = 100,
                Height = 30,
                Top = (buttonPanel.Height - 30) / 2
            };

            inviteButton.Click += (s, e) =>
            {
                try
                {
                    GameService.GameIntegration.Chat.Send($"/sqinvite {application.AccountName}");
                }
                catch (Exception ex)
                {
                    ShowError($"Failed to invite player: {ex.Message}");
                }
            };

            return panel;
        }

        private async Task ApplyToGroupAsync(string groupId)
        {
            try
            {
                await _lfgClient.CreateGroupApplication(groupId);
                GameService.Content.PlaySoundEffectByName("notification");
                ScreenNotification.ShowNotification(
                    "Application sent successfully"
                );
            }
            catch (Exception ex)
            {
                ShowError($"Failed to apply to group: {ex.Message}");
            }
        }
    }
}