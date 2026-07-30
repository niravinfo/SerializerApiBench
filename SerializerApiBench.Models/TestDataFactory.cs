using SerializerApiBench.Models.Proto;

namespace SerializerApiBench.Models;

public static class TestDataFactory
{
    public static TestPayload[] Generate(int count)
    {
        var rnd = new Random(42); // fixed seed: identical content across formats/runs
        var array = new TestPayload[count];

        for (int i = 0; i < count; i++)
        {
            array[i] = new TestPayload
            {
                Id = i,
                Name = $"Item-{i}",
                IsActive = i % 2 == 0,
                Score = rnd.NextDouble() * 100,
                CreatedAt = new DateTime(2026, 1, 1).AddDays(i),
                Description = $"This is a description for item number {i}, used for benchmark payload generation.",
                Age = rnd.Next(18, 80),
                Rating = rnd.NextDouble() * 5,
                Tags = ["tag1", "tag2", "tag3"],
                Categories = new List<string> { "categoryA", "categoryB" },
                Address = new Address
                {
                    Street = $"{rnd.Next(1, 999)} Main St",
                    City = "Springfield",
                    ZipCode = "62704"
                }
            };
        }

        return array;
    }

    public static TestPayloadListProto GetTestPayloadListProto(TestPayload[] payloads)
    {
        var protoList = new TestPayloadListProto();
        protoList.Items.Add(payloads.Select(p => p.ToProto()));
        return protoList;
    }
}
