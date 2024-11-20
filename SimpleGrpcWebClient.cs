using System.Linq;
using Google.Protobuf;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;

namespace Gw2Lfg
{
    public class SimpleGrpcWebClient
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;
        private readonly CancellationToken _cancellationToken;
        private const string GrpcWebFormat = "application/grpc-web+proto";
        private const string GrpcStatusHeader = "grpc-status";
        private const string GrpcMessageHeader = "grpc-message";

        public SimpleGrpcWebClient(HttpClient httpClient, string apiKey, CancellationToken cancellationToken)
        {
            _httpClient = httpClient;
            _apiKey = apiKey;
            _cancellationToken = cancellationToken;
        }

        private void HandleGrpcError(HttpResponseMessage response)
        {
            var status = "0";
            var message = "Unknown error";

            if (response.Headers.TryGetValues(GrpcStatusHeader, out var statusValues))
            {
                status = statusValues.First();
            }

            if (response.Headers.TryGetValues(GrpcMessageHeader, out var messageValues))
            {
                message = messageValues.First();
            }

            if (status == "16") // Status code for Unauthenticated
            {
                throw new UnauthorizedAccessException($"Authentication failed: {message}");
            }
            else if (status != "0") // 0 is OK
            {
                throw new Exception($"gRPC error {status}: {message}");
            }
        }

        public async Task<TResponse> UnaryCallAsync<TRequest, TResponse>(
            string methodName,
            TRequest request,
            CancellationToken cancellationToken = default)
            where TRequest : IMessage
            where TResponse : IMessage, new()
        {
            var response = await SendGrpcWebRequest(
                methodName, request.ToByteArray(), false, cancellationToken);
            HandleGrpcError(response);

            byte[] responseBytes = await response.Content.ReadAsByteArrayAsync();
            if (responseBytes.Length < 5)
            {
                throw new Exception($"Response too short: {responseBytes.Length} bytes");
            }

            // Parse frame header
            byte compressionFlag = responseBytes[0];
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

            // Parse response
            var responseMessage = new TResponse();
            responseMessage.MergeFrom(messageBytes);

            return responseMessage;
        }

        public async IAsyncEnumerable<TResponse> ServerStreamingCallAsync<TRequest, TResponse>(
            string methodName,
            TRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
            where TRequest : IMessage
            where TResponse : IMessage, new()
        {
            var response = await SendGrpcWebRequest(methodName, request.ToByteArray(), true, cancellationToken);
            HandleGrpcError(response);

            using var stream = await response.Content.ReadAsStreamAsync();
            var frameHeaderSize = 5; // 1 byte flag + 4 bytes length

            while (true)
            {
                var headerBuffer = new byte[frameHeaderSize];
                int headerBytesRead = 0;
                while (headerBytesRead < frameHeaderSize)
                {
                    int bytesRead = await stream.ReadAsync(
                        headerBuffer,
                        headerBytesRead,
                        frameHeaderSize - headerBytesRead
                    );

                    if (bytesRead == 0)
                    {
                        if (headerBytesRead == 0)
                        {
                            yield break;
                        }
                        throw new Exception("Unexpected end of stream while reading frame header");
                    }
                    headerBytesRead += bytesRead;
                }

                byte compressionFlag = headerBuffer[0];
                var messageLengthBytes = new byte[4];
                Array.Copy(headerBuffer, 1, messageLengthBytes, 0, 4);
                int messageLength = BitConverter.ToInt32(messageLengthBytes.Reverse().ToArray(), 0);

                if (messageLength < 0 || messageLength > 1024 * 1024) // 1MB max message size
                {
                    throw new Exception($"Invalid message length: {messageLength}");
                }

                if (compressionFlag == 0x80) // Trailer frame
                {
                    var trailerBuffer = new byte[messageLength];
                    int trailerBytesRead = 0;
                    while (trailerBytesRead < messageLength)
                    {
                        int bytesRead = await stream.ReadAsync(
                            trailerBuffer,
                            trailerBytesRead,
                            messageLength - trailerBytesRead
                        );
                        if (bytesRead == 0)
                        {
                            throw new Exception("Unexpected end of stream while reading trailer");
                        }
                        trailerBytesRead += bytesRead;
                    }

                    string trailer = Encoding.UTF8.GetString(trailerBuffer);

                    if (trailer.Contains("grpc-status: 0"))
                    {
                        yield break;
                    }
                    else if (trailer.Contains("grpc-status: 16"))
                    {
                        throw new UnauthorizedAccessException("Authentication failed");
                    }
                    else
                    {
                        throw new Exception($"gRPC error in trailer: {trailer}");
                    }
                }

                var messageBuffer = new byte[messageLength];
                int totalBytesRead = 0;
                while (totalBytesRead < messageLength)
                {
                    int bytesRead = await stream.ReadAsync(
                        messageBuffer,
                        totalBytesRead,
                        messageLength - totalBytesRead,
                        CancellationTokenSource.CreateLinkedTokenSource(
                            cancellationToken, _cancellationToken).Token
                    );

                    if (bytesRead == 0)
                    {
                        throw new Exception($"Unexpected end of stream. Expected {messageLength} bytes, got {totalBytesRead}");
                    }

                    totalBytesRead += bytesRead;
                }

                var responseMessage = new TResponse();
                responseMessage.MergeFrom(messageBuffer);
                yield return responseMessage;
            }
        }

        private async Task<HttpResponseMessage> SendGrpcWebRequest(
            string methodName,
            byte[] messageBytes,
            bool stream,
            CancellationToken cancellationToken)
        {
            byte[] framedRequest = new byte[messageBytes.Length + 5];
            framedRequest[0] = 0; // No compression
            var lengthBytes = BitConverter.GetBytes(messageBytes.Length).Reverse().ToArray();
            Array.Copy(lengthBytes, 0, framedRequest, 1, 4);
            Array.Copy(messageBytes, 0, framedRequest, 5, messageBytes.Length);

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, methodName)
            {
                Content = new ByteArrayContent(framedRequest)
            };

            // Add required headers
            httpRequest.Headers.Add("x-grpc-web", "1");
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(GrpcWebFormat));
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            httpRequest.Headers.TransferEncodingChunked = true;
            httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(GrpcWebFormat);

            var response = await _httpClient.SendAsync(
                httpRequest,
                stream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead,
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationToken).Token
            );

            response.EnsureSuccessStatusCode();

            return response;
        }
    }
}