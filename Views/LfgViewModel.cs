#nullable enable

using System.ComponentModel;
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

    public class LfgViewModel : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<LfgViewModel>();
        private readonly object _stateLock = new();
        private LfgModel _state = new("", "", [], null, [], false, false, false);
        private readonly SynchronizationContext _synchronizationContext;
        private bool _disposed;

        // Event handlers
        public event EventHandler<PropertyChangedEventArgs>? AccountNameChanged;
        public event EventHandler<PropertyChangedEventArgs>? ApiKeyChanged;
        public event EventHandler<PropertyChangedEventArgs>? GroupsChanged;
        public event EventHandler<PropertyChangedEventArgs>? MyGroupChanged;
        public event EventHandler<PropertyChangedEventArgs>? GroupApplicationsChanged;
        public event EventHandler<PropertyChangedEventArgs>? VisibleChanged;
        public event EventHandler<PropertyChangedEventArgs>? IsLoadingGroupsChanged;
        public event EventHandler<PropertyChangedEventArgs>? IsLoadingApplicationsChanged;

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

        private void RaisePropertyChanged(string propertyName)
        {
            if (_disposed) return;

            var handler = propertyName switch
            {
                nameof(AccountName) => AccountNameChanged,
                nameof(ApiKey) => ApiKeyChanged,
                nameof(Groups) => GroupsChanged,
                nameof(MyGroup) => MyGroupChanged,
                nameof(GroupApplications) => GroupApplicationsChanged,
                nameof(Visible) => VisibleChanged,
                nameof(IsLoadingGroups) => IsLoadingGroupsChanged,
                nameof(IsLoadingApplications) => IsLoadingApplicationsChanged,
                _ => null
            };

            handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string AccountName
        {
            get
            {
                lock (_stateLock) return _state.AccountName;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));

                bool changed;
                lock (_stateLock)
                {
                    changed = _state.AccountName != value;
                    if (changed)
                    {
                        _state = _state with { AccountName = value };
                    }
                }

                if (changed)
                {
                    RaisePropertyChanged(nameof(AccountName));
                    UpdateMyGroup(); // This will raise its own event if needed
                }
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

                bool changed;
                lock (_stateLock)
                {
                    changed = _state.ApiKey != value;
                    if (changed)
                    {
                        _state = _state with { ApiKey = value };
                    }
                }

                if (changed)
                {
                    RaisePropertyChanged(nameof(ApiKey));
                }
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

                bool changed;
                lock (_stateLock)
                {
                    var newGroups = value.ToArray();
                    changed = !_state.Groups.SequenceEqual(newGroups);
                    if (changed)
                    {
                        _state = _state with { Groups = [.. newGroups] };
                    }
                }

                if (changed)
                {
                    UpdateMyGroup(); // This will raise its own event if needed
                    RaisePropertyChanged(nameof(Groups));
                }
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
                bool changed;
                lock (_stateLock)
                {
                    changed = !Equals(_state.MyGroup?.Id, value?.Id);
                    if (changed)
                    {
                        _state = _state with { MyGroup = value };
                    }
                }

                if (changed)
                {
                    RaisePropertyChanged(nameof(MyGroup));
                }
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

                bool changed;
                lock (_stateLock)
                {
                    var newApplications = value.ToArray();
                    changed = !_state.GroupApplications.SequenceEqual(newApplications);
                    if (changed)
                    {
                        _state = _state with { GroupApplications = [..newApplications] };
                    }
                }

                if (changed)
                {
                    RaisePropertyChanged(nameof(GroupApplications));
                }
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
                bool changed;
                lock (_stateLock)
                {
                    changed = _state.Visible != value;
                    if (changed)
                    {
                        _state = _state with { Visible = value };
                    }
                }

                if (changed)
                {
                    RaisePropertyChanged(nameof(Visible));
                }
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
                bool changed;
                lock (_stateLock)
                {
                    changed = _state.IsLoadingGroups != value;
                    if (changed)
                    {
                        _state = _state with { IsLoadingGroups = value };
                    }
                }

                if (changed)
                {
                    RaisePropertyChanged(nameof(IsLoadingGroups));
                }
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
                bool changed;
                lock (_stateLock)
                {
                    changed = _state.IsLoadingApplications != value;
                    if (changed)
                    {
                        _state = _state with { IsLoadingApplications = value };
                    }
                }

                if (changed)
                {
                    RaisePropertyChanged(nameof(IsLoadingApplications));
                }
            }
        }

        public void AddGroup(Proto.Group group)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));

            bool changed;
            lock (_stateLock)
            {
                changed = !_state.Groups.Any(g => g.Id == group.Id);
                if (changed)
                {
                    _state = _state with { Groups = [.. _state.Groups, group] };
                }
            }

            if (changed)
            {
                RaisePropertyChanged(nameof(Groups));
                UpdateMyGroup();
            }
        }

        public void UpdateGroup(Proto.Group updatedGroup)
        {
            if (updatedGroup == null) throw new ArgumentNullException(nameof(updatedGroup));

            bool changed;
            lock (_stateLock)
            {
                var old = _state.Groups.FirstOrDefault(g => g.Id != updatedGroup.Id);
                changed = old != null && !old.Equals(updatedGroup);
                if (changed)
                {
                    _state = _state with { Groups = _state.Groups.Replace(old, updatedGroup) };
                }
            }

            if (changed)
            {
                RaisePropertyChanged(nameof(Groups));
                UpdateMyGroup();
            }
        }

        public void RemoveGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId)) throw new ArgumentException("Group ID cannot be null or empty", nameof(groupId));

            bool changed;
            lock (_stateLock)
            {
                var newGroups = _state.Groups.Where(g => g.Id != groupId).ToImmutableArray();;
                changed = newGroups.Length != _state.Groups.Length;
                if (changed)
                {
                    _state = _state with { Groups = newGroups };
                }
            }

            if (changed)
            {
                RaisePropertyChanged(nameof(Groups));
                UpdateMyGroup();
            }
        }

        private void UpdateMyGroup()
        {
            Proto.Group? newMyGroup;
            bool changed;

            lock (_stateLock)
            {
                newMyGroup = _state.Groups.FirstOrDefault(g => g.CreatorId == _state.AccountName);
                changed = !Equals(_state.MyGroup, newMyGroup);
                if (changed)
                {
                    _state = _state with { MyGroup = newMyGroup };
                }
            }

            if (changed)
            {
                RaisePropertyChanged(nameof(MyGroup));
            }
        }

        public void UpdateApplication(Proto.GroupApplication updatedApplication)
        {
            if (updatedApplication == null) throw new ArgumentNullException(nameof(updatedApplication));

            bool changed;
            lock (_stateLock)
            {
                var old = _state.GroupApplications.FirstOrDefault(a => a.Id == updatedApplication.Id);
                changed = old != null && !old.Equals(updatedApplication);
                if (changed)
                {
                    _state = _state with { GroupApplications = _state.GroupApplications.Replace(old, updatedApplication) };
                }
            }

            if (changed)
            {
                RaisePropertyChanged(nameof(GroupApplications));
            }
        }

        public void RemoveApplication(string applicationId)
        {
            if (string.IsNullOrEmpty(applicationId)) throw new ArgumentException("Application ID cannot be null or empty", nameof(applicationId));

            bool changed;
            lock (_stateLock)
            {
                var newApplications = _state.GroupApplications.Where(a => a.Id != applicationId).ToImmutableArray();
                changed = newApplications.Length != _state.GroupApplications.Length;
                if (changed)
                {
                    _state = _state with { GroupApplications = newApplications };
                }
            }

            if (changed)
            {
                RaisePropertyChanged(nameof(GroupApplications));
            }
        }

        public void AddApplication(Proto.GroupApplication newApplication)
        {
            if (newApplication == null) throw new ArgumentNullException(nameof(newApplication));

            bool changed;
            lock (_stateLock)
            {
                changed = !_state.GroupApplications.Any(a => a.Id == newApplication.Id);
                if (changed)
                {
                    _state = _state with { GroupApplications = [.. _state.GroupApplications, newApplication] };
                }
            }

            if (changed)
            {
                RaisePropertyChanged(nameof(GroupApplications));
            }
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
                Groups = [.. initialGroups.Groups];
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
                GroupApplications = [.. initialApplications.Applications];
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

        private void OnVisibleChanged(object sender, PropertyChangedEventArgs e)
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

        private async void OnApiKeyChanged(object sender, PropertyChangedEventArgs e)
        {
            Connect(ApiKey);
            await TrySubscribeGroups();
            await TrySubscribeApplications();
        }

        private async void OnGroupsChanged(object sender, PropertyChangedEventArgs e)
        {
            // TODO: This now runs on every heartbeat because the update time of the group
            // is changed. Should it?
            await TrySubscribeApplications();
        }

        private async void OnMyGroupChanged(object sender, PropertyChangedEventArgs e)
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