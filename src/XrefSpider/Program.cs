using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XrefSpider;

await Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        services.AddHttpClient<ISpider, AwsSdkForDotNetV3Spider>();
        services.AddHostedService<Worker>();
    })
    .RunConsoleAsync();
