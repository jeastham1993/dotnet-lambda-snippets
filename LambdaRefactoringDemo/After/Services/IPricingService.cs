using LambdaRefactoringDemo.After.Models;

namespace LambdaRefactoringDemo.After.Services;

public interface IPricingService
{
    PricingResult CalculatePricing(List<OrderLine> items);
}

public class PricingResult
{
    public decimal Subtotal { get; init; }
    public decimal DiscountPercent { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
}
