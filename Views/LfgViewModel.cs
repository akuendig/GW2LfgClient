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
        public event PropertyChangedEventHandler ApiKeyChanged;

        private string _apiKey = "";
        public string ApiKey
        {
            get => _apiKey;
            set {
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