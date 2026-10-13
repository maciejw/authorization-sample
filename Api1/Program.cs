using Api1;
using Api1.Opa;
using Api1.Transfers;

using Microsoft.AspNetCore.Authorization;

using OperationAuthorization;
using OperationAuthorization.AspNetCore;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddAuthentication().AddJwtBearer(AuthenticationSchemes.Users, options =>
{
    options.TokenValidationParameters.ValidAudience = "api1";
    options.MapInboundClaims = false;
    options.TokenValidationParameters.RoleClaimType = "role";
    options.EventsType = typeof(ScopeNormalizationEvents);
}).AddJwtBearer(AuthenticationSchemes.Services, options =>
{
    options.TokenValidationParameters.ValidAudience = "api1";
    options.MapInboundClaims = false;
    options.EventsType = typeof(ScopeNormalizationEvents);
});

builder.Services.AddScoped<ScopeNormalizationEvents>();
builder.Services.AddSingleton<ITaskCatalog, TaskCatalog>();
builder.Services.AddSingleton<IRoleDefinitionStore, RoleDefinitions>();
builder.Services.AddScoped<RequestPermissions>();
builder.Services.AddScoped<IAuthorizationHandler, TaskHandler>();
builder.Services.AddScoped<IAuthorizationHandler, TransferLimitHandler>();
builder.Services.AddScoped<IAuthorizationHandler, OpaAuthorizationHandler>();
builder.Services.AddHttpClient("opa", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["HttpClients:Opa:BaseAddress"]!);
    client.Timeout = TimeSpan.FromSeconds(3);
    client.DefaultRequestHeaders.Authorization = new("Bearer",
        OpaServiceToken.Create(builder.Configuration["HttpClients:Opa:SigningKey"]!));
}).RedactLoggedHeaders(_ => false);
builder.Services.AddAuthorization();
builder.Services.AddOptions<AuthorizationOptions>().Configure<ITaskCatalog>(Policies.AddTransferPolicies);
builder.Services.AddOperationAuthorizationInfrastructure();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapTransfersApi();
app.MapTransferReconciliationApi();
app.MapOperationAuthorizationInfrastructure(service: "api1", version: 1);

app.Run();
