using LambdaRefactoringDemo.After.Models;
using LambdaRefactoringDemo.After.Validation;

namespace LambdaRefactoringDemo.After.Tests;

/// <summary>
/// Example unit tests for validation logic - clean, isolated, fast.
/// </summary>
public class OrderValidatorTests
{
    private readonly OrderValidator _sut = new();

    public void Validate_NullRequest_ReturnsFailure()
    {
        var result = _sut.Validate(null);
        Assert(!result.IsValid, "Should fail for null request");
        Assert(result.ErrorMessage == "Invalid order request", "Should have correct error message");
    }

    public void Validate_MissingCustomerId_ReturnsFailure()
    {
        var request = new OrderRequest
        {
            CustomerId = "",
            Items = new List<OrderItemRequest> { new() { ProductId = "P1", Quantity = 1 } }
        };

        var result = _sut.Validate(request);
        Assert(!result.IsValid, "Should fail for missing customer ID");
    }

    public void Validate_EmptyItems_ReturnsFailure()
    {
        var request = new OrderRequest
        {
            CustomerId = "C123",
            Items = new List<OrderItemRequest>()
        };

        var result = _sut.Validate(request);
        Assert(!result.IsValid, "Should fail for empty items");
    }

    public void Validate_InvalidQuantity_ReturnsFailure()
    {
        var request = new OrderRequest
        {
            CustomerId = "C123",
            Items = new List<OrderItemRequest> { new() { ProductId = "P1", Quantity = 0 } }
        };

        var result = _sut.Validate(request);
        Assert(!result.IsValid, "Should fail for zero quantity");
    }

    public void Validate_InvalidEmail_ReturnsFailure()
    {
        var request = new OrderRequest
        {
            CustomerId = "C123",
            Email = "notanemail",
            Items = new List<OrderItemRequest> { new() { ProductId = "P1", Quantity = 1 } }
        };

        var result = _sut.Validate(request);
        Assert(!result.IsValid, "Should fail for invalid email");
    }

    public void Validate_ValidRequest_ReturnsSuccess()
    {
        var request = new OrderRequest
        {
            CustomerId = "C123",
            Email = "test@example.com",
            Items = new List<OrderItemRequest> { new() { ProductId = "P1", Quantity = 2 } }
        };

        var result = _sut.Validate(request);
        Assert(result.IsValid, "Should pass for valid request");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception($"Assertion failed: {message}");
    }
}
