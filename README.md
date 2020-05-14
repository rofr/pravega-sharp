# .NET Standard 2.1 Client for Pravega gRPC Gateway

This library provides a nuget package that allows you to connect to Pravega through the pravega-grpc-gateway. 

Pravega provides a new storage abstraction - a stream - for continuous and unbounded data. A Pravega stream is an elastic set of durable and append-only segments, each segment being an unbounded sequence of bytes. Streams provide exactly-once semantics, and atomicity for groups of events using transactions.
See https://pravega.io

## Projects in this repository
  * PravegaSharp.Grpc  - .NET Standard 2.1 gRPC client library
  * PravegaSharp.Demo  - a runnable console application that performs some basic stream operations

## Getting started
In order to bring in protobuf definitions, [pravega-grpc-gateway](https://github.com/pravega/pravega-grpc-gateway) is included as a Git submodule.

After cloning this repo, initialize and update its submodule:

    git submodule update --init

Or clone this repo with `--recurse-submodules`:

    git clone --recurse-submodules https://github.com/rofr/pravega-sharp.git

The `pravega-grpc-gateway` repo is now cloned within the project, and PravegaSharp is ready to build.

To try the demo you need a pravega environment and grpc gateway to connect to. We have provided some scripts that will get you up and running quickly. Set the HOST_IP env var to the ip of your machine and then run `start-pravega.sh`. You need docker and docker-compose installed.

Once the gateway is up and running, start the PravegaSharp.Demo console application.

## How to use the gPRC client in your own project

* Add a nuget reference to PravegaSharp.Grpc
* Create an instance of the `PravegaGateway.GatewayClient` pointing at the grpc-gateway:

```csharp
   //Required unless the gateway is running https 
   AppContext.SetSwitch(
    "System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport",
    true);
   var channel = GrpcChannel.For("http://localhost:54672");
   var client = new PravegaGateway.GatewayClient(channel);
```

The samples below show how to perform some common stream operations.

### Listing streams
```csharp
    var request = new ListStreamsRequest { Scope = ScopeName };

    using var call = _client.ListStreams(request);
    while (await call.ResponseStream.MoveNext())
    {
        var result =  call.ResponseStream.Current;
        Console.WriteLine($"Scope: {result.Scope}, Stream: {result.Stream}");
    }
```
### Creating a scope
```csharp
    var request = new CreateScopeRequest { Scope = ScopeName };
    var response = await _client.CreateScopeAsync(request);
    
    //If the scope already existed, response.Created is false
    Console.WriteLine($"Scope created: {response.Created}");
```

### Creating a stream
```csharp
    var request = new CreateStreamRequest
    {
        Scope = ScopeName,
        Stream = StreamName
    };
    var response = await _client.CreateStreamAsync(request);
    //if the stream already existed, response.Created is false
    Console.WriteLine("Stream created: " + response.Created);
```

### Writing to a stream
```csharp
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
```

### Reading from a stream
```csharp
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
```
