#nullable enable

using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Gw2Lfg
{
    public class ApplicationListPanel : Panel
    {
        private readonly LfgViewModel _viewModel;
        private readonly Dictionary<string, ApplicationListRowPanel> _applicationPanels = new();
        private FlowPanel _applicationsPanel;
        private LoadingSpinner? _applicationsLoadingSpinner;

        public ApplicationListPanel(LfgViewModel viewModel)
        {
            _viewModel = viewModel;
            ShowBorder = true;
            HeightSizingMode = SizingMode.Fill;
            WidthSizingMode = SizingMode.Fill;

            _viewModel.GroupApplicationsChanged += OnGroupApplicationsChanged;
            _viewModel.IsLoadingApplicationsChanged += OnIsApplicationsLoadingChanged;

            BuildApplicationsList();
        }

        private void BuildApplicationsList()
        {
            _applicationPanels.Clear();
            _applicationsLoadingSpinner = new LoadingSpinner
            {
                Parent = this,
                Location = new Point((Width - 64) / 2, (Height - 64) / 2),
                Visible = _viewModel.IsLoadingApplications,
                ZIndex = ZIndex + 1,
            };

            Resized += (s, e) =>
            {
                _applicationsLoadingSpinner.Location = new Point((Width - 64) / 2, (Height - 64) / 2);
            };

            _applicationsPanel = new FlowPanel
            {
                Parent = this,
                Width = Width - 10,
                Height = Height - 10,
                Top = 5,
                Left = 5,
                CanScroll = true,
            };
            Resized += (s, e) =>
            {
                _applicationsPanel.Width = Width - 10;
                _applicationsPanel.Height = Height - 10;
            };

            foreach (var application in _viewModel.GroupApplications)
            {
                CreateApplicationPanel(application);
            }

            _applicationsPanel.SortChildren<ApplicationListRowPanel>((a, b) =>
            {
                var appA = a;
                var appB = b;
                return -HasEnoughKillProof(appA.Application.KillProof, _viewModel.MyGroup)
                    .CompareTo(HasEnoughKillProof(appB.Application.KillProof, _viewModel.MyGroup));
            });
        }

        private void OnGroupApplicationsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            RefreshApplicationsList();
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
                    var panel = CreateApplicationPanel(application);
                    _applicationPanels[application.Id] = panel;
                }
            }

            _applicationsPanel.SortChildren<ApplicationListRowPanel>((a, b) =>
            {
                var appA = a;
                var appB = b;
                return -HasEnoughKillProof(appA.Application.KillProof, _viewModel.MyGroup)
                    .CompareTo(HasEnoughKillProof(appB.Application.KillProof, _viewModel.MyGroup));
            });
        }

        private ApplicationListRowPanel CreateApplicationPanel(Proto.GroupApplication application)
        {
            var panel = new ApplicationListRowPanel(application)
            {
                Parent = _applicationsPanel,
                Height = 50,
                Width = _applicationsPanel.Width - 10,
                ShowBorder = true,
            };
            _applicationsPanel.Resized += (s, e) =>
            {
                panel.Width = Width - 10;
            };

            var applicantInfo = new Panel
            {
                Parent = panel,
                Left = 10,
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

            var kpWarning = new Image(AsyncTexture2D.FromAssetId(107050)) // Flag icon
            {
                Parent = panel,
                Size = new Point(30, 30),
                Right = buttonPanel.Left - 10,
                Top = (panel.Height - 40) / 2,
                Visible = !HasEnoughKillProof(application.KillProof, _viewModel.MyGroup!),
                BasicTooltipText = "Insufficient KillProof",
            };
            buttonPanel.Moved += (s, e) =>
            {
                kpWarning.Right = buttonPanel.Left - 10;
            };
            panel.Resized += (s, e) =>
            {
                kpWarning.Right = buttonPanel.Left - 10;
                kpWarning.Top = (panel.Height - 40) / 2;
            };

            return panel;
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

            return group.KillProofId switch
            {
                Proto.KillProofId.KpLi => kp.Li >= group.KillProofMinimum,
                Proto.KillProofId.KpUfe => kp.Ufe >= group.KillProofMinimum,
                Proto.KillProofId.KpBskp => kp.Bskp >= group.KillProofMinimum,
                _ => false,
            };
        }
    }
}