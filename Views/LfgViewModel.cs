using System.ComponentModel;
using System.Collections.Generic;

namespace Gw2Lfg
{
    // Example implementation
    public class LfgViewModel
    {
        
        private string _accountName = "";
        public string AccountName
        {
            get => _accountName;
            set {
                if (_accountName == value)
                    return;
                _accountName = value;
                AccountNameChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AccountName)));
            }
        }
        public event PropertyChangedEventHandler AccountNameChanged;

        private List<Proto.Group> _groups = new();
        public List<Proto.Group> Groups
        {
            get => _groups;
            set {
                if (_groups == value)
                    return;
                _groups = value;
                GroupsChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Groups)));
            }
        }
        public event PropertyChangedEventHandler GroupsChanged;
    }
}