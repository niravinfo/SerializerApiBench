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

app.MapPost("/api/messagepack/roundtrip", async (HttpContext ctx) =>
{
    var bytes = await ReadAllBytesAsync(ctx.Request.Body);
    var list = MessagePackSerializer.Deserialize<List<TestPayload>>(bytes, mpOptions);
    var result = MessagePackSerializer.Serialize(list, mpOptions);
    ctx.Response.ContentType = "application/x-msgpack";
    await ctx.Response.Body.WriteAsync(result);
});

app.MapPost("/api/messagepack-lz4/roundtrip", async (HttpContext ctx) =>
{
    var bytes = await ReadAllBytesAsync(ctx.Request.Body);
    var list = MessagePackSerializer.Deserialize<List<TestPayload>>(bytes, mpLz4Options);
    var result = MessagePackSerializer.Serialize(list, mpLz4Options);
    ctx.Response.ContentType = "application/x-msgpack";
    await ctx.Response.Body.WriteAsync(result);
});

app.MapPost("/api/protobuf-net/roundtrip", async (HttpContext ctx) =>
{
    var bytes = await ReadAllBytesAsync(ctx.Request.Body);
    using var ms = new MemoryStream(bytes);
    var list = Serializer.Deserialize<List<TestPayload>>(ms);
    using var outMs = new MemoryStream();
    Serializer.Serialize(outMs, list);
    ctx.Response.ContentType = "application/x-protobuf";
    await ctx.Response.Body.WriteAsync(outMs.ToArray());
});

app.MapPost("/api/google-protobuf/roundtrip", async (HttpContext ctx) =>
{
    var bytes = await ReadAllBytesAsync(ctx.Request.Body);
    var listProto = TestPayloadListProto.Parser.ParseFrom(bytes);
    ctx.Response.ContentType = "application/x-protobuf";
    await ctx.Response.Body.WriteAsync(listProto.ToByteArray());
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

app.MapGet("/api/messagepack/serialize-only", async (HttpContext ctx, int count) =>
{
    var bytes = MessagePackSerializer.Serialize(cache[count], mpOptions);
    ctx.Response.ContentType = "application/x-msgpack";
    await ctx.Response.Body.WriteAsync(bytes);
});

app.MapGet("/api/messagepack-lz4/serialize-only", async (HttpContext ctx, int count) =>
{
    var bytes = MessagePackSerializer.Serialize(cache[count], mpLz4Options);
    ctx.Response.ContentType = "application/x-msgpack";
    await ctx.Response.Body.WriteAsync(bytes);
});

app.MapGet("/api/protobuf-net/serialize-only", async (HttpContext ctx, int count) =>
{
    using var ms = new MemoryStream();
    Serializer.Serialize(ms, cache[count]);
    ctx.Response.ContentType = "application/x-protobuf";
    await ctx.Response.Body.WriteAsync(ms.ToArray());
});

app.MapGet("/api/google-protobuf/serialize-only", async (HttpContext ctx, int count) =>
{
    var listProto = new TestPayloadListProto();
    listProto.Items.AddRange(cache[count].Select(p => p.ToProto()));
    ctx.Response.ContentType = "application/x-protobuf";
    await ctx.Response.Body.WriteAsync(listProto.ToByteArray());
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

app.MapPost("/api/messagepack/deserialize-only", async (HttpContext ctx) =>
{
    var bytes = await ReadAllBytesAsync(ctx.Request.Body);
    var list = MessagePackSerializer.Deserialize<List<TestPayload>>(bytes, mpOptions);
    await ctx.Response.WriteAsync(list.Count.ToString());
});

app.MapPost("/api/messagepack-lz4/deserialize-only", async (HttpContext ctx) =>
{
    var bytes = await ReadAllBytesAsync(ctx.Request.Body);
    var list = MessagePackSerializer.Deserialize<List<TestPayload>>(bytes, mpLz4Options);
    await ctx.Response.WriteAsync(list.Count.ToString());
});

app.MapPost("/api/protobuf-net/deserialize-only", async (HttpContext ctx) =>
{
    var bytes = await ReadAllBytesAsync(ctx.Request.Body);
    using var ms = new MemoryStream(bytes);
    var list = Serializer.Deserialize<List<TestPayload>>(ms);
    await ctx.Response.WriteAsync(list.Count.ToString());
});

app.MapPost("/api/google-protobuf/deserialize-only", async (HttpContext ctx) =>
{
    var bytes = await ReadAllBytesAsync(ctx.Request.Body);
    var listProto = TestPayloadListProto.Parser.ParseFrom(bytes);
    await ctx.Response.WriteAsync(listProto.Items.Count.ToString());
});

app.Run();

static async Task<byte[]> ReadAllBytesAsync(Stream s)
{
    using var ms = new MemoryStream();
    await s.CopyToAsync(ms);
    return ms.ToArray();
}
