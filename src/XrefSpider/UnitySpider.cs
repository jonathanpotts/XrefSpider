using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using XrefSpider.Models;
using YamlDotNet.Serialization;
using System;
using System.Globalization;

namespace XrefSpider
{
    /// <summary>
    /// Spider that crawls the Unity scripting reference.
    /// </summary>
    public class UnitySpider : ISpider
    {
        /// <summary>
        /// Sitemap URL.
        /// </summary>
        private const string _sitemapUrl = "https://docs.unity3d.com/sitemap.xml";

        /// <summary>
        /// Scripting reference URL.
        /// </summary>
        private const string _scriptingReferenceUrl = "https://docs.unity3d.com/ScriptReference/";

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
        /// Creates an instance of the Unity spider.
        /// </summary>
        /// <param name="client">HTTP client.</param>
        /// <param name="logger">Logger.</param>
        public UnitySpider(HttpClient client, ILogger<UnitySpider> logger)
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
            _logger.LogInformation($"Crawling {_sitemapUrl}");

            var response = await _client.GetAsync(_sitemapUrl);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Unable to access sitemap");
                return null;
            }

            var sitemapXml = await response.Content.ReadAsStringAsync();
            var sitemapDoc = new XmlDocument();
            sitemapDoc.LoadXml(sitemapXml);
            var sitemapUrls = sitemapDoc
                .GetElementsByTagName("loc")
                .OfType<XmlNode>()
                .Select(x => x.InnerText)
                .Where(x => x.StartsWith(_scriptingReferenceUrl))
                .Where(x => x != $"{_scriptingReferenceUrl}index.html");

            foreach (var url in sitemapUrls)
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
        /// Enumeration of Unity script reference page types.
        /// </summary>
        private enum PageType
        {
            Class,
            Interface,
            Enumeration,
            Struct,
            Property,
            Constructor,
            Method,
            Message,
            Operator
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
                _logger.LogWarning($"Unabled to access {url}");
                return;
            }

            var pageHtml = await response.Content.ReadAsStringAsync();
            var pageDoc = new HtmlDocument();
            pageDoc.LoadHtml(pageHtml);
            var contentBlock = pageDoc.DocumentNode.Descendants("div").First(x => x.HasClass("content-block"));

            var signature = contentBlock.Descendants("div")
                .First(x => x.HasClass("signature"))
                .InnerText
                .Trim();

            var hasSignature = !string.IsNullOrWhiteSpace(signature);

            var heading = contentBlock.Descendants().SkipWhile(x => x.Name != "h1").First().InnerText.Trim();

            PageType pageType;

            if (!hasSignature)
            {
                var typeString = contentBlock.Descendants().SkipWhile(x => x.Name != "h1").First(x => x.Name == "p").InnerText.Trim().Split(' ')[0];

                if (!Enum.TryParse(typeString, true, out pageType))
                {
                    if (!heading.Contains('('))
                    {
                        return;
                    }

                    pageType = PageType.Message;
                }
            }
            else
            {
                if (url.Contains("-ctor"))
                {
                    pageType = PageType.Constructor;
                }
                else if (url.Contains("-operator"))
                {
                    pageType = PageType.Operator;
                }
                else if (signature.Contains('('))
                {
                    pageType = PageType.Method;
                }
                else
                {
                    pageType = PageType.Property;
                }
            }

            Xref xref = null;

