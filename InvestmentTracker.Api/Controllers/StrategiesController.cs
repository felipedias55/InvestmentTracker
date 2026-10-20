using InvestmentTracker.Application.Strategies;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentTracker.Api.Controllers
{
    [ApiController]
    [Route("api/portfolios/{id:int}/strategies")]
    public sealed class StrategiesController(StrategyService repository) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> List(int id, CancellationToken ct)
        {
            var result = await repository.ListAsync(id, ct);
            return result is null ? NotFound() : Ok(result);
        }
        [HttpPost]
        public async Task<IActionResult> Save(int id, SaveStrategy request, CancellationToken ct)
        {
            var result = await repository.SaveAsync(id, request, ct);
            return result is null ? NotFound() : Ok(result);
        }
    }
}
