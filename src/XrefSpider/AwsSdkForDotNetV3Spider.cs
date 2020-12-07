using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using XrefSpider.Models;
using YamlDotNet.Serialization;
using Microsoft.Extensions.Logging;

namespace XrefSpider
{
    /// <summary>
    /// Spider that crawls the AWS SDK for .NET V3 documentation.
    /// </summary>
    public class AwsSdkForDotNetV3Spider : ISpider
    {
        /// <summary>
        /// AWS SDK for .NET V3 API documentation URL.
        /// </summary>
        private const string _docsUrl = "https://docs.aws.amazon.com/sdkfornet/v3/apidocs/";

        /// <summary>
        /// AWS SDK for .NET V3 table of contents page name.
        /// </summary>
        private const string _tocPage = "TOC.html";

        /// <summary>
        /// HTTP client.
        /// </summary>
        private readonly HttpClient _client;

        /// <summary>
        /// Logger.
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// Crawled URLs.
        /// </summary>
        private readonly HashSet<string> _crawledUrls = new();

        /// <summary>
        /// Xrefs.
        /// </summary>
        private readonly List<Xref> _xrefs = new();

        /// <summary>
        /// Creates an instance of the AWS SDK for .NET V3 spider.
        /// </summary>
        /// <param name="client">HTTP client.</param>
        /// <param name="logger">Logger.</param>
        public AwsSdkForDotNetV3Spider(HttpClient client, ILogger<AwsSdkForDotNetV3Spider> logger)
        {
            _client = client;
            _logger = logger;
        }

        /// <summary>
        /// Crawls the documentation and creates the xref map.
        /// </summary>
        /// <returns>Task that crawls the documentation.</returns>
        public async Task CrawlAsync()
        {
            var docsUri = new Uri(_docsUrl);
            var tocUrl = new Uri(docsUri, _tocPage).ToString();

            _logger.LogInformation($"Crawling {tocUrl}");

            var tocHtml = await _client.GetStringAsync(tocUrl);
            var tocDoc = new HtmlDocument();
            tocDoc.LoadHtml(tocHtml);
            var tocUrls = tocDoc.GetElementbyId("toc")
                .Descendants("li")
                .SelectMany(x => x.Descendants("a"))
                .Select(x => x.GetAttributeValue("href", null))
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(x => new Uri(docsUri, x).ToString());

            foreach (var url in tocUrls)
            {
                await CrawlPageAsync(url);
            }

            var ser = new SerializerBuilder()
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                .Build();
            var yaml = ser.Serialize(_xrefs);

            Console.WriteLine();
            Console.Write(yaml);
        }
        
        /// <summary>
        /// Enumeration of AWS SDK for .NET V3 documentation page types.
        /// </summary>
        private enum PageType
        {
            Namespace,
            Class,
            Interface,
            Enumeration,
            Constructor,
            Method
        }

        /// <summary>
        /// Crawls the documentation page and adds metadata to the xref map.
        /// </summary>
        /// <returns>Task that crawls the documentation page.</returns>
        private async Task CrawlPageAsync(string url)
        {
            if (_crawledUrls.Contains(url))
            {
                return;
            }

            _crawledUrls.Add(url);

            _logger.LogInformation($"Crawling {url}");

            var response = await _client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
                {
                    _logger.LogWarning($"Unabled to access {url}");
                    return;
                }
                
            }

            var pageHtml = await response.Content.ReadAsStringAsync();
            var pageDoc = new HtmlDocument();
            pageDoc.LoadHtml(pageHtml);
            
            if (!Enum.TryParse<PageType>(pageDoc.GetElementbyId("titles").Descendants("h2").First().InnerText, out var pageType))
            {
                return;
            }

            var name = pageDoc.GetElementbyId("titles").Descendants("h1").First().InnerText;
            string fullName = null;

            if (pageType is PageType.Class or PageType.Interface or PageType.Enumeration)
            {
                fullName = pageDoc.DocumentNode
                    .Descendants()
                    .SkipWhile(x => x.Id != "inheritancehierarchy")
                    .First(x => x.Name == "div")
                    .Descendants()
                    .Reverse()
                    .SkipWhile(x => x.Name != "br")
                    .First(x => x.Name == "#text")
                    .InnerText
                    .Replace("\n", "")
                    .Replace("&nbsp;", "");
            }

            var uniqueId = pageType switch
            {
                PageType.Namespace => name,
                PageType.Class or PageType.Interface or PageType.Enumeration or PageType.Method => fullName,
                _ => null
            };

            var commentId = pageType switch
            {
                PageType.Namespace => $"N:{uniqueId}",
                PageType.Class or PageType.Interface or PageType.Enumeration => $"T:{uniqueId}",
                PageType.Method or PageType.Constructor => $"M:{uniqueId}",
                _ => null
            };

            var nameWithType = pageType switch
            {
                PageType.Namespace or PageType.Class or PageType.Interface or PageType.Enumeration => name,
                _ => null
            };

            Xref xref = new()
            {
                UniqueId = uniqueId,
                Name = name,
                Url = url,
                CommentId = commentId,
                FullName = fullName,
                NameWithType = nameWithType
            };

            _xrefs.Add(xref);
        }
    }
}
