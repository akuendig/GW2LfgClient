#nullable enable

using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Gw2Lfg
{
    public class GroupListRowPanel : Panel
    {
        private static readonly Logger Logger = Logger.GetLogger<GroupListRowPanel>();
        private const int PADDING = 10;
        private readonly System.Timers.Timer _statusUpdateTimer;

        public Proto.Group Group { get; private set; }
        public Proto.GroupApplication MyApplication { get; private set; }
        private readonly LfgClient _lfgClient;
        private Label _statusLabel;
        private StandardButton _applyButton;
        private StandardButton _cancelApplicationButton;
        private Label _myGroupLabel;
        public enum GroupStatus
        {
            Active,
            Away,
            Inactive
        }

        public GroupStatus Status { get; private set; }

        public GroupListRowPanel(Proto.Group group, LfgViewModel viewModel, LfgClient lfgClient)
        {
            Group = group;
            _lfgClient = lfgClient;

            // Update status every 10 seconds
            _statusUpdateTimer = new System.Timers.Timer(TimeSpan.FromSeconds(10).TotalMilliseconds);
            _statusUpdateTimer.Elapsed += (s, e) => UpdateStatus();
            _statusUpdateTimer.Start();

            var state = viewModel.State;
            MyApplication = state.MyApplications.FirstOrDefault(a => a.GroupId == group.Id);
            BuildLayout(state.AccountName, state.MyApplications);
        }

        private void BuildLayout(string accountName, ImmutableArray<Proto.GroupApplication> myApplications)
        {
            HeightSizingMode = SizingMode.AutoSize;
            ShowBorder = true;

            var infoPanel = new Panel
            {
                Parent = this,
                Left = PADDING,
                Top = 5,
                Width = Width - 2 * 100 - 3 * PADDING,
                HeightSizingMode = SizingMode.AutoSize
            };
            Resized += (s, e) =>
            {
                infoPanel.Width = Width - 2 * 100 - 3 * PADDING;
            };

            var titleLabel = new Label
            {
                Parent = infoPanel,
                Text = Group.Title,
                AutoSizeHeight = true,
                Width = infoPanel.Width,
                Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size16, ContentService.FontStyle.Regular)
            };
            infoPanel.Resized += (s, e) =>
            {
                titleLabel.Width = infoPanel.Width;
            };

            int height = titleLabel.Height;

            var kpRequirement = FormatKillProofRequirement(Group);
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
                Parent = this,
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

            _statusLabel = new Label
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
                _statusLabel.Top = (statusPanel.Height - 30) / 2;
            };
            UpdateStatus();

            var buttonPanel = new Panel
            {
                Parent = this,
                Left = Width - 110,
                Width = 100,
                HeightSizingMode = SizingMode.Fill,
            };
            Resized += (s, e) =>
            {
                buttonPanel.Left = Width - 110;
            };

            var isYourGroup = Group.CreatorId == accountName;
            _applyButton = new StandardButton
            {
                Parent = buttonPanel,
                Text = "Apply",
                Width = 100,
                Height = 30,
                Top = (buttonPanel.Height - 30) / 2,
                Visible = !isYourGroup && MyApplication == null,
            };
            _applyButton.Click += OnApplyButtonClickedApply;
            buttonPanel.Resized += (s, e) =>
            {
                _applyButton.Top = (buttonPanel.Height - 30) / 2;
            };
            _cancelApplicationButton = new StandardButton
            {
                Parent = buttonPanel,
                Text = "Cancel",
                Width = 100,
                Height = 30,
                Top = (buttonPanel.Height - 30) / 2,
                Visible = !isYourGroup && MyApplication != null,
            };
            _cancelApplicationButton.Click += OnApplyButtonClickedCancel;
            buttonPanel.Resized += (s, e) =>
            {
                _cancelApplicationButton.Top = (buttonPanel.Height - 30) / 2;
            };

            _myGroupLabel = new Label
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
                _myGroupLabel.Top = (buttonPanel.Height - 30) / 2;
            };
        }

        private async void OnApplyButtonClickedApply(object sender, EventArgs e)
        {

            _applyButton.Enabled = false;
            try
            {
                await ApplyToGroupAsync(Group.Id);
            }
            finally
            {
                _applyButton.Enabled = true;
            }
        }

        private async void OnApplyButtonClickedCancel(object sender, EventArgs e)
        {

            _applyButton.Enabled = false;
            try
            {
                await CancelApplicationAsync(Group.Id, MyApplication.Id);
            }
            finally
            {
                _applyButton.Enabled = true;
            }
        }

        public void Update(string accountName, Proto.Group updatedGroup, ImmutableArray<Proto.GroupApplication> myApplications)
        {
            Group = updatedGroup;
            MyApplication = myApplications.FirstOrDefault(a => a.GroupId == updatedGroup.Id);
            var infoPanel = (Panel)Children.First();
            var titleLabel = (Label)infoPanel.Children.First();
            titleLabel.Text = updatedGroup.Title;

            var kpRequirement = FormatKillProofRequirement(updatedGroup);
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

            var isYourGroup = updatedGroup.CreatorId == accountName;
            var buttonPanel = (Panel)Children.Last();
            buttonPanel.Height = infoPanel.Height;
            _applyButton.Top = (buttonPanel.Height - 30) / 2;
            _applyButton.Visible = !isYourGroup && MyApplication == null;
            _cancelApplicationButton.Top = (buttonPanel.Height - 30) / 2;
            _cancelApplicationButton.Visible = !isYourGroup && MyApplication != null;
            _myGroupLabel.Top = (buttonPanel.Height - 30) / 2;
            _myGroupLabel.Visible = isYourGroup;

            UpdateStatus();
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
            finally
            {
                Notifications.ShowInfo("Application submitted");
            }
        }

        private async Task CancelApplicationAsync(string groupId, string applicationId)
        {
            try
            {
                _cancelApplicationButton.Enabled = false;
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _lfgClient.DeleteGroupApplication(groupId, applicationId, cts.Token);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to cancel application");
            }
            finally
            {
                _cancelApplicationButton.Enabled = true;
            }
        }

        public void UpdateStatus()
        {
            var lastHeartbeat = DateTimeOffset.FromUnixTimeSeconds(Group.UpdatedAtSec);
            var now = DateTimeOffset.UtcNow;
            var timeSinceHeartbeat = now - lastHeartbeat;

            if (timeSinceHeartbeat < TimeSpan.FromMinutes(2))
            {
                _statusLabel.Text = "Active";
                _statusLabel.TextColor = Color.Green;
                Status = GroupStatus.Active;
            }
            else if (timeSinceHeartbeat < TimeSpan.FromMinutes(5))
            {
                _statusLabel.Text = "Away";
                _statusLabel.TextColor = Color.Yellow;
                Status = GroupStatus.Away;
            }
            else
            {
                _statusLabel.Text = "Inactive";
                _statusLabel.TextColor = Color.Red;
                Status = GroupStatus.Inactive;
            }
        }

        private static string FormatKillProofRequirement(Proto.Group group)
        {
            if (group.KillProofMinimum == 0 || group.KillProofId == Proto.KillProofId.KpUnknown)
            {
                return "";
            }
            return $"{group.KillProofMinimum} {KillProof.FormatId(group.KillProofId)}";
        }

        protected override void DisposeControl()
        {
            _statusUpdateTimer?.Dispose();
            base.DisposeControl();
        }
    }
}