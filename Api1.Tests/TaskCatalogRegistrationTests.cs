using Api1.Tests.Transfers;
using Api1.Transfers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Moq;

using OperationAuthorization;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Api1.Tests;

public sealed class TaskCatalogRegistrationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Policies_use_definitions_from_the_registered_catalog_instance()
    {
        var definitions = new TaskCatalogBuilder()
            .Create(TaskCatalog.Tasks.MakeTransfer, new TaskExtension(TaskCatalog.Tasks.MakeTransferHigherLimit,
                new TransferLimitRequirement(500, TaskCatalog.Tasks.MakeTransferHigherLimit)))
            .Build();
        var catalog = new Mock<ITaskCatalog>(MockBehavior.Strict);
        catalog.Setup(tasks => tasks.GetDefinitions()).Returns(definitions);
        catalog.Setup(tasks => tasks.GetGrantedTasks(TaskCatalog.Tasks.MakeTransfer)).Returns([TaskCatalog.Tasks.MakeTransfer]);
        catalog.Setup(tasks => tasks.GetGrantedTasks(TaskCatalog.Tasks.ViewTransfers)).Returns([TaskCatalog.Tasks.ViewTransfers]);
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ITaskCatalog>();
            services.AddSingleton(catalog.Object);
        }));
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.Token("cashier"));

        using var standard = await client.PostAsJsonAsync("/transfers", new { amount = 500 }, TestContext.Current.CancellationToken);
        using var above = await client.PostAsJsonAsync("/transfers", new { amount = 501 }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, standard.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, above.StatusCode);
        catalog.Verify(tasks => tasks.GetDefinitions(), Times.Once);
    }
}
