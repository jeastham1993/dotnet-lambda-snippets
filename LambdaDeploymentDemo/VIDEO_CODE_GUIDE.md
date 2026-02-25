# Video Code Guide: You're Deploying .NET Lambda Wrong. Here's How to Fix It.

This document maps the codebase to specific sections of the YouTube video, making it easy to follow along or reference code during editing.

---

## Project Structure Overview

```
LambdaDeploymentDemo/
├── src/
│   └── ProductApi/
│       ├── Functions.cs              # Lambda handler — thin orchestrator
│       ├── Startup.cs                # DI configuration
│       ├── Models/
│       │   └── ProductModels.cs      # Product, CreateProductRequest
│       └── Services/
│           ├── IProductService.cs
│           └── ProductService.cs     # DynamoDB access — throws on missing env var
│
├── cdk/
│   └── src/ProductApiCdk/
│       ├── ProductApiStack.cs        # THE KEY FILE — Lambda + alias + alarm
│       └── Program.cs
│
├── .github/workflows/
│   ├── deploy-simple.yml             # The WRONG way — shown in opening
│   └── deploy-production.yml         # The RIGHT way — four-stage pipeline
│
└── test-events/
    └── get-products.json             # Synthetic event for pre-deployment check
```

---

## Prerequisites

### Deploy the CDK stack (one-time setup)

```bash
cd cdk
dotnet restore src/ProductApiCdk/
cdk bootstrap   # first time only
cdk deploy
```

CDK creates:
- DynamoDB `Products` table
- Lambda function `ProductApi` with `PRODUCTS_TABLE_NAME` injected
- `prod` alias pointing at the initial version
- CloudWatch alarm `ProductApi-prod-ErrorRate`

### GitHub Actions secrets required

| Secret | Value |
|---|---|
| `AWS_ROLE_ARN` | ARN of the IAM role GitHub Actions assumes (needs Lambda, CloudWatch permissions) |

---

## Video Section → Code Mapping

### Hook (0:00 – 1:00)
**Open on `deploy-simple.yml` running in GitHub Actions — green deploy, then CloudWatch climbing to 100% errors.**

**File to show:** `.github/workflows/deploy-simple.yml`

```yaml
- name: Deploy
  run: |
    aws lambda update-function-code \
      --function-name ProductApi \
      --zip-file fileb://productapi.zip
```

Highlight: four lines, no version, no alias, no rollback. Every user hits broken code the moment this step completes.

**To trigger the failure for the hook:**

1. Remove `PRODUCTS_TABLE_NAME` from the CDK stack environment block in `ProductApiStack.cs`
2. Run `cdk deploy` (or push directly via `deploy-simple.yml`)
3. Open CloudWatch → Lambda → `ProductApi` — error rate climbs immediately

**"This is what most GitHub Actions Lambda workflows look like. Build, package, deploy. Done."**

---

### Reengagement (1:00 – 3:00)
**Introduce the four-stage pipeline on screen as a diagram.**

**File to show:** `.github/workflows/deploy-production.yml` — scroll to the `jobs:` section.

Point at each job name in sequence:

| Job | What it does |
|---|---|
| `build` | Compile and package the Lambda zip |
| `publish` | Upload new code, publish an immutable numbered version |
| `pre-deployment-check` | Invoke the version directly — before any traffic moves |
| `canary` | 10% traffic shift, alarm poll, promote or rollback |

**"Each stage is useless without the one before it. You need all four."**

---

### Setup (3:00 – 6:00)
**Lambda versions and aliases — the foundation everything else depends on.**

#### Versions

**File to show:** `cdk/src/ProductApiCdk/ProductApiStack.cs`

```csharp
var function = new Function(this, "ProductApiFunction", new FunctionProps
{
    FunctionName = "ProductApi",
    Runtime = Runtime.DOTNET_8,
    ...
});
```

Then switch to `.github/workflows/deploy-production.yml`, the `publish` job:

```yaml
- name: Publish version
  id: publish
  run: |
    VERSION=$(aws lambda publish-version \
      --function-name "$FUNCTION_NAME" \
      --description "Deployed from $GITHUB_SHA" \
      --query 'Version' \
      --output text)
    echo "version=$VERSION" >> "$GITHUB_OUTPUT"
    echo "New version: $VERSION"
```

**"Every deployment publishes an immutable snapshot. No more wondering what was running before this commit."**

