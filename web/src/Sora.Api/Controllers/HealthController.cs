using Microsoft.AspNetCore.Mvc;

namespace Sora.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get() => Ok(new
    {
        status = "ok",
        application = "Sora.Api",
        utc = DateTimeOffset.UtcNow
    });
}
