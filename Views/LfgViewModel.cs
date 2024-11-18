#nullable enable

using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System;
using Blish_HUD;

namespace Gw2Lfg
{
    public class LfgViewModel : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<LfgViewModel>();
        private readonly object _stateLock = new();
        private bool _disposed;

        // State containers
        private string _accountName = "";
        private string _apiKey = "";
        private Proto.Group[] _groups = Array.Empty<Proto.Group>();
        private Proto.GroupApplication[] _groupApplications = Array.Empty<Proto.GroupApplication>();
        private Proto.Group? _myGroup;

        // Event handlers with thread safety
        public event EventHandler<PropertyChangedEventArgs>? AccountNameChanged;
        public event EventHandler<PropertyChangedEventArgs>? ApiKeyChanged;
        public event EventHandler<PropertyChangedEventArgs>? GroupsChanged;
        public event EventHandler<PropertyChangedEventArgs>? MyGroupChanged;
        public event EventHandler<PropertyChangedEventArgs>? GroupApplicationsChanged;

        public string AccountName
        {
            get
            {
                lock (_stateLock) return _accountName;
            }
            set
            {
                bool changed;
                lock (_stateLock)
                {
                    changed = _accountName != value;
                    if (changed) _accountName = value;
                }
                if (changed)
                {
                    AccountNameChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AccountName)));
                    UpdateMyGroup(); // Recalculate MyGroup when account name changes
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
                bool changed;
                lock (_stateLock)
                {
                    changed = _apiKey != value;
                    if (changed) _apiKey = value;
                }
                if (changed)
                {
                    ApiKeyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ApiKey)));
                }
            }
        }

        public Proto.Group[] Groups
        {
            get
            {
                lock (_stateLock) return _groups.ToArray();
            }
            set
            {
                bool changed;
                lock (_stateLock)
                {
                    var newGroups = value ?? Array.Empty<Proto.Group>();
                    changed = !_groups.SequenceEqual(newGroups);
                    if (changed)
                    {
                        _groups = newGroups.ToArray();
                    }
                }
                if (changed)
                {
                    GroupsChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Groups)));
                    UpdateMyGroup(); // Recalculate MyGroup when groups change
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
                    if (changed) _myGroup = value;
                }
                if (changed)
                {
                    MyGroupChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MyGroup)));
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
                bool changed;
                lock (_stateLock)
                {
                    var newApps = value ?? Array.Empty<Proto.GroupApplication>();
                    changed = !_groupApplications.SequenceEqual(newApps);
                    if (changed)
                    {
                        _groupApplications = newApps.ToArray();
                    }
                }
                if (changed)
                {
                    GroupApplicationsChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GroupApplications)));
                }
            }
        }

        // Methods for atomic state updates
        public void UpdateGroup(Proto.Group updatedGroup)
        {
            lock (_stateLock)
            {
                var index = Array.FindIndex(_groups, g => g.Id == updatedGroup.Id);
                if (index != -1)
                {
                    var newGroups = _groups.ToArray();
                    newGroups[index] = updatedGroup;
                    Groups = newGroups;
                }
            }
        }

        public void RemoveGroup(string groupId)
        {
            lock (_stateLock)
            {
                Groups = _groups.Where(g => g.Id != groupId).ToArray();
            }
        }

        public void AddGroup(Proto.Group group)
        {
            lock (_stateLock)
            {
                if (!_groups.Any(g => g.Id == group.Id))
                {
                    Groups = _groups.Append(group).ToArray();
                }
            }
        }

        public void UpdateApplication(Proto.GroupApplication application)
        {
            lock (_stateLock)
            {
                var index = Array.FindIndex(_groupApplications, a => a.Id == application.Id);
                if (index != -1)
                {
                    var newApps = _groupApplications.ToArray();
                    newApps[index] = application;
                    GroupApplications = newApps;
                }
                else
                {
                    GroupApplications = _groupApplications.Append(application).ToArray();
                }
            }
        }

        public void RemoveApplication(string applicationId)
        {
            lock (_stateLock)
            {
                GroupApplications = _groupApplications.Where(a => a.Id != applicationId).ToArray();
            }
        }

        private void UpdateMyGroup()
        {
            lock (_stateLock)
            {
                MyGroup = _groups.FirstOrDefault(g => g.CreatorId == _accountName);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // Clear all event handlers
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