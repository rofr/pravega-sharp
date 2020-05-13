using System;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using PravegaGrpcClient = PravegaGateway.PravegaGatewayClient;

namespace PravegaSharp.Demo
{
    class Program
    {
        private const string ScopeName = "myscope";
        private const string StreamName = "mystream";
            
        private static PravegaGrpcClient _client;
        
        static async Task Main(string[] args)
        {
            //https://docs.microsoft.com/en-us/aspnet/core/grpc/troubleshoot?view=aspnetcore-3.0#call-insecure-grpc-services-with-net-core-client
            AppContext.SetSwitch(
                "System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            
            var channel = GrpcChannel.ForAddress("http://localhost:54672/");
            _client = new PravegaGrpcClient(channel);

            await CreateScope();
            await CreateStream();
            await DisplayStreams();
            await WriteEvents();
            await ReadAllEvents();
        }

        private static async Task ReadAllEvents()
        {
            var request = new ReadEventsRequest();
            request.Scope = ScopeName;
            request.Stream = StreamName;
            
            // read to the current end of the stream
            // without this parameter the reader will block and
            // wait for future events when the end is reached
            request.ToStreamCut = await GetTailStreamCut();

            using var call = _client.ReadEvents(request);
            while (await call.ResponseStream.MoveNext())
            {
                var response = call.ResponseStream.Current;
                Console.WriteLine("---- Event received ------");
                Console.WriteLine("   Payload size: " + response.Event.Length);
                Console.WriteLine("   Position.Description: " + response.Position.Description);
                Console.WriteLine("   EventPointer.Description: " + response.EventPointer.Description);
                Console.WriteLine("   StreamCut.Description: " + response.StreamCut.Description);
            }
        }

        /// <summary>
        /// Get the last StreamCut in the stream
        /// </summary>
        /// <returns></returns>
        private static async Task<StreamCut> GetTailStreamCut()
        {
            var request = new GetStreamInfoRequest
            {
                Scope = ScopeName,
                Stream = StreamName
            };
            var response = await _client.GetStreamInfoAsync(request);
            return response.TailStreamCut;
        }

        /// <summary>
        /// Write a number of empty arrays of random size
        /// </summary>
        private static async Task WriteEvents()
        {
            Console.WriteLine("Writing 20 events");
            var random = new Random();
            using var call = _client.WriteEvents();
            
            var writer = call.RequestStream;
            for (int i = 0; i < 20; i++)
            {
                var payload = new byte[random.Next(1024 * 16)];
                var request = new WriteEventsRequest
                {
                    Event = ByteString.CopyFrom(payload),
                    Scope = ScopeName,
                    Stream = StreamName
                };
                await writer.WriteAsync(request);
            }

            await writer.CompleteAsync();
            await call.ResponseAsync;
        }

        private static async Task DisplayStreams()
        {
            Console.WriteLine("------- Streams ----------");
            var request = new ListStreamsRequest {Scope = ScopeName};

            using var call = _client.ListStreams(request);
            while (await call.ResponseStream.MoveNext())
            {
                var result =  call.ResponseStream.Current;
                Console.WriteLine($"Scope: {result.Scope}, Stream: {result.Stream}");
            }
        }

        private static async Task CreateStream()
        {
            var request = new CreateStreamRequest
            {
                Scope = ScopeName,
                Stream = StreamName,
                ScalingPolicy = CreateScalingPolicy()
            };
            var response = await _client.CreateStreamAsync(request);
            //if the stream already existed, response.Created is false
            Console.WriteLine("Stream created: " + response.Created);
        }

        /// <summary>
        /// Create and return a policy that uses a single segment for the entire stream.
        /// <remarks>
        /// A stream consists of one of more segments.
        /// The order of messages is preserved within each segment. 
        /// If you require preserved order for the entire stream, you may only
        /// have a single segment.
        /// </remarks>
        /// </summary>
        private static ScalingPolicy CreateScalingPolicy()
        {
            return new ScalingPolicy
            {
                MinNumSegments = 1,
                ScaleType = ScalingPolicy.Types.ScalingPolicyType.FixedNumSegments
            };
        }

        private static async Task CreateScope()
        {
            var request = new CreateScopeRequest { Scope = ScopeName };
            var response = await _client.CreateScopeAsync(request);
            
            //If the scope already existed, response.Created is false
            Console.WriteLine($"Scope created: {response.Created}");
        }
    }
}