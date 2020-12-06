using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System;

namespace XrefSpider
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            List<SpiderType> spiders = new();

            if (args.Contains("--docfx") || args.Contains("-d"))
            {
                spiders.Add(SpiderType.DocFX);
            }

            if (args.Contains("--awssdk") || args.Contains("-a"))
            {
                spiders.Add(SpiderType.AwsSdkForDotNetV3);
            }

            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    var logger = services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();

                    if (spiders.Count < 1)
                    {
                        logger.LogError("A spider was not specified.");
                        Environment.Exit(-1);
                    }
                    else if (spiders.Count > 1)
                    {
                        logger.LogError("Too many spiders were specified.");
                        Environment.Exit(-1);
                    }

                    var spiderType = spiders.FirstOrDefault();

                    switch (spiderType)
                    {
                        case SpiderType.DocFX:
                            services.AddHttpClient<ISpider, DocFXSpider>();
                            break;

                        case SpiderType.AwsSdkForDotNetV3:
                            services.AddHttpClient<ISpider, AwsSdkForDotNetV3Spider>();
                            break;

                        default:
                            break;
                    }

                    services.AddHostedService<Worker>();
                })
                .Build();

            await host.RunAsync();
        }
    }
}
