using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Azure.Data.Tables;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddSingleton(new TableServiceClient(
            Environment.GetEnvironmentVariable("AzureWebJobsStorage")));
    })
    .Build();

host.Run();
