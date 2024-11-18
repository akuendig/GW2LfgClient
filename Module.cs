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
        private CancellationTokenSource _apiKeyCts = new();
        private CancellationTokenSource _groupsSubCts = new();
        private CancellationTokenSource _applicationsSubCts = new();
        private SimpleGrpcWebClient _grpcClient;
        private LfgClient _client;
        private LfgView _lfgView;
        private readonly LfgViewModel _viewModel = new();

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
                Subtitle = "Example Subtitle",
                SavesPosition = true,
                Id = $"{nameof(Gw2LfgModule)}_ExampleModule_9A19103F-16F7-4668-BE54-9A1E7A4F7556",
            };
            _lfgWindow.PropertyChanged += LfgWindow_PropertyChanged;

            _viewModel.AccountNameChanged += (sender, args) =>
            {
                _lfgWindow.Subtitle = _viewModel.AccountName;
            };
            _viewModel.ApiKeyChanged += OnApiKeyChanged;
            _viewModel.MyGroupChanged += OnMyGroupChanged;

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
            _grpcClient = new SimpleGrpcWebClient(_httpClient, _viewModel.ApiKey, _apiKeyCts.Token);
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
            Gw2ApiManager.SubtokenUpdated -= OnSubtokenUpdated;
            _apiKeyCts.Cancel();
            _groupsSubCts.Cancel();
            _applicationsSubCts.Cancel();
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
            _apiKeyCts?.Cancel();
            _apiKeyCts = new CancellationTokenSource();
            _grpcClient = new SimpleGrpcWebClient(_httpClient, _viewModel.ApiKey, _apiKeyCts.Token);
            _client = new LfgClient(_grpcClient);
            TrySubscribeGroups();
        }

        private void OnMyGroupChanged(object sender, PropertyChangedEventArgs e)
        {
            _viewModel.GroupApplications = [];
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

        private async Task RefreshGroupsAndSubscribe()
        {
            if (string.IsNullOrEmpty(_viewModel.ApiKey))
            {
                return;
            }

            try
            {
                // Make a snapshot of the current token.
                CancellationToken cancellationToken = _groupsSubCts.Token;
                // First get initial list
                var initialGroups = await _client.ListGroups(cancellationToken);
                _viewModel.Groups = initialGroups.Groups.ToArray();

                // Then start subscription for updates
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await foreach (var update in _client.SubscribeGroups(cancellationToken))
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                break;
                            }

                            switch (update.UpdateCase)
                            {
                                case Proto.GroupsUpdate.UpdateOneofCase.NewGroup:
                                    _viewModel.AddGroup(update.NewGroup);
                                    break;
                                case Proto.GroupsUpdate.UpdateOneofCase.RemovedGroupId:
                                    _viewModel.RemoveGroup(update.RemovedGroupId);
                                    break;
                                case Proto.GroupsUpdate.UpdateOneofCase.UpdatedGroup:
                                    _viewModel.UpdateGroup(update.UpdatedGroup);
                                    break;
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Normal cancellation, ignore
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Group subscription error");
                    }
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize groups");
            }
        }

        private async Task RefreshApplicationsAndSubscribe()
        {
            if (string.IsNullOrEmpty(_viewModel.ApiKey) || _viewModel.MyGroup == null)
            {
                return;
            }

            try
            {
                // Make a snapshot of the current token.
                CancellationToken cancellationToken = _applicationsSubCts.Token;
                // First get initial list
                var initialApplications = await _client.ListGroupApplications(
                    _viewModel.MyGroup.Id,
                    cancellationToken
                );
                _viewModel.GroupApplications = initialApplications.Applications.ToArray();

                // Then start subscription for updates
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await foreach (var update in _client.SubscribeGroupApplications(
                            _viewModel.MyGroup.Id,
                            cancellationToken))
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                break;
                            }

                            switch (update.UpdateCase)
                            {
                                case Proto.GroupApplicationUpdate.UpdateOneofCase.NewApplication:
                                    _viewModel.AddApplication(update.NewApplication);
                                    break;
                                case Proto.GroupApplicationUpdate.UpdateOneofCase.RemovedApplicationId:
                                    _viewModel.RemoveApplication(update.RemovedApplicationId);
                                    break;
                                case Proto.GroupApplicationUpdate.UpdateOneofCase.UpdatedApplication:
                                    _viewModel.UpdateApplication(update.UpdatedApplication);
                                    break;
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Normal cancellation, ignore
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Application subscription error");
                    }
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize applications");
            }
        }

        // Update TrySubscribeGroups to use the new method
        private void TrySubscribeGroups()
        {
            _groupsSubCts?.Cancel();
            _groupsSubCts = new CancellationTokenSource();
            _ = RefreshGroupsAndSubscribe();
        }

        // Update TrySubscribeApplications to use the new method
        private void TrySubscribeApplications()
        {
            _applicationsSubCts?.Cancel();
            _applicationsSubCts = new CancellationTokenSource();
            _ = RefreshApplicationsAndSubscribe();
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

    private async void LfgWindow_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Visible")
        {
            if (_lfgWindow.Visible)
            {
                // Clear existing state before resubscribing
                _viewModel.Groups = [];
                _viewModel.GroupApplications = [];
                
                // Cancel any existing subscriptions
                _groupsSubCts?.Cancel();
                _applicationsSubCts?.Cancel();
                
                // Create new cancellation tokens
                _groupsSubCts = new CancellationTokenSource();
                _applicationsSubCts = new CancellationTokenSource();
                
                // Start new subscriptions
                await RefreshGroupsAndSubscribe();
                await RefreshApplicationsAndSubscribe();
            }
            else
            {
                // Cancel subscriptions when hiding window
                _groupsSubCts?.Cancel();
                _applicationsSubCts?.Cancel();
            }
        }
    }
    }
}