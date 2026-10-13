# Operation authorization sample

The API demonstrates three checks:

1. JWT signature, issuer, lifetime and audience: invalid or missing credentials return **401**.
2. Scope grants access to an API area: missing scope returns **403**.
3. User roles grant tasks; task policies evaluate their requirements: missing task permission returns **403**.

## Endpoints

| Endpoint | Scheme | Audience | Scope | Task checks |
| --- | --- | --- | --- | --- |
| GET /transfers | Bearer | api1 | transfers | api1:view-transfers |
| GET /transfers/{id} | Bearer | api1 | transfers | api1:view-transfers |
| POST /transfers | Bearer | api1 | transfers | make-transfer; also make-transfer-higher-limit above 1,000 |
| POST /transfers/opa-centralized | Bearer | api1 | transfers | OPA resolves role task grants and checks amount |
| POST /transfers/opa | Bearer | api1 | transfers | make-transfer; OPA decides the amount rule |
| POST /transfers/reconcile | ServiceBearer | api1 | transfers.reconcile | None |
| GET /infrastructure/tasks | InfrastructureBearer | authorization-catalog | authorization.tasks.read | None |

User endpoints share the `transfers` scope policy and add individual task policies. Transfer mappings and policies live in `Api1/Transfers`; the OPA examples and HTTP bridge live in `Api1/Opa`, with their tests in `Api1.Tests/Opa`. Reconciliation accepts service tokens without user roles or a subject. Each policy selects its own authentication scheme. A token carrying the right scope but the wrong issuer or audience is rejected with 401.

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
    "api1:view-transfers",
    "api1:make-transfer",
    "api1:make-transfer-higher-limit"
  ]
}
```

The service supplies its name and catalog revision in Program.cs. A shared `authorization-catalog` audience deliberately lets an authorized IAM client discover task catalogs across the organization, while business endpoints retain their service-specific audience. Infrastructure can use the same issuer as other services but validates its own audience.

## OPA comparison

A third endpoint, `POST /transfers/opa`, delegates the transfer decision to Open Policy Agent. Token, issuer, audience, scope and base-task checks stay in ASP.NET Core. The endpoint passes the transfer resource to authorization. `OpaAuthorizationHandler` builds the input from that resource and resolves effective task grants from `context.User` when `ResolveTasks` is enabled. The generic `OpaRequirement` selects the task-specific decision path, so no task or service selector is sent in the input; `OpaAuthorizationHandler` posts that input to `/v1/data/api1/make_transfer_higher_limit_v1/allow`. No JWT is forwarded.

The rule in `opa/api1.make_transfer_higher_limit_v1.rego` is direct: require the base task grant, then allow an amount at or below the standard limit or a caller with the higher-limit grant. `opa/authorization.json` holds supporting data: role assignments, flat task grants, and the standard limit. Standard amounts do not require the higher-limit grant. Changing the Rego rule changes the OPA example without changing C#. The existing local task-policy example remains alongside it for comparison.

### Centralized permission resolution

`POST /transfers/opa-centralized` is the centralized comparison. ASP.NET Core validates the JWT and the `transfers` scope, then the OPA handler sends authenticated role claims and transfer facts to `/v1/data/api1/make_transfer_higher_limit_v2/allow`:

```json
{
  "input": {
    "roles": ["supervisor"],
    "resource": { "amount": 2000 }
  }
}
```

No precomputed task list is sent and no local task policy is attached to this endpoint. OPA resolves roles -> tasks using `opa/authorization.json`, and evaluates the transfer rule directly in `opa/api1.make_transfer_higher_limit_v2.rego`. The centralized policy is independent of `opa/api1.make_transfer_higher_limit_v1.rego` and looks up `data.authorization.services.api1` directly. It expands assigned tasks using their flat grant lists, without recursion. Unknown roles or tasks grant nothing. Roles come from the authenticated principal, never the request body. The shared HTTP bridge builds both input contracts from the authorization context. The unused subject claim is omitted.

The JSON data file is a small stand-in for a centrally managed IAM snapshot. It separates role composition, flattened application task grants and the standard limit from the Rego logic; a later IAM integration could distribute the snapshot as OPA data or a bundle. Role assignments still come from the token issuer. The local definitions remain for the other examples, so the sample intentionally shows both approaches rather than connecting a real IAM system.

Integration tests prove that local role/task resolvers are not called and that updating a role's tasks inside OPA changes the decision without changing C#. The Rego tests also check policy selection, implied base grants, data-driven limits, and forged task lists. A consistency test compares task grants, roles and the standard limit with the application catalog; integration tests compare both OPA paths with the local C# policy.

Start OPA for manual use:

```powershell
docker compose up -d
```

With Podman, use `podman compose` instead of `docker compose`. `compose.yaml` pins OPA 1.21.2, mounts `opa/` read-only with file watching, and publishes OPA only on localhost port 8181. The API uses `HttpClients:Opa:BaseAddress` from configuration, defaulting to `http://127.0.0.1:8181/` to avoid localhost IPv6 forwarding issues with Windows Podman. Run the API normally and send the OPA requests in `Api1.http`.

Only `result: true` grants access. False or missing decisions, malformed responses, HTTP errors and the three-second timeout deny access with 403; failures are logged. This deliberately small example has no retry or decision cache.

OPA also authorizes the calling service separately from the end user. It runs with `--authentication=token --authorization=basic`; `opa/system.authz.rego` verifies HS256 service tokens with issuer `authorization-sample-services`, audience `opa`, and an expiry. API1's token has `client_id: api1` and may only POST to its v1 and v2 decision paths. It cannot read or modify OPA data or policies. The sample `iam` identity may GET/PUT API1 role assignments, and GET `/health` is anonymous for readiness checks.

The API's named HTTP client creates a five-minute service token each time a client is created. Its signing key comes from `HttpClients:Opa:SigningKey`. OPA receives the same key through `OPA_SERVICE_SIGNING_KEY`, expanded by `opa/config.yaml`. The checked-in defaults are a demonstration key for local development. To use another key, set both before starting Compose and the API:

```powershell
$env:OPA_SERVICE_SIGNING_KEY = '<your HMAC key of at least 32 bytes>'
$env:HttpClients__Opa__SigningKey = $env:OPA_SERVICE_SIGNING_KEY
```

The service token is an HTTP Authorization header; end-user roles remain decision input facts. This sample keeps HTTP on localhost; token authentication does not encrypt transport. Container tests use a separate test key and verify token validation, service identity, and allowed methods/paths.

Run the Rego tests directly through Compose:

```powershell
docker compose run --rm opa test /policies --verbose --fail-on-empty
docker compose down
```

The .NET tests use Testcontainers 4.16.0 to start their own OPA container with a random host port, copy in the same policy and authorization-data files, wait for HTTP readiness, and supply its URL to `WebApplicationFactory`. The fixture owns startup and disposal; Ryuk stays enabled. Compose does not need to be running. Integration tests exercise the actual API and Rego decision together and execute the Rego tests inside that container. Mocked bridge tests separately cover malformed responses, HTTP errors and timeouts.

Docker Desktop and Linux Docker normally require no runtime overrides. For this computer's existing rootless Podman machine, use the same session configuration as `D:/testcontainers-tests`:

```powershell
$env:DOCKER_HOST = 'npipe://./pipe/podman-machine-default'
$env:TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE = '/run/user/1000/podman/podman.sock'
$env:TESTCONTAINERS_RYUK_DISABLED = 'false'
dotnet test --solution authorization-sample.slnx
```

## Tasks and roles

`TaskCatalog` owns an instance field exposed by `ITaskCatalog.GetDefinitions()` as an immutable dictionary of task definitions containing immutable grant and requirement arrays. `TaskCatalogBuilder.Create` implicitly adds `TaskRequirement(taskName)` for a base task and accepts direct extensions through `params TaskExtension[]`. Each extension copies the base requirements and grants its own task name plus the base task. There are no task references to traverse recursively. `AddTaskPolicies(definitions)` registers a named policy for each entry. Authorization options resolve the singleton `ITaskCatalog` through DI and use its instance definitions, so catalog construction can later consume configuration. `ITaskCatalog.GetTaskNames()` exports the names, and `GetAuthorizationRequirements(name)` returns the requirements.

```csharp
new TaskCatalogBuilder()
    .Create(Tasks.MakeTransfer,
        new TaskExtension(Tasks.MakeTransferHigherLimit,
            new TransferLimitRequirement(1_000, Tasks.MakeTransferHigherLimit)))
    .Build();
```

`TaskExtension` contains only a name and immutable requirements, so it cannot contain nested extensions. `Build()` returns an immutable snapshot; later builder additions cannot change it. Duplicate task names are rejected.

| Task policy | Requirements |
| --- | --- |
| api1:view-transfers | Task grant: view-transfers |
| api1:make-transfer | Task grant: make-transfer |
| api1:make-transfer-higher-limit | Task grant: make-transfer AND transfer limit: 1,000, overridden by make-transfer-higher-limit |

| Security-officer-owned role | Granted tasks |
| --- | --- |
| viewer | api1:view-transfers |
| cashier | view-transfers, make-transfer |
| supervisor | view-transfers, make-transfer-higher-limit |

`RequestPermissions` resolves each role-assigned task through its prebuilt grant array and lazily caches the effective task names in an immutable set once per request. The higher-limit task therefore grants the base task too. It does not derive grants by inspecting policy requirements. `TaskHandler` checks task membership. Unknown task names are ignored during grant resolution and have no registered policy; unknown roles grant nothing.

`POST /transfers` requires the `MakeTransfer` policy at the endpoint, then calls `AuthorizeAsync(user, transfer, Tasks.MakeTransferHigherLimit)` in its body. Both requirements in that additional policy must succeed. At or below 1,000, the limit requirement succeeds without an override task. Above 1,000, it requires its configured override task; otherwise it explicitly fails with a reason. The supervisor role assigns only the higher-limit task for transfer creation; its definition also grants the base task.

The named policy is selected from the full catalog, even when the caller does not hold its name as a grant. This lets a cashier pass the higher-limit policy for a standard amount. Requirement instances are constructed once in the definitions and reused. `ScopeNormalizationEvents` splits space-separated scopes without duplicating claims.

## Run locally

Requires .NET 10 and PowerShell 7. OPA examples and integration tests also require a Docker-compatible Linux container engine:

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

`Api1.Tests` exercises the actual API with signed JWTs: validation, issuer and audience isolation, scopes, task checks, catalog metadata, request isolation, and the higher-limit boundary.
