#nullable enable

using System.Linq;
using System;
using Blish_HUD;
using System.Threading;
using System.Collections.Immutable;
using System.Net.Http;
using System.Threading.Tasks;

namespace System.Runtime.CompilerServices
{
    // Define this type because we are comiling against a .NET version lower than 5.0.
    internal static class IsExternalInit { }
}

namespace Gw2Lfg
{
    public record LfgModel(
        string AccountName,
        string ApiKey,
        ImmutableArray<Proto.Group> Groups,
        Proto.Group? MyGroup,
        ImmutableArray<Proto.GroupApplication> GroupApplications,
        bool Visible,
        bool IsLoadingGroups,
        bool IsLoadingApplications
    );

    public class LfgViewModelPropertyChangedEventArgs<T>(LfgModel oldState, LfgModel newState, Func<LfgModel, T> lens) :
     LfgPropertyChangedEventArgs<LfgModel, T>(oldState, newState, lens)
    { };

    public class LfgViewModel : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<LfgViewModel>();
        private readonly object _stateLock = new();
        private LfgModel _state = new("", "", ImmutableArray<Proto.Group>.Empty, null, ImmutableArray<Proto.GroupApplication>.Empty, false, false, false);
        private readonly SynchronizationContext _synchronizationContext;
        private bool _disposed;

        // Event handlers
        public event EventHandler<LfgViewModelPropertyChangedEventArgs<string>>? AccountNameChanged;
        public event EventHandler<LfgViewModelPropertyChangedEventArgs<string>>? ApiKeyChanged;
        public event EventHandler<LfgViewModelPropertyChangedEventArgs<ImmutableArray<Proto.Group>>>? GroupsChanged;
        public event EventHandler<LfgViewModelPropertyChangedEventArgs<Proto.Group?>>? MyGroupChanged;
        public event EventHandler<LfgViewModelPropertyChangedEventArgs<ImmutableArray<Proto.GroupApplication>>>? GroupApplicationsChanged;
        public event EventHandler<LfgViewModelPropertyChangedEventArgs<bool>>? VisibleChanged;
        public event EventHandler<LfgViewModelPropertyChangedEventArgs<bool>>? IsLoadingGroupsChanged;
        public event EventHandler<LfgViewModelPropertyChangedEventArgs<bool>>? IsLoadingApplicationsChanged;

        private readonly HttpClient _httpClient;
        private SimpleGrpcWebClient _grpcClient;
        private LfgClient _client;
        private CancellationTokenSource _apiKeyCts = new();
        private CancellationTokenSource _groupsSubCts = new();
        private CancellationTokenSource _applicationsSubCts = new();
        private CancellationTokenSource _heartbeatCts = new();

        public LfgViewModel(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _synchronizationContext = SynchronizationContext.Current ?? new SynchronizationContext();

            ApiKeyChanged += OnApiKeyChanged;
            // GroupsChanged += OnGroupsChanged;
            MyGroupChanged += OnMyGroupChanged;
            VisibleChanged += OnVisibleChanged;
        }

        private void RaisePropertyChanged<T>(string propertyName, LfgModel oldState, LfgModel newState, Func<LfgModel, T> lens)
        {
            if (_disposed) return;

            var handler = propertyName switch
            {
                nameof(AccountName) => AccountNameChanged as EventHandler<LfgViewModelPropertyChangedEventArgs<T>>,
                nameof(ApiKey) => ApiKeyChanged as EventHandler<LfgViewModelPropertyChangedEventArgs<T>>,
                nameof(Groups) => GroupsChanged as EventHandler<LfgViewModelPropertyChangedEventArgs<T>>,
                nameof(MyGroup) => MyGroupChanged as EventHandler<LfgViewModelPropertyChangedEventArgs<T>>,
                nameof(GroupApplications) => GroupApplicationsChanged as EventHandler<LfgViewModelPropertyChangedEventArgs<T>>,
                nameof(Visible) => VisibleChanged as EventHandler<LfgViewModelPropertyChangedEventArgs<T>>,
                nameof(IsLoadingGroups) => IsLoadingGroupsChanged as EventHandler<LfgViewModelPropertyChangedEventArgs<T>>,
                nameof(IsLoadingApplications) => IsLoadingApplicationsChanged as EventHandler<LfgViewModelPropertyChangedEventArgs<T>>,
                _ => null
            };

            handler?.Invoke(this, new LfgViewModelPropertyChangedEventArgs<T>(oldState, newState, lens));
        }

