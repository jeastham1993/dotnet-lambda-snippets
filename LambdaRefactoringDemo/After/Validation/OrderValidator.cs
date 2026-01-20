using LambdaRefactoringDemo.After.Models;

namespace LambdaRefactoringDemo.After.Validation;

public class ValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }

    public static ValidationResult Success() => new() { IsValid = true };
    public static ValidationResult Failure(string message) => new() { IsValid = false, ErrorMessage = message };
}

public interface IOrderValidator
{
    ValidationResult Validate(OrderRequest? request);
}

public class OrderValidator : IOrderValidator
{
    public ValidationResult Validate(OrderRequest? request)
    {
        if (request == null)
            return ValidationResult.Failure("Invalid order request");

        if (string.IsNullOrWhiteSpace(request.CustomerId))
            return ValidationResult.Failure("CustomerId is required");

        if (request.Items == null || request.Items.Count == 0)
            return ValidationResult.Failure("At least one item is required");

        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.ProductId))
                return ValidationResult.Failure("All items must have a ProductId");

            if (item.Quantity <= 0)
                return ValidationResult.Failure($"Item {item.ProductId} must have quantity greater than 0");
        }

        if (!string.IsNullOrEmpty(request.Email) && !request.Email.Contains("@"))
            return ValidationResult.Failure("Invalid email format");

        return ValidationResult.Success();
    }
}
