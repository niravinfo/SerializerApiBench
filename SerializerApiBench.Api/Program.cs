using Google.Protobuf;
using MessagePack;
using ProtoBuf;
using SerializerApiBench.Models;
using SerializerApiBench.Models.Proto;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Configure logging to console only, no debug or other providers, to avoid polluting the benchmark results.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

var mpOptions = MessagePackSerializerOptions.Standard;
var mpLz4Options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

// Pre-generate and cache the payload per count, in memory, at startup.
// "serialize-only" endpoints serialize straight from this cache, so the request
// itself carries no body and no deserialize cost — purely isolating serialize time
// (+ response transfer). Same seed as PayloadGenerator, so the data is identical
// to what "roundtrip" / "deserialize-only" receive as request bodies.
var cache = new Dictionary<int, List<TestPayload>>
{
    [10] = TestDataFactory.Generate(10),
    [100] = TestDataFactory.Generate(100),
    [1000] = TestDataFactory.Generate(1000),
};

// Pre-convert and cache Google Protobuf versions for fair benchmarking
// (other formats don't need conversion from cached data)
var protoCache = new Dictionary<int, TestPayloadListProto>();
foreach (var kvp in cache)
{
    var listProto = new TestPayloadListProto();
    listProto.Items.AddRange(kvp.Value.Select(p => p.ToProto()));
    protoCache[kvp.Key] = listProto;
}

app.MapGet("/", () => "SerializerApiBench.Api is running. See README for endpoint list.");

// ============================================================
// ROUNDTRIP: deserialize request -> reserialize -> respond
// Mirrors a real service-to-service call. Use for the "realistic" numbers.
// ============================================================

app.MapPost("/api/json/roundtrip", async (HttpContext ctx) =>
{
    var list = await JsonSerializer.DeserializeAsync<List<TestPayload>>(ctx.Request.Body);
    ctx.Response.ContentType = "application/json";
    await JsonSerializer.SerializeAsync(ctx.Response.Body, list);
});

app.MapPost("/api/newtonsoft-json/roundtrip", async (HttpContext ctx) =>
{
    using var reader = new StreamReader(ctx.Request.Body);
    using var jsonReader = new Newtonsoft.Json.JsonTextReader(reader);
    var serializer = new Newtonsoft.Json.JsonSerializer();
    var list = serializer.Deserialize<List<TestPayload>>(jsonReader);

    ctx.Response.ContentType = "application/json";
    await using var writer = new StreamWriter(ctx.Response.Body);
    await using var jsonWriter = new Newtonsoft.Json.JsonTextWriter(writer);
    serializer.Serialize(jsonWriter, list);
});

app.MapPost("/api/messagepack/roundtrip", async (HttpContext ctx) =>
{
    var list = await MessagePackSerializer.DeserializeAsync<List<TestPayload>>(ctx.Request.Body, mpOptions);
    ctx.Response.ContentType = "application/x-msgpack";
    await MessagePackSerializer.SerializeAsync(ctx.Response.Body, list, mpOptions);
});

app.MapPost("/api/messagepack-lz4/roundtrip", async (HttpContext ctx) =>
{
    var list = await MessagePackSerializer.DeserializeAsync<List<TestPayload>>(ctx.Request.Body, mpLz4Options);
    ctx.Response.ContentType = "application/x-msgpack";
    await MessagePackSerializer.SerializeAsync(ctx.Response.Body, list, mpLz4Options);
});

app.MapPost("/api/protobuf-net/roundtrip", async (HttpContext ctx) =>
{
    var list = Serializer.Deserialize<List<TestPayload>>(ctx.Request.Body);
    ctx.Response.ContentType = "application/x-protobuf";
    Serializer.Serialize(ctx.Response.Body, list);
    await ctx.Response.Body.FlushAsync();
});

app.MapPost("/api/google-protobuf/roundtrip", async (HttpContext ctx) =>
{
    var listProto = TestPayloadListProto.Parser.ParseFrom(ctx.Request.Body);
    ctx.Response.ContentType = "application/x-protobuf";
    listProto.WriteTo(ctx.Response.Body);
    await ctx.Response.Body.FlushAsync();
});

