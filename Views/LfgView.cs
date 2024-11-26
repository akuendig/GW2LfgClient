#nullable enable

using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using System;
using System.Threading.Tasks;
using Blish_HUD;
using System.Threading;

namespace Gw2Lfg
{
    public class LfgView(LfgClient lfgClient, LfgViewModel viewModel) : View, IDisposable
    {
        private const int PADDING = 10;
        private bool _disposed;
        private readonly LfgClient _lfgClient = lfgClient;
        private readonly LfgViewModel _viewModel = viewModel;

        private LandingView? _landingView;
        private Panel? _mainContentPanel;
        private bool _hasApiKey = !string.IsNullOrEmpty(viewModel.ApiKey);

        protected override void Build(Container buildPanel)
        {

            _mainContentPanel = new Panel
            {
                Parent = buildPanel,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.Fill,
                Visible = _hasApiKey
            };

            _landingView = new LandingView
            {
                Parent = buildPanel,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.Fill,
                Visible = !_hasApiKey
            }.Build();

            BuildMainLayout(_mainContentPanel);
            RegisterEventHandlers();

            UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            if (_landingView != null)
                _landingView.Visible = !_hasApiKey;

            if (_mainContentPanel != null)
                _mainContentPanel.Visible = _hasApiKey;
        }

        private void BuildMainLayout(Container buildPanel)
        {
            var groupListPanel = new GroupListPanel(_viewModel, _lfgClient)
            {
                Parent = buildPanel,
                Width = (int)(buildPanel.ContentRegion.Width * 0.6f),
            };
            var groupManagementPanel = new GroupManagementPanel(_lfgClient, _viewModel)
            {
                Parent = buildPanel,
                Left = groupListPanel.Right + PADDING,
                Width = buildPanel.ContentRegion.Width - groupListPanel.Width - PADDING,
                HeightSizingMode = SizingMode.Fill,
                ShowBorder = true,
            };
            buildPanel.Resized += (s, e) =>
            {
                groupListPanel.Width = (int)(buildPanel.ContentRegion.Width * 0.6f);
                groupListPanel.Width = buildPanel.ContentRegion.Width - groupListPanel.Width - PADDING;
            };
        }

        private void RegisterEventHandlers()
        {
            _viewModel.ApiKeyChanged += OnApiKeyChanged;
        }

        private void UnregisterEventHandlers()
        {
            _viewModel.ApiKeyChanged -= OnApiKeyChanged;
        }

        private void OnApiKeyChanged(object sender, LfgViewModelPropertyChangedEventArgs<string> e)
        {
            _hasApiKey = !string.IsNullOrEmpty(e.NewValue);
            UpdateVisibility();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            UnregisterEventHandlers();
        }
    }
}