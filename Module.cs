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
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.ComponentModel;

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
        private readonly HttpClient _httpClient = new();
        private CancellationTokenSource _cancellationTokenSource = new();
        private SimpleGrpcWebClient _grpcClient;
        private LfgClient _client;
        private LfgView _lfgView;
        private readonly LfgViewModel _viewModel = new();
        private Task _groupUpdatesSubscriber;
        private Task _applicationUpdatesSubscriber;

        [ImportingConstructor]
        public Gw2LfgModule([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters)
        {
            // ModuleParameters is already assigned by the base class constructor
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
            await Task.WhenAll(
                TrySetAccountName(),
                TrySetApiKey()
            );
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
            _moduleIcon.Click += ModuleIcon_Click;

            _lfgWindow = new StandardWindow(
                ContentsManager.GetTexture("textures/mainwindow_background.png"), // The background texture of the window.
                                                                                  // AsyncTexture2D.FromAssetId(155997),
                new Rectangle(40, 26, 913, 691),              // The windowRegion
                new Rectangle(70, 71, 839, 605)              // The contentRegion
                                                             // new Point(913, 691)                          // The size of the window
                )
            {
                Parent = GameService.Graphics.SpriteScreen,
                Title = "Community LFG",
                // Emblem = ContentsManager.GetTexture("controls/window/156022"),
                Subtitle = "Example Subtitle",
                SavesPosition = true,
                Id = $"{nameof(Gw2LfgModule)}_ExampleModule_9A19103F-16F7-4668-BE54-9A1E7A4F7556",
            };

            _viewModel.AccountNameChanged += (sender, args) =>
            {
                _lfgWindow.Subtitle = _viewModel.AccountName;
            };
            _viewModel.ApiKeyChanged += OnApiKeyChanged;
            _viewModel.GroupsChanged += OnGroupsChanged;

#if DEBUG
            _viewModel.Groups = [new Proto.Group
            {
                Id = "1",
                Title = "Test Group without requirements"
            },
            new Proto.Group{
                Id = "1",
                Title = "Test Group",
                KillProofId = Proto.KillProofId.KpLi,
                KillProofMinimum = 250
            }];
#endif

            _lfgView = new LfgView(_httpClient, _viewModel);
            _grpcClient = new SimpleGrpcWebClient(_httpClient, _viewModel.ApiKey, _cancellationTokenSource.Token);
            _client = new LfgClient(_grpcClient);

            Gw2ApiManager.SubtokenUpdated += OnSubtokenUpdated;

#if DEBUG
            _lfgWindow.Show(_lfgView);
#endif
        }

        protected override void Unload()
        {
            _moduleIcon?.Dispose();
            _viewModel.ApiKeyChanged -= OnApiKeyChanged;
            _viewModel.GroupsChanged -= OnGroupsChanged;
            Gw2ApiManager.SubtokenUpdated -= OnSubtokenUpdated;
            _cancellationTokenSource.Cancel();
        }

        private async void OnSubtokenUpdated(object sender, EventArgs e)
        {
            await Task.WhenAll(
                TrySetAccountName(),
                TrySetApiKey()
            );
        }

        private void OnApiKeyChanged(object sender, PropertyChangedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            _grpcClient = new SimpleGrpcWebClient(_httpClient, _viewModel.ApiKey, _cancellationTokenSource.Token);
            _client = new LfgClient(_grpcClient);
            TrySubscribeGroups();
        }

        private void OnGroupsChanged(object sender, PropertyChangedEventArgs e)
        {
            TrySubscribeApplications();
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
                    _viewModel.ApiKey = subtoken.Subtoken;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to get subtoken");
                }
            }
        }

        private void TrySubscribeGroups()
        {
            if (_viewModel.ApiKey == "")
            {
                return;
            }
            if (_groupUpdatesSubscriber != null)
            {
                // TODO: properly cancel the previous subscription
                // _groupUpdatesSubscriber.Dispose();
            }
            _groupUpdatesSubscriber = Task.Run(async () =>
            {
                try
                {
                    _viewModel.Groups = (await _client.ListGroups()).Groups.ToArray();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to list groups");
                }
                await foreach (var update in _client.SubscribeGroups())
                {
                    switch (update.UpdateCase)
                    {
                        case Proto.GroupsUpdate.UpdateOneofCase.NewGroup:
                            _viewModel.Groups = _viewModel.Groups.Append(update.NewGroup).ToArray();
                            break;
                        case Proto.GroupsUpdate.UpdateOneofCase.RemovedGroupId:
                            _viewModel.Groups = _viewModel.Groups.Where(
                                g => g.Id != update.RemovedGroupId
                            ).ToArray();
                            break;
                        case Proto.GroupsUpdate.UpdateOneofCase.UpdatedGroup:
                            _viewModel.Groups = _viewModel.Groups.Select(
                                g => g.Id == update.UpdatedGroup.Id ? update.UpdatedGroup : g
                            ).ToArray();
                            break;
                    }
                }
            }, _cancellationTokenSource.Token);
        }

        private void TrySubscribeApplications()
        {
            if (_viewModel.ApiKey == "")
            {
                return;
            }
            if (_applicationUpdatesSubscriber != null)
            {
                // TODO: properly cancel the previous subscription
                // _applicationUpdatesSubscriber.Dispose();
            }
            if (_viewModel.MyGroup == null)
            {
                return;
            }
            _applicationUpdatesSubscriber = Task.Run(async () =>
            {
                await foreach (var update in _client.SubscribeGroupApplications(_viewModel.MyGroup.Id))
                {
                    switch (update.UpdateCase)
                    {
                        case Proto.GroupApplicationUpdate.UpdateOneofCase.NewApplication:
                            _viewModel.GroupApplications = _viewModel.GroupApplications.Append(update.NewApplication).ToArray();
                            break;
                        case Proto.GroupApplicationUpdate.UpdateOneofCase.RemovedApplicationId:
                            //_viewModel.GroupApplications = _viewModel.GroupApplications.Where(
                            //    g => g.Id != update.RemovedApplicationId
                            //).ToArray();
                            break;
                        case Proto.GroupApplicationUpdate.UpdateOneofCase.UpdatedApplication:
                            //_viewModel.GroupApplications = _viewModel.GroupApplications.Select(
                            //    g => g.Id == update.UpdatedApplication.Id ? update.UpdatedApplication : g
                            //).ToArray();
                            break;
                    }
                }
            }, _cancellationTokenSource.Token);
        }

        private void ModuleIcon_Click(object sender, MouseEventArgs e)
        {
            if (!_lfgWindow.Visible)
            {
                _lfgWindow.Show(_lfgView);
            }
            else
            {
                _lfgWindow.Hide();
            }
        }
    }
}