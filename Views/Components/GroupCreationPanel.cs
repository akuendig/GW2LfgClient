using Blish_HUD;
using Blish_HUD.Controls;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Gw2Lfg.Components
{
    public class GroupCreationPanel : Panel
    {
        private const int PADDING = 10;
        private readonly LfgClient _lfgClient;
        private TextBox? _descriptionBox;
        private Panel? _requirementsPanel;
        private TextBox? _requirementsNumber;
        private Dropdown? _requirementsDropdown;

        public GroupCreationPanel(LfgClient lfgClient)
        {
            _lfgClient = lfgClient;
            Title = "Create Group";
            BuildUI();
        }

        private void BuildUI()
        {
            BuildGroupInputs();
            BuildCreateButton();
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
                PlaceholderText = "Group Description"
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
                PlaceholderText = "0"
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

        private void PopulateKillProofDropdown()
        {
            _requirementsDropdown!.Items.Add("");
            _requirementsDropdown.Items.Add("LI");
            _requirementsDropdown.Items.Add("UFE");
            _requirementsDropdown.Items.Add("BSKP");
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
    }
}
