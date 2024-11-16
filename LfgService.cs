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

        public async Task<CreateGroupResponse> CreateGroup(string title, uint KillProofMinimum, KillProofId killProofId)
        {
            var request = new CreateGroupRequest
            {
                ClientKey = ApiKey,
                Title = title,
                KillProofMinimum = KillProofMinimum,
                KillProofId = killProofId,
            };

            return await _client.UnaryCallAsync<CreateGroupRequest, CreateGroupResponse>("/gw2lfg.LfgService/CreateGroup", request);
        }

        public async Task<UpdateGroupResponse> UpdateGroup(Group group)
        {
            var request = new UpdateGroupRequest
            {
                ClientKey = ApiKey,
                Group = group,
            };
            return await _client.UnaryCallAsync<UpdateGroupRequest, UpdateGroupResponse>("/gw2lfg.LfgService/UpdateGroup", request);
        }

        public async Task<DeleteGroupResponse> DeleteGroup(string groupId)
        {
            var request = new DeleteGroupRequest
            {
                ClientKey = ApiKey,
                GroupId = groupId,
            };
            return await _client.UnaryCallAsync<DeleteGroupRequest, DeleteGroupResponse>("/gw2lfg.LfgService/DeleteGroup", request);
        }

        public async Task<ListGroupsResponse> ListGroups()
        {
            var request = new ListGroupsRequest
            {
                ClientKey = ApiKey,
            };
            return await _client.UnaryCallAsync<ListGroupsRequest, ListGroupsResponse>("/gw2lfg.LfgService/ListGroups", request);
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