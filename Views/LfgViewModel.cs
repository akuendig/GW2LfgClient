using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;

namespace Gw2Lfg
{
    // Example implementation
    public class LfgViewModel
    {
        private string _accountName = "";
        public string AccountName
        {
            get => _accountName;
            set
            {
                if (_accountName == value)
                    return;
                _accountName = value;
                AccountNameChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AccountName)));
            }
        }
        public event PropertyChangedEventHandler ApiKeyChanged;

        private string _apiKey = "";
        public string ApiKey
        {
            get => _apiKey;
            set
            {
                if (_apiKey == value)
                    return;
                _apiKey = value;
                ApiKeyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AccountName)));
            }
        }
        public event PropertyChangedEventHandler AccountNameChanged;

        private IEnumerable<Proto.Group> _groups = [];
        public IEnumerable<Proto.Group> Groups
        {
            get => _groups;
            set
            {
                if (_groups == value)
                    return;
                if (_groups != null && value != null && _groups.Select(a => a.Id).OrderBy(a => a).SequenceEqual(value.Select(a => a.Id).OrderBy(a => a)))
                    return;
                _groups = value;
                GroupsChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Groups)));
            }
        }
        public event PropertyChangedEventHandler GroupsChanged;

        public Proto.Group MyGroup
        {
            get; private set;
        }
        public event PropertyChangedEventHandler MyGroupChanged;

        private IEnumerable<Proto.GroupApplication> _groupApplications = [];
        public IEnumerable<Proto.GroupApplication> GroupApplications
        {
            get => _groupApplications;
            set
            {
                if (_groupApplications == value)
                    return;
                if (_groupApplications != null && value != null &&
                    _groupApplications.Select(a => a.GroupId).OrderBy(a => a).SequenceEqual(value.Select(a => a.GroupId).OrderBy(a => a)))
                    return;
                _groupApplications = value;
                GroupApplicationsChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GroupApplications)));
            }
        }
        public event PropertyChangedEventHandler GroupApplicationsChanged;

        public LfgViewModel()
        {
            GroupsChanged += (sender, e) =>
            {
                var myGroup = Groups.FirstOrDefault(group => group.CreatorId == AccountName);
                if (MyGroup?.Id == myGroup?.Id)
                    return;
                MyGroup = myGroup;
                MyGroupChanged?.Invoke(this, e);
            };
        }
    }
}