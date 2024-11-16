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

namespace Gw2Lfg
{
    [Export(typeof(Module))]
    public class Gw2LfgModule : Module
    {
        private static readonly Logger Logger = Logger.GetLogger<Gw2LfgModule>();

        private ContentsManager ContentsManager => ModuleParameters.ContentsManager;
        private DirectoriesManager DirectoriesManager => ModuleParameters.DirectoriesManager;
        private Gw2ApiManager Gw2ApiManager => ModuleParameters.Gw2ApiManager;

        private CornerIcon _moduleIcon;
        private StandardWindow _lfgWindow;
        private LfgView _lfgView;
        private LfgViewModel _viewModel = new();
        private SettingEntry<string> _apiKeySetting;
        private SimpleGrpcWebClient _grpcClient;
        private LfgClient _client;
        private Task _groupUpdatesSubscriber;

        [ImportingConstructor]
        public Gw2LfgModule([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters)
        {
            // ModuleParameters is already assigned by the base class constructor
        }

        protected override void DefineSettings(SettingCollection settings)
        {
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
            TrySubscribeGroups();
            // while (!Gw2ApiManager.HasPermission(Gw2Sharp.WebApi.V2.Models.TokenPermission.Account))
            // {
            //     await Task.Delay(100);
            // }
            // try
            // {
            //     var account = await ModuleParameters.Gw2ApiManager.Gw2ApiClient.V2.Account.GetAsync();
            //     _viewModel.AccountName = account.Name;
            // }
            // catch (Exception ex)
            // {
            //     Logger.Error(ex, "Failed to get account name");
            // }
        }

        protected override void Initialize()
        {
            _moduleIcon = new CornerIcon(
                // ContentsManager.GetTexture("icons/group.png"),
                AsyncTexture2D.FromAssetId(156409),
                "GW2 LFG");

            _moduleIcon.Click += ModuleIcon_Click;

            var httpClient = new System.Net.Http.HttpClient()
            {
                BaseAddress = new Uri("http://localhost:5001"),
            };
            _grpcClient = new SimpleGrpcWebClient(httpClient);
            _client = new LfgClient(_grpcClient, "");

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

            _lfgView = new LfgView(_client, _viewModel, ModuleParameters.Gw2ApiManager);

            Gw2ApiManager.SubtokenUpdated += OnSubtokenUpdated;

#if DEBUG
            _lfgWindow.Show(_lfgView);
#endif
        }

        protected override void Unload()
        {
            _moduleIcon?.Dispose();
            Gw2ApiManager.SubtokenUpdated -= OnSubtokenUpdated;
        }

        private async void OnSubtokenUpdated(object sender, EventArgs e)
        {
            await Task.WhenAll(
                TrySetAccountName(),
                TrySetApiKey()
            );
            TrySubscribeGroups();
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

        private async void TrySubscribeGroups()
        {
            if (_viewModel.ApiKey == "")
            {
                return;
            }
            if (_groupUpdatesSubscriber != null)
            {
                _groupUpdatesSubscriber.Dispose();
            }
            _groupUpdatesSubscriber = Task.Run(async () =>
            {
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
            });
            try
            {
                _viewModel.Groups = (await _client.ListGroups()).Groups.ToArray();
            } catch (Exception ex)
            {
                Logger.Error(ex, "Failed to list groups");
            }
        }

        protected override void OnModuleLoaded(EventArgs e)
        {

            // Base handler must be called
            base.OnModuleLoaded(e);
        }

        protected override void Update(GameTime gameTime)
        {
            // Update any necessary UI elements
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