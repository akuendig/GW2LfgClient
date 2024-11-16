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
using Grpc.Core;
using System.IO;

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
        private Channel _channel;
        private LfgClient _client;

        [ImportingConstructor]
        public Gw2LfgModule([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters)
        {
            // ModuleParameters is already assigned by the base class constructor
        }

        protected override void DefineSettings(SettingCollection settings)
        {
            _apiKeySetting = settings.DefineSetting(
                "ApiKey",
                "",
                () => "GW2 API Key",
                () => "API key with progression access");
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
            string nativeLibPath = Path.Combine(DirectoriesManager.GetFullDirectoryPath("grpc_library"), "grpc_csharp_ext.x64.dll");
            using (var destinationStream = new FileStream(nativeLibPath, FileMode.Create, FileAccess.Write))
            {
                ContentsManager.GetFileStream("grpc_library/grpc_csharp_ext.x64.dll").CopyTo(destinationStream);
            }
            if (File.Exists(nativeLibPath))
            {
                // Set environment variable to help Grpc.Core find the native library
                Environment.SetEnvironmentVariable("GRPC_CSHARP_EXT_OVERRIDE_LOCATION", nativeLibPath);
            }
            else
            {
                Logger.Error("GRPC native library not found at: " + nativeLibPath);
            }
            
            _channel = new Channel("http://localhost:5001", ChannelCredentials.Insecure);
            _client = new LfgClient(_channel, _apiKeySetting.Value);
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

#if DEBUG
            _viewModel.Groups.Add(new Proto.Group
            {
                Id = "1",
                Title = "Test Group without requirements"
            });
            _viewModel.Groups.Add(new Proto.Group
            {
                Id = "1",
                Title = "Test Group",
                KillProofId = Proto.KillProofId.KpLi,
                KillProofMinimum = 250
            });
#endif

            _lfgView = new LfgView(_client, _apiKeySetting.Value, _viewModel, ModuleParameters.Gw2ApiManager);
            _viewModel.AccountNameChanged += (sender, args) =>
            {
                _lfgWindow.Subtitle = _viewModel.AccountName;
            };

            Gw2ApiManager.SubtokenUpdated += OnSubtokenUpdated;

#if DEBUG
            _lfgWindow.Show(_lfgView);
#endif
        }

        protected override void Unload()
        {
            _moduleIcon?.Dispose();
            _channel?.ShutdownAsync();
            Gw2ApiManager.SubtokenUpdated -= OnSubtokenUpdated;
        }

        private async void OnSubtokenUpdated(object sender, EventArgs e)
        {
            await TrySetAccountName();
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

        // private void InitializeWebSocket()
        // {
        //     var url = new Uri("ws://your-render-server/ws");
        //     _wsClient = new WebsocketClient(url);

        //     _wsClient.MessageReceived.Subscribe(msg => 
        //     {
        //         GameService.GameThread.Enqueue(() => HandleWebSocketMessage(msg));
        //     });

        //     _wsClient.ReconnectionHappened.Subscribe(info => 
        //     {
        //         Logger.Info($"Reconnected: {info.Type}");
        //     });

        //     _wsClient.Start();
        // }

        // private void HandleWebSocketMessage(ResponseMessage msg)
        // {
        //     try
        //     {
        //         var message = JsonSerializer.Deserialize<WebSocketMessage>(msg.Text);
        //         switch (message.Type)
        //         {
        //             case "new_group":
        //                 _lfgWindow?.AddGroup(message.Payload.ToObject<Group>());
        //                 break;
        //             case "application_status":
        //                 _lfgWindow?.UpdateApplicationStatus(message.Payload.ToObject<ApplicationStatus>());
        //                 break;
        //             // Add more message handlers as needed
        //         }
        //     }
        //     catch (Exception ex)
        //     {
        //         Logger.Error(ex, "Failed to handle WebSocket message");
        //     }
        // }

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