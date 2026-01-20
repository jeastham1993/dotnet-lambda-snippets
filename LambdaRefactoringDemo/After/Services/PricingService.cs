using LambdaRefactoringDemo.After.Models;

namespace LambdaRefactoringDemo.After.Services;

public class PricingService : IPricingService
{
    private const decimal TaxRate = 0.08m;

    public PricingResult CalculatePricing(List<OrderLine> items)
    {
        var subtotal = items.Sum(i => i.LineTotal);
        var discountPercent = GetDiscountPercent(subtotal);
        var discountAmount = subtotal * discountPercent;
        var taxableAmount = subtotal - discountAmount;
        var taxAmount = taxableAmount * TaxRate;
        var totalAmount = taxableAmount + taxAmount;

        return new PricingResult
        {
            Subtotal = subtotal,
            DiscountPercent = discountPercent,
            DiscountAmount = discountAmount,
            TaxAmount = taxAmount,
            TotalAmount = totalAmount
        };
    }

    private static decimal GetDiscountPercent(decimal subtotal) => subtotal switch
    {
        >= 1000 => 0.15m,
        >= 500 => 0.10m,
        >= 100 => 0.05m,
        _ => 0m
    };
}
