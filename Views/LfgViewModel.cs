#nullable enable

using System.Linq;
using System;
using Blish_HUD;
using System.Threading;
using System.Collections.Immutable;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace System.Runtime.CompilerServices
{
    // Define this type because we are comiling against a .NET version lower than 5.0.
    internal static class IsExternalInit { }
}

namespace Gw2Lfg
{
    public record LfgModel(
        ImmutableArray<Proto.Group> Groups,
        ImmutableArray<Proto.GroupApplication> GroupApplications,
        ImmutableArray<Proto.GroupApplication> MyApplications,
        string AccountName = "",
        string ApiKey = "",
        string ServerAddress = "",
        Proto.Group? MyGroup = null,
        bool Visible = false,
        bool IsLoadingGroups = false,
        bool IsLoadingApplications = false,
        bool IsConnected = false,
        DateTimeOffset LastHeartbeat = default
    );

    public class LfgViewModelPropertyChangedEventArgs<T>(LfgModel oldState, LfgModel newState, Func<LfgModel, T> lens) :
     LfgPropertyChangedEventArgs<LfgModel, T>(oldState, newState, lens)
    { };

    public class LfgViewModel : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<LfgViewModel>();
        private readonly object _stateLock = new();
        private LfgModel _state = new(ImmutableArray<Proto.Group>.Empty, ImmutableArray<Proto.GroupApplication>.Empty, ImmutableArray<Proto.GroupApplication>.Empty);
        private readonly SynchronizationContext _synchronizationContext;
        private bool _disposed;

        // Event handlers
        public event EventHandler<LfgViewModelPropertyChangedEventArgs<string>>? AccountNameChanged;
        public event EventHandler<LfgViewModelPropertyChangedEventArgs<string>>? ApiKeyChanged;
        public event EventHandler<LfgViewModelPropertyChangedEventArgs<string>>? ServerAddressChanged;
        public event EventHandler<LfgViewModelPropertyChangedEventArgs<ImmutableArray<Proto.Group>>>? GroupsChanged;
        public event EventHandler<LfgViewModelPropertyChangedEventArgs<Proto.Group?>>? MyGroupChanged;
        public event EventHandler<LfgViewModelPropertyChangedEventArgs<ImmutableArray<Proto.GroupApplication>>>? GroupApplicationsChanged;
        public event EventHandler<LfgViewModelPropertyChangedEventArgs<ImmutableArray<Proto.GroupApplication>>>? MyApplicationsChanged;
        public event EventHandler<LfgViewModelPropertyChangedEventArgs<bool>>? VisibleChanged;
        public event EventHandler<LfgViewModelPropertyChangedEventArgs<bool>>? IsLoadingGroupsChanged;
        public event EventHandler<LfgViewModelPropertyChangedEventArgs<bool>>? IsLoadingApplicationsChanged;
        public event EventHandler<LfgViewModelPropertyChangedEventArgs<bool>>? IsConnectedChanged;
        public event EventHandler<LfgViewModelPropertyChangedEventArgs<DateTimeOffset>>? LastHeartbeatChanged;

        private SimpleGrpcWebClient _grpcClient;
        private LfgClient _client;
        private CancellationTokenSource _apiKeyCts = new();
        private CancellationTokenSource _groupsSubCts = new();
        private CancellationTokenSource _applicationsSubCts = new();
        private CancellationTokenSource _heartbeatCts = new();
        private readonly SemaphoreSlim _refreshGroupsSemaphore = new(1, 1);

        public LfgViewModel()
        {
            _synchronizationContext = SynchronizationContext.Current ?? new SynchronizationContext();

            ApiKeyChanged += OnApiKeyChanged;
            ServerAddressChanged += OnServerAddressChanged;
            MyGroupChanged += OnMyGroupChanged;
            VisibleChanged += OnVisibleChanged;
        }

        public LfgModel State { get { lock (_stateLock) return _state; } }

