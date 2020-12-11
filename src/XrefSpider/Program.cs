using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace XrefSpider
{
    public class Program
    {
        private const string _help =
            "XrefSpider\n" +
            "Created by Jonathan Potts (jonathanpotts.com)\n" +
            "\n" +
            "Used to crawl API documentation sites to create xref maps to use with DocFX and other consumers.\n" +
            "\n" +
            "Usage:\n" +
            "XrefSpider [--awssdk|-a] [--unity|-u] output-file\n";

        public static async Task Main(string[] args)
        {
            if (args.Length == 0 || args.Contains("--help") || args.Contains("-help") || args.Contains("-?"))
            {
                Console.Write(_help);
                return;
            }

            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    var logger = services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();

                    bool spiderSet = false;

                    if (args.Contains("--awssdk") && args.Contains("-a"))
                    {
                        logger.LogError("For AWS SDK for .NET, you must specify --awssdk or -a but not both.");
                    }
                    else if (args.Contains("--awssdk") || args.Contains("-a"))
                    {
                        if (spiderSet)
                        {
                            logger.LogError("There are too many spiders specified.");
                            Environment.Exit(-1);
                        }

                        services.AddHttpClient<ISpider, AwsSdkForDotNetV3Spider>();
                        spiderSet = true;
                    }

                    if (args.Contains("--unity") && args.Contains("-u"))
                    {
                        logger.LogError("For Unity, you must specify --unity or -u but not both.");
                    }
                    else if (args.Contains("--unity") || args.Contains("-u"))
                    {
                        if (spiderSet)
                        {
                            logger.LogError("There are too many spiders specified.");
                            Environment.Exit(-1);
                        }

                        services.AddHttpClient<ISpider, UnitySpider>();
                        spiderSet = true;
                    }

                    try
                    {
                        var spider = services.BuildServiceProvider().GetRequiredService<ISpider>();
                    }
                    catch (Exception)
                    {
                        logger.LogError("There was no spider specified.");
                        Environment.Exit(-1);
                    }

                    var fileName = args.SingleOrDefault(x => x is not ("--awssdk" or "-a" or "--unity" or "-u"));

                    if (fileName is null)
                    {
                        logger.LogError("An output file name was not provided or the argument list is invalid.");
                        Environment.Exit(-1);
                    }

                    try
                    {
                        using var file = File.Create(fileName);
                        file.Close();
                        
                        File.Delete(fileName);
                    }
                    catch (Exception)
                    {
                        logger.LogError("The output file cannot be written to.");
                        Environment.Exit(-1);
                    }

                    Worker.FileName = fileName;

                    services.AddHostedService<Worker>();
                })
                .Build();

            await host.RunAsync();
        }
    }
}