        public LfgModel State { get { lock (_stateLock) return _state; } }

        public string AccountName
        {
            get
            {
                lock (_stateLock) return _state.AccountName;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));

                LfgModel oldState;
                LfgModel newState;
                lock (_stateLock)
                {
                    if (_state.AccountName != value)
                    {
                        oldState = _state;
                        _state = _state with { AccountName = value };
                        newState = _state;
                    }
                    else return;
                }

                RaisePropertyChanged(nameof(AccountName), oldState, newState, s => s.AccountName);
                UpdateMyGroup();
            }
        }

        public string ApiKey
        {
            get
            {
                lock (_stateLock) return _state.ApiKey;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));

                LfgModel oldState;
                LfgModel newState;
                lock (_stateLock)
                {
                    if (_state.ApiKey != value)
                    {
                        oldState = _state;
                        _state = _state with { ApiKey = value };
                        newState = _state;
                    }
                    else return;
                }

                RaisePropertyChanged(nameof(ApiKey), oldState, newState, s => s.ApiKey);
            }
        }

        public ImmutableArray<Proto.Group> Groups
        {
            get
            {
                lock (_stateLock) return _state.Groups;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));

                LfgModel oldState;
                LfgModel newState;
                lock (_stateLock)
                {
                    if (!_state.Groups.SequenceEqual(value))
                    {
                        oldState = _state;
                        _state = _state with { Groups = value };
                        newState = _state;
                    }
                    else return;
                }

                UpdateMyGroup();
                RaisePropertyChanged(nameof(Groups), oldState, newState, s => s.Groups);
            }
        }

        public Proto.Group? MyGroup
        {
            get
            {
                lock (_stateLock) return _state.MyGroup;
            }
            private set
            {
                LfgModel oldState;
                LfgModel newState;
                lock (_stateLock)
                {
                    if (!Equals(_state.MyGroup?.Id, value?.Id))
                    {
                        oldState = _state;
                        _state = _state with { MyGroup = value };
                        newState = _state;
                    }
                    else return;
                }

                RaisePropertyChanged(nameof(MyGroup), oldState, newState, s => s.MyGroup);
            }
        }

        public ImmutableArray<Proto.GroupApplication> GroupApplications
        {
            get
            {
                lock (_stateLock) return _state.GroupApplications;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));

                LfgModel oldState;
                LfgModel newState;
                lock (_stateLock)
                {
                    if (!_state.GroupApplications.SequenceEqual(value))
                    {
                        oldState = _state;
                        _state = _state with { GroupApplications = value };
                        newState = _state;
                    }
                    else return;
                }

                RaisePropertyChanged(nameof(GroupApplications), oldState, newState, s => s.GroupApplications);
            }
        }

        public bool Visible
        {
            get
            {
                lock (_stateLock) return _state.Visible;
            }
            set
            {
                LfgModel oldState;
                LfgModel newState;
                lock (_stateLock)
                {
                    if (_state.Visible != value)
                    {
                        oldState = _state;
                        _state = _state with { Visible = value };
                        newState = _state;
                    }
                    else return;
                }

                RaisePropertyChanged(nameof(Visible), oldState, newState, s => s.Visible);
            }
        }

        public bool IsLoadingGroups
        {
            get
            {
                lock (_stateLock) return _state.IsLoadingGroups;
            }
            set
            {
                LfgModel oldState;
                LfgModel newState;
                lock (_stateLock)
                {
                    if (_state.IsLoadingGroups != value)
                    {
                        oldState = _state;
                        _state = _state with { IsLoadingGroups = value };
                        newState = _state;
                    }
                    else return;
                }

                RaisePropertyChanged(nameof(IsLoadingGroups), oldState, newState, s => s.IsLoadingGroups);
            }
        }

        public bool IsLoadingApplications
        {
            get
            {
                lock (_stateLock) return _state.IsLoadingApplications;
            }
            set
            {
                LfgModel oldState;
                LfgModel newState;
                lock (_stateLock)
                {
                    if (_state.IsLoadingApplications != value)
                    {
                        oldState = _state;
                        _state = _state with { IsLoadingApplications = value };
                        newState = _state;
                    }
                    else return;
                }

                RaisePropertyChanged(nameof(IsLoadingApplications), oldState, newState, s => s.IsLoadingApplications);
            }
        }

        public void AddGroup(Proto.Group group)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));

            LfgModel oldState;
            LfgModel newState;
            lock (_stateLock)
            {
                if (!_state.Groups.Any(g => g.Id == group.Id))
                {
                    oldState = _state;
                    _state = _state with { Groups = _state.Groups.Add(group) };
                    newState = _state;
                }
                else return;
            }

            RaisePropertyChanged(nameof(Groups), oldState, newState, s => s.Groups);
            UpdateMyGroup();
        }

        public void UpdateGroup(Proto.Group updatedGroup)
        {
            if (updatedGroup == null) throw new ArgumentNullException(nameof(updatedGroup));

            LfgModel oldState;
            LfgModel newState;
            lock (_stateLock)
            {
                var old = _state.Groups.FirstOrDefault(g => g.Id != updatedGroup.Id);
                if (old != null && !old.Equals(updatedGroup))
                {
                    oldState = _state;
                    _state = _state with { Groups = _state.Groups.Replace(old, updatedGroup) };
                    newState = _state;
                }
                else return;
            }

            RaisePropertyChanged(nameof(Groups), oldState, newState, s => s.Groups);
            UpdateMyGroup();
        }

        public void RemoveGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId)) throw new ArgumentException("Group ID cannot be null or empty", nameof(groupId));

            LfgModel oldState;
            LfgModel newState;
            lock (_stateLock)
            {
                var newGroups = _state.Groups.Where(g => g.Id != groupId).ToImmutableArray();
                if (newGroups.Length != _state.Groups.Length)
                {
                    oldState = _state;
                    _state = _state with { Groups = newGroups };
                    newState = _state;
                }
                else return;
            }

            RaisePropertyChanged(nameof(Groups), oldState, newState, s => s.Groups);
            UpdateMyGroup();
        }

        public void UpdateApplication(Proto.GroupApplication updatedApplication)
        {
            if (updatedApplication == null) throw new ArgumentNullException(nameof(updatedApplication));

            LfgModel oldState;
            LfgModel newState;
            lock (_stateLock)
            {
                var old = _state.GroupApplications.FirstOrDefault(a => a.Id == updatedApplication.Id);
                if (old != null && !old.Equals(updatedApplication))
                {
                    oldState = _state;
                    _state = _state with { GroupApplications = _state.GroupApplications.Replace(old, updatedApplication) };
                    newState = _state;
                }
                else return;
            }

            RaisePropertyChanged(nameof(GroupApplications), oldState, newState, s => s.GroupApplications);
        }

        public void RemoveApplication(string applicationId)
        {
            if (string.IsNullOrEmpty(applicationId)) throw new ArgumentException("Application ID cannot be null or empty", nameof(applicationId));

            LfgModel oldState;
            LfgModel newState;
            lock (_stateLock)
            {
                var newApplications = _state.GroupApplications.Where(a => a.Id != applicationId).ToImmutableArray();
                if (newApplications.Length != _state.GroupApplications.Length)
                {
                    oldState = _state;
                    _state = _state with { GroupApplications = newApplications };
                    newState = _state;
                }
                else return;
            }

            RaisePropertyChanged(nameof(GroupApplications), oldState, newState, s => s.GroupApplications);
        }

        public void AddApplication(Proto.GroupApplication newApplication)
        {
            if (newApplication == null) throw new ArgumentNullException(nameof(newApplication));

            LfgModel oldState;
            LfgModel newState;
            lock (_stateLock)
            {
                if (!_state.GroupApplications.Any(a => a.Id == newApplication.Id))
                {
                    oldState = _state;
                    _state = _state with { GroupApplications = _state.GroupApplications.Add(newApplication) };
                    newState = _state;
                }
                else return;
            }

            RaisePropertyChanged(nameof(GroupApplications), oldState, newState, s => s.GroupApplications);
        }

        private void UpdateMyGroup()
        {
            LfgModel oldState;
            LfgModel newState;
            lock (_stateLock)
            {
                var newMyGroup = _state.Groups.FirstOrDefault(g => g.CreatorId == _state.AccountName);
                if (!Equals(_state.MyGroup?.Id, newMyGroup?.Id))
                {
                    oldState = _state;
                    _state = _state with { MyGroup = newMyGroup };
                    newState = _state;
                }
                else return;
            }

            RaisePropertyChanged(nameof(MyGroup), oldState, newState, s => s.MyGroup);
        }

        private async Task RefreshGroupsAndSubscribe()
        {
            // TODO: This should probably retry in case the server goes away?
            if (string.IsNullOrEmpty(ApiKey))
            {
                return;
            }

            CancellationToken cancellationToken = _groupsSubCts.Token;
            try
            {
                IsLoadingGroups = true;
                var initialGroups = await _client.ListGroups(cancellationToken);
                Groups = initialGroups.Groups.ToImmutableArray();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize groups");
            }
            finally { IsLoadingGroups = false; }

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

        private async Task RefreshApplicationsAndSubscribe()
        {
            if (string.IsNullOrEmpty(ApiKey) || MyGroup == null)
            {
                return;
            }
            string myGroupId = MyGroup.Id;
            CancellationToken cancellationToken = _applicationsSubCts.Token;

            // TODO: This should probably retry in case the server goes away?
            try
            {
                IsLoadingApplications = true;
                var initialApplications = await _client.ListGroupApplications(myGroupId, cancellationToken);
                GroupApplications = initialApplications.Applications.ToImmutableArray();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize applications");
            }
            finally { IsLoadingApplications = false; }

            _ = Task.Run(async () =>
            {
                try
                {
                    await foreach (var update in _client.SubscribeGroupApplications(myGroupId, cancellationToken))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        switch (update.UpdateCase)
                        {
                            case Proto.GroupApplicationUpdate.UpdateOneofCase.NewApplication:
                                AddApplication(update.NewApplication);
                                break;
                            case Proto.GroupApplicationUpdate.UpdateOneofCase.RemovedApplicationId:
                                RemoveApplication(update.RemovedApplicationId);
                                break;
                            case Proto.GroupApplicationUpdate.UpdateOneofCase.UpdatedApplication:
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

        private void OnVisibleChanged(object sender, LfgViewModelPropertyChangedEventArgs<bool> e)
        {
            if (Visible)
            {
                SendHeartbeats();
                TrySubscribeGroups();
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
            Connect(ApiKey);
            await TrySubscribeGroups();
            await TrySubscribeApplications();
        }

        private async void OnGroupsChanged(object sender, LfgViewModelPropertyChangedEventArgs<ImmutableArray<Proto.Group>> e)
        {
            // TODO: This now runs on every heartbeat because the update time of the group
            // is changed. Should it?
            await TrySubscribeApplications();
        }

        private async void OnMyGroupChanged(object sender, LfgViewModelPropertyChangedEventArgs<Proto.Group?> e)
        {
            // TODO: This now runs on every heartbeat because the update time of the group
            // is changed. Should it?
            await TrySubscribeApplications();
        }

        public void Connect(string apiKey)
        {
            lock (_stateLock)
            {
                _applicationsSubCts.Cancel();
                _groupsSubCts.Cancel();
                _heartbeatCts.Cancel();

                _apiKeyCts.Cancel();
                _apiKeyCts = new CancellationTokenSource();
                _grpcClient = new SimpleGrpcWebClient(_httpClient, apiKey, _apiKeyCts.Token);
                _client = new LfgClient(_grpcClient);
            }
        }

        private async Task TrySubscribeGroups()
        {
            _groupsSubCts?.Cancel();
            _groupsSubCts?.Dispose();
            _groupsSubCts = new CancellationTokenSource();
            await RefreshGroupsAndSubscribe();
        }

        private async Task TrySubscribeApplications()
        {
            _applicationsSubCts?.Cancel();
            _applicationsSubCts?.Dispose();
            _applicationsSubCts = new CancellationTokenSource();
            await RefreshApplicationsAndSubscribe();
        }

        private async Task SendHeartbeats()
        {
            _heartbeatCts?.Cancel();
            _heartbeatCts?.Dispose();
            _heartbeatCts = new CancellationTokenSource();

            try
            {
                CancellationToken cancellationToken = _heartbeatCts.Token;
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (!string.IsNullOrEmpty(ApiKey))
                    {
                        await _client.SendHeartbeat(cancellationToken);
                    }
                    await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation, ignore
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Heartbeat error");
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