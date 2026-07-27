using SerializerApiBench.Models.Proto;

namespace SerializerApiBench.Models;

public static class ProtoMapper
{
    public static TestPayloadProto ToProto(this TestPayload p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        IsActive = p.IsActive,
        Score = p.Score,
        CreatedAt = p.CreatedAt.ToString("O"),
        Description = p.Description,
        Age = p.Age,
        Rating = p.Rating,
        Tags = { p.Tags },
        Categories = { p.Categories },
        Address = new AddressProto
        {
            Street = p.Address.Street,
            City = p.Address.City,
            State = p.Address.State,
            Zip = p.Address.Zip,
            Country = p.Address.Country
        }
    };

    public static TestPayload FromProto(this TestPayloadProto p)
    {
        var payload = new TestPayload
        {
            Id = p.Id,
            Name = p.Name,
            IsActive = p.IsActive,
            Score = p.Score,
            CreatedAt = DateTime.Parse(p.CreatedAt),
            Description = p.Description,
            Age = p.Age,
            Rating = p.Rating,
            Address = new Address
            {
                Street = p.Address.Street,
                City = p.Address.City,
                State = p.Address.State,
                Zip = p.Address.Zip,
                Country = p.Address.Country
            }
        };
        payload.Tags.AddRange(p.Tags);
        payload.Categories.AddRange(p.Categories);
        return payload;
    }
}
