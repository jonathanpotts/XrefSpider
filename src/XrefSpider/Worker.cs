using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace XrefSpider
{
    /// <summary>
    /// XrefSpider worker that runs on the .NET generic host.
    /// </summary>
    public class Worker : IHostedService
    {
        /// <summary>
        /// Application lifetime.
        /// </summary>
        private readonly IHostApplicationLifetime _appLifetime;

        /// <summary>
        /// Spider.
        /// </summary>
        private readonly ISpider _spider;

        /// <summary>
        /// Creates an instance of the XrefSpider worker.
        /// </summary>
        /// <param name="appLifetime">Application lifetime.</param>
        /// <param name="spider">Spider.</param>
        public Worker(IHostApplicationLifetime appLifetime, ISpider spider)
        {
            _appLifetime = appLifetime;
            _spider = spider;
        }

        /// <summary>
        /// Triggered when the application host is starting.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task to perform while the application host is starting.</returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _appLifetime.ApplicationStarted.Register(() => Task.Run(OnStartedAsync));

            return Task.CompletedTask;
        }

        /// <summary>
        /// Triggered when the application host has started.
        /// </summary>
        /// <returns>Task to perform after the application host has started.</returns>
        public async Task OnStartedAsync()
        {
            await _spider.CrawlAsync();

            _appLifetime.StopApplication();
        }

        /// <summary>
        /// Triggered when the application host is shutting down.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task to perform while the application host is shutting down.</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
