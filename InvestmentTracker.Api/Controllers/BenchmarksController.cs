using InvestmentTracker.Application.Benchmarks;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentTracker.Api.Controllers
{
    [ApiController]
    [Route("api/portfolios/{portfolioId:int}/history/benchmarks")]
    public sealed class BenchmarksController(BenchmarkService service) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Get(int portfolioId, [FromQuery] string mode, [FromQuery] string period, CancellationToken ct)
        {
            var result = await service.CompareAsync(portfolioId, mode, period, ct);
            return result is null ? NotFound() : Ok(result);
        }
    }
}
