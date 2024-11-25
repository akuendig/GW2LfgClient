using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Gw2Lfg
{
    public class GroupManagementPanel : Panel
    {
        private const int PADDING = 10;
        private readonly LfgViewModel _viewModel;
        private readonly LfgClient _lfgClient;
        private TextBox? _descriptionBox;
        private Panel? _requirementsPanel;
        private TextBox? _requirementsNumber;
        private Dropdown? _requirementsDropdown;
        private FlowPanel? _applicationsList;
        private LoadingSpinner? _applicationsLoadingSpinner;

        public GroupManagementPanel(LfgViewModel viewModel, LfgClient lfgClient)
        {
            _viewModel = viewModel;
            _lfgClient = lfgClient;
            Title = _viewModel.MyGroup == null ? "Create Group" : "Manage Group";
            BuildUI();
        }

        private void BuildUI()
        {
            BuildGroupInputs();
            if (_viewModel.MyGroup != null)
            {
                BuildManagementButtons();
                BuildApplicationsList();
            }
            else
            {
                BuildCreateButton();
            }
        }

        private void BuildGroupInputs()
        {
            _descriptionBox = new TextBox
            {
                Parent = this,
                Width = Width - (PADDING * 2),
                Height = 30,
                Left = PADDING,
                Top = PADDING,
                PlaceholderText = "Group Description",
                Text = _viewModel.MyGroup?.Title ?? "",
            };

            _requirementsPanel = new Panel
            {
                Parent = this,
                Width = Width - (PADDING * 2),
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

        private void BuildCreateButton()
        {
            var createButton = new StandardButton
            {
                Parent = this,
                Text = "Create Group",
                Width = 120,
                Height = 30,
                Top = _requirementsPanel!.Bottom + PADDING,
                Left = (Width - 120) / 2,
            };

            createButton.Click += async (s, e) =>
            {
                createButton.Enabled = false;
                try
                {
                    await CreateGroupAsync();
                }
                finally
                {
                    createButton.Enabled = true;
                }
            };
        }

        private void BuildManagementButtons()
        {
            var buttonPanel = new Panel
            {
                Parent = this,
                WidthSizingMode = SizingMode.Fill,
                Height = 40,
                Top = _requirementsPanel!.Bottom + PADDING,
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

            updateButton.Click += async (s, e) =>
            {
                updateButton.Enabled = false;
                closeButton.Enabled = false;
                try
                {
                    await UpdateGroupAsync();
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
                    await CloseGroupAsync();
                }
                finally
                {
                    updateButton.Enabled = true;
                    closeButton.Enabled = true;
                }
            };
        }

        private void BuildApplicationsList()
        {
            var applicationsLabel = new Label
            {
                Parent = this,
                Text = "Applications",
                Top = _requirementsPanel!.Bottom + 50,
                Width = Width - (PADDING * 2),
                Left = PADDING,
                Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size16, ContentService.FontStyle.Regular),
            };

            _applicationsList = new FlowPanel
            {
                Parent = this,
                Top = applicationsLabel.Bottom + PADDING,
                Height = Height - applicationsLabel.Bottom - (PADDING * 2),
                Width = Width - (PADDING * 2),
                Left = PADDING,
                FlowDirection = ControlFlowDirection.TopToBottom,
                ControlPadding = new Vector2(0, 5),
                ShowBorder = true,
            };

            _applicationsLoadingSpinner = new LoadingSpinner
            {
                Parent = this,
                Location = new Point(
                    _applicationsList.Left + (_applicationsList.Width - 64) / 2,
                    _applicationsList.Top + (_applicationsList.Height - 64) / 2
                ),
                Visible = _viewModel.IsLoadingApplications,
            };
        }

        private void PopulateKillProofDropdown()
        {
            _requirementsDropdown!.Items.Add("");
            _requirementsDropdown.Items.Add("LI");
            _requirementsDropdown.Items.Add("UFE");
            _requirementsDropdown.Items.Add("BSKP");
            _requirementsDropdown.SelectedItem = FormatKillProofId(
                _viewModel.MyGroup?.KillProofId ?? Proto.KillProofId.KpUnknown
            );
        }

        private async Task CreateGroupAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_descriptionBox?.Text))
                {
                    ShowError("Please enter a group description");
                    return;
                }

                uint.TryParse(_requirementsNumber?.Text, out uint minKp);
                var kpId = ParseKillProofId(_requirementsDropdown?.SelectedItem ?? "");

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _lfgClient.CreateGroup(
                    _descriptionBox.Text.Trim(),
                    minKp,
                    kpId,
                    cts.Token
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
                if (_viewModel.MyGroup == null || string.IsNullOrWhiteSpace(_descriptionBox?.Text))
                {
                    return;
                }

                uint.TryParse(_requirementsNumber?.Text, out uint minKp);
                var kpId = ParseKillProofId(_requirementsDropdown?.SelectedItem ?? "");

                var updatedGroup = new Proto.Group
                {
                    Id = _viewModel.MyGroup.Id,
                    Title = _descriptionBox.Text.Trim(),
                    KillProofMinimum = minKp,
                    KillProofId = kpId,
                };

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _lfgClient.UpdateGroup(updatedGroup, cts.Token);
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

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _lfgClient.DeleteGroup(_viewModel.MyGroup.Id, cts.Token);
            }
            catch (Exception ex)
            {
                ShowError($"Failed to close group: {ex.Message}");
            }
        }

        private static void ShowError(string message)
        {
            ScreenNotification.ShowNotification(message, ScreenNotification.NotificationType.Error);
        }

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
    }
}
