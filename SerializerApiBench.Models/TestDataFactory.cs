namespace SerializerApiBench.Models;

public static class TestDataFactory
{
    public static List<TestPayload> Generate(int count)
    {
        var rnd = new Random(42); // fixed seed: identical content across formats/runs
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
}
