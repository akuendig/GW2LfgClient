using Blish_HUD;
using Blish_HUD.Modules;
using Blish_HUD.Settings;
using Blish_HUD.Input;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using Blish_HUD.Content;
using Blish_HUD.Modules.Managers;
using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using System.Net.Http;
using System.Threading;

namespace Gw2Lfg
{
    [Export(typeof(Module))]
    public class Gw2LfgModule : Module
    {
        private static readonly Logger Logger = Logger.GetLogger<Gw2LfgModule>();

        private ContentsManager ContentsManager => ModuleParameters.ContentsManager;
        private SettingsManager SettingsManager => ModuleParameters.SettingsManager;
        private DirectoriesManager DirectoriesManager => ModuleParameters.DirectoriesManager;
        private Gw2ApiManager Gw2ApiManager => ModuleParameters.Gw2ApiManager;

        private CornerIcon _moduleIcon;
        private StandardWindow _lfgWindow;
        private SettingEntry<string> _serverAddressSetting;
        private readonly HttpClient _httpClient = new()
        {
            // Set high timeout for server streaming requests.
            Timeout = TimeSpan.FromHours(1),
        };
        private readonly SimpleGrpcWebClient _grpcClient;
        private readonly LfgClient _lfgClient;
        private LfgView _lfgView;
        private readonly LfgViewModel _viewModel;

        [ImportingConstructor]
        public Gw2LfgModule([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters)
        {
            _grpcClient = new SimpleGrpcWebClient(_httpClient, "", CancellationToken.None);
            _lfgClient = new LfgClient(_grpcClient);
            _viewModel = new LfgViewModel(_httpClient);
        }

        protected override void DefineSettings(SettingCollection settings)
        {
            _serverAddressSetting = settings.DefineSetting(
                "serverAddress",
                "http://127.0.0.1:50051",
                () => "Server Address",
                () => "The address of the backend server used to coordinate groups."
            );
        }

        //
        // Summary:
        //     Load content and more here. This call is asynchronous, so it is a good time to
        //     run any long running steps for your module including loading resources from file
        //     or ref.
        protected override async Task LoadAsync()
        {
            // Load any necessary resources
            await base.LoadAsync();
            await TrySetAccountName();
            await TrySetApiKey();
        }

        protected override void Initialize()
        {
            _httpClient.BaseAddress = new Uri(_serverAddressSetting.Value);
            _serverAddressSetting.SettingChanged += (sender, args) =>
            {
                _httpClient.BaseAddress = new Uri(_serverAddressSetting.Value);
            };
            _moduleIcon = new CornerIcon(
                // ContentsManager.GetTexture("icons/group.png"),
                AsyncTexture2D.FromAssetId(156409),
                "GW2 LFG");
            _moduleIcon.Click += OnModuleIcon_Click;

            // Note that the windowRegion and contentRegion are matched to the size of the background image.
            _lfgWindow = new StandardWindow(
                ContentsManager.GetTexture("textures/mainwindow_background.png"),
                new Rectangle(40, 26, 913, 691),  // The windowRegion
                new Rectangle(70, 71, 839, 605)   // The contentRegion
            )
            {
                Parent = GameService.Graphics.SpriteScreen,
                Title = "Community LFG",
                // Emblem = ContentsManager.GetTexture("controls/window/156022"),
                Subtitle = "",
                SavesPosition = true,
                Id = $"{nameof(Gw2LfgModule)}_ExampleModule_9A19103F-16F7-4668-BE54-9A1E7A4F7556",
            };
            _lfgWindow.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_lfgWindow.Visible))
                {
                    _viewModel.Visible = _lfgWindow.Visible;
                }
            };

            _viewModel.AccountNameChanged += (sender, args) =>
            {
                _lfgWindow.Subtitle = args.NewValue;
            };

            Gw2ApiManager.SubtokenUpdated += OnSubtokenUpdated;

#if DEBUG
            //_lfgView = new LfgView(_httpClient, _viewModel);
            //_lfgWindow.Show(_lfgView);
#endif
        }

        protected override void Unload()
        {
            _moduleIcon?.Dispose();
            Gw2ApiManager.SubtokenUpdated -= OnSubtokenUpdated;
            _viewModel.Dispose();
        }

        private async void OnSubtokenUpdated(object sender, EventArgs e)
        {
            await TrySetAccountName();
            await TrySetApiKey();
        }

        private async Task TrySetAccountName()
        {
            if (Gw2ApiManager.HasPermission(Gw2Sharp.WebApi.V2.Models.TokenPermission.Account))
            {
                try
                {
                    var account = await Gw2ApiManager.Gw2ApiClient.V2.Account.GetAsync();
                    _viewModel.AccountName = account.Name;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to get account name");
                }
            }
        }

        private async Task TrySetApiKey()
        {
            if (Gw2ApiManager.HasPermission(Gw2Sharp.WebApi.V2.Models.TokenPermission.Account))
            {
                try
                {
                    var subtoken = await Gw2ApiManager.Gw2ApiClient.V2.CreateSubtoken.WithPermissions(
                        [Gw2Sharp.WebApi.V2.Models.TokenPermission.Account]).GetAsync();
                    _grpcClient.SetApiKey(subtoken.Subtoken);
                    _viewModel.ApiKey = subtoken.Subtoken;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to get subtoken");
                }
            }
        }

        private void OnModuleIcon_Click(object sender, MouseEventArgs e)
        {
            if (_lfgView == null)
            {
                _lfgView = new LfgView(_lfgClient, _viewModel);
                _lfgWindow.Show(_lfgView);
            }
            else
            {
                _lfgWindow.ToggleWindow();
            }
        }
    }
}