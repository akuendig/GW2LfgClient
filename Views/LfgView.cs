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
using Blish_HUD.Content;

namespace Gw2Lfg
{
    public class LfgView : View, IDisposable
    {
        private const int PADDING = 10;
        private bool _disposed;
        private CancellationTokenSource _cancellationTokenSource;
        private HttpClient _httpClient;
        private SimpleGrpcWebClient _grpcClient = null!;
        private LfgClient _lfgClient = null!;
        private readonly Dictionary<string, GroupListRowPanel> _groupPanels = [];
        private readonly Dictionary<string, ApplicationPanel> _applicationPanels = [];
        private readonly LfgViewModel _viewModel;

        private LoadingSpinner? _groupsLoadingSpinner;
        private FlowPanel? _applicationsList;
        private LoadingSpinner? _applicationsLoadingSpinner;
        private Panel? _groupManagementPanel;
        private StandardButton? _createButton;
        private TextBox? _descriptionBox;
        private Panel? _requirementsPanel;
        private TextBox? _requirementsNumber;
        private Dropdown? _requirementsDropdown;
        private LandingView? _landingView;
        private Panel? _mainContentPanel;
        private bool _hasApiKey = false;

        public LfgView(HttpClient httpClient, LfgViewModel viewModel)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _httpClient = httpClient;
            _viewModel = viewModel;
            _hasApiKey = !string.IsNullOrEmpty(viewModel.ApiKey);

            if (_hasApiKey)
            {
                ReinitializeClients();
            }
        }

        protected override void Build(Container buildPanel)
        {

            _mainContentPanel = new Panel
            {
                Parent = buildPanel,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.Fill,
                Visible = _hasApiKey
            };

            _landingView = new LandingView
            {
                Parent = buildPanel,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.Fill,
                Visible = !_hasApiKey
            }.Build();

            BuildMainLayout(_mainContentPanel);
            ReinitializeClients();
            RegisterEventHandlers();

            UpdateVisibility();
        }

