using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using XrefSpider.Models;
using YamlDotNet.Serialization;

namespace XrefSpider
{
    /// <summary>
    /// Spider that crawls the AWS SDK for .NET V3 documentation.
    /// </summary>
    public class AwsSdkForDotNetV3Spider : ISpider, IDisposable
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
        private readonly HttpClient _client = new();

        /// <summary>
        /// Crawled URLs.
        /// </summary>
        private readonly HashSet<string> _crawledUrls = new();

        /// <summary>
        /// Xrefs.
        /// </summary>
        private readonly List<Xref> _xrefs = new();

        /// <summary>
        /// Disposed value.
        /// </summary>
        private bool _disposedValue;

        /// <summary>
        /// Crawls the documentation and creates the xref map.
        /// </summary>
        /// <returns>Task that crawls the documentation.</returns>
        public async Task CrawlAsync()
        {
            var docsUri = new Uri(_docsUrl);
            var tocUrl = new Uri(docsUri, _tocPage).ToString();

            Console.WriteLine($"Crawling {tocUrl}...");

            var tocHtml = await _client.GetStringAsync(tocUrl);
            var tocDoc = new HtmlDocument();
            tocDoc.LoadHtml(tocHtml);
            var tocUrls = tocDoc.GetElementbyId("toc")
                .Descendants("li")
                .SelectMany(x => x.Descendants("a"))
                .Select(x => x.GetAttributeValue("href", null))
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(x => new Uri(docsUri, x).ToString());

            HashSet<string> crawledUrls = new();
            List<Xref> xrefs = new();

            foreach (var url in tocUrls)
            {
                await CrawlPageAsync(url);
            }

            var ser = new SerializerBuilder().Build();
            var yaml = ser.Serialize(xrefs);

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

            Console.WriteLine($"Crawling {url}...");

            var pageHtml = await _client.GetStringAsync(url);
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

        /// <summary>
        /// Disposes resources used by this object.
        /// </summary>
        /// <param name="disposing">Boolean value determining if the object is being disposed.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _client.Dispose();
                }

                _disposedValue = true;
            }
        }

        /// <summary>
        /// Disposes resources used by this object.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
