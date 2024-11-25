#nullable enable

using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;

namespace Gw2Lfg
{
    public class ApplicationListPanel : Panel
    {
        private readonly LfgViewModel _viewModel;
        private readonly Dictionary<string, ApplicationListRowPanel> _applicationPanels = new();
        private FlowPanel _applicationsFlowPanel;
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
            RefreshApplicationsList();
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

            _applicationsFlowPanel = new FlowPanel
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
                _applicationsFlowPanel.Width = Width - 10;
                _applicationsFlowPanel.Height = Height - 10;
            };
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
                    var panel = new ApplicationListRowPanel(application, _viewModel.MyGroup)
                    {
                        Parent = _applicationsFlowPanel,
                        Width = _applicationsFlowPanel.Width - 10,
                    };
                    _applicationsFlowPanel.Resized += (s, e) =>
                    {
                        panel.Width = _applicationsFlowPanel.Width - 10;
                    };
                    _applicationPanels[application.Id] = panel;
                }
            }

            _applicationsFlowPanel.SortChildren<ApplicationListRowPanel>((a, b) =>
            {
                return -a.HasEnoughKillProof().CompareTo(b.HasEnoughKillProof());
            });
        }
    }
}