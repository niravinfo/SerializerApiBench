using Google.Protobuf;
using MessagePack;
using ProtoBuf;
using SerializerApiBench.Models;
using SerializerApiBench.Models.Proto;
using System.Text.Json;

var counts = new[] { 10, 100, 1000 };

// repo-root/payloads, regardless of where this exe runs from
var outDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "payloads"));
Directory.CreateDirectory(outDir);

var mpOptions = MessagePackSerializerOptions.Standard;
var mpLz4Options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

foreach (var count in counts)
{
    var list = TestDataFactory.Generate(count);

    var utf8Json = JsonSerializer.SerializeToUtf8Bytes(list);

    File.WriteAllBytes(Path.Combine(outDir, $"payload_json_{count}.bin"), utf8Json);
    File.WriteAllBytes(Path.Combine(outDir, $"payload_newtonsoft-json_{count}.bin"), utf8Json);

    File.WriteAllBytes(Path.Combine(outDir, $"payload_messagepack_{count}.bin"),
        MessagePackSerializer.Serialize(list, mpOptions));

    File.WriteAllBytes(Path.Combine(outDir, $"payload_messagepack-lz4_{count}.bin"),
        MessagePackSerializer.Serialize(list, mpLz4Options));

    using (var protoMs = new MemoryStream())
    {
        Serializer.Serialize(protoMs, list);
        File.WriteAllBytes(Path.Combine(outDir, $"payload_protobuf-net_{count}.bin"), protoMs.ToArray());
    }

    var listProto = TestDataFactory.GetTestPayloadListProto(list);
    File.WriteAllBytes(Path.Combine(outDir, $"payload_google-protobuf_{count}.bin"), listProto.ToByteArray());

    Console.WriteLine($"Generated payloads for count={count}");
}

Console.WriteLine($"Done. Payloads written to: {outDir}");
