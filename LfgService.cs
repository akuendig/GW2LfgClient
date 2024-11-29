using Blish_HUD;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gw2Lfg.Proto;
using System.Threading;

namespace Gw2Lfg
{
    public class LfgClient(SimpleGrpcWebClient client)
    {
        private static readonly Logger Logger = Logger.GetLogger<Gw2LfgModule>();
        private readonly SimpleGrpcWebClient _client = client;

        public async Task<CreateGroupResponse> CreateGroup(
            string title, uint KillProofMinimum, KillProofId killProofId,
            CancellationToken cancellationToken = default)
        {
            var request = new CreateGroupRequest
            {
                Title = title,
                KillProofMinimum = KillProofMinimum,
                KillProofId = killProofId,
            };

            return await _client.UnaryCallAsync<CreateGroupRequest, CreateGroupResponse>(
                "/gw2lfg.LfgService/CreateGroup", request, cancellationToken
            );
        }

        public async Task<UpdateGroupResponse> UpdateGroup(Group group, CancellationToken cancellationToken = default)
        {
            var request = new UpdateGroupRequest
            {
                Group = group,
            };
            return await _client.UnaryCallAsync<UpdateGroupRequest, UpdateGroupResponse>(
                "/gw2lfg.LfgService/UpdateGroup", request, cancellationToken
            );
        }

        public async Task<DeleteGroupResponse> DeleteGroup(string groupId, CancellationToken cancellationToken = default)
        {
            var request = new DeleteGroupRequest
            {
                GroupId = groupId,
            };
            return await _client.UnaryCallAsync<DeleteGroupRequest, DeleteGroupResponse>(
                "/gw2lfg.LfgService/DeleteGroup", request, cancellationToken
            );
        }

        public async Task<ListGroupsResponse> ListGroups(CancellationToken cancellationToken = default)
        {
            var request = new ListGroupsRequest();
            return await _client.UnaryCallAsync<ListGroupsRequest, ListGroupsResponse>(
                "/gw2lfg.LfgService/ListGroups", request, cancellationToken
            );
        }

        public async Task<CreateGroupApplicationResponse> CreateGroupApplication(string groupId, CancellationToken cancellationToken = default)
        {
            var request = new CreateGroupApplicationRequest
            {
                GroupId = groupId,
            };
            return await _client.UnaryCallAsync<CreateGroupApplicationRequest, CreateGroupApplicationResponse>(
                "/gw2lfg.LfgService/CreateGroupApplication", request, cancellationToken
            );
        }

        public async Task<UpdateGroupApplicationResponse> UpdateGroupApplication(GroupApplication application, CancellationToken cancellationToken = default)
        {
            var request = new UpdateGroupApplicationRequest
            {
            };
            return await _client.UnaryCallAsync<UpdateGroupApplicationRequest, UpdateGroupApplicationResponse>(
                "/gw2lfg.LfgService/UpdateGroupApplication", request, cancellationToken
            );
        }

        public async Task<ListGroupApplicationsResponse> ListGroupApplications(string groupId, CancellationToken cancellationToken = default)
        {
            var request = new ListGroupApplicationsRequest
            {
                GroupId = groupId,
            };
            return await _client.UnaryCallAsync<ListGroupApplicationsRequest, ListGroupApplicationsResponse>(
                "/gw2lfg.LfgService/ListGroupApplications", request, cancellationToken
            );
        }

        public async Task<ListGroupApplicationsResponse> ListMyApplications(string accountName, CancellationToken cancellationToken = default)
        {
            var request = new ListGroupApplicationsRequest
            {
                AccountName = accountName,
            };
            return await _client.UnaryCallAsync<ListGroupApplicationsRequest, ListGroupApplicationsResponse>(
                "/gw2lfg.LfgService/ListGroupApplications", request, cancellationToken
            );
        }

        public async Task<DeleteGroupApplicationResponse> DeleteGroupApplication(string groupId, string applicationId, CancellationToken cancellationToken = default)
        {
            var request = new DeleteGroupApplicationRequest
            {
                GroupId = groupId,
                ApplicationId = applicationId,
            };
            return await _client.UnaryCallAsync<DeleteGroupApplicationRequest, DeleteGroupApplicationResponse>(
                "/gw2lfg.LfgService/DeleteGroupApplication", request, cancellationToken
            );
        }

        public IAsyncEnumerable<GroupApplicationUpdate> SubscribeGroupApplications(string groupId, CancellationToken cancellationToken = default)
        {
            var request = new SubscribeGroupApplicationsRequest();
            if (!string.IsNullOrWhiteSpace(groupId)) {
                request.GroupId = groupId;
            }
            return _client.ServerStreamingCallAsync<SubscribeGroupApplicationsRequest, GroupApplicationUpdate>(
                "/gw2lfg.LfgService/SubscribeGroupApplications", request, cancellationToken
            );
        }

        public IAsyncEnumerable<GroupsUpdate> SubscribeGroups(CancellationToken cancellationToken = default)
        {
            var request = new SubscribeGroupsRequest();
            return _client.ServerStreamingCallAsync<SubscribeGroupsRequest, GroupsUpdate>(
                "/gw2lfg.LfgService/SubscribeGroups", request, cancellationToken
            );
        }

        public async Task SendHeartbeat(CancellationToken cancellationToken = default)
        {
            var request = new HeartbeatRequest();
            await _client.UnaryCallAsync<HeartbeatRequest, HeartbeatResponse>(
                "/gw2lfg.LfgService/Heartbeat", request, cancellationToken
            );
        }
    }
}