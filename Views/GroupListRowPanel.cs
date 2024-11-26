#nullable enable

using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Gw2Lfg
{
    public class GroupListRowPanel : Panel
    {
        private const int PADDING = 10;
        private readonly System.Timers.Timer _statusUpdateTimer;

        public Proto.Group Group { get; private set; }
        private readonly LfgClient _lfgClient;
        private Label _statusLabel;

        public GroupListRowPanel(Proto.Group group, LfgViewModel viewModel, LfgClient lfgClient)
        {
            Group = group;
            _lfgClient = lfgClient;

            // Update status every 10 seconds
            _statusUpdateTimer = new System.Timers.Timer(TimeSpan.FromSeconds(10).TotalMilliseconds);
            _statusUpdateTimer.Elapsed += (s, e) => UpdateStatus();
            _statusUpdateTimer.Start();

            BuildLayout(viewModel.AccountName);
        }

        private void BuildLayout(string accountName)
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
                    await ApplyToGroupAsync(Group.Id);
                }
                finally
                {
                    if (applyButton.Parent != null)
                    {
                        applyButton.Enabled = true;
                    }
                }
            };
        }

        public void Update(string accountName, Proto.Group updatedGroup)
        {
            Group = updatedGroup;
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

            var buttonPanel = (Panel)Children.Last();
            buttonPanel.Height = infoPanel.Height;
            var applyButton = buttonPanel.GetChildrenOfType<StandardButton>().First();
            applyButton.Top = (buttonPanel.Height - 30) / 2;
            var myGroupLabel = buttonPanel.GetChildrenOfType<Label>().First();
            myGroupLabel.Top = (buttonPanel.Height - 30) / 2;
            var isYourGroup = updatedGroup.CreatorId == accountName;
            applyButton.Visible = !isYourGroup;
            myGroupLabel.Visible = isYourGroup;

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

        public void UpdateStatus()
        {
            var lastHeartbeat = DateTimeOffset.FromUnixTimeSeconds(Group.UpdatedAtSec);
            var now = DateTimeOffset.UtcNow;
            var timeSinceHeartbeat = now - lastHeartbeat;

            if (timeSinceHeartbeat < TimeSpan.FromMinutes(2))
            {
                _statusLabel.Text = "Active";
                _statusLabel.TextColor = Color.Green;
            }
            else if (timeSinceHeartbeat < TimeSpan.FromMinutes(5))
            {
                _statusLabel.Text = "Away";
                _statusLabel.TextColor = Color.Yellow;
            }
            else
            {
                _statusLabel.Text = "Inactive";
                _statusLabel.TextColor = Color.Red;
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