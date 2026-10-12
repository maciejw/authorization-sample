#requires -Version 7.0

param(
    [string] $ValidFor = '365d'
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'Api1/Api1.csproj'

# Keys and generated tokens are stored only in this developer's user secrets.
$samples = @(
    @{ Variable = 'viewerToken'; Name = 'alice'; Role = 'viewer'; Scope = 'transfers' },
    @{ Variable = 'cashierToken'; Name = 'alice'; Role = 'cashier'; Scope = 'transfers' },
    @{ Variable = 'supervisorToken'; Name = 'alice'; Role = 'supervisor'; Scope = 'openid transfers' },
    @{ Variable = 'noScopeToken'; Name = 'alice'; Role = 'supervisor'; Scope = 'openid' },
    @{ Variable = 'serviceToken'; Name = 'not-available'; Scope = 'transfers.reconcile'; ClientId = 'reconciliation-worker'; Scheme = 'ServiceBearer'; Audience = 'api1' },
    @{ Variable = 'iamToken'; Name = 'not-available'; Scope = 'authorization.tasks.read'; ClientId = 'iam-exporter'; Scheme = 'InfrastructureBearer'; Audience = 'authorization-catalog' },
    @{ Variable = 'iamNoScopeToken'; Name = 'not-available'; Scope = 'openid'; ClientId = 'iam-exporter'; Scheme = 'InfrastructureBearer'; Audience = 'authorization-catalog' }
)

foreach ($sample in $samples) {
    $audience = if ($sample.Audience) { $sample.Audience } else { 'api1' }
    $scheme = if ($sample.Scheme) { $sample.Scheme } else { 'Bearer' }
    $arguments = @('user-jwts', 'create', '--project', $project,
        '--appsettings-file', 'appsettings.Development.json', '--scheme', $scheme,
        '--audience', $audience, '--name', $sample.Name, '--scope', $sample.Scope,
        '--valid-for', $ValidFor, '--output', 'token')
    if ($sample.Role) {
        $arguments += @('--claim', "role=$($sample.Role)")
    }
    else {
        $arguments += @('--issuer', 'sample-services', '--claim', "client_id=$($sample.ClientId)")
    }

    $token = & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "Failed to generate $($sample.Variable)." }

    & dotnet user-secrets set "HttpSamples:$($sample.Variable)" "$token" --project $project
    if ($LASTEXITCODE -ne 0) { throw "Failed to store $($sample.Variable)." }
}

# user-jwts also writes URL audiences; each scheme's audience is fixed in code.
$configPath = Join-Path $PSScriptRoot 'Api1/appsettings.Development.json'
$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json -AsHashtable
foreach ($scheme in @('Bearer', 'ServiceBearer', 'InfrastructureBearer')) {
    $config.Authentication.Schemes[$scheme].Remove('ValidAudiences') | Out-Null
    $config.Authentication.Schemes[$scheme].Authority = ''
}
$config | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $configPath

Write-Host 'Tokens saved. Run Api1, select dev in Api1.http, and send a request.'
