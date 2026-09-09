using InvestmentTracker.Application.Income;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentTracker.Api.Controllers
{
    [ApiController]
    [Route("api/portfolios/{portfolioId:int}/income")]
    public sealed class IncomeController(IIncomeService service) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> List(int portfolioId, CancellationToken ct)
            => Ok(await service.ListAsync(portfolioId, ct));
        [HttpPost]
        public async Task<IActionResult> Save(int portfolioId, SaveIncomeDto dto, CancellationToken ct)
        {
            var result = await service.SaveAsync(portfolioId, dto, ct);
            return result is null ? NotFound() : CreatedAtAction(nameof(List), new { portfolioId }, result);
        }
    }
}
