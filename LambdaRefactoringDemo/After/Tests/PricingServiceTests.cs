using LambdaRefactoringDemo.After.Models;
using LambdaRefactoringDemo.After.Services;

namespace LambdaRefactoringDemo.After.Tests;

/// <summary>
/// Example unit tests demonstrating testability after refactoring.
/// These tests are simple, focused, and fast - impossible with the monolithic version.
/// </summary>
public class PricingServiceTests
{
    private readonly PricingService _sut = new();

    public void CalculatePricing_Under100_NoDiscount()
    {
        // Arrange
        var items = new List<OrderLine>
        {
            new() { ProductId = "P1", ProductName = "Widget", Quantity = 2, UnitPrice = 25m, LineTotal = 50m }
        };

        // Act
        var result = _sut.CalculatePricing(items);

        // Assert
        Assert(result.Subtotal == 50m, "Subtotal should be 50");
        Assert(result.DiscountPercent == 0m, "No discount under $100");
        Assert(result.DiscountAmount == 0m, "Discount amount should be 0");
    }

    public void CalculatePricing_Over100_5PercentDiscount()
    {
        // Arrange
        var items = new List<OrderLine>
        {
            new() { ProductId = "P1", ProductName = "Widget", Quantity = 5, UnitPrice = 30m, LineTotal = 150m }
        };

        // Act
        var result = _sut.CalculatePricing(items);

        // Assert
        Assert(result.Subtotal == 150m, "Subtotal should be 150");
        Assert(result.DiscountPercent == 0.05m, "5% discount for orders over $100");
    }

    public void CalculatePricing_Over500_10PercentDiscount()
    {
        // Arrange
        var items = new List<OrderLine>
        {
            new() { ProductId = "P1", ProductName = "Widget", Quantity = 10, UnitPrice = 60m, LineTotal = 600m }
        };

        // Act
        var result = _sut.CalculatePricing(items);

        // Assert
        Assert(result.DiscountPercent == 0.10m, "10% discount for orders over $500");
    }

    public void CalculatePricing_Over1000_15PercentDiscount()
    {
        // Arrange
        var items = new List<OrderLine>
        {
            new() { ProductId = "P1", ProductName = "Widget", Quantity = 50, UnitPrice = 25m, LineTotal = 1250m }
        };

        // Act
        var result = _sut.CalculatePricing(items);

        // Assert
        Assert(result.DiscountPercent == 0.15m, "15% discount for orders over $1000");
    }

    public void CalculatePricing_AppliesTaxAfterDiscount()
    {
        // Arrange - $1000 order, 15% discount = $850 taxable, 8% tax = $68
        var items = new List<OrderLine>
        {
            new() { ProductId = "P1", ProductName = "Widget", Quantity = 10, UnitPrice = 100m, LineTotal = 1000m }
        };

        // Act
        var result = _sut.CalculatePricing(items);

        // Assert
        Assert(result.Subtotal == 1000m, "Subtotal should be 1000");
        Assert(result.DiscountAmount == 150m, "15% of 1000 = 150");
        Assert(result.TaxAmount == 68m, "8% of 850 = 68");
        Assert(result.TotalAmount == 918m, "850 + 68 = 918");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception($"Assertion failed: {message}");
    }
}
