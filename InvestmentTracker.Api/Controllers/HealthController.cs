using Microsoft.AspNetCore.Mvc;

namespace InvestmentTracker.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "ok",
            application = "InvestmentTracker.Api"
        });
    }
}