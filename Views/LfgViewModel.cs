#nullable enable

using System.ComponentModel;
using System.Linq;
using System;
using Blish_HUD;
using System.Threading;

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
        private Proto.Group[] _groups = Array.Empty<Proto.Group>();
        private Proto.GroupApplication[] _groupApplications = Array.Empty<Proto.GroupApplication>();
        private Proto.Group? _myGroup;

        // Event handlers
        public event EventHandler<PropertyChangedEventArgs>? AccountNameChanged;
        public event EventHandler<PropertyChangedEventArgs>? ApiKeyChanged;
        public event EventHandler<PropertyChangedEventArgs>? GroupsChanged;
        public event EventHandler<PropertyChangedEventArgs>? MyGroupChanged;
        public event EventHandler<PropertyChangedEventArgs>? GroupApplicationsChanged;

        public LfgViewModel()
        {
            // Capture the UI synchronization context when the ViewModel is created
            _synchronizationContext = SynchronizationContext.Current ?? new SynchronizationContext();
        }

        private void RaisePropertyChanged(string propertyName)
        {
            if (_disposed) return;

            // Raise the event on the UI thread
            _synchronizationContext.Post(_ =>
            {
                if (!_disposed)
                {
                    switch (propertyName)
                    {
                        case nameof(AccountName):
                            AccountNameChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                            break;
                        case nameof(ApiKey):
                            ApiKeyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                            break;
                        case nameof(Groups):
                            GroupsChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                            break;
                        case nameof(MyGroup):
                            MyGroupChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                            break;
                        case nameof(GroupApplications):
                            GroupApplicationsChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                            break;
                    }
                }
            }, null);
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
                    RaisePropertyChanged(nameof(Groups));
                    UpdateMyGroup(); // This will raise its own event if needed
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
                lock (_stateLock) return [.. _groupApplications];
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));

                bool changed;
                lock (_stateLock)
                {
                    var newApps = value.ToArray();
                    changed = !_groupApplications.SequenceEqual(newApps);
                    if (changed)
                    {
                        _groupApplications = newApps;
                    }
                }

                if (changed)
                {
                    RaisePropertyChanged(nameof(GroupApplications));
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
            }
        }
    }
}