
#nullable enable

using Blish_HUD.Controls;
using System;
using System.Threading;
using System.Threading.Tasks;
using Blish_HUD;

namespace Gw2Lfg
{
    public class GroupManagementPanel : Panel
    {
        private const int PADDING = 10;
        private readonly LfgClient _lfgClient;
        private readonly LfgViewModel _viewModel;

        private StandardButton? _createButton;
        private TextBox? _descriptionBox;
        private Panel? _requirementsPanel;
        private TextBox? _requirementsNumber;
        private Dropdown? _requirementsDropdown;
        private ApplicationListPanel? _applicationsList;

        public GroupManagementPanel(LfgClient lfgClient, LfgViewModel viewModel)
        {
            _lfgClient = lfgClient;
            _viewModel = viewModel;
            
            _viewModel.MyGroupChanged += OnMyGroupChanged;
            
            RefreshContent(_viewModel.MyGroup);
        }

        private void OnMyGroupChanged(object sender, LfgViewModelPropertyChangedEventArgs<Proto.Group?> e)
        {
            RefreshContent(e.NewValue);
        }

        private void RefreshContent(Proto.Group? group)
        {
            ClearChildren();

            if (group == null)
            {
                BuildCreateGroupPanel();
            }
            else
            {
                BuildGroupManagementPanel(group);
            }
        }

        private void BuildCreateGroupPanel()
        {
            var panel = new Panel
            {
                Parent = this,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.Fill,
                Title = "Create Group",
            };

            BuildGroupInputs(panel, null);

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

        private void BuildGroupManagementPanel(Proto.Group group)
        {
            var panel = new Panel
            {
                Parent = this,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.Fill,
                Title = "Manage Group",
            };

            BuildGroupInputs(panel, group);

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
                                   group.Id,
                                   _descriptionBox?.Text ?? "",
                                   _requirementsNumber?.Text ?? "",
                                   _requirementsDropdown?.SelectedItem ?? ""
                               );
                }
                catch (Exception ex)
                {
                    Notifications.ShowError($"Failed to update group: {ex.Message}");
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
                    await CloseGroupAsync(group.Id);
                }
                catch (Exception ex)
                {
                    Notifications.ShowError($"Failed to close group: {ex.Message}");
                }
                finally
                {
                    updateButton.Enabled = true;
                    closeButton.Enabled = true;
                }
            };

            BuildApplicationsList(panel, buttonPanel.Bottom + PADDING);
        }

        private void BuildGroupInputs(Panel parent, Proto.Group? group)
        {
            _descriptionBox = new TextBox
            {
                Parent = parent,
                Width = parent.Width - (PADDING * 2),
                Height = 30,
                Left = PADDING,
                Top = PADDING,
                PlaceholderText = "Group Description",
                Text = group?.Title ?? "",
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
                Text = group?.KillProofMinimum.ToString() ?? "",
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

            PopulateKillProofDropdown(group);
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

        private void PopulateKillProofDropdown(Proto.Group? myGroup)
        {
            _requirementsDropdown!.Items.Add("");
            _requirementsDropdown.Items.Add("LI");
            _requirementsDropdown.Items.Add("UFE");
            _requirementsDropdown.Items.Add("BSKP");
            _requirementsDropdown.SelectedItem = KillProof.FormatId(
                myGroup?.KillProofId ?? Proto.KillProofId.KpUnknown
            );
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

        protected override void DisposeControl()
        {
            _viewModel.MyGroupChanged -= OnMyGroupChanged;
            base.DisposeControl();
        }
    }
}