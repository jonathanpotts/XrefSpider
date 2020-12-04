using Microsoft.AspNetCore.Mvc;

namespace XrefService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class XrefController : ControllerBase
    {
        [HttpGet("[action]")]
        public IActionResult Query([FromQuery] string uid)
        {
            return Ok();
        }
    }
}
