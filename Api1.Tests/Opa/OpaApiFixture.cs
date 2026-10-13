using Api1.Tests.Transfers;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Api1.Tests.Opa;

public sealed class OpaApiFixture : IAsyncLifetime
{
    public const string SigningKey = "test-only-opa-signing-key-at-least-64-bytes-for-algorithm-tests-12345";

    private readonly ApiFactory factory = new();
    public WebApplicationFactory<Program> Api { get; private set; } = null!;

    public IContainer Opa { get; } = CreateContainer();

    private static IContainer CreateContainer()
    {
        var builder = new ContainerBuilder("openpolicyagent/opa:1.21.2")
            .WithEnvironment("OPA_SERVICE_SIGNING_KEY", SigningKey)
            .WithCommand("run", "--server", "--addr=0.0.0.0:8181",
                "--authentication=token", "--authorization=basic", "--config-file=/policies/config.yaml",
                "/policies/system.authz.rego",
                "/policies/api1.make_transfer_higher_limit_v1.rego", "/policies/api1.make_transfer_higher_limit_v2.rego", "/policies/authorization.json")
            .WithPortBinding(8181, true)
            .WithLabel("tests.assembly", "Api1.Tests")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(8181).ForPath("/health")));
        foreach (var file in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "opa")))
            builder = builder.WithResourceMapping(File.ReadAllBytes(file), "/policies/" + Path.GetFileName(file));
        return builder.Build();
    }

    public async ValueTask InitializeAsync()
    {
        try
        {
            await Opa.StartAsync(TestContext.Current.CancellationToken);
            var address = $"http://{Opa.Hostname}:{Opa.GetMappedPublicPort(8181)}/";
            Api = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["HttpClients:Opa:BaseAddress"] = address, ["HttpClients:Opa:SigningKey"] = SigningKey })));
        }
        catch
        {
            await Opa.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try { await factory.DisposeAsync(); }
        finally { await Opa.DisposeAsync(); }
    }
}