            if (pageType is PageType.Enumeration)
            {
                // The Unity docs does not provide enough information to create an xref mapping for an enumeration
                return;
            }
            else if (pageType is PageType.Class or PageType.Interface or PageType.Struct)
            {
                var name = heading;
                var subheading = contentBlock.Descendants().SkipWhile(x => x.Name != "h1").First(x => x.Name == "p").InnerText.Trim();
                var @namespace = subheading.Split(' ').Where(x => !string.IsNullOrWhiteSpace(x)).ElementAt(2);
                var fullName = $"{@namespace}.{name}";

                xref = new()
                {
                    UniqueId = fullName,
                    Name = name,
                    Url = url,
                    CommentId = $"T:{fullName}",
                    FullName = fullName,
                    NameWithType = name
                };
            }
            else if (pageType is PageType.Property or PageType.Method or PageType.Message)
            {
                var typeUrl = new Uri(
                    new(url),
                    contentBlock.Descendants().SkipWhile(x => x.Name != "h1").First().Descendants("a").First().GetAttributeValue("href", null)
                    ).ToString();

                if (typeUrl is null)
                {
                    return;
                }

                if (!_crawledUrls.Contains(typeUrl))
                {
                    await CrawlPageAsync(typeUrl);
                }

                var typeXref = _xrefs.First(x => x.Url == typeUrl);

                var name = heading.Split('.').Last();
                name = name.Substring(0, name.IndexOf('('));
                var nameWithType = heading;
                var fullName = $"{typeXref.FullName}.{name}";

                if (pageType is PageType.Property)
                {
                    xref = new()
                    {
                        UniqueId = fullName,
                        Name = name,
                        Url = url,
                        CommentId = $"P:{fullName}",
                        FullName = fullName,
                        NameWithType = nameWithType
                    };
                }
                else if (pageType is PageType.Method or PageType.Message)
                {
                    xref = new()
                    {
                        UniqueId = $"{fullName}*",
                        Name = name,
                        Url = url,
                        CommentId = $"Overload:{fullName}",
                        IsSpec = true,
                        FullName = fullName,
                        NameWithType = nameWithType
                    };
                }
            }
            else if (pageType is PageType.Constructor)
            {
                var typeUrl = url.Replace("-ctor", "");

                if (typeUrl is null)
                {
                    return;
                }

                if (!_crawledUrls.Contains(typeUrl))
                {
                    await CrawlPageAsync(typeUrl);
                }

                var typeXref = _xrefs.First(x => x.Url == typeUrl);

                xref = new()
                {
                    UniqueId = $"{typeXref.FullName}.#ctor*",
                    Name = typeXref.Name,
                    Url = url,
                    CommentId = $"Overload:{typeXref.FullName}.#ctor",
                    IsSpec = true,
                    FullName = $"{typeXref.FullName}.{typeXref.Name}",
                    NameWithType = $"{typeXref.Name}.{typeXref.Name}"
                };
            }
            else if (pageType is PageType.Operator)
            {
                var unary = signature.Count(x => x is ',') == 1;

                string operatorName = null;

                if (heading.Contains("++"))
                {
                    operatorName = "Increment";
                }
                else if (heading.Contains("--"))
                {
                    operatorName = "Decrement";
                }
                else if (heading.Contains("+") && unary)
                {
                    operatorName = "UnaryPlus";
                }
                else if (heading.Contains("-") && unary)
                {
                    operatorName = "UnaryNegation";
                }
                else if (heading.Contains("*"))
                {
                    operatorName = "Multiply";
                }
                else if (heading.Contains("/"))
                {
                    operatorName = "Division";
                }
                else if (heading.Contains("%"))
                {
                    operatorName = "Modulus";
                }
                else if (heading.Contains("+"))
                {
                    operatorName = "Addition";
                }
                else if (heading.Contains("-"))
                {
                    operatorName = "Subtraction";
                }

                /* TODO:
                 * Example:
                 * - uid: SharpApi.Email.NullEmailSender.op_Inequality*
                 *    name: Inequality
                 *    href: api/SharpApi.Email.NullEmailSender.html#SharpApi_Email_NullEmailSender_op_Inequality_
                 *    commentId: Overload:SharpApi.Email.NullEmailSender.op_Inequality
                 *    isSpec: "True"
                 *    fullName: SharpApi.Email.NullEmailSender.Inequality
                 *    nameWithType: NullEmailSender.Inequality
                 * - uid: SharpApi.Email.NullEmailSender.op_Implicit*
                 *    name: Implicit
                 *    href: api/SharpApi.Email.NullEmailSender.html#SharpApi_Email_NullEmailSender_op_Implicit_
                 *    commentId: Overload:SharpApi.Email.NullEmailSender.op_Implicit
                 *    isSpec: "True"
                 *    fullName: SharpApi.Email.NullEmailSender.Implicit
                 *    nameWithType: NullEmailSender.Implicit
                 */
            }

            if (xref is null)
            {
                return;
            }

            _xrefs.Add(xref);
        }
    }
}
