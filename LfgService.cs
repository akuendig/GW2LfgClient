using Blish_HUD;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gw2Lfg.Proto;

namespace Gw2Lfg
{
    public class LfgClient
    {
        private static readonly Logger Logger = Logger.GetLogger<Gw2LfgModule>();
        private readonly SimpleGrpcWebClient _client;

        public LfgClient(SimpleGrpcWebClient client)
        {
            _client = client;
        }

        public async Task<CreateGroupResponse> CreateGroup(string title, uint KillProofMinimum, KillProofId killProofId)
        {
            var request = new CreateGroupRequest
            {
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
                Group = group,
            };
            return await _client.UnaryCallAsync<UpdateGroupRequest, UpdateGroupResponse>("/gw2lfg.LfgService/UpdateGroup", request);
        }

        public async Task<DeleteGroupResponse> DeleteGroup(string groupId)
        {
            var request = new DeleteGroupRequest
            {
                GroupId = groupId,
            };
            return await _client.UnaryCallAsync<DeleteGroupRequest, DeleteGroupResponse>("/gw2lfg.LfgService/DeleteGroup", request);
        }

        public async Task<ListGroupsResponse> ListGroups()
        {
            var request = new ListGroupsRequest();
            return await _client.UnaryCallAsync<ListGroupsRequest, ListGroupsResponse>("/gw2lfg.LfgService/ListGroups", request);
        }

        public async Task<CreateGroupApplicationResponse> CreateGroupApplication(string groupId)
        {
            var request = new CreateGroupApplicationRequest
            {
                GroupId = groupId,
            };
            return await _client.UnaryCallAsync<CreateGroupApplicationRequest, CreateGroupApplicationResponse>("/gw2lfg.LfgService/CreateGroupApplication", request);
        }

        public async Task<UpdateGroupApplicationResponse> UpdateGroupApplication(GroupApplication application)
        {
            var request = new UpdateGroupApplicationRequest
            {
            };
            return await _client.UnaryCallAsync<UpdateGroupApplicationRequest, UpdateGroupApplicationResponse>("/gw2lfg.LfgService/UpdateGroupApplication", request);
        }

        public async Task<ListGroupApplicationsResponse> ListGroupApplications(string groupId)
        {
            var request = new ListGroupApplicationsRequest
            {
                GroupId = groupId,
            };
            return await _client.UnaryCallAsync<ListGroupApplicationsRequest, ListGroupApplicationsResponse>("/gw2lfg.LfgService/ListGroupApplications", request);
        }

        public async Task<DeleteGroupApplicationResponse> DeleteGroupApplication(string groupId, string applicationId)
        {
            var request = new DeleteGroupApplicationRequest
            {
                GroupId = groupId,
                ApplicationId = applicationId,
            };
            return await _client.UnaryCallAsync<DeleteGroupApplicationRequest, DeleteGroupApplicationResponse>("/gw2lfg.LfgService/DeleteGroupApplication", request);
        }

        public IAsyncEnumerable<GroupApplicationUpdate> SubscribeGroupApplications(string groupId)
        {
            var request = new SubscribeGroupApplicationsRequest
            {
                GroupId = groupId,
            };
            return _client.ServerStreamingCallAsync<SubscribeGroupApplicationsRequest, GroupApplicationUpdate>("/gw2lfg.LfgService/SubscribeGroupApplications", request);
        }

        public IAsyncEnumerable<GroupsUpdate> SubscribeGroups()
        {
            var request = new SubscribeGroupsRequest
            {
            };
            return _client.ServerStreamingCallAsync<SubscribeGroupsRequest, GroupsUpdate>("/gw2lfg.LfgService/SubscribeGroups", request);
        }
    }
}