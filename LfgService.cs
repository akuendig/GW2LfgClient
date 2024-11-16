using Blish_HUD;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Gw2Lfg.Proto;
using System.Text.Json;
using System.Net.Http;
using System.Linq;
using Google.Protobuf;
using System.IO;
using System.Text;

namespace Gw2Lfg
{
    public class ManualGrpcWebClient
    {
        private readonly HttpClient _httpClient;
        private const string GrpcWebFormat = "application/grpc-web+proto";

        public ManualGrpcWebClient(string baseUrl)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl)
            };
        }

        public async Task<TResponse> UnaryCallAsync<TRequest, TResponse>(
            string methodName,
            TRequest request)
            where TRequest : IMessage
            where TResponse : IMessage, new()
        {
            // Serialize the request message
            byte[] requestBytes = request.ToByteArray();
            
            // Add 5 byte header (1 byte compression flag + 4 byte length)
            byte[] framedRequest = new byte[requestBytes.Length + 5];
            framedRequest[0] = 0; // No compression
            var lengthBytes = BitConverter.GetBytes(requestBytes.Length).Reverse().ToArray();
            Array.Copy(lengthBytes, 0, framedRequest, 1, 4);
            Array.Copy(requestBytes, 0, framedRequest, 5, requestBytes.Length);

            // Create HTTP request
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, methodName)
            {
                Content = new ByteArrayContent(framedRequest)
            };

            // Add required headers
            httpRequest.Headers.Add("x-grpc-web", "1");
            httpRequest.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(GrpcWebFormat);

            // Send request
            var response = await _httpClient.SendAsync(httpRequest);

            // Check for successful response
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"HTTP error: {response.StatusCode}");
            }

            // Check for grpc-status in headers
            if (response.Headers.TryGetValues("grpc-status", out var statusValues))
            {
                var status = int.Parse(statusValues.First());
                if (status != 0) // 0 is OK
                {
                    string errorMessage = "";
                    if (response.Headers.TryGetValues("grpc-message", out var messageValues))
                    {
                        errorMessage = messageValues.First();
                    }
                    throw new Exception($"gRPC error {status}: {errorMessage}");
                }
            }

            // Read response bytes
            byte[] responseBytes = await response.Content.ReadAsByteArrayAsync();
            if (responseBytes.Length < 5)
            {
                throw new Exception($"Response too short: {responseBytes.Length} bytes");
            }

            // First byte is compression flag
            byte compressionFlag = responseBytes[0];
            
            // Next 4 bytes are message length
            var messageLengthBytes = new byte[4];
            Array.Copy(responseBytes, 1, messageLengthBytes, 0, 4);
            int messageLength = BitConverter.ToInt32(messageLengthBytes.Reverse().ToArray(), 0);

            if (messageLength + 5 > responseBytes.Length)
            {
                throw new Exception($"Indicated message length ({messageLength}) exceeds response size ({responseBytes.Length - 5})");
            }

            // Extract message bytes
            var messageBytes = new byte[messageLength];
            Array.Copy(responseBytes, 5, messageBytes, 0, messageLength);

            // Check for trailer frame
            if (responseBytes.Length > messageLength + 5)
            {
                int trailerStart = messageLength + 5;
                if (responseBytes[trailerStart] == 0x80) // Trailer flag
                {
                    var trailerLengthBytes = new byte[4];
                    Array.Copy(responseBytes, trailerStart + 1, trailerLengthBytes, 0, 4);
                    int trailerLength = BitConverter.ToInt32(trailerLengthBytes.Reverse().ToArray(), 0);
                    
                    if (trailerLength > 0)
                    {
                        var trailerBytes = new byte[trailerLength];
                        Array.Copy(responseBytes, trailerStart + 5, trailerBytes, 0, trailerLength);
                        string trailer = Encoding.UTF8.GetString(trailerBytes);
                        Console.WriteLine($"Trailer: {trailer}");
                    }
                }
            }

            // Parse response
            var responseMessage = new TResponse();
            responseMessage.MergeFrom(messageBytes);

            return responseMessage;
        }

        // Example of handling server streaming
        public async IAsyncEnumerable<TResponse> ServerStreamingCallAsync<TRequest, TResponse>(
            string methodName,
            TRequest request)
            where TRequest : IMessage
            where TResponse : IMessage, new()
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, methodName)
            {
                Content = new ByteArrayContent(request.ToByteArray())
            };

            httpRequest.Headers.Add("x-grpc-web", "1");
            httpRequest.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(GrpcWebFormat);

            var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new BinaryReader(stream);

            while (stream.CanRead)
            {
                var responseMessage = new TResponse();
                try
                {
                    // Read message length (4 bytes)
                    var lengthBytes = new byte[4];
                    await stream.ReadAsync(lengthBytes, 0, 4);
                    int messageLength = BitConverter.ToInt32(lengthBytes.Reverse().ToArray(), 0);

                    // Read message bytes
                    var messageBytes = new byte[messageLength];
                    await stream.ReadAsync(messageBytes, 0, messageLength);

                    // Parse message
                    responseMessage.MergeFrom(messageBytes);
                }
                catch (EndOfStreamException)
                {
                    break;
                }

                yield return responseMessage;
            }
        }
    }
    public class LfgClient : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<Gw2LfgModule>();
        private CancellationTokenSource _cts = new();
        private readonly string _clientId;
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public event Action<Group> OnNewGroup;
        public event Action<Group> OnGroupUpdated;
        public event Action<Group> OnGroupRemoved;

        public LfgClient(HttpClient httpClient, string baseUrl, string key)
        {
            _httpClient = httpClient;
            _baseUrl = baseUrl;
            _clientId = Guid.NewGuid().ToString();
        }

        public async Task StartListening()
        {
            // Implement HTTP-based listening logic here
        }

        public async Task<Group> CreateGroup(string title, uint KillProofMinimum, KillProofId killProofId)
        {
            try
            {
                var request = new CreateGroupRequest
                {
                    Title = title,
                    KillProofMinimum = KillProofMinimum,
                    KillProofId = killProofId,
                };

                ManualGrpcWebClient client = new ManualGrpcWebClient(_baseUrl);
                var response = await client.UnaryCallAsync<CreateGroupRequest, Group>("/gw2lfg.LfgService/CreateGroup", request);
                return response;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to create group");
                return null;
            }
        }

        public async Task<JoinResponse> JoinGroup(string groupId, string role, string apiKey)
        {
            try
            {
                var request = new JoinRequest
                {
                    GroupId = groupId,
                    RoleName = role,
                    ApiKey = apiKey
                };
                var response = await _httpClient.PostAsync($"{_baseUrl}/JoinGroup", new StringContent(JsonSerializer.Serialize(request)));
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<JoinResponse>(responseBody);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to join group");
                throw;
            }
        }

        public IAsyncEnumerable<JoinRequest> SubscribeToApplications(string groupId)
        {
            // Implement HTTP-based subscription logic here
            return null;
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}