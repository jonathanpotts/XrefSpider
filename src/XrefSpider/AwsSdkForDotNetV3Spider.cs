using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Web;
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
        public async Task<string> CrawlAsync()
        {
            var docsUri = new Uri(_docsUrl);
            var tocUrl = new Uri(docsUri, _tocPage).ToString();

            _logger.LogInformation($"Crawling {tocUrl}");

            var response = await _client.GetAsync(tocUrl);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Unable to access table of contents");
                return null;
            }

            var tocHtml = await response.Content.ReadAsStringAsync();
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

            return yaml;
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

            Xref xref = null;

            if (pageType is PageType.Namespace)
            {
                var uniqueId = pageDoc.GetElementbyId("titles").Descendants("h1").First().InnerText;

                xref = new()
                {
                    UniqueId = uniqueId,
                    Name = uniqueId,
                    Url = url,
                    CommentId = $"N:{uniqueId}",
                    NameWithType = uniqueId
                };
            }
            else if (pageType is PageType.Class or PageType.Interface or PageType.Enumeration)
            {
                var uniqueId = HttpUtility.HtmlDecode(pageDoc.DocumentNode
                    .Descendants()
                    .SkipWhile(x => x.Id != "inheritancehierarchy")
                    .First(x => x.Name == "div")
                    .Descendants()
                    .Reverse()
                    .SkipWhile(x => x.Name != "br")
                    .First(x => x.Name == "#text")
                    .InnerText)
                    .Replace(" ", "");

                var name = HttpUtility.HtmlDecode(pageDoc.GetElementbyId("titles").Descendants("h1").First().InnerText);

                xref = new()
                {
                    UniqueId = uniqueId,
                    Name = name,
                    Url = url,
                    CommentId = $"T:{uniqueId}",
                    FullName = uniqueId,
                    NameWithType = name
                };
            }
            else if (pageType is PageType.Constructor or PageType.Method)
            {                
                var @namespace = HttpUtility.HtmlDecode(
                    pageDoc.GetElementbyId("namespaceblock").Descendants().SkipWhile(x => x.Name != "strong").ElementAt(2).InnerText
                    );
                var methodName = HttpUtility.HtmlDecode(
                    pageDoc.GetElementbyId("titles").Descendants("h1").First().InnerText.Replace(" ", "")
                    );

                if (methodName.Contains('('))
                {
                    methodName = methodName.Substring(0, methodName.IndexOf('('));
                }

                var parameterTypes = pageDoc.DocumentNode
                    .Descendants()
                    .SkipWhile(x => x.Id != "parameters")
                    .FirstOrDefault()?
                    .ParentNode
                    .Descendants("dl")
                    .Select(x => HttpUtility.HtmlDecode(x.Descendants("dd").First().InnerText).Split('\n')[1]["Type: ".Length..])
                    ?? Enumerable.Empty<string>();

                var parameterList = string.Join(", ", parameterTypes);

                var fullName = $"{@namespace}.{methodName}({parameterList})";

                var uniqueId = (pageType is PageType.Constructor) ? 
                    $"{@namespace}.{methodName}.#ctor({parameterList})" :
                    fullName;

                uniqueId = uniqueId.Replace("()", "");

                parameterList = string.Join(", ", parameterTypes.Select(x => x.Split('.').Last()));

                var nameWithType = $"{methodName}({parameterList})";

                var name = nameWithType.Split('.').Last();

                xref = new()
                {
                    UniqueId = uniqueId,
                    Name = name,
                    Url = url,
                    CommentId = $"M:{uniqueId}",
                    FullName = fullName,
                    NameWithType = nameWithType
                };
            }

            if (xref is null)
            {
                return;
            }

            _xrefs.Add(xref);

            if (pageType is PageType.Class or PageType.Interface)
            {
                var pageUri = new Uri(url);

                var constructorUrls = pageDoc.DocumentNode
                    .Descendants()
                    .SkipWhile(x => x.Id != "constructors")
                    .FirstOrDefault(x => x.Name == "div")?
                    .Descendants("tr")
                    .Select(x => x.Descendants("td").Skip(1).FirstOrDefault()?.Descendants("a").FirstOrDefault()?.GetAttributeValue("href", null))
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Select(x => new Uri(pageUri, x).ToString());

                foreach (var constructorUrl in constructorUrls ?? Enumerable.Empty<string>())
                {
                    await CrawlPageAsync(constructorUrl);
                }

                var methodUrls = pageDoc.DocumentNode
                    .Descendants()
                    .SkipWhile(x => x.Id != "methods")
                    .FirstOrDefault(x => x.Name == "div")?
                    .Descendants("tr")
                    .Select(x => x.Descendants("td").Skip(1).FirstOrDefault()?.Descendants("a").FirstOrDefault()?.GetAttributeValue("href", null))
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Select(x => new Uri(pageUri, x).ToString());

                foreach (var methodUrl in methodUrls ?? Enumerable.Empty<string>())
                {
                    await CrawlPageAsync(methodUrl);
                }
            }
        }
    }
}
