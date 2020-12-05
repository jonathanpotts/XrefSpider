using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using XrefService.Models;

namespace XrefService.Controllers
{
    [Route("api/xref/[controller]")]
    [ApiController]
    public class AwsController : ControllerBase
    {
        private static readonly Uri _docsUri = new Uri("https://docs.aws.amazon.com/sdkfornet/v3/apidocs/");
        private const string _tocPage = "TOC.html";
        private readonly IHttpClientFactory _httpClientFactory;

        public AwsController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> QueryAsync([FromQuery] string uid)
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

        private async Task<bool> GetXrefNamespaceOrTypeAsync(string uid, Xref xref)
        {
            using var client = _httpClientFactory.CreateClient();

            var response = await client.GetAsync(new Uri(_docsUri, _tocPage));

            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(await response.Content.ReadAsStringAsync());

            var htmlNodes = htmlDoc.DocumentNode.Descendants("ul").FirstOrDefault(n => n.HasClass("awstoc"))?.Descendants("li");

            if (htmlNodes == null)
            {
                return false;
            }

            var nameParts = uid.Split('.').ToList();

            HtmlNode matchedNode = null;

            while (nameParts.Count > 0)
            {
                var id = string.Join('_', nameParts);

                matchedNode = htmlNodes.FirstOrDefault(n => n.Id == id);

                if (matchedNode != null)
                {
                    xref.UniqueId = string.Join('.', nameParts);
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

            xref.HypertextReference = new Uri(_docsUri, matchedUrl).ToString();

            return true;
        }

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

            if (uid == xref.UniqueId)
            {

            }

            return true;
        }
    }
}
