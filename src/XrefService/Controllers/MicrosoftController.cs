using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;

namespace XrefService.Controllers
{
    /// <summary>
    /// Provides xref (cross-reference) metadata for Microsoft products such as .NET and Azure.
    /// </summary>
    [Route("xref/microsoft")]
    [ApiController]
    [SwaggerTag("Microsoft - .NET, Azure, etc.")]
    public class MicrosoftController : ControllerBase
    {
        /// <summary>
        /// Microsoft xref service URL.
        /// </summary>
        private const string _xrefUrl = "https://xref.docs.microsoft.com/query";

        /// <summary>
        /// Queries the xref service for metadata for the specified unique identifier.
        /// </summary>
        /// <param name="uid">Unique identifier.</param>
        /// <returns>Collection of found xref metadata.</returns>
        [HttpGet("[action]")]
        public IActionResult Query([FromQuery, Required] string uid)
        {
            return RedirectPermanent($"{_xrefUrl}?uid={uid}");
        }
    }
}
