using Blish_HUD.Controls;
using Gw2Lfg.Components;
using Microsoft.Xna.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Gw2Lfg
{
    public class GroupListPanel : Panel
    {
        private const int PADDING = 10;
        private readonly LfgViewModel _viewModel;
        private readonly LfgClient _lfgClient;
        private GroupFilterPanel? _filterPanel;
        private GroupListView? _groupListView;
        private LoadingSpinner? _loadingSpinner;

        public GroupListPanel(LfgViewModel viewModel, LfgClient lfgClient)
        {
            _viewModel = viewModel;
            _lfgClient = lfgClient;
            BuildUI();
        }

        private void BuildUI()
        {
            _filterPanel = new GroupFilterPanel
            {
                Parent = this
            };
            _filterPanel.FiltersChanged += (s, e) => UpdateGroupsList();

            _groupListView = new GroupListView(_viewModel)
            {
                Parent = this,
                Top = 50,
                Height = Height - 50
            };

            _loadingSpinner = new LoadingSpinner
            {
                Parent = this,
                Location = new Point(
                    (Width - 64) / 2,
                    (Height - 64) / 2
                ),
                Visible = _viewModel.IsLoadingGroups
            };

            this.Resized += (s, e) =>
            {
                _groupListView.Height = Height - 50;
                _loadingSpinner.Location = new Point(
                    (Width - 64) / 2,
                    (Height - 64) / 2
                );
            };
        }

        public void UpdateGroupsList()
        {
            if (_filterPanel == null || _groupListView == null) return;
            _groupListView.UpdateGroups(
                _viewModel.Groups,
                _filterPanel.SearchText,
                _filterPanel.ContentType
            );
        }

        public async Task ApplyToGroupAsync(string groupId)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _lfgClient.CreateGroupApplication(groupId, cts.Token);
                ScreenNotification.ShowNotification("Successfully applied", ScreenNotification.NotificationType.Info);
            }
            catch (Exception ex)
            {
                ScreenNotification.ShowNotification($"Failed to apply to group: {ex.Message}", ScreenNotification.NotificationType.Error);
            }
        }
    }
}
