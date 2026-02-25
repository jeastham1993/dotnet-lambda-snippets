# Lambda Deployment Demo

Companion code for the video **"You're Deploying .NET Lambda Wrong. Here's How to Fix It."**

This sample shows a production-grade GitHub Actions deployment pipeline for a .NET Lambda, built on four pillars that make a bad deployment impossible to reach production:

1. **Versions** — every deployment publishes an immutable numbered snapshot
2. **Aliases** — a stable `prod` pointer that consumers never need to change
3. **Canary traffic shifting** — 10% of traffic moves first; 90% stays on the known-good version
4. **Automated rollback** — a CloudWatch alarm polls the canary; if it fires, the alias is restored before most users ever see an error

---

## Project Structure

```
src/ProductApi/           .NET 8 Lambda — product catalogue API (GET/POST /products)
  Functions.cs            Thin handler using Lambda Annotations
  Startup.cs              DI registration
  Services/
    ProductService.cs     DynamoDB access — fails fast on missing env var

cdk/src/ProductApiCdk/
  ProductApiStack.cs      CDK stack — DynamoDB table, Lambda function, prod alias, alarm

.github/workflows/
  deploy-simple.yml       The WRONG approach — shown in the opening
  deploy-production.yml   The RIGHT approach — four-stage canary pipeline

test-events/
  get-products.json       Synthetic API Gateway v2 event for pre-deployment validation
```

---

## Prerequisites

- .NET 8 SDK
- AWS CDK v2 (`npm install -g aws-cdk`)
- AWS credentials configured locally

---

## Deploy the infrastructure (one-time)

```bash
cd cdk
dotnet restore src/ProductApiCdk/

# First time only
cdk bootstrap

cdk deploy
```

CDK creates:
- DynamoDB `Products` table
- Lambda function `ProductApi` with `PRODUCTS_TABLE_NAME` injected
- `prod` alias pointing at the initial version
- CloudWatch alarm `ProductApi-prod-ErrorRate` watching the `:prod` qualifier

---

## GitHub Actions setup

Add one secret to the repository:

| Secret | Value |
|---|---|
| `AWS_ROLE_ARN` | ARN of the IAM role GitHub Actions will assume |

The role needs permissions for: `lambda:UpdateFunctionCode`, `lambda:PublishVersion`, `lambda:GetAlias`, `lambda:UpdateAlias`, `cloudwatch:DescribeAlarms`, and `lambda:InvokeFunction`.

---

## The two workflows

### `deploy-simple.yml` — the wrong way

Builds, packages, and calls `aws lambda update-function-code` directly. No version, no alias, no canary. One bad commit takes down production immediately.

Push any commit to `main` to trigger it.

### `deploy-production.yml` — the right way

Four jobs run in sequence:

| Job | What it does |
|---|---|
| `build` | Compiles the Lambda and uploads the zip as an artifact |
| `publish` | Uploads the new code, records the previous alias version, runs `aws lambda publish-version` |
| `pre-deployment-check` | Invokes the new version number directly with `test-events/get-products.json` and asserts the response is a 200 |
| `canary` | Shifts 10% of traffic to the new version, polls the CloudWatch alarm every 30 seconds for 5 minutes, then promotes to 100% or rolls the alias back |

Push any commit to `main` to trigger it.

---

## Simulating the failure scenario

The video demonstrates a missing environment variable breaking production. To reproduce it:

### With `deploy-simple.yml` (no safety net)

1. Remove the `PRODUCTS_TABLE_NAME` entry from the `Environment` block in `cdk/src/ProductApiCdk/ProductApiStack.cs`
2. Run `cdk deploy` to push the broken configuration
3. Push a code commit to trigger `deploy-simple.yml`
4. Watch CloudWatch → Lambda → `ProductApi` — error rate climbs to 100% immediately

### With `deploy-production.yml` (canary pipeline)

1. Remove the `PRODUCTS_TABLE_NAME` entry from `ProductApiStack.cs`
2. Push the commit — `deploy-production.yml` triggers
3. The `publish` job succeeds (code uploads fine)
4. The `pre-deployment-check` job invokes the new version number directly — `ProductService` throws `InvalidOperationException` on cold start and returns a 500
5. The assertion fails, the workflow stops, the `prod` alias never moves
6. Production is unaffected — check the Lambda console to confirm the alias still points at the previous version

---

## Manual approval gate (optional)

For releases that need a human sign-off before full promotion, uncomment the `manual-approval` job in `deploy-production.yml`:

```yaml
manual-approval:
  name: Approve full promotion
  needs: [publish, pre-deployment-check]
  runs-on: ubuntu-latest
  environment: production   # add required reviewers in GitHub repo settings
  steps:
    - name: Approved
      run: echo "Deployment approved — proceeding to canary"
```

Then add `needs: manual-approval` to the `canary` job. Configure the `production` environment in **GitHub → Settings → Environments** with required reviewers to enforce the gate.

---

## Restoring the environment variable

After running the failure demo, restore `PRODUCTS_TABLE_NAME` in `ProductApiStack.cs` and run `cdk deploy` to fix the function configuration.
