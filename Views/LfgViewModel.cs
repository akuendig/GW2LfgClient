#nullable enable

using System.ComponentModel;
using System.Linq;
using System;
using Blish_HUD;
using System.Threading;
using System.Net.Http;
using System.Threading.Tasks;

namespace Gw2Lfg
{
    public class LfgViewModel : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<LfgViewModel>();
        private readonly object _stateLock = new();
        private readonly SynchronizationContext _synchronizationContext;
        private bool _disposed;

        // State containers
        private string _accountName = "";
        private string _apiKey = "";
        private Proto.Group[] _groups = [];
        private Proto.GroupApplication[] _groupApplications = [];
        private Proto.Group? _myGroup;
        private bool _visible = false;
        private bool _isLoadingGroups = false;
        private bool _isLoadingApplications = false;

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

        public LfgViewModel(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _synchronizationContext = SynchronizationContext.Current ?? new SynchronizationContext();

            ApiKeyChanged += OnApiKeyChanged;
            GroupsChanged += OnGroupsChanged;
        }

        public void Connect(string apiKey)
        {
            _apiKeyCts.Cancel();
            _apiKeyCts.Dispose();
            _apiKeyCts = new CancellationTokenSource();
            _grpcClient = new SimpleGrpcWebClient(_httpClient, apiKey, _apiKeyCts.Token);
            _client = new LfgClient(_grpcClient);
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
                lock (_stateLock) return _accountName;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));

                bool changed;
                lock (_stateLock)
                {
                    changed = _accountName != value;
                    if (changed)
                    {
                        _accountName = value;
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
                lock (_stateLock) return _apiKey;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));

                bool changed;
                lock (_stateLock)
                {
                    changed = _apiKey != value;
                    if (changed)
                    {
                        _apiKey = value;
                    }
                }

                if (changed)
                {
                    RaisePropertyChanged(nameof(ApiKey));
                }
            }
        }

        public Proto.Group[] Groups
        {
            get
            {
                lock (_stateLock) return [.. _groups];
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));

                bool changed;
                lock (_stateLock)
                {
                    var newGroups = value.ToArray();
                    changed = !_groups.SequenceEqual(newGroups);
                    if (changed)
                    {
                        _groups = newGroups;
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
                lock (_stateLock) return _myGroup;
            }
            private set
            {
                bool changed;
                lock (_stateLock)
                {
                    changed = !Equals(_myGroup?.Id, value?.Id);
                    if (changed)
                    {
                        _myGroup = value;
                    }
                }

                if (changed)
                {
                    RaisePropertyChanged(nameof(MyGroup));
                }
            }
        }

        public Proto.GroupApplication[] GroupApplications
        {
            get
            {
                lock (_stateLock) return _groupApplications.ToArray();
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));

                bool changed;
                lock (_stateLock)
                {
                    var newApplications = value.ToArray();
                    changed = !_groupApplications.SequenceEqual(newApplications);
                    if (changed)
                    {
                        _groupApplications = newApplications;
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
                lock (_stateLock) return _visible;
            }
            set
            {
                bool changed;
                lock (_stateLock)
                {
                    changed = _visible != value;
                    if (changed)
                    {
                        _visible = value;
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
                lock (_stateLock) return _isLoadingGroups;
            }
            set
            {
                bool changed;
                lock (_stateLock)
                {
                    changed = _isLoadingGroups != value;
                    if (changed)
                    {
                        _isLoadingGroups = value;
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
                lock (_stateLock) return _isLoadingApplications;
            }
            set
            {
                bool changed;
                lock (_stateLock)
                {
                    changed = _isLoadingApplications != value;
                    if (changed)
                    {
                        _isLoadingApplications = value;
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
                changed = !_groups.Any(g => g.Id == group.Id);
                if (changed)
                {
                    _groups = [.. _groups, group];
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
                var index = Array.FindIndex(_groups, g => g.Id == updatedGroup.Id);
                changed = index != -1 && !_groups[index].Equals(updatedGroup);
                if (changed)
                {
                    var newGroups = _groups.ToArray();
                    newGroups[index] = updatedGroup;
                    _groups = newGroups;
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
                var newGroups = _groups.Where(g => g.Id != groupId).ToArray();
                changed = newGroups.Length != _groups.Length;
                if (changed)
                {
                    _groups = newGroups;
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
                newMyGroup = _groups.FirstOrDefault(g => g.CreatorId == _accountName);
                changed = !Equals(_myGroup?.Id, newMyGroup?.Id);
                if (changed)
                {
                    _myGroup = newMyGroup;
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
                var index = Array.FindIndex(_groupApplications, a => a.Id == updatedApplication.Id);
                changed = index != -1 && !_groupApplications[index].Equals(updatedApplication);
                if (changed)
                {
                    var newApplications = _groupApplications.ToArray();
                    newApplications[index] = updatedApplication;
                    _groupApplications = newApplications;
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
                var newApplications = _groupApplications.Where(a => a.Id != applicationId).ToArray();
                changed = newApplications.Length != _groupApplications.Length;
                if (changed)
                {
                    _groupApplications = newApplications;
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
                changed = !_groupApplications.Any(a => a.Id == newApplication.Id);
                if (changed)
                {
                    _groupApplications = [.. _groupApplications, newApplication];
                }
            }

            if (changed)
            {
                RaisePropertyChanged(nameof(GroupApplications));
            }
        }

        private async Task RefreshGroupsAndSubscribe()
        {
            if (string.IsNullOrEmpty(ApiKey))
            {
                return;
            }

            try
            {
                CancellationToken cancellationToken = _groupsSubCts.Token;
                IsLoadingGroups = true;
                var initialGroups = await _client.ListGroups(cancellationToken);
                Groups = initialGroups.Groups.ToArray();

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
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize groups");
            }
            finally { IsLoadingGroups = false; }
        }

        private async Task RefreshApplicationsAndSubscribe()
        {
            if (string.IsNullOrEmpty(ApiKey) || MyGroup == null)
            {
                return;
            }
            string myGroupId = MyGroup.Id;

            try
            {
                CancellationToken cancellationToken = _applicationsSubCts.Token;
                IsLoadingApplications = true;
                var initialApplications = await _client.ListGroupApplications(myGroupId, cancellationToken);
                GroupApplications = initialApplications.Applications.ToArray();

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
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize applications");
            }
            finally { IsLoadingApplications = false; }
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

        private async void OnApiKeyChanged(object sender, PropertyChangedEventArgs e)
        {
            Connect(ApiKey);
            await TrySubscribeGroups();
            await TrySubscribeApplications();
        }

        private async void OnGroupsChanged(object sender, PropertyChangedEventArgs e)
        {
            await TrySubscribeApplications();
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
                    _disposed = true;
                }
                _apiKeyCts.Cancel();
                _groupsSubCts.Cancel();
                _applicationsSubCts.Cancel();
            }
        }
    }
}