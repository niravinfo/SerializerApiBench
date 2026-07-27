using Google.Protobuf;
using MessagePack;
using ProtoBuf;
using SerializerApiBench.Models;
using SerializerApiBench.Models.Proto;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Deliberately separate routes per serializer instead of content negotiation,
// so k6 hits an identical code path (deserialize -> reserialize -> respond)
// for every format and the comparison stays apples-to-apples.

var mpOptions = MessagePackSerializerOptions.Standard;
var mpLz4Options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

app.MapGet("/", () => "SerializerApiBench.Api is running. POST binary payloads to /api/{format}/echo");

app.MapPost("/api/json/echo", async (HttpContext ctx) =>
{
    var list = await JsonSerializer.DeserializeAsync<List<TestPayload>>(ctx.Request.Body);
    ctx.Response.ContentType = "application/json";
    await JsonSerializer.SerializeAsync(ctx.Response.Body, list);
});

app.MapPost("/api/messagepack/echo", async (HttpContext ctx) =>
{
    var bytes = await ReadAllBytesAsync(ctx.Request.Body);
    var list = MessagePackSerializer.Deserialize<List<TestPayload>>(bytes, mpOptions);
    var result = MessagePackSerializer.Serialize(list, mpOptions);
    ctx.Response.ContentType = "application/x-msgpack";
    await ctx.Response.Body.WriteAsync(result);
});

app.MapPost("/api/messagepack-lz4/echo", async (HttpContext ctx) =>
{
    var bytes = await ReadAllBytesAsync(ctx.Request.Body);
    var list = MessagePackSerializer.Deserialize<List<TestPayload>>(bytes, mpLz4Options);
    var result = MessagePackSerializer.Serialize(list, mpLz4Options);
    ctx.Response.ContentType = "application/x-msgpack";
    await ctx.Response.Body.WriteAsync(result);
});

app.MapPost("/api/protobuf-net/echo", async (HttpContext ctx) =>
{
    var bytes = await ReadAllBytesAsync(ctx.Request.Body);
    using var ms = new MemoryStream(bytes);
    var list = Serializer.Deserialize<List<TestPayload>>(ms);
    using var outMs = new MemoryStream();
    Serializer.Serialize(outMs, list);
    ctx.Response.ContentType = "application/x-protobuf";
    await ctx.Response.Body.WriteAsync(outMs.ToArray());
});

app.MapPost("/api/google-protobuf/echo", async (HttpContext ctx) =>
{
    var bytes = await ReadAllBytesAsync(ctx.Request.Body);
    var listProto = TestPayloadListProto.Parser.ParseFrom(bytes);
    ctx.Response.ContentType = "application/x-protobuf";
    await ctx.Response.Body.WriteAsync(listProto.ToByteArray());
});

app.Run();

static async Task<byte[]> ReadAllBytesAsync(Stream s)
{
    using var ms = new MemoryStream();
    await s.CopyToAsync(ms);
    return ms.ToArray();
}