Show the Lambda console version list — version 1, version 2, version 3 each with the commit SHA in the description.

#### Aliases

Back in `ProductApiStack.cs`:

```csharp
var prodAlias = function.AddAlias("prod");
```

**"A stable `prod` alias sits between your consumers and your versions. The workflow shifts traffic against the alias — your consumers never change their endpoint."**

Show the Lambda console: `ProductApi:prod` currently pointing at version N.

#### The CloudWatch alarm

```csharp
var errorAlarm = new Alarm(this, "ProductApiErrorAlarm", new AlarmProps
{
    AlarmName = "ProductApi-prod-ErrorRate",
    Metric = new Metric(new MetricProps
    {
        Namespace = "AWS/Lambda",
        MetricName = "Errors",
        DimensionsMap = new Dictionary<string, string>
        {
            ["FunctionName"] = function.FunctionName,
            ["Resource"] = $"{function.FunctionName}:prod",
        },
        ...
    }),
    Threshold = 1,
    EvaluationPeriods = 1,
});
```

**"The alarm watches the `:prod` qualifier specifically — so it tracks errors on the alias, not the function as a whole."**

---

### Climax (6:00 – 8:00)
**The canary stage — the part that makes the opening incident impossible.**

**File to show:** `.github/workflows/deploy-production.yml`, `canary` job.

#### Step 1 — Shift 10%

```yaml
- name: Shift 10% traffic to new version (canary start)
  run: |
    aws lambda update-alias \
      --function-name "$FUNCTION_NAME" \
      --name "$ALIAS_NAME" \
      --routing-config "AdditionalVersionWeights={\"$NEW_VERSION\":0.1}"
```

Show the Lambda console alias view after this step runs — version N at 90%, version N+1 at 10%.

#### Step 2 — Alarm polling loop

```yaml
- name: Wait and watch the alarm
  run: |
    for i in $(seq 1 10); do
      sleep 30

      STATE=$(aws cloudwatch describe-alarms \
        --alarm-names "$ALARM_NAME" \
        --query 'MetricAlarms[0].StateValue' \
        --output text)

      echo "Poll $i/10 — alarm state: $STATE"

      if [ "$STATE" = "ALARM" ]; then
        aws lambda update-alias \
          --function-name "$FUNCTION_NAME" \
          --name "$ALIAS_NAME" \
          --function-version "$PREVIOUS_VERSION" \
          --routing-config '{}'

        exit 1
      fi
    done
```

Show GitHub Actions log as it polls — `Poll 1/10 — alarm state: OK`, `Poll 2/10 — alarm state: OK`...

#### Step 3a — Promote (alarm stayed green)

```yaml
- name: Promote to 100%
  if: success()
  run: |
    aws lambda update-alias \
      --function-name "$FUNCTION_NAME" \
      --name "$ALIAS_NAME" \
      --function-version "$NEW_VERSION" \
      --routing-config '{}'
```

#### Step 3b — Rollback (alarm fired)

The `exit 1` inside the polling loop rolls back the alias immediately and marks the job failed. GitHub Actions shows the workflow as red with the rollback annotation in the log.

#### Manual approval gate (optional)

```yaml
# manual-approval:
#   name: Approve full promotion
#   needs: [publish, pre-deployment-check]
#   runs-on: ubuntu-latest
#   environment: production   # add required reviewers in GitHub settings
```

**"For big releases or Fridays — uncomment this block, add reviewers to the `production` environment in GitHub settings, and the workflow pauses here waiting for approval."**

#### Triggering the failure deliberately

1. Remove `PRODUCTS_TABLE_NAME` from `ProductApiStack.cs` environment block
2. Push the commit — `deploy-production.yml` kicks in
3. `publish` job succeeds (code is uploaded and versioned)
4. `pre-deployment-check` invokes the new version directly
5. Lambda throws `InvalidOperationException` — cold start fails with 500
6. Workflow fails at `pre-deployment-check`, alias never moves, production unaffected

**File to show:** `src/ProductApi/Services/ProductService.cs`

```csharp
_tableName = Environment.GetEnvironmentVariable("PRODUCTS_TABLE_NAME")
    ?? throw new InvalidOperationException(
        "PRODUCTS_TABLE_NAME environment variable is not set. " +
        "Check the Lambda function configuration.");
```

**"Production never went down. 90% of users never saw a single error."**

---