// ============================================================
// SERIALIZE-ONLY: GET ?count=10|100|1000, no request body.
// Server serializes the cached in-memory list and returns it.
// ============================================================

app.MapGet("/api/json/serialize-only", async (HttpContext ctx, int count) =>
{
    ctx.Response.ContentType = "application/json";
    await JsonSerializer.SerializeAsync(ctx.Response.Body, cache[count]);
});

app.MapGet("/api/newtonsoft-json/serialize-only", async (HttpContext ctx, int count) =>
{
    ctx.Response.ContentType = "application/json";
    await using var writer = new StreamWriter(ctx.Response.Body);
    await using var jsonWriter = new Newtonsoft.Json.JsonTextWriter(writer);
    var serializer = new Newtonsoft.Json.JsonSerializer();
    serializer.Serialize(jsonWriter, cache[count]);
});

app.MapGet("/api/messagepack/serialize-only", async (HttpContext ctx, int count) =>
{
    ctx.Response.ContentType = "application/x-msgpack";

    // Serialize directly to response body stream for maximum performance
    // No intermediate byte[] allocation
    await MessagePackSerializer.SerializeAsync(ctx.Response.Body, cache[count], mpOptions);
});

app.MapGet("/api/messagepack-lz4/serialize-only", async (HttpContext ctx, int count) =>
{
    ctx.Response.ContentType = "application/x-msgpack";
    await MessagePackSerializer.SerializeAsync(ctx.Response.Body, cache[count], mpLz4Options);
});

app.MapGet("/api/protobuf-net/serialize-only", async (HttpContext ctx, int count) =>
{
    ctx.Response.ContentType = "application/x-protobuf";
    Serializer.Serialize(ctx.Response.Body, cache[count]);
    await ctx.Response.Body.FlushAsync();
});

app.MapGet("/api/google-protobuf/serialize-only", async (HttpContext ctx, int count) =>
{
    ctx.Response.ContentType = "application/x-protobuf";
    protoCache[count].WriteTo(ctx.Response.Body);
    await ctx.Response.Body.FlushAsync();
});

// ============================================================
// DESERIALIZE-ONLY: POST body, deserialize, respond with a tiny ack.
// No reserialization, so the response cost doesn't pollute the measurement.
// ============================================================

app.MapPost("/api/json/deserialize-only", async (HttpContext ctx) =>
{
    var list = await JsonSerializer.DeserializeAsync<List<TestPayload>>(ctx.Request.Body);
    await ctx.Response.WriteAsync((list?.Count ?? 0).ToString());
});

app.MapPost("/api/newtonsoft-json/deserialize-only", async (HttpContext ctx) =>
{
    using var reader = new StreamReader(ctx.Request.Body);
    using var jsonReader = new Newtonsoft.Json.JsonTextReader(reader);
    var serializer = new Newtonsoft.Json.JsonSerializer();
    var list = serializer.Deserialize<List<TestPayload>>(jsonReader);
    await ctx.Response.WriteAsync((list?.Count ?? 0).ToString());
});

app.MapPost("/api/messagepack/deserialize-only", async (HttpContext ctx) =>
{
    var list = await MessagePackSerializer.DeserializeAsync<List<TestPayload>>(ctx.Request.Body, mpOptions);
    await ctx.Response.WriteAsync(list.Count.ToString());
});

app.MapPost("/api/messagepack-lz4/deserialize-only", async (HttpContext ctx) =>
{
    var list = await MessagePackSerializer.DeserializeAsync<List<TestPayload>>(ctx.Request.Body, mpLz4Options);
    await ctx.Response.WriteAsync(list.Count.ToString());
});

app.MapPost("/api/protobuf-net/deserialize-only", async (HttpContext ctx) =>
{
    var list = Serializer.Deserialize<List<TestPayload>>(ctx.Request.Body);
    await ctx.Response.WriteAsync(list.Count.ToString());
});

app.MapPost("/api/google-protobuf/deserialize-only", async (HttpContext ctx) =>
{
    var listProto = TestPayloadListProto.Parser.ParseFrom(ctx.Request.Body);
    await ctx.Response.WriteAsync(listProto.Items.Count.ToString());
});

app.Run();
