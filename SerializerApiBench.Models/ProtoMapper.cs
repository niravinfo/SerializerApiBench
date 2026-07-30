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
            ZipCode = p.Address.ZipCode,
        }
    };
}
