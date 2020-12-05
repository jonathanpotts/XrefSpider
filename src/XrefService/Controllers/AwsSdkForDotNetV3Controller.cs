using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using XrefService.Models;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;

namespace XrefService.Controllers
{
    /// <summary>
    /// Provides xref (cross-reference) metadata for the AWS SDK for .NET V3.
    /// </summary>
    [Route("xref/aws/dotnet/v3")]
    [ApiController]
    [SwaggerTag("AWS SDK for .NET V3")]
    public class AwsSdkForDotNetV3Controller : ControllerBase
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
        /// HTTP client factory.
        /// </summary>
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// Creates an instance of the controller.
        /// </summary>
        /// <param name="httpClientFactory">HTTP client factory.</param>
        public AwsSdkForDotNetV3Controller(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Queries the xref service for metadata for the specified unique identifier.
        /// </summary>
        /// <param name="uid">Unique identifier.</param>
        /// <returns>Collection of found xref metadata.</returns>
        [HttpGet("[action]")]
        public async Task<IActionResult> QueryAsync([FromQuery, Required] string uid)
        {
            List<Xref> xrefs = new();
            Xref xref = new();

            if (!await GetXrefMetadataAsync(uid, xref))
            {
                return Ok(xrefs);
            }

            xrefs.Add(xref);
            return Ok(xrefs);
        }

        /// <summary>
        /// Gets the xref namespace or type metadata for the specified unique identifier.
        /// </summary>
        /// <param name="uid">Unique identifier.</param>
        /// <param name="xref">Xref metadata.</param>
        /// <returns>True if the metadata was successfully populated; false if the metadata is invalid.</returns>
        private async Task<bool> GetXrefNamespaceOrTypeAsync(string uid, Xref xref)
        {
            if (xref == null)
            {
                return false;
            }

            using var client = _httpClientFactory.CreateClient();

            var response = await client.GetAsync(new Uri(new(_docsUrl), _tocPage));

            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(await response.Content.ReadAsStringAsync());

            var tocNodes = htmlDoc.DocumentNode.Descendants("ul").FirstOrDefault(d => d.HasClass("awstoc"))?.Descendants("li");

            if (tocNodes == null)
            {
                return false;
            }

            var nameParts = uid.Split('.').ToList();

            HtmlNode matchedNode = null;

            while (nameParts.Count > 0)
            {
                var id = string.Join('_', nameParts);

                matchedNode = tocNodes.FirstOrDefault(n => n.Id == id);

                if (matchedNode != null)
                {
                    xref.UniqueId = string.Join('.', nameParts);
                    xref.FullName = xref.UniqueId;
                    break;
                }

                nameParts.RemoveAt(nameParts.Count - 1);
            }

            if (matchedNode == null)
            {
                return false;
            }

            var matchedUrl = matchedNode.Descendants("a").FirstOrDefault()?.GetAttributeValue("href", null);

            if (matchedUrl == null)
            {
                return false;
            }

            xref.HypertextReference = new Uri(new(_docsUrl), matchedUrl).ToString();

            return true;
        }

        /// <summary>
        /// Gets the xref metadata for the specified unique identifier.
        /// </summary>
        /// <param name="uid">Unique identifier.</param>
        /// <param name="xref">Xref metadata.</param>
        /// <returns>True if the metadata was successfully populated; false if the metadata is invalid.</returns>
        private async Task<bool> GetXrefMetadataAsync(string uid, Xref xref)
        {
            if (xref == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(xref.HypertextReference) && !await GetXrefNamespaceOrTypeAsync(uid, xref))
            {
                return false;
            }

            using var client = _httpClientFactory.CreateClient();

            var response = await client.GetAsync(xref.HypertextReference);

            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(await response.Content.ReadAsStringAsync());

            var titlesNode = htmlDoc.DocumentNode.Descendants("div").FirstOrDefault(d => d.Id == "titles");

            if (titlesNode == null)
            {
                return false;
            }

            var name = titlesNode.Descendants("h1").FirstOrDefault()?.InnerText;

            if (name == null)
            {
                return false;
            }

            xref.Name = name;

            var memberType = titlesNode.Descendants("h2").FirstOrDefault()?.InnerText;

            if (memberType == null)
            {
                return false;
            }

            xref.SchemaType = memberType switch
            {
                "Namespace" => "NetNamespace",
                "Class" or "Interface" => "NetType",
                _ => null
            };

            xref.CommentId = memberType switch
            {
                "Namespace" => $"N:{xref.FullName}",
                "Class" or "Interface" => $"T:{xref.FullName}",
                _ => null
            };

            if (memberType != "Namespace")
            {
                xref.NameWithType = xref.Name;

                var summary = htmlDoc.DocumentNode.Descendants("div")
                    .FirstOrDefault(d => d.Id == "summaryblock")?
                    .Descendants("p").FirstOrDefault()?
                    .InnerText;

                summary = summary.Trim();

                xref.SummaryHtml = $"<p>{summary}</p>\n";
            }

            if (uid == xref.UniqueId)
            {
                return true;
            }

            return true;
        }
    }
}
