# Lambda Testing Demo

Companion code for the video **"Your .NET Lambda Has No Tests. Here's How to Fix That."**

This sample walks through the four-layer testing pyramid for a .NET Lambda that:
- Accepts order placement requests via API Gateway
- Calls an upstream **Product Catalog API** to enrich each order with product names and prices
- Persists orders to **DynamoDB**

The production incident used as the video hook: the Product Catalog team released a v2 API that renamed three fields. The Lambda didn't throw — it silently stored empty product names and zero prices for 48 hours. A contract test would have caught it in CI before a single order was taken.

---

## Project Structure

```
src/LambdaTestingDemo/          The Lambda function (ports and adapters)
  Functions.cs                  Thin handler — delegates all logic to OrderService
  Startup.cs                    DI registration
  Adapters/                     External dependencies behind interfaces
    IProductCatalogClient.cs    Port: what the Lambda needs from the catalog
    HttpProductCatalogClient.cs Adapter: the real HTTP implementation
  Repositories/                 DynamoDB persistence
  Services/
    OrderService.cs             All business logic — the testable core

tests/
  LambdaTestingDemo.UnitTests/      Layer 1: fast, no AWS, mocked dependencies
  LambdaTestingDemo.ContractTests/  Layer 2: verifies upstream API shape with WireMock
  LambdaTestingDemo.IntegrationTests/ Layer 3: real deployed AWS resources

load-tests/
  artillery.yml                 Layer 4: Artillery load test

infra/
  LambdaTestingDemoStack.cs     CDK stack — all resources include a suffix
```

---

## Layer 1: Unit Tests

Fast, focused, no AWS credentials required. Test the business logic in `OrderService` in complete isolation using `NSubstitute` mocks.

```bash
dotnet test tests/LambdaTestingDemo.UnitTests
```

These run in milliseconds and cover:
- Valid order placement and product enrichment
- Validation failures (missing customer ID, empty items, zero quantity)
- Product not found / out of stock
- Repository is only called on success

---

## Layer 2: Contract Tests

Verifies the shape of the upstream Product Catalog API. Run these in CI before deploying — they catch breaking changes from upstream teams before they corrupt your data.

```bash
dotnet test tests/LambdaTestingDemo.ContractTests
```

The test `GetProduct_UpstreamRenamesFields_DataSilentlyCorrupts` demonstrates exactly what the production incident looked like: the Lambda returns a valid-looking object with empty `ProductName` and zero `UnitPrice` because the upstream silently renamed the fields.

---

## Layer 3: Integration Tests

Tests the real deployed Lambda via API Gateway — the same path production traffic takes. Catches IAM permission errors, missing environment variables, and real service integration failures that mocks can never surface.

### Prerequisites

Deploy a test stack first (see [Deploying](#deploying) below), then set:

```bash
export API_GATEWAY_URL=https://<api-id>.execute-api.<region>.amazonaws.com
export RESOURCE_SUFFIX=yourname   # or whatever suffix you deployed with
```

Then run:

```bash
dotnet test tests/LambdaTestingDemo.IntegrationTests
```

---

## Layer 4: Load Tests

Hammers the deployed API to surface cold start problems, memory pressure, and downstream service limits before your users do.

### Prerequisites

```bash
npm install -g artillery
export API_GATEWAY_URL=https://<api-id>.execute-api.<region>.amazonaws.com
```

Run the load test:

```bash
cd load-tests
artillery run artillery.yml
```

The config runs three phases: warm-up (2→10 req/s), sustained load (25 req/s), and peak (100 req/s). The test fails if p99 exceeds 3000ms or the error rate exceeds 1%.

---

## Deploying

The CDK stack uses a **suffix** to isolate every resource name. This is what makes safe, parallel test environments possible.

| Suffix | Use case | Resource names |
|--------|----------|----------------|
| `prod` | Production | `Orders-prod`, `PlaceOrder-prod` |
| `yourname` | Local dev / integration tests | `Orders-yourname`, `PlaceOrder-yourname` |
| `abc123f` | CI per-commit | `Orders-abc123f`, `PlaceOrder-abc123f` |

```bash
# Install dependencies
cd infra
dotnet restore

# Deploy a developer stack
dotnet cdk deploy -c suffix=yourname

# Deploy production`
dotnet cdk deploy -c suffix=prod
```

CDK outputs the `ApiEndpoint` URL — use this as `API_GATEWAY_URL` for integration and load tests.

To tear down a non-production stack:

```bash
dotnet cdk destroy -c suffix=yourname
```

> Production stacks (`suffix=prod`) use `RemovalPolicy.RETAIN` on the DynamoDB table. All other suffixes use `DESTROY` so test stacks clean up completely.

---

## Running Everything Together (CI Example)

```bash
# 1. Unit tests — always run, no infra needed
dotnet test tests/LambdaTestingDemo.UnitTests

# 2. Contract tests — always run, no infra needed
dotnet test tests/LambdaTestingDemo.ContractTests

# 3. Deploy a per-commit test stack
SUFFIX=$(git rev-parse --short HEAD)
dotnet cdk deploy -c suffix=$SUFFIX --require-approval never

# 4. Integration tests against the real stack
export API_GATEWAY_URL=$(dotnet cdk output -c suffix=$SUFFIX ApiEndpoint)
dotnet test tests/LambdaTestingDemo.IntegrationTests

# 5. Load test (optional gate)
cd load-tests && artillery run artillery.yml

# 6. Promote to prod (or tear down on failure)
dotnet cdk deploy -c suffix=prod --require-approval never
dotnet cdk destroy -c suffix=$SUFFIX --require-approval never
```
