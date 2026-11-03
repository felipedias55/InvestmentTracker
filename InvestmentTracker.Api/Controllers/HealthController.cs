using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using InvestmentTracker.Api.Health;

namespace InvestmentTracker.Api.Controllers
{

    [ApiController]
    [Route("api/health")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public class HealthController(HealthCheckService healthChecks) : ControllerBase
    {
        [HttpGet]
        [HttpGet("live")]
        public IActionResult Get()
        {
            return Ok(new
            {
                status = "ok",
                application = "InvestmentTracker.Api"
            });
        }

        [HttpGet("ready")]
        public async Task<IActionResult> Ready(CancellationToken cancellationToken)
        {
            var report = await healthChecks.CheckHealthAsync(
                registration => registration.Tags.Contains(DatabaseHealthCheck.ReadinessTag), cancellationToken);
            var available = report.Status == HealthStatus.Healthy && report.Entries.Count > 0;
            // Public diagnostics expose component status, never exception messages or connection details.
            return StatusCode(available ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable, new
            {
                status = available ? "ok" : "unavailable",
                application = "InvestmentTracker.Api",
                checks = report.Entries.ToDictionary(entry => entry.Key,
                    entry => entry.Value.Status == HealthStatus.Healthy ? "ok" : "unavailable")
            });
        }
    }
}