        private void UpdateState<T>(Func<LfgModel, Tuple<LfgModel, bool>> update, Func<LfgModel, T> getter,
            EventHandler<LfgViewModelPropertyChangedEventArgs<T>>? handler)
        {
            LfgModel oldState;
            LfgModel newState;
            lock (_stateLock)
            {
                var result = update(_state);
                if (result.Item2)
                {
                    oldState = _state;
                    _state = result.Item1;
                    newState = _state;
                }
                else return;
            }

            if (!_disposed)
            {
                _synchronizationContext.Post(
                    _ => handler?.Invoke(this, new LfgViewModelPropertyChangedEventArgs<T>(oldState, newState, getter)),
                    null);
            }
        }

        private void SetState<T>(T value, Func<LfgModel, T> getter, Func<LfgModel, T, LfgModel> setter,
            EventHandler<LfgViewModelPropertyChangedEventArgs<T>>? handler) => UpdateState(
                s =>
                {
                    if (EqualityComparer<T>.Default.Equals(getter(s), value)) return Tuple.Create(s, false);
                    return Tuple.Create(setter(s, value), true);
                },
                getter,
                handler);

        public string AccountName
        {
            get
            {
                lock (_stateLock) return _state.AccountName;
            }
            set
            {
                SetState(
                    value,
                    s => s.AccountName,
                    (s, v) => s with { AccountName = v },
                    AccountNameChanged);
                UpdateMyGroup();
            }
        }

        public string ApiKey
        {
            get
            {
                lock (_stateLock) return _state.ApiKey;
            }
            set => SetState(value,
                s => s.ApiKey,
                (s, v) => s with { ApiKey = v },
                ApiKeyChanged);
        }

        public string ServerAddress
        {
            get
            {
                lock (_stateLock) return _state.ServerAddress;
            }
            set => SetState(value,
                s => s.ServerAddress,
                (s, v) => s with { ServerAddress = v },
                ServerAddressChanged);
        }

        public ImmutableArray<Proto.Group> Groups
        {
            get
            {
                lock (_stateLock) return _state.Groups;
            }
            set
            {
                SetState(
                    value,
                    s => s.Groups,
                    (s, v) => s with { Groups = v },
                    GroupsChanged);
                UpdateMyGroup();
            }
        }

        public Proto.Group? MyGroup
        {
            get
            {
                lock (_stateLock) return _state.MyGroup;
            }
            private set => SetState(
                value,
                s => s.MyGroup,
                (s, v) => s with { MyGroup = v },
                MyGroupChanged);
        }

        public ImmutableArray<Proto.GroupApplication> GroupApplications
        {
            get
            {
                lock (_stateLock) return _state.GroupApplications;
            }
            set => SetState(
                value,
                s => s.GroupApplications,
                (s, v) => s with { GroupApplications = v },
                GroupApplicationsChanged);
        }

        public ImmutableArray<Proto.GroupApplication> MyApplications
        {
            get
            {
                lock (_stateLock) return _state.MyApplications;
            }
            set => SetState(
                value,
                s => s.MyApplications,
                (s, v) => s with { MyApplications = v },
                MyApplicationsChanged);
        }

        public bool Visible
        {
            get
            {
                lock (_stateLock) return _state.Visible;
            }
            set => SetState(
                value,
                s => s.Visible,
                (s, v) => s with { Visible = v },
                VisibleChanged);
        }

        public bool IsLoadingGroups
        {
            get
            {
                lock (_stateLock) return _state.IsLoadingGroups;
            }
            set => SetState(
                value,
                s => s.IsLoadingGroups,
                (s, v) => s with { IsLoadingGroups = v },
                IsLoadingGroupsChanged);
        }

        public bool IsLoadingApplications
        {
            get
            {
                lock (_stateLock) return _state.IsLoadingApplications;
            }
            set => SetState(
                value,
                s => s.IsLoadingApplications,
                (s, v) => s with { IsLoadingApplications = v },
                IsLoadingApplicationsChanged);
        }

        public bool IsConnected
        {
            get
            {
                lock (_stateLock) return _state.IsConnected;
            }
            private set => SetState(
                value,
                s => s.IsConnected,
                (s, v) => s with { IsConnected = v },
                IsConnectedChanged);
        }

        public DateTimeOffset LastHeartbeat
        {
            get
            {
                lock (_stateLock) return _state.LastHeartbeat;
            }
            private set => SetState(
                value,
                s => s.LastHeartbeat,
                (s, v) => s with { LastHeartbeat = v },
                LastHeartbeatChanged);
        }