        private void ReinitializeClients()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            _grpcClient = new SimpleGrpcWebClient(
                _httpClient, _viewModel.ApiKey, _cancellationTokenSource.Token);
            _lfgClient = new LfgClient(_grpcClient);
        }

        private void UpdateVisibility()
        {
            if (_landingView != null)
                _landingView.Visible = !_hasApiKey;

            if (_mainContentPanel != null)
                _mainContentPanel.Visible = _hasApiKey;
        }

        private void BuildMainLayout(Container buildPanel)
        {
            var leftPanel = new GroupListPanel(_viewModel, _lfgClient)
            {
                Parent = buildPanel,
                Width = (int)(buildPanel.ContentRegion.Width * 0.6f),
            };

            var rightPanel = new Panel
            {
                Parent = buildPanel,
                Left = leftPanel.Right + PADDING,
                Width = buildPanel.ContentRegion.Width - leftPanel.Width - PADDING,
                HeightSizingMode = SizingMode.Fill,
                ShowBorder = true,
            };

            buildPanel.Resized += (s, e) =>
            {
                leftPanel.Width = (int)(buildPanel.ContentRegion.Width * 0.6f);
                rightPanel.Left = leftPanel.Right + PADDING;
                rightPanel.Width = buildPanel.ContentRegion.Width - leftPanel.Width - PADDING;
            };

            BuildManagementPanel(rightPanel);
        }

        private void BuildManagementPanel(Panel parent)
        {
            _groupManagementPanel = new Panel
            {
                Parent = parent,
                Width = parent.Width - PADDING,
                Height = parent.Height - (PADDING * 2),
            };
            parent.Resized += (s, e) =>
            {
                _groupManagementPanel.Width = parent.Width - PADDING;
                _groupManagementPanel.Height = parent.Height - (PADDING * 2);
            };

            RefreshManagementPanel();
        }

        private void RefreshManagementPanel()
        {
            _groupManagementPanel!.ClearChildren();

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
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.Fill,
                Title = "Create Group",
            };

            BuildGroupInputs(panel);

            _createButton = new StandardButton
            {
                Parent = panel,
                Text = "Create Group",
                Width = 120,
                Height = 30,
                Top = _requirementsPanel!.Bottom + PADDING,
                Left = (panel.Width - 120) / 2,
            };
            panel.Resized += (s, e) =>
            {
                _createButton.Left = (panel.Width - 120) / 2;
                _createButton.Top = _requirementsPanel.Bottom + PADDING;
            };

            _createButton.Click += async (s, e) =>
            {
                _createButton.Enabled = false;
                try
                {
                    await CreateGroupAsync(
                        _descriptionBox?.Text ?? "",
                        _requirementsNumber?.Text ?? "",
                        _requirementsDropdown?.SelectedItem ?? ""
                    );
                }
                finally
                {
                    _createButton.Enabled = true;
                }
            };
        }

        private void BuildGroupManagementPanel()
        {
            var panel = new Panel
            {
                Parent = _groupManagementPanel,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.Fill,
                Title = "Manage Group",
            };

            BuildGroupInputs(panel);

            var buttonPanel = new Panel
            {
                Parent = panel,
                WidthSizingMode = SizingMode.Fill,
                Height = 40,
                Top = _requirementsPanel!.Bottom + PADDING,
            };
            panel.Resized += (s, e) =>
            {
                buttonPanel.Top = _requirementsPanel.Bottom + PADDING;
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
            buttonPanel.Resized += (s, e) =>
            {
                updateButton.Left = (buttonPanel.Width - 210) / 2;
                closeButton.Left = updateButton.Right + PADDING;
            };

            updateButton.Click += async (s, e) =>
            {
                updateButton.Enabled = false;
                closeButton.Enabled = false;
                try
                {
                    await UpdateGroupAsync(
                                   _viewModel?.MyGroup?.Id ?? "",
                                   _descriptionBox?.Text ?? "",
                                   _requirementsNumber?.Text ?? "",
                                   _requirementsDropdown?.SelectedItem ?? ""
                               );
                }
                finally
                {
                    updateButton.Enabled = true;
                    closeButton.Enabled = true;
                }
            };
            closeButton.Click += async (s, e) =>
            {
                updateButton.Enabled = false;
                closeButton.Enabled = false;
                try
                {
                    await CloseGroupAsync(_viewModel?.MyGroup?.Id ?? "");
                }
                finally
                {
                    updateButton.Enabled = true;
                    closeButton.Enabled = true;
                }
            };

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
            parent.Resized += (s, e) =>
            {
                _descriptionBox.Width = parent.Width - (PADDING * 2);
                _requirementsPanel.Width = parent.Width - (PADDING * 2);
                _requirementsPanel.Top = _descriptionBox.Bottom + PADDING;
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
            _requirementsPanel.Resized += (s, e) =>
            {
                _requirementsNumber.Left = _requirementsPanel.Width - 150;
            };

            _requirementsDropdown = new Dropdown
            {
                Parent = _requirementsPanel,
                Width = 90,
                Height = 30,
                Left = _requirementsPanel.Width - 90,
            };
            _requirementsPanel.Resized += (s, e) =>
            {
                _requirementsDropdown.Left = _requirementsPanel.Width - 90;
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
            parent.Resized += (s, e) =>
            {
                applicationsLabel.Width = parent.Width - (PADDING * 2);
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
            parent.Resized += (s, e) =>
            {
                _applicationsList.Top = applicationsLabel.Bottom + PADDING;
                _applicationsList.Height = parent.Height - applicationsLabel.Bottom - (PADDING * 2);
                _applicationsList.Width = parent.Width - (PADDING * 2);
            };

            _applicationPanels.Clear();
            _applicationsLoadingSpinner = new LoadingSpinner
            {
                Parent = parent,
                Location = new Point(
                    _applicationsList.Left + (_applicationsList.Width - 64) / 2,
                    _applicationsList.Top + (_applicationsList.Height - 64) / 2
                ),
                Visible = _viewModel.IsLoadingApplications,
                ZIndex = _applicationsList.ZIndex + 1,
            };
            parent.Resized += (s, e) =>
            {
                _applicationsLoadingSpinner.Location = new Point(
                    _applicationsList.Left + (_applicationsList.Width - 64) / 2,
                    _applicationsList.Top + (_applicationsList.Height - 64) / 2
                );
            };

            foreach (var application in _viewModel.GroupApplications)
            {
                CreateApplicationPanel(_applicationsList, application);
            }

            _applicationsList.SortChildren<ApplicationPanel>((a, b) =>
            {
                var appA = a;
                var appB = b;
                return -HasEnoughKillProof(appA.Application.KillProof, _viewModel.MyGroup)
                    .CompareTo(HasEnoughKillProof(appB.Application.KillProof, _viewModel.MyGroup));
            });
        }

        private void RegisterEventHandlers()
        {
            _viewModel.ApiKeyChanged += OnApiKeyChanged;
            _viewModel.MyGroupChanged += OnMyGroupChanged;
            _viewModel.GroupApplicationsChanged += OnGroupApplicationsChanged;
            _viewModel.VisibleChanged += OnVisibleChanged;
            _viewModel.IsLoadingGroupsChanged += OnIsGroupsLoadingChanged;
            _viewModel.IsLoadingApplicationsChanged += OnIsApplicationsLoadingChanged;
        }

        private void UnregisterEventHandlers()
        {
            _viewModel.ApiKeyChanged -= OnApiKeyChanged;
            _viewModel.MyGroupChanged -= OnMyGroupChanged;
            _viewModel.GroupApplicationsChanged -= OnGroupApplicationsChanged;
            _viewModel.VisibleChanged -= OnVisibleChanged;
            _viewModel.IsLoadingGroupsChanged -= OnIsGroupsLoadingChanged;
            _viewModel.IsLoadingApplicationsChanged -= OnIsApplicationsLoadingChanged;
        }

        private void OnApiKeyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            _hasApiKey = !string.IsNullOrEmpty(_viewModel.ApiKey);
            ReinitializeClients();
            UpdateVisibility();
        }

        private void OnMyGroupChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            RefreshManagementPanel();
        }

        private void OnGroupApplicationsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            RefreshApplicationsList();
        }

        private void OnVisibleChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // TODO: Enable/Disable heartbeat and refresh logic
            if (_viewModel.Visible)
            {
                RefreshApplicationsList();
            }
        }

        private void OnIsGroupsLoadingChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_groupsLoadingSpinner == null) return;
            _groupsLoadingSpinner.Visible = _viewModel.IsLoadingGroups;
        }

        private void OnIsApplicationsLoadingChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_applicationsLoadingSpinner == null) return;
            _applicationsLoadingSpinner.Visible = _viewModel.IsLoadingApplications;
        }

        private void RefreshApplicationsList()
        {
            if (_viewModel.MyGroup == null)
            {
                return;
            }
            if (_applicationsList == null) return;

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

            _applicationsList.SortChildren<ApplicationPanel>((a, b) =>
            {
                var appA = a;
                var appB = b;
                return -HasEnoughKillProof(appA.Application.KillProof, _viewModel.MyGroup)
                    .CompareTo(HasEnoughKillProof(appB.Application.KillProof, _viewModel.MyGroup));
            });
        }

        private void PopulateKillProofDropdown()
        {
            _requirementsDropdown!.Items.Add("");
            _requirementsDropdown.Items.Add("LI");
            _requirementsDropdown.Items.Add("UFE");
            _requirementsDropdown.Items.Add("BSKP");
            _requirementsDropdown.SelectedItem = KillProof.FormatId(
                _viewModel.MyGroup?.KillProofId ?? Proto.KillProofId.KpUnknown
            );
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

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

            UnregisterEventHandlers();
        }

        private static string FormatKillProofDetails(Proto.KillProof kp)
        {
            if (kp == null)
            {
                return "No KillProof.me data available";
            }
            return $"LI: {kp.Li}     UFE: {kp.Ufe}     BSKP: {kp.Bskp} \n" +
                   $"W1: {kp.W1}     W2:  {kp.W2}\n" +
                   $"W3: {kp.W3}     W4:  {kp.W4}\n" +
                   $"W5: {kp.W5}     W6:  {kp.W6}\n" +
                   $"W7: {kp.W7}     W8:  {kp.W8}";
        }

        private static bool HasEnoughKillProof(Proto.KillProof kp, Proto.Group? group)
        {
            if (group == null || group.KillProofMinimum == 0 || group.KillProofId == Proto.KillProofId.KpUnknown)
            {
                return true;
            }

            switch (group.KillProofId)
            {
                case Proto.KillProofId.KpLi:
                    return kp.Li >= group.KillProofMinimum;
                case Proto.KillProofId.KpUfe:
                    return kp.Ufe >= group.KillProofMinimum;
                case Proto.KillProofId.KpBskp:
                    return kp.Bskp >= group.KillProofMinimum;
                default:
                    return false;
            }
        }

        private ApplicationPanel CreateApplicationPanel(FlowPanel parent, Proto.GroupApplication application)
        {
            var panel = new ApplicationPanel(application)
            {
                Parent = parent,
                Height = 50,
                Width = parent.Width - PADDING,
                ShowBorder = true,
            };
            parent.Resized += (s, e) =>
            {
                panel.Width = parent.Width - PADDING;
            };

            var applicantInfo = new Panel
            {
                Parent = panel,
                Left = PADDING,
                HeightSizingMode = SizingMode.Fill,
                Width = panel.Width - 120
            };
            panel.Resized += (s, e) =>
            {
                applicantInfo.Width = panel.Width - 120;
            };

            var nameLabel = new Label
            {
                Parent = applicantInfo,
                Text = application.AccountName,
                Height = 30,
                Top = (applicantInfo.Height - 30) / 2,
                Width = applicantInfo.Width,
                BasicTooltipText = FormatKillProofDetails(application.KillProof),
                Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size16, ContentService.FontStyle.Regular)
            };
            applicantInfo.Resized += (s, e) =>
            {
                nameLabel.Top = (applicantInfo.Height - 30) / 2;
                nameLabel.Width = applicantInfo.Width;
            };

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

            var inviteButton = new StandardButton
            {
                Parent = buttonPanel,
                Text = "Invite",
                Width = 100,
                Height = 30,
                Top = (buttonPanel.Height - 30) / 2
            };
            buttonPanel.Resized += (s, e) =>
            {
                inviteButton.Top = (buttonPanel.Height - 30) / 2;
            };

            inviteButton.Click += (s, e) =>
            {
                try
                {
                    GameService.GameIntegration.Chat.Send($"/sqinvite {application.AccountName}");
                }
                catch (Exception ex)
                {
                    Notifications.ShowError($"Failed to invite player: {ex.Message}");
                }
            };

            var kpWarning = new Image(AsyncTexture2D.FromAssetId(107050))// Flag icon
            {
                Parent = panel,
                Size = new Point(30, 30),
                Right = buttonPanel.Left - PADDING,
                Top = (panel.Height - 40) / 2,
                Visible = !HasEnoughKillProof(application.KillProof, _viewModel.MyGroup!),
                BasicTooltipText = "Insufficient KillProof",
            };
            buttonPanel.Moved += (s, e) =>
            {
                kpWarning.Right = buttonPanel.Left - PADDING;
            };
            panel.Resized += (s, e) =>
            {
                kpWarning.Right = buttonPanel.Left - PADDING;
                kpWarning.Top = (panel.Height - 40) / 2;
            };

            return panel;
        }

        private async Task CreateGroupAsync(string description, string minKpText, string kpIdText)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(description))
                {
                    Notifications.ShowError("Please enter a group description");
                    return;
                }

                uint.TryParse(minKpText, out uint minKp);
                var kpId = KillProof.ParseId(kpIdText);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _lfgClient.CreateGroup(
                    description.Trim(),
                    minKp,
                    kpId,
                    cts.Token
                );
            }
            catch (Exception ex)
            {
                Notifications.ShowError($"Failed to create group: {ex.Message}");
            }
        }

        private async Task UpdateGroupAsync(string groupId, string description, string minKpText, string kpIdText)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(description))
                {
                    return;
                }

                uint.TryParse(minKpText, out uint minKp);
                var kpId = KillProof.ParseId(kpIdText);

                var updatedGroup = new Proto.Group
                {
                    Id = groupId,
                    Title = description.Trim(),
                    KillProofMinimum = minKp,
                    KillProofId = kpId,
                };

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _lfgClient.UpdateGroup(updatedGroup, cts.Token);
            }
            catch (Exception ex)
            {
                Notifications.ShowError($"Failed to update group: {ex.Message}");
            }
        }

        private async Task CloseGroupAsync(string groupId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(groupId))
                {
                    return;
                }

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _lfgClient.DeleteGroup(groupId, cts.Token);
            }
            catch (Exception ex)
            {
                Notifications.ShowError($"Failed to close group: {ex.Message}");
            }
        }
    }
}