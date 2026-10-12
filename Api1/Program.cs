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
builder.Services.AddSingleton<ITaskCatalog, TransferTasks>();
builder.Services.AddSingleton<IRoleDefinitionStore, TransferRoles>();
builder.Services.AddScoped<RequestPermissions>();
builder.Services.AddScoped<IAuthorizationHandler, OperationHandler>();

builder.Services.AddAuthorizationBuilder().AddTransferPolicies();
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
