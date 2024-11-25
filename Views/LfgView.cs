#nullable enable

using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using System;
using System.Threading.Tasks;
using Blish_HUD;
using System.Net.Http;
using System.Threading;

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
        private readonly LfgViewModel _viewModel;

        private ApplicationListPanel? _applicationsList;
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

            _applicationsList = new ApplicationListPanel(_viewModel)
            {
                Parent = parent,
                Top = applicationsLabel.Bottom + PADDING,
                Height = parent.Height - applicationsLabel.Bottom - (PADDING * 2),
                Width = parent.Width - (PADDING * 2),
                Left = PADDING,
            };
            parent.Resized += (s, e) =>
            {
                _applicationsList.Top = applicationsLabel.Bottom + PADDING;
                _applicationsList.Height = parent.Height - applicationsLabel.Bottom - (PADDING * 2);
                _applicationsList.Width = parent.Width - (PADDING * 2);
            };
        }

        private void RegisterEventHandlers()
        {
            _viewModel.ApiKeyChanged += OnApiKeyChanged;
            _viewModel.MyGroupChanged += OnMyGroupChanged;
        }

        private void UnregisterEventHandlers()
        {
            _viewModel.ApiKeyChanged -= OnApiKeyChanged;
            _viewModel.MyGroupChanged -= OnMyGroupChanged;
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

            UnregisterEventHandlers();
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