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
    var list = GenerateTestData(count);

    File.WriteAllBytes(Path.Combine(outDir, $"payload_json_{count}.bin"),
        JsonSerializer.SerializeToUtf8Bytes(list));

    File.WriteAllBytes(Path.Combine(outDir, $"payload_messagepack_{count}.bin"),
        MessagePackSerializer.Serialize(list, mpOptions));

    File.WriteAllBytes(Path.Combine(outDir, $"payload_messagepack-lz4_{count}.bin"),
        MessagePackSerializer.Serialize(list, mpLz4Options));

    using (var protoMs = new MemoryStream())
    {
        Serializer.Serialize(protoMs, list);
        File.WriteAllBytes(Path.Combine(outDir, $"payload_protobuf-net_{count}.bin"), protoMs.ToArray());
    }

    var listProto = new TestPayloadListProto();
    listProto.Items.AddRange(list.Select(p => p.ToProto()));
    File.WriteAllBytes(Path.Combine(outDir, $"payload_google-protobuf_{count}.bin"), listProto.ToByteArray());

    Console.WriteLine($"Generated payloads for count={count}");
}

Console.WriteLine($"Done. Payloads written to: {outDir}");

static List<TestPayload> GenerateTestData(int count)
{
    var rnd = new Random(42); // fixed seed: identical payload content across all formats/runs
    var list = new List<TestPayload>(count);
    for (int i = 0; i < count; i++)
    {
        list.Add(new TestPayload
        {
            Id = i,
            Name = $"Item-{i}",
            IsActive = i % 2 == 0,
            Score = rnd.NextDouble() * 100,
            CreatedAt = DateTime.UtcNow.AddMinutes(-i),
            Description = $"This is a description for item number {i}, used for benchmark payload generation.",
            Age = rnd.Next(18, 80),
            Rating = (float)(rnd.NextDouble() * 5),
            Tags = new List<string> { "tag1", "tag2", "tag3" },
            Categories = new List<string> { "categoryA", "categoryB" },
            Address = new Address
            {
                Street = $"{rnd.Next(1, 999)} Main St",
                City = "Springfield",
                State = "IL",
                Zip = "62704",
                Country = "USA"
            }
        });
    }
    return list;
}