        public void AddGroup(Proto.Group group)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));

            UpdateState(
                s =>
                {
                    if (s.Groups.Any(g => g.Id == group.Id)) return Tuple.Create(s, false);
                    return Tuple.Create(s with { Groups = s.Groups.Add(group) }, true);
                },
                s => s.Groups,
                GroupsChanged);
            UpdateMyGroup();
        }

        public void UpdateGroup(Proto.Group updatedGroup)
        {
            if (updatedGroup == null) throw new ArgumentNullException(nameof(updatedGroup));

            UpdateState(
                s =>
                {
                    var old = _state.Groups.FirstOrDefault(g => g.Id == updatedGroup.Id);
                    if (old == null || old.Equals(updatedGroup)) return Tuple.Create(s, false);
                    else return Tuple.Create(s with { Groups = s.Groups.Replace(old, updatedGroup) }, true);
                },
                s => s.Groups,
                GroupsChanged);
            UpdateMyGroup();
        }

        public void RemoveGroup(string groupId)
        {
            UpdateState(
                s =>
                {
                    var newGroups = _state.Groups.Where(g => g.Id != groupId).ToImmutableArray();
                    if (s.Groups.Length == newGroups.Length) return Tuple.Create(s, false);
                    else return Tuple.Create(s with { Groups = newGroups }, true);
                },
                s => s.Groups,
                GroupsChanged);
            UpdateMyGroup();
        }

        public void AddApplication(Proto.GroupApplication newApplication)
        {

            if (newApplication == null) throw new ArgumentNullException(nameof(newApplication));

            UpdateState(
                s =>
                {
                    if (s.GroupApplications.Any(g => g.Id == newApplication.Id)) return Tuple.Create(s, false);
                    return Tuple.Create(s with { GroupApplications = s.GroupApplications.Add(newApplication) }, true);
                },
                s => s.GroupApplications,
                GroupApplicationsChanged);
        }

        public void UpdateApplication(Proto.GroupApplication updatedApplication)
        {

            if (updatedApplication == null) throw new ArgumentNullException(nameof(updatedApplication));

            UpdateState(
                s =>
                {
                    var old = _state.GroupApplications.FirstOrDefault(a => a.Id == updatedApplication.Id);
                    if (old == null || old.Equals(updatedApplication)) return Tuple.Create(s, false);
                    else return Tuple.Create(s with { GroupApplications = s.GroupApplications.Replace(old, updatedApplication) }, true);
                },
                s => s.GroupApplications,
                GroupApplicationsChanged);
        }

        public void RemoveApplication(string applicationId)
        {
            UpdateState(
                s =>
                {
                    var newGroupApplications = _state.GroupApplications.Where(a => a.Id != applicationId).ToImmutableArray();
                    if (s.GroupApplications.Length == newGroupApplications.Length) return Tuple.Create(s, false);
                    else return Tuple.Create(s with { GroupApplications = newGroupApplications }, true);
                },
                s => s.GroupApplications,
                GroupApplicationsChanged);
        }

        public void AddMyApplication(Proto.GroupApplication newApplication)
        {

            if (newApplication == null) throw new ArgumentNullException(nameof(newApplication));

            UpdateState(
                s =>
                {
                    if (s.MyApplications.Any(g => g.Id == newApplication.Id)) return Tuple.Create(s, false);
                    return Tuple.Create(s with { MyApplications = s.MyApplications.Add(newApplication) }, true);
                },
                s => s.MyApplications,
                MyApplicationsChanged);
        }

        public void UpdateMyApplication(Proto.GroupApplication updatedApplication)
        {

            if (updatedApplication == null) throw new ArgumentNullException(nameof(updatedApplication));

            UpdateState(
                s =>
                {
                    var old = _state.MyApplications.FirstOrDefault(a => a.Id == updatedApplication.Id);
                    if (old == null || old.Equals(updatedApplication)) return Tuple.Create(s, false);
                    else return Tuple.Create(s with { MyApplications = s.MyApplications.Replace(old, updatedApplication) }, true);
                },
                s => s.MyApplications,
                MyApplicationsChanged);
        }

        public void RemoveMyApplication(string applicationId)
        {
            UpdateState(
                s =>
                {
                    var newMyApplications = _state.MyApplications.Where(a => a.Id != applicationId).ToImmutableArray();
                    if (s.MyApplications.Length == newMyApplications.Length) return Tuple.Create(s, false);
                    else return Tuple.Create(s with { MyApplications = newMyApplications }, true);
                },
                s => s.MyApplications,
                MyApplicationsChanged);
        }

        private void UpdateMyGroup()
        {
            UpdateState(
                s =>
                {
                    var newMyGroup = _state.Groups.FirstOrDefault(g => g.CreatorId == _state.AccountName);
                    if (Equals(_state.MyGroup?.Id, newMyGroup?.Id)) return Tuple.Create(s, false);
                    else return Tuple.Create(s with { MyGroup = newMyGroup }, true);
                },
                s => s.MyGroup,
                MyGroupChanged);
        }

        private async Task RefreshGroupsAndSubscribe(LfgModel state, CancellationToken cancellationToken = default)
        {
            await RefreshGroupsAsync();

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
                                AddGroup(update.NewGroup);
                                break;
                            case Proto.GroupsUpdate.UpdateOneofCase.RemovedGroupId:
                                RemoveGroup(update.RemovedGroupId);
                                break;
                            case Proto.GroupsUpdate.UpdateOneofCase.UpdatedGroup:
                                UpdateGroup(update.UpdatedGroup);
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

        private async Task RefreshApplicationsAndSubscribe(LfgModel state, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(state.ApiKey))
            {
                return;
            }

            if (state.MyGroup != null)
            {
                try
                {
                    IsLoadingApplications = true;
                    var initialApplications = await _client.ListGroupApplications(state.MyGroup?.Id ?? "", cancellationToken);
                    GroupApplications = initialApplications.Applications.ToImmutableArray();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to initialize applications");
                }
                finally { IsLoadingApplications = false; }
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await foreach (var update in _client.SubscribeGroupApplications(state.MyGroup?.Id, cancellationToken))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        switch (update.UpdateCase)
                        {
                            case Proto.GroupApplicationUpdate.UpdateOneofCase.NewApplication:
                                if (update.NewApplication.AccountName == state.AccountName)
                                    AddMyApplication(update.NewApplication);
                                else
                                    AddApplication(update.NewApplication);
                                break;
                            case Proto.GroupApplicationUpdate.UpdateOneofCase.RemovedApplicationId:
                                // Since we don't know who's application this was, we just remove
                                // it from both and rely on the fact that it's a NOOP if the groups
                                // is not in the list.
                                RemoveMyApplication(update.RemovedApplicationId);
                                RemoveApplication(update.RemovedApplicationId);
                                break;
                            case Proto.GroupApplicationUpdate.UpdateOneofCase.UpdatedApplication:
                                if (update.UpdatedApplication.AccountName == state.AccountName)
                                    UpdateMyApplication(update.UpdatedApplication);
                                else
                                    UpdateApplication(update.UpdatedApplication);
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

        private async Task RefreshMyApplications(LfgModel state, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(state.ApiKey) || string.IsNullOrWhiteSpace(state.AccountName))
            {
                return;
            }

            // TODO: This should probably retry in case the server goes away?
            try
            {
                var initialApplications = await _client.ListMyApplications(state.AccountName, cancellationToken);
                MyApplications = initialApplications.Applications.ToImmutableArray();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load applications");
            }
        }

        private void OnVisibleChanged(object sender, LfgViewModelPropertyChangedEventArgs<bool> e)
        {
            if (Visible)
            {
                SendHeartbeats();
                TrySubscribeGroups(e.NewState);
                TrySubscribeApplications(e.NewState);
            }
            else
            {
                _groupsSubCts.Cancel();
                _applicationsSubCts.Cancel();
                _heartbeatCts.Cancel();
            }
        }

        private async void OnApiKeyChanged(object sender, LfgViewModelPropertyChangedEventArgs<string> e)
        {
            Connect(e.NewState.ApiKey, e.NewState.ServerAddress);
            SendHeartbeats();
            await TrySubscribeGroups(e.NewState);
            await TrySubscribeApplications(e.NewState);
        }

        private async void OnServerAddressChanged(object sender, LfgViewModelPropertyChangedEventArgs<string> e)
        {
            Connect(e.NewState.ApiKey, e.NewState.ServerAddress);
            SendHeartbeats();
            await TrySubscribeGroups(e.NewState);
            await TrySubscribeApplications(e.NewState);
        }

        private async void OnMyGroupChanged(object sender, LfgViewModelPropertyChangedEventArgs<Proto.Group?> e)
        {
            // TODO: This now runs on every heartbeat because the update time of the group
            // is changed. Should it?
            await TrySubscribeApplications(e.NewState);
        }

        public void Connect(string apiKey, string serverAddress)
        {
            lock (_stateLock)
            {
                _applicationsSubCts.Cancel();
                _groupsSubCts.Cancel();
                _heartbeatCts.Cancel();

                _apiKeyCts.Cancel();
                _apiKeyCts = new CancellationTokenSource();
                _grpcClient = new SimpleGrpcWebClient(apiKey, serverAddress, _apiKeyCts.Token);
                _client = new LfgClient(_grpcClient);
            }
        }

        private async Task TrySubscribeGroups(LfgModel state)
        {
            CancellationToken cancellationToken;
            lock (_stateLock)
            {
                _groupsSubCts?.Cancel();
                _groupsSubCts?.Dispose();
                _groupsSubCts = new CancellationTokenSource();
                cancellationToken = _groupsSubCts.Token;
            }
            try
            {
                await RefreshGroupsAndSubscribe(state, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation, ignore
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "TrySubscribeApplications error");
            }
        }

        private async Task TrySubscribeApplications(LfgModel state)
        {
            CancellationToken cancellationToken;
            lock (_stateLock)
            {
                _applicationsSubCts?.Cancel();
                _applicationsSubCts?.Dispose();
                _applicationsSubCts = new CancellationTokenSource();
                cancellationToken = _applicationsSubCts.Token;
            }
            try
            {
                await RefreshMyApplications(state, cancellationToken);
                await RefreshApplicationsAndSubscribe(state, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation, ignore
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "TrySubscribeApplications error");
            }
        }

        private async Task SendHeartbeats()
        {
            CancellationToken cancellationToken;
            lock (_stateLock)
            {
                _heartbeatCts?.Cancel();
                _heartbeatCts?.Dispose();
                _heartbeatCts = new CancellationTokenSource();
                cancellationToken = _heartbeatCts.Token;
            }

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (!string.IsNullOrWhiteSpace(ApiKey))
                    {
                        try
                        {
                            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                            await _client.SendHeartbeat(cts.Token);
                            IsConnected = true;
                            LastHeartbeat = DateTimeOffset.UtcNow;
                        }
                        catch (OperationCanceledException)
                        {
                            // Normal cancellation, ignore
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "Heartbeat error");
                            IsConnected = false;
                        }
                    }
                    await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation, ignore
            }
            finally
            {
                IsConnected = false;
            }
        }

        public async Task RefreshGroupsAsync()
        {
            if (string.IsNullOrWhiteSpace(State.ApiKey))
            {
                return;
            }

            await _refreshGroupsSemaphore.WaitAsync();
            try
            {
                IsLoadingGroups = true;
                var initialGroups = await _client.ListGroups(CancellationToken.None);
                Groups = initialGroups.Groups.ToImmutableArray();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to refresh groups");
            }
            finally
            {
                IsLoadingGroups = false;
                _refreshGroupsSemaphore.Release();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                lock (_stateLock)
                {
                    AccountNameChanged = null;
                    ApiKeyChanged = null;
                    GroupsChanged = null;
                    MyGroupChanged = null;
                    GroupApplicationsChanged = null;
                    MyApplicationsChanged = null;
                    VisibleChanged = null;
                    IsLoadingGroupsChanged = null;
                    IsLoadingApplicationsChanged = null;
                    _disposed = true;
                }
                _apiKeyCts.Cancel();
                _groupsSubCts.Cancel();
                _applicationsSubCts.Cancel();
            }
        }
    }
}