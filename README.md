# Operation authorization sample

The API demonstrates three checks:

1. JWT signature, issuer, lifetime and audience: invalid or missing credentials return **401**.
2. Scope grants access to an API area: missing scope returns **403**.
3. User roles expand through role -> tasks -> operations: missing operation permission returns **403**.

## Endpoints

| Endpoint | Scheme | Audience | Scope | User operation |
| --- | --- | --- | --- | --- |
| GET /transfers | Bearer | api1 | transfers | view-transfers |
| GET /transfers/{id} | Bearer | api1 | transfers | view-transfers |
| POST /transfers | Bearer | api1 | transfers | make-transfer; also make-transfer-higher-limit above 1,000 |
| POST /transfers/reconcile | ServiceBearer | api1 | transfers.reconcile | None |
| GET /infrastructure/tasks | InfrastructureBearer | authorization-catalog | authorization.tasks.read | None |

User endpoints share the `transfers` scope policy and add individual operation policies. Their mappings and policies live in `Api1/Transfers`. Reconciliation accepts service tokens without user roles or a subject. Each policy selects its own authentication scheme. A token carrying the right scope but the wrong issuer or audience is rejected with 401.

## Reusable infrastructure endpoint

`OperationAuthorization.AspNetCore` registers and maps the task catalog endpoint:

```csharp
builder.Services.AddOperationAuthorizationInfrastructure();
// After building the application:
app.MapOperationAuthorizationInfrastructure(service: "api1", version: 1);
```

The application registers its `ITaskCatalog`. The extensions register `InfrastructureBearer`, audience `authorization-catalog`, scope normalization, and the `authorization.tasks.read` policy. Authority binds from `Authentication:Schemes:InfrastructureBearer:Authority`. The mapping accepts an optional route pattern, defaulting to `/infrastructure/tasks`.

The response is a complete catalog snapshot:

```json
{
  "service": "api1",
  "version": 1,
  "tasks": [
    "view-transfers",
    "make-transfer",
    "make-transfer-higher-limit"
  ]
}
```

The service supplies its name and catalog revision in Program.cs. A shared `authorization-catalog` audience deliberately lets an authorized IAM client discover task catalogs across the organization, while business endpoints retain their service-specific audience. Infrastructure can use the same issuer as other services but validates its own audience.

## Tasks and roles

`ITaskCatalog.GetTaskNames()` enumerates all tasks for export. `GetOperations(name)` supplies each task's operations from the same definition map.

| Application-owned task | Operations |
| --- | --- |
| view-transfers | view-transfers |
| make-transfer | make-transfer |
| make-transfer-higher-limit | make-transfer, make-transfer-higher-limit |

| Security-officer-owned role | Tasks |
| --- | --- |
| viewer | view-transfers |
| cashier | view-transfers, make-transfer |
| supervisor | view-transfers, make-transfer-higher-limit |

Unknown roles and tasks grant nothing. Role claims come from the issuer. `RequestPermissions` lazily resolves an immutable operation set once per request. `OperationHandler` serves both endpoint policies and the imperative higher-limit check. `ScopeNormalizationEvents`, supplied by the ASP.NET Core library, splits space-separated scopes without duplicating claims.

## Run locally

Requires .NET 10 and PowerShell 7:

```powershell
pwsh ./Generate-SampleTokens.ps1
dotnet run --project Api1 --launch-profile http
```

Open `Api1/Api1.http`, select **dev** in Visual Studio, and send the requests. `http-client.env.json` resolves generated tokens through the `AspnetUserSecrets` provider. The script generates seven tokens and stores them and their signing keys in user secrets. Default validity is 365 days; use `-ValidFor 30d` to change it. Re-running preserves unrelated secrets and previously issued tokens.

Local user tokens use issuer `dotnet-user-jwts`. Reconciliation and infrastructure tokens use `sample-services`, with separate schemes and audiences. Their subject is `not-available`, because `user-jwts` always supplies one; no user roles are added. Tests also cover service tokens with no subject. Development sets all three authorities to empty strings for local validation and logs authentication and authorization outcomes. Clear any authority environment overrides before using the local demo.

For real issuers, configure the three `Authentication:Schemes:<scheme>:Authority` settings. Audiences stay fixed in code:

```powershell
$env:Authentication__Schemes__Bearer__Authority = 'https://your-issuer'
$env:Authentication__Schemes__ServiceBearer__Authority = 'https://your-service-issuer'
$env:Authentication__Schemes__InfrastructureBearer__Authority = 'https://your-service-issuer'
dotnet run --project Api1
```

POST /transfers echoes a positive amount; nonpositive amounts return 400. Reads return one illustrative pending transfer with ID 1; unknown IDs return 404. Reconciliation returns a placeholder count. No money is moved or reconciled, and no persistence or account ownership checks are implemented.

```powershell
dotnet test --solution authorization-sample.slnx
```

`Api1.Tests` exercises the actual API with signed JWTs: validation, issuer and audience isolation, scopes, operation checks, catalog metadata, request isolation, and the higher-limit boundary.
