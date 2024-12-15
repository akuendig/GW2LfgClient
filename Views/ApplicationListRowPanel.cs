#nullable enable

using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Gw2Lfg.Proto;
using Microsoft.Xna.Framework;
using System;

namespace Gw2Lfg
{
    public class ApplicationListRowPanel : Panel
    {
        private static readonly Logger Logger = Logger.GetLogger<ApplicationListRowPanel>();
        private const int PADDING = 10;
        private readonly System.Timers.Timer _statusUpdateTimer;
        private Label _statusLabel;

        private readonly Group _group;
        public GroupApplication Application { get; private set; }
        public DateTimeOffset LastUpdated { get; private set; }

        public ApplicationListRowPanel(GroupApplication application, Group group)
        {
            _group = group;
            Application = application;
            Height = 50;
            ShowBorder = true;

            _statusUpdateTimer = new System.Timers.Timer(10000); // 10 second interval
            _statusUpdateTimer.Elapsed += (s, e) => UpdateStatus(Application);
            _statusUpdateTimer.Start();

            BuildLayout(application);
        }

        private void BuildLayout(GroupApplication application)
        {
            var applicantInfo = new Panel
            {
                Parent = this,
                Left = 10,
                HeightSizingMode = SizingMode.Fill,
                Width = Width - 120
            };
            Resized += (s, e) =>
            {
                applicantInfo.Width = Width - 120;
            };

            var nameLabel = new Label
            {
                Parent = applicantInfo,
                Text = application.AccountName,
                Height = 30,
                Top = (applicantInfo.Height - 30) / 2,
                Width = applicantInfo.Width,
                BasicTooltipText = application.AccountName + "\n\n" + FormatKillProofDetails(application.KillProof),
                Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size16, ContentService.FontStyle.Regular)
            };
            applicantInfo.Resized += (s, e) =>
            {
                nameLabel.Top = (applicantInfo.Height - 30) / 2;
                nameLabel.Width = applicantInfo.Width;
            };

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

            var kpWarning = new Image(AsyncTexture2D.FromAssetId(107050)) // Flag icon
            {
                Parent = this,
                Size = new Point(30, 30),
                Right = buttonPanel.Left - 10,
                Top = (Height - 40) / 2,
                Visible = !HasEnoughKillProof(),
                BasicTooltipText = "Insufficient KillProof",
            };
            buttonPanel.Moved += (s, e) =>
            {
                kpWarning.Right = buttonPanel.Left - 10;
            };
            Resized += (s, e) =>
            {
                kpWarning.Right = buttonPanel.Left - 10;
                kpWarning.Top = (Height - 40) / 2;
            };

            var statusPanel = new Panel
            {
                Parent = this,
                Top = 5,
                Left = applicantInfo.Right + PADDING,
                Width = 100,
                Height = applicantInfo.Height,
            };
            applicantInfo.Resized += (s, e) =>
            {
                statusPanel.Left = applicantInfo.Right + PADDING;
                statusPanel.Height = applicantInfo.Height;
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
            statusPanel.Resized += (s, e) =>
            {
                _statusLabel.Width = statusPanel.Width;
                _statusLabel.Top = (statusPanel.Height - 30) / 2;
            };

            UpdateStatus(application);
        }

        private void UpdateStatus(Proto.GroupApplication application)
        {
            if (_statusLabel == null) return;

            var timeSinceUpdate = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(application.UpdatedAtSec);
            var text = timeSinceUpdate.TotalMinutes switch
            {
                < 1 => "Active now",
                < 2 => "1m ago",
                < 60 => $"{(int)timeSinceUpdate.TotalMinutes}m ago",
                < 120 => "1h ago",
                _ => $"{(int)timeSinceUpdate.TotalHours}h ago"
            };
            _statusLabel.Text = text;

            LastUpdated = DateTimeOffset.UtcNow;
        }

        public void Update(GroupApplication updatedApplication)
        {
            Application = updatedApplication;

            UpdateStatus(updatedApplication);
        }

        protected override void DisposeControl() 
        {
            _statusUpdateTimer?.Dispose();
            base.DisposeControl();
        }

        public bool HasEnoughKillProof()
        {
            if (_group == null || _group.KillProofMinimum == 0 || _group.KillProofId == KillProofId.KpUnknown)
            {
                return true;
            }

            return _group.KillProofId switch
            {
                KillProofId.KpLi => Application.KillProof.Li >= _group.KillProofMinimum,
                KillProofId.KpUfe => Application.KillProof.Ufe >= _group.KillProofMinimum,
                KillProofId.KpBskp => Application.KillProof.Bskp >= _group.KillProofMinimum,
                _ => false,
            };
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
    }
}