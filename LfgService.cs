using Blish_HUD;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Gw2Lfg.Proto;

namespace Gw2Lfg
{
    public class LfgClient : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<Gw2LfgModule>();
        private CancellationTokenSource _cts = new();
        private readonly string _clientId;
        private readonly string _apiKey;
        private readonly SimpleGrpcWebClient _client;

        public event Action<Group> OnNewGroup;
        public event Action<Group> OnGroupUpdated;
        public event Action<Group> OnGroupRemoved;

        public LfgClient(SimpleGrpcWebClient client, string key)
        {
            _client = client;
            _clientId = Guid.NewGuid().ToString();
        }

        public async Task StartListening()
        {
            // Implement HTTP-based listening logic here
        }

        public async Task<Group> CreateGroup(string title, uint KillProofMinimum, KillProofId killProofId)
        {
            var request = new CreateGroupRequest
            {
                ClientKey = _apiKey,
                Title = title,
                KillProofMinimum = KillProofMinimum,
                KillProofId = killProofId,
            };

            return await _client.UnaryCallAsync<CreateGroupRequest, Group>("/gw2lfg.LfgService/CreateGroup", request);
        }

        public async Task<JoinGroupResponse> JoinGroup(string groupId, string role, string apiKey)
        {
            var request = new JoinGroupRequest
            {
                ClientKey = _apiKey,
                GroupId = groupId,
                RoleName = role,
            };
            return await _client.UnaryCallAsync<JoinGroupRequest, JoinGroupResponse>("/gw2lfg.LfgService/JoinGroup", request);
        }

        public IAsyncEnumerable<JoinGroupRequest> SubscribeToApplications(string groupId)
        {
            var request = new SubscribeToApplicationsRequest
            {
                ClientKey = _apiKey,
                GroupId = groupId,
            };
            return _client.ServerStreamingCallAsync<SubscribeToApplicationsRequest, JoinGroupRequest>("/gw2lfg.LfgService/SubscribeToApplications", request);
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}