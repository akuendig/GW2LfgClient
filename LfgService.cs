using Blish_HUD;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Gw2Lfg.Proto;
using Grpc.Core;

namespace Gw2Lfg
{
    public class LfgClient : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<Gw2LfgModule>();
        private readonly LfgService.LfgServiceClient _client;
        private CancellationTokenSource _cts = new();
        private readonly string _clientId;
        private readonly string _apiKey;

        public event Action<Group> OnNewGroup;
        public event Action<Group> OnGroupUpdated;
        public event Action<Group> OnGroupRemoved;

        public LfgClient(Channel channel, string key)
        {
            _apiKey = key;
            _client = new LfgService.LfgServiceClient(channel);
            _clientId = Guid.NewGuid().ToString();
        }

        public async Task StartListening()
        {
            try
            {
                using var call = _client.SubscribeGroups(new SubscribeGroupsRequest
                {
                    ClientKey = _apiKey
                });

                while (await call.ResponseStream.MoveNext())
                {
                    var update = call.ResponseStream.Current;
                    switch (update.UpdateCase)
                    {
                        case GroupsUpdate.UpdateOneofCase.NewGroup:
                            OnNewGroup?.Invoke(update.NewGroup);
                            break;
                        case GroupsUpdate.UpdateOneofCase.UpdatedGroup:
                            OnGroupUpdated?.Invoke(update.UpdatedGroup);
                            break;
                        case GroupsUpdate.UpdateOneofCase.RemovedGroup:
                            OnGroupRemoved?.Invoke(update.RemovedGroup);
                            break;
                    }
                }
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                // Normal cancellation
            }
        }

        public async Task<Proto.Group> CreateGroup(string title, uint KillProofMinimum, KillProofId killProofId)
        {
            try
            {
                return  _client.CreateGroup(new CreateGroupRequest
                {
                    Title = title,
                    KillProofMinimum = KillProofMinimum,
                    KillProofId = killProofId,
                });
            }
            catch (RpcException ex)
            {
                Logger.Error(ex, "Failed to create group");
                throw;
            }
        }

        public async Task<JoinResponse> JoinGroup(string groupId, string role, string apiKey)
        {
            try
            {
                return await _client.JoinGroupAsync(new JoinRequest
                {
                    GroupId = groupId,
                    RoleName = role,
                    ApiKey = apiKey
                });
            }
            catch (RpcException ex)
            {
                Logger.Error(ex, "Failed to join group");
                throw;
            }
        }

        public IAsyncEnumerable<JoinRequest> SubscribeToApplications(string groupId)
        {
            var call = _client.SubscribeToApplications(new SubscribeToApplicationsRequest
            {
                GroupId = groupId
            });

            return ReadAllAsync(call.ResponseStream);
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        private async IAsyncEnumerable<T> ReadAllAsync<T>(IAsyncStreamReader<T> stream)
        {
            while (await stream.MoveNext(_cts.Token))
            {
                yield return stream.Current;
            }
        }
    }
}