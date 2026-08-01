using SerializerApiBench.Models.Proto;
using System.Text;

namespace SerializerApiBench.Models;

public static class TestDataFactory
{
    private static readonly string[] FirstNames =
    {
        "James", "Mary", "John", "Patricia", "Robert", "Jennifer", "Michael", "Linda",
        "David", "Elizabeth", "William", "Barbara", "Richard", "Susan", "Joseph", "Jessica",
        "Thomas", "Sarah", "Charles", "Karen", "Daniel", "Nancy", "Matthew", "Lisa",
        "Anthony", "Margaret", "Mark", "Sandra", "Donald", "Ashley", "Steven", "Kimberly",
        "Paul", "Emily", "Andrew", "Donna", "Joshua", "Michelle", "Kenneth", "Dorothy"
    };

    private static readonly string[] LastNames =
    {
        "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis",
        "Rodriguez", "Martinez", "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Thomas",
        "Taylor", "Moore", "Jackson", "Martin", "Lee", "Perez", "Thompson", "White",
        "Harris", "Sanchez", "Clark", "Ramirez", "Lewis", "Robinson", "Walker", "Young",
        "Allen", "King", "Wright", "Scott", "Torres", "Nguyen", "Hill", "Flores"
    };

    // City/state/zip base triples. The zip shown is the base (postal prefix) for that
    // city; each record gets a nearby variant so addresses are realistic but not identical.
    private static readonly (string City, string State, string Zip)[] Cities =
    {
        ("Austin", "TX", "78701"),
        ("Seattle", "WA", "98101"),
        ("Denver", "CO", "80202"),
        ("Portland", "OR", "97204"),
        ("Boston", "MA", "02108"),
        ("Nashville", "TN", "37203"),
        ("Chicago", "IL", "60601"),
        ("Phoenix", "AZ", "85001"),
        ("Atlanta", "GA", "30303"),
        ("San Diego", "CA", "92101"),
        ("Dallas", "TX", "75201"),
        ("Miami", "FL", "33130"),
        ("Minneapolis", "MN", "55401"),
        ("Charlotte", "NC", "28202"),
        ("Columbus", "OH", "43215"),
        ("Raleigh", "NC", "27601"),
        ("Salt Lake City", "UT", "84101"),
        ("Tampa", "FL", "33602"),
        ("Kansas City", "MO", "64106"),
        ("Cleveland", "OH", "44113"),
        ("New Orleans", "LA", "70112"),
        ("San Antonio", "TX", "78205"),
        ("Sacramento", "CA", "95814"),
        ("Louisville", "KY", "40202"),
        ("Pittsburgh", "PA", "15222")
    };

    private static readonly string[] StreetNames =
    {
        "Main", "Oak", "Maple", "Cedar", "Elm", "Pine", "Walnut", "Cherry", "Birch", "Spruce",
        "Washington", "Lincoln", "Franklin", "Adams", "Jefferson", "Madison", "Monroe", "Jackson",
        "Park", "Lake", "Hill", "Valley", "Ridge", "Brook", "Meadow", "Highland", "Sunset", "Willow",
        "River", "Church", "Market", "Broadway", "Union", "Center", "Liberty", "Columbia"
    };

    private static readonly string[] StreetSuffixes = { "St", "Ave", "Blvd", "Rd", "Ln", "Dr", "Ct", "Way", "Pl", "Ter" };

    private static readonly string[] Tags =
    {
        "priority", "new", "featured", "sale", "clearance", "bestseller", "backorder",
        "refurbished", "premium", "standard", "vip", "verified", "digital", "physical",
        "export", "domestic", "subscription", "one-time", "recurring", "fragile",
        "perishable", "oversized", "lightweight", "small", "large", "express", "scheduled",
        "samples", "prototype", "discontinued"
    };

    private static readonly string[] Categories =
    {
        "Electronics", "Home & Garden", "Apparel", "Sports & Outdoors", "Toys & Games",
        "Health & Beauty", "Automotive", "Books & Media", "Office Supplies", "Pet Supplies",
        "Grocery", "Baby & Kids", "Furniture", "Tools & Hardware", "Jewelry"
    };

    // Shared common-English vocabulary used to build prose descriptions. A limited,
    // realistic vocabulary (like real product copy) means the text still compresses
    // meaningfully, but far less than the old "identical sentence for every item".
    private static readonly string[] Prose =
    {
        "the", "a", "an", "and", "of", "to", "in", "for", "with", "on", "at", "from",
        "by", "about", "as", "into", "through", "during", "between", "after", "before",
        "product", "service", "system", "device", "design", "quality", "value", "performance",
        "customer", "experience", "team", "support", "warranty", "guarantee", "delivery",
        "packaging", "material", "construction", "finish", "color", "size", "weight", "capacity",
        "feature", "specification", "detail", "upgrade", "version", "model", "edition", "set",
        "kit", "accessory", "component", "module", "durable", "reliable", "efficient", "versatile",
        "compact", "lightweight", "powerful", "modern", "premium", "advanced", "ergonomic",
        "adjustable", "rechargeable", "portable", "weatherproof", "comfortable", "easy", "simple",
        "convenient", "clean", "smooth", "stylish", "professional", "offers", "provides", "delivers",
        "includes", "features", "supports", "ensures", "maintains", "combines", "integrates",
        "performs", "reduces", "improves", "enhances", "extends", "designed", "engineered", "built",
        "crafted", "tested", "optimized", "easily", "reliably", "consistently", "seamlessly",
        "quickly", "comfortably", "perfectly", "simply", "you", "your", "our", "their", "every",
        "each", "many", "more", "most", "some", "other", "few", "several", "also", "even", "still",
        "very", "really", "always", "often", "ideal", "great", "best", "top", "perfect", "complete",
        "full", "standard", "custom", "special", "limited", "exclusive", "available", "ready",
        "fast", "new", "improved", "proven", "long", "lasting", "daily", "weekly", "monthly",
        "yearly", "household", "industrial", "commercial", "residential", "office", "home",
        "garage", "kitchen", "bathroom", "bedroom", "garden", "outdoor", "indoor", "weather",
        "water", "heat", "cold", "dust", "shock", "scratch", "fade", "rust", "corrosion",
        "friendly", "works", "fits", "mounts", "connects", "charges", "powers", "stores",
        "holds", "carries", "protects", "covers", "locks", "seals"
    };

    // Deterministic: fixed seed means every format/count gets identical, reproducible data.
    public static TestPayload[] Generate(int count, int seed = 42)
    {
        var rnd = new Random(seed);
        var array = new TestPayload[count];

        for (int i = 0; i < count; i++)
        {
            var city = Cities[rnd.Next(Cities.Length)];
            array[i] = new TestPayload
            {
                Id = i,
                Name = $"{FirstNames[rnd.Next(FirstNames.Length)]} {LastNames[rnd.Next(LastNames.Length)]}",
                IsActive = rnd.Next(2) == 0,
                Score = Math.Round(rnd.NextDouble() * 100, 2),
                CreatedAt = new DateTime(2026, 1, 1)
                    .AddDays(rnd.Next(365))
                    .AddSeconds(rnd.Next(86400)),
                Description = GenerateDescription(rnd),
                Age = rnd.Next(100) < 8 ? null : rnd.Next(18, 80),
                Rating = rnd.Next(100) < 12 ? null : Math.Round(rnd.NextDouble() * 5, 2),
                Tags = PickRandom(rnd, Tags, 2, 4),
                Categories = PickRandom(rnd, Categories, 1, 3).ToList(),
                Address = new Address
                {
                    Street = $"{rnd.Next(1, 9999)} {StreetNames[rnd.Next(StreetNames.Length)]} {StreetSuffixes[rnd.Next(StreetSuffixes.Length)]}",
                    City = city.City,
                    ZipCode = $"{city.Zip[..3]}{rnd.Next(0, 100):00}"
                }
            };
        }

        return array;
    }

    // Builds 2-3 sentences from the shared vocabulary. Shared word pool keeps the
    // text compressible like real copy, while the per-item word choices keep it varied.
    private static string GenerateDescription(Random rnd)
    {
        int sentences = rnd.Next(2, 4);
        var sb = new StringBuilder();

        for (int s = 0; s < sentences; s++)
        {
            int wordCount = rnd.Next(9, 16);
            var sentence = new StringBuilder();

            for (int w = 0; w < wordCount; w++)
            {
                if (w > 0) sentence.Append(' ');
                sentence.Append(Prose[rnd.Next(Prose.Length)]);
            }

            var text = sentence.ToString();
            text = char.ToUpperInvariant(text[0]) + text[1..];
            sb.Append(text).Append(". ");
        }

        return sb.ToString().TrimEnd();
    }

    private static T[] PickRandom<T>(Random rnd, T[] source, int min, int max)
    {
        int count = rnd.Next(min, max + 1);
        var result = new T[count];
        var used = new HashSet<int>();

        for (int i = 0; i < count; i++)
        {
            int idx;
            do { idx = rnd.Next(source.Length); } while (!used.Add(idx));
            result[i] = source[idx];
        }

        return result;
    }

    public static TestPayloadListProto GetTestPayloadListProto(TestPayload[] payloads)
    {
        var protoList = new TestPayloadListProto();
        protoList.Items.Add(payloads.Select(p => p.ToProto()));
        return protoList;
    }
}
