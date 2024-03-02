using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()

    .ConfigureServices(services =>
    {
        //services.ConfigureFunctionsApplicationInsights();
        //services.AddHttpClient();
        //services.AddSingleton<SqliteConnection>(new SqliteConnection("Data Source=events.db"));
    }).Build();

host.Run();
