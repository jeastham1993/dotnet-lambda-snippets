namespace LambdaTestingDemo.Models;

// This model defines the CONTRACT between our Lambda and the upstream Product Catalog API.
// A contract test validates that this shape hasn't silently changed on the upstream side.
// Rename a field here? The contract test catches it before deployment.
// Upstream renames a field without telling you? The contract test catches it in CI.
public class ProductDetails
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public string Category { get; set; } = string.Empty;
    public bool InStock { get; set; }
}
