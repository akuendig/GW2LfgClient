
using System.Linq;
using Google.Protobuf;
using System.IO;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Gw2Lfg
{
    public class SimpleGrpcWebClient
    {
        private readonly HttpClient _httpClient;
        private const string GrpcWebFormat = "application/grpc-web+proto";

        public SimpleGrpcWebClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<TResponse> UnaryCallAsync<TRequest, TResponse>(
            string methodName,
            TRequest request)
            where TRequest : IMessage
            where TResponse : IMessage, new()
        {
            var response = await SendGrpcWebRequest(methodName, request.ToByteArray(), false);

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
            var response = await SendGrpcWebRequest(methodName, request.ToByteArray(), true);

            using var stream = await response.Content.ReadAsStreamAsync();
            var frameHeaderSize = 5; // 1 byte flag + 4 bytes length

            while (true)
            {
                // Read frame header (5 bytes)
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
                            // Clean end of stream
                            yield break;
                        }
                        throw new Exception("Unexpected end of stream while reading frame header");
                    }
                    headerBytesRead += bytesRead;
                }

                // Parse frame header
                byte compressionFlag = headerBuffer[0];
                var messageLengthBytes = new byte[4];
                Array.Copy(headerBuffer, 1, messageLengthBytes, 0, 4);
                int messageLength = BitConverter.ToInt32(messageLengthBytes.Reverse().ToArray(), 0);

                Console.WriteLine($"Reading frame: compression={compressionFlag}, length={messageLength}");

                if (messageLength < 0 || messageLength > 1024 * 1024) // 1MB max message size
                {
                    throw new Exception($"Invalid message length: {messageLength}");
                }

                // Check if this is a trailer frame (flag = 0x80)
                if (compressionFlag == 0x80)
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
                    Console.WriteLine($"Trailer received: {trailer}");

                    if (trailer.Contains("grpc-status: 0"))
                    {
                        // Normal end of stream
                        yield break;
                    }
                    else
                    {
                        throw new Exception($"gRPC error in trailer: {trailer}");
                    }
                }

                // Read message directly into final buffer
                var messageBuffer = new byte[messageLength];
                int totalBytesRead = 0;
                while (totalBytesRead < messageLength)
                {
                    int bytesRead = await stream.ReadAsync(
                        messageBuffer,
                        totalBytesRead,
                        messageLength - totalBytesRead
                    );

                    if (bytesRead == 0)
                    {
                        throw new Exception($"Unexpected end of stream. Expected {messageLength} bytes, got {totalBytesRead}");
                    }

                    totalBytesRead += bytesRead;
                }

                // Parse message
                var responseMessage = new TResponse();
                responseMessage.MergeFrom(messageBuffer);
                yield return responseMessage;
            }
        }

        private async Task<HttpResponseMessage> SendGrpcWebRequest(string methodName, byte[] messageBytes, bool stream)
        {
            // Add 5 byte header (1 byte compression flag + 4 byte length)
            byte[] framedRequest = new byte[messageBytes.Length + 5];
            framedRequest[0] = 0; // No compression
            var lengthBytes = BitConverter.GetBytes(messageBytes.Length).Reverse().ToArray();
            Array.Copy(lengthBytes, 0, framedRequest, 1, 4);
            Array.Copy(messageBytes, 0, framedRequest, 5, messageBytes.Length);

            // Create HTTP request
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, methodName)
            {
                Content = new ByteArrayContent(framedRequest)
            };

            // Create a cancellation token source with a timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(300));

            // Add required headers
            httpRequest.Headers.Add("accept", GrpcWebFormat);
            httpRequest.Headers.TransferEncodingChunked = true;
            httpRequest.Headers.Add("x-grpc-web", "1");
            httpRequest.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(GrpcWebFormat);

            // Send request
            HttpResponseMessage response;
            if (stream)
            {
                response = await _httpClient.SendAsync(
                    httpRequest,
                    HttpCompletionOption.ResponseHeadersRead,
                    cts.Token
                );
            }
            else
            {
                response = await _httpClient.SendAsync(httpRequest);
            }

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

            return response;
        }
    }
}