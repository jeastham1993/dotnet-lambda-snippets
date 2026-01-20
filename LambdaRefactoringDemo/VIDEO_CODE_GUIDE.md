# Video Code Guide: This .NET Lambda Is A Disaster (Let's Fix It)

This document maps the codebase to specific sections of the YouTube video, making it easy to follow along or reference code during editing.

---

## Project Structure Overview

```
LambdaRefactoringDemo/
├── Before/
│   └── OrderFunction.cs          # THE DISASTER - 580 lines, everything in one file
│
├── After/
│   ├── OrderFunction.cs          # THE RESCUE - Clean 60-line handler
│   ├── Startup.cs                # Dependency injection configuration
│   ├── Models/
│   │   └── OrderModels.cs        # Clean, focused data models
│   ├── Validation/
│   │   └── OrderValidator.cs     # Extracted validation logic
│   ├── Repositories/
│   │   ├── IInventoryRepository.cs
│   │   ├── InventoryRepository.cs
│   │   ├── IOrderRepository.cs
│   │   └── OrderRepository.cs    # Database access layer
│   ├── Services/
│   │   ├── IPricingService.cs
│   │   ├── PricingService.cs     # Business logic - pricing/discounts
│   │   ├── INotificationService.cs
│   │   ├── NotificationService.cs
│   │   ├── IOrderService.cs
│   │   └── OrderService.cs       # Order processing orchestration
│   └── Tests/
│       ├── PricingServiceTests.cs
│       └── OrderValidatorTests.cs # Now testable!
│
├── template.yaml
└── LambdaRefactoringDemo.csproj
```

---

## Video Section → Code Mapping

### Hook (0:00 - 1:00)
**Open directly on a 400-line Lambda handler function. No introduction, just code.**

**File to show:** `Before/OrderFunction.cs`

Scroll slowly through the entire file, letting the size register:
- Line count visible in editor: **580 lines**
- All models embedded at bottom
- Comments mark the sections (for video clarity)

**"This Lambda works. It validates requests, queries DynamoDB, transforms data, sends notifications... all in one method."**

Show it executing:
```bash
curl -X POST $API_URL/before/orders \
  -H "Content-Type: application/json" \
  -d '{"customerId":"C123","email":"test@example.com","items":[{"productId":"P1","quantity":2}]}'
```

**"So what's actually wrong with it? Let me try to write a unit test."**

Attempt to show test setup - highlight the impossibility:
- Would need real DynamoDB
- Would need real SNS
- Can't mock anything
- Can't isolate behavior

---

### Reengagement (1:00 - 3:00)
**"Let's rescue this. First, what is this function actually doing?"**

Walk through responsibilities in `Before/OrderFunction.cs`:

| Section | Lines | Responsibility |
|---------|-------|----------------|
| Input Validation | 35-135 | Parsing, null checks, business rules |
| Inventory Check | 140-210 | DynamoDB queries, stock verification |
| Pricing Calculation | 215-280 | Discounts, tax, totals |
| Create Order | 285-360 | Build item, save to DynamoDB |
| Update Inventory | 365-400 | Reduce stock quantities |
| Send Notifications | 405-470 | SNS publish |
| Build Response | 475-500 | Format API response |

**Show target structure** (file tree or diagram):
```
After/
├── OrderFunction.cs      ← Thin handler
├── Validation/           ← Input validation
├── Repositories/         ← Database access
├── Services/             ← Business logic
└── Tests/                ← Now possible!
```

---

### Setup / Extraction (3:00 - 6:00)
**"Let's start by pulling out input validation."**

**First extraction:** Show creating `After/Validation/OrderValidator.cs`

```csharp
public class OrderValidator : IOrderValidator
{
    public ValidationResult Validate(OrderRequest? request)
    {
        if (request == null)
            return ValidationResult.Failure("Invalid order request");

        if (string.IsNullOrWhiteSpace(request.CustomerId))
            return ValidationResult.Failure("CustomerId is required");

        // ... clean, focused validation
    }
}
```

**Line count comparison:**
- Before: 100 lines of validation scattered in handler
- After: 45 lines in dedicated class

---

**"Next, the database access."**

**Second extraction:** Show `After/Repositories/InventoryRepository.cs`

```csharp
public class InventoryRepository : IInventoryRepository
{
    public async Task<InventoryItem?> GetByProductIdAsync(string productId)
    {
        // Clean, focused database access
    }
}
```

Key point: **Interface + Implementation** enables mocking

---

**"Now the business logic has a home."**

**Third extraction:** Show `After/Services/PricingService.cs`

```csharp
public class PricingService : IPricingService
{
    public PricingResult CalculatePricing(List<OrderLine> items)
    {
        var subtotal = items.Sum(i => i.LineTotal);
        var discountPercent = GetDiscountPercent(subtotal);
        // Pure business logic - no AWS dependencies!
    }

    private static decimal GetDiscountPercent(decimal subtotal) => subtotal switch
    {
        >= 1000 => 0.15m,
        >= 500 => 0.10m,
        >= 100 => 0.05m,
        _ => 0m
    };
}
```

This is **pure C#** - no AWS SDK, no side effects, trivially testable.

---

### Climax (6:00 - 8:00)
**"Look at this handler now."**

Show the clean handler `After/OrderFunction.cs`:

```csharp
public async Task<APIGatewayProxyResponse> ProcessOrder(
    APIGatewayProxyRequest request,
    ILambdaContext context)
{
    var orderRequest = DeserializeRequest(request.Body);

    var validation = _validator.Validate(orderRequest);
    if (!validation.IsValid)
        return BadRequest(validation.ErrorMessage!);

    var result = await _orderService.ProcessOrderAsync(orderRequest!);

    if (!result.Success)
        return BadRequest(result.ErrorMessage!);

    return Created(OrderResponse.FromOrder(result.Order!));
}
```

**The dramatic comparison:**

| Metric | Before | After |
|--------|--------|-------|
| Handler lines | ~470 | 15 |
| Total file lines | 580 | 60 |
| Responsibilities | 7 | 1 (orchestration) |
| Testable | No | Yes |

**"Same request, same response."**

Run both endpoints, show identical output:
```bash
# Before (monolithic)
curl -X POST $API_URL/before/orders -d '...'

# After (clean)
curl -X POST $API_URL/after/orders -d '...'
```

---

### Goosh / Bonus (8:00 - 9:00)
**"I can actually test this now."**

Show `After/Tests/PricingServiceTests.cs`:

```csharp
public void CalculatePricing_Over1000_15PercentDiscount()
{
    // Arrange
    var items = new List<OrderLine>
    {
        new() { ProductId = "P1", Quantity = 50, UnitPrice = 25m, LineTotal = 1250m }
    };

    // Act
    var result = _sut.CalculatePricing(items);

    // Assert
    Assert(result.DiscountPercent == 0.15m, "15% discount for orders over $1000");
}
```

**Key points:**
- No mocks needed for pure business logic
- Test runs in milliseconds
- Focused on one behavior
- **This was impossible before**

Show `After/Tests/OrderValidatorTests.cs` briefly:
```csharp
public void Validate_InvalidEmail_ReturnsFailure()
{
    var request = new OrderRequest { CustomerId = "C123", Email = "notanemail", ... };
    var result = _sut.Validate(request);
    Assert(!result.IsValid, "Should fail for invalid email");
}
```

---

### Wrap Up (9:00 - 10:00)
**"Your handler should be thin. It's an entry point, not your entire application."**

**Final side-by-side:**

**Before** (`Before/OrderFunction.cs`):
- 580 lines
- 7 responsibilities
- Untestable
- Hard to modify

**After** (`After/OrderFunction.cs`):
- 60 lines
- 1 responsibility (orchestration)
- Fully testable
- Easy to extend

**Summary of extracted components:**

| Component | File | Purpose |
|-----------|------|---------|
| OrderValidator | Validation/OrderValidator.cs | Input validation |
| InventoryRepository | Repositories/InventoryRepository.cs | Database reads |
| OrderRepository | Repositories/OrderRepository.cs | Database writes |
| PricingService | Services/PricingService.cs | Business logic |
| NotificationService | Services/NotificationService.cs | External notifications |
| OrderService | Services/OrderService.cs | Orchestration |

**End abruptly on the clean version - no lengthy outro.**

---

## Files to Show On Screen

| Video Moment | File | What to Highlight |
|--------------|------|-------------------|
| Opening scroll | Before/OrderFunction.cs | Full 580 lines, line numbers visible |
| "What is it doing?" | Before/OrderFunction.cs | Comment markers for each section |
| Validation extraction | After/Validation/OrderValidator.cs | Clean, focused class |
| Repository extraction | After/Repositories/InventoryRepository.cs | Interface-based design |
| Business logic | After/Services/PricingService.cs | Pure C#, no dependencies |
| Clean handler | After/OrderFunction.cs | 15 lines that tell a story |
| Side-by-side | Both OrderFunction.cs files | Split screen comparison |
| Testing | After/Tests/PricingServiceTests.cs | Simple, fast, focused |

---

## Key Code Snippets for B-Roll

### The Monolith Problem
```csharp
// Before: Everything tangled together
if (string.IsNullOrEmpty(request.Body)) { /* 20 lines */ }
try { orderRequest = JsonSerializer.Deserialize... } catch { /* 15 lines */ }
if (orderRequest == null) { /* 10 lines */ }
// ... 400 more lines
```

### The Clean Solution
```csharp
// After: Reads like a story
var validation = _validator.Validate(orderRequest);
if (!validation.IsValid) return BadRequest(validation.ErrorMessage!);

var result = await _orderService.ProcessOrderAsync(orderRequest!);
if (!result.Success) return BadRequest(result.ErrorMessage!);

return Created(OrderResponse.FromOrder(result.Order!));
```

### Pure Business Logic
```csharp
// PricingService - no AWS, no side effects
private static decimal GetDiscountPercent(decimal subtotal) => subtotal switch
{
    >= 1000 => 0.15m,
    >= 500 => 0.10m,
    >= 100 => 0.05m,
    _ => 0m
};
```

---

## Deployment Commands

```bash
# Build
sam build

# Deploy both versions
sam deploy

# Test Before (monolithic)
curl -X POST $API_URL/before/orders \
  -H "Content-Type: application/json" \
  -d '{"customerId":"C123","email":"test@example.com","items":[{"productId":"P1","quantity":2}]}'

# Test After (clean) - same input, same output
curl -X POST $API_URL/after/orders \
  -H "Content-Type: application/json" \
  -d '{"customerId":"C123","email":"test@example.com","items":[{"productId":"P1","quantity":2}]}'
```
