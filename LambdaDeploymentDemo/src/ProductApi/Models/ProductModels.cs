namespace ProductApi.Models;

public record Product
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required decimal Price { get; init; }
    public required string Category { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public record CreateProductRequest
{
    public required string Name { get; init; }
    public required decimal Price { get; init; }
    public required string Category { get; init; }
}
