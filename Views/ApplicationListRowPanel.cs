#nullable enable

using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using System;

namespace Gw2Lfg
{
    public class ApplicationListRowPanel : Panel
    {
        public Proto.Group Group { get; set; }
        public Proto.GroupApplication Application { get; set; }

        public ApplicationListRowPanel(Proto.GroupApplication application, Proto.Group group)
        {
            Group = group;
            Application = application;
            Height = 50;
            ShowBorder = true;

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
        }

        public bool HasEnoughKillProof()
        {
            if (Group == null || Group.KillProofMinimum == 0 || Group.KillProofId == Proto.KillProofId.KpUnknown)
            {
                return true;
            }

            return Group.KillProofId switch
            {
                Proto.KillProofId.KpLi => Application.KillProof.Li >= Group.KillProofMinimum,
                Proto.KillProofId.KpUfe => Application.KillProof.Ufe >= Group.KillProofMinimum,
                Proto.KillProofId.KpBskp => Application.KillProof.Bskp >= Group.KillProofMinimum,
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