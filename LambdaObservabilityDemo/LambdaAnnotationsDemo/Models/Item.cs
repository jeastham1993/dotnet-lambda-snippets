namespace LambdaAnnotationsDemo.Models;

public record Item
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public required DateTime CreatedAt { get; init; }
}