### Goosh / Bonus (8:00 – 9:00)
**Pre-deployment validation — catching the bad commit before the canary even starts.**

**File to show:** `.github/workflows/deploy-production.yml`, `pre-deployment-check` job.

```yaml
- name: Invoke new version with synthetic test event
  run: |
    aws lambda invoke \
      --function-name "${FUNCTION_NAME}:${NEW_VERSION}" \
      --payload file://test-events/get-products.json \
      --cli-binary-format raw-in-base64-out \
      response.json

- name: Assert response is healthy
  run: |
    STATUS=$(jq -r '.statusCode' response.json)
    if [ "$STATUS" != "200" ]; then
      echo "Pre-deployment check FAILED: expected 200, got $STATUS"
      exit 1
    fi
```

**File to show:** `test-events/get-products.json` — a synthetic `GET /products` API Gateway v2 event.

**"The version qualifier `ProductApi:3` invokes version 3 directly — it never touches the alias. No production traffic involved at all."**

Show GitHub Actions log:

```
Pre-deployment check FAILED: expected 200, got 500
```

Alias unchanged in the Lambda console. Production clean.

---

### Wrap Up (10:00)
**Return to the opening incident and walk through exactly which stage would have caught it.**

| Failure type | Caught by |
|---|---|
| Missing environment variable | `pre-deployment-check` — cold start throws, returns 500 |
| Broken cold start / missing dependency | `pre-deployment-check` — Lambda fails to initialise |
| Runtime regression (errors under load) | `canary` — alarm fires within 5 minutes, alias rolled back |
| Silent regression (no errors, wrong output) | `pre-deployment-check` — response assertion fails |

**"Production-grade deployment is not optional. And with GitHub Actions and Lambda aliases, it's surprisingly little YAML."**

---

## Files to Show On Screen

| Video Moment | File | What to Highlight |
|---|---|---|
| Opening disaster | `.github/workflows/deploy-simple.yml` | The four-line deploy, nothing else |
| Pipeline overview | `.github/workflows/deploy-production.yml` | Job names as a list |
| CDK — function definition | `cdk/src/ProductApiCdk/ProductApiStack.cs` | `FunctionName`, `Environment` block |
| CDK — alias | `ProductApiStack.cs` | `function.AddAlias("prod")` |
| CDK — alarm | `ProductApiStack.cs` | `Resource = FunctionName:prod` dimension |
| Publish version | `deploy-production.yml` `publish` job | `aws lambda publish-version` |
| Canary shift | `deploy-production.yml` `canary` job | `routing-config 0.1` line |
| Rollback | `deploy-production.yml` `canary` job | `exit 1` and preceding `update-alias` |
| Manual approval | `deploy-production.yml` | Commented-out `manual-approval` job |
| Fail-fast constructor | `src/ProductApi/Services/ProductService.cs` | `?? throw new InvalidOperationException` |
| Pre-deployment invoke | `deploy-production.yml` `pre-deployment-check` job | `--function-name ${FUNCTION_NAME}:${NEW_VERSION}` |
| Test event | `test-events/get-products.json` | Full payload, `routeKey: GET /products` |

---

## Key Code Snippets for B-Roll

### The wrong workflow
```yaml
# deploy-simple.yml — no safety net
- name: Deploy
  run: |
    aws lambda update-function-code \
      --function-name ProductApi \
      --zip-file fileb://productapi.zip
```

### Publishing an immutable version
```bash
aws lambda publish-version \
  --function-name ProductApi \
  --description "Deployed from abc1234" \
  --query 'Version' \
  --output text
# Returns: 7
```

### Canary traffic shift
```bash
aws lambda update-alias \
  --function-name ProductApi \
  --name prod \
  --routing-config 'AdditionalVersionWeights={"7":0.1}'
# prod: 90% version 6, 10% version 7
```

### Automatic rollback
```bash
aws lambda update-alias \
  --function-name ProductApi \
  --name prod \
  --function-version 6 \
  --routing-config '{}'
# prod: 100% version 6
```

### The fail-fast constructor
```csharp
// ProductService.cs
_tableName = Environment.GetEnvironmentVariable("PRODUCTS_TABLE_NAME")
    ?? throw new InvalidOperationException(
        "PRODUCTS_TABLE_NAME environment variable is not set.");
// Cold start fails immediately — pre-deployment check catches it
// before a single production request touches the new version
```
