using Blish_HUD;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Gw2Lfg.Proto;

namespace Gw2Lfg
{
    public class LfgClient
    {
        private static readonly Logger Logger = Logger.GetLogger<Gw2LfgModule>();
        private readonly SimpleGrpcWebClient _client;
    
        public string ApiKey { get; set; }

        public LfgClient(SimpleGrpcWebClient client, string apiKey)
        {
            _client = client;
            ApiKey = apiKey;
        }

        public async Task<Group> CreateGroup(string title, uint KillProofMinimum, KillProofId killProofId)
        {
            var request = new CreateGroupRequest
            {
                ClientKey = ApiKey,
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
                ClientKey = ApiKey,
                GroupId = groupId,
                RoleName = role,
            };
            return await _client.UnaryCallAsync<JoinGroupRequest, JoinGroupResponse>("/gw2lfg.LfgService/JoinGroup", request);
        }

        public IAsyncEnumerable<JoinGroupRequest> SubscribeToApplications(string groupId)
        {
            var request = new SubscribeToApplicationsRequest
            {
                ClientKey = ApiKey,
                GroupId = groupId,
            };
            return _client.ServerStreamingCallAsync<SubscribeToApplicationsRequest, JoinGroupRequest>("/gw2lfg.LfgService/SubscribeToApplications", request);
        }

        public IAsyncEnumerable<GroupsUpdate> SubscribeGroups()
        {
            var request = new SubscribeGroupsRequest
            {
                ClientKey = ApiKey,
            };
            return _client.ServerStreamingCallAsync<SubscribeGroupsRequest, GroupsUpdate>("/gw2lfg.LfgService/SubscribeGroups", request);
        }
    }
}