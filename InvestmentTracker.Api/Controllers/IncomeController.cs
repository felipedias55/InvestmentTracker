using InvestmentTracker.Application.Income;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentTracker.Api.Controllers
{
    [ApiController]
    [Route("api/portfolios/{portfolioId:int}/income")]
    public sealed class IncomeController(IIncomeService service, IIncomeConversionService conversions) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> List(int portfolioId, CancellationToken ct)
            => Ok(await service.ListAsync(portfolioId, ct));
        [HttpPost("{id:int}/conversions")]
        public async Task<IActionResult> Convert(int portfolioId, int id, SaveIncomeConversionDto dto, CancellationToken ct)
        {
            var result = await conversions.SaveAsync(portfolioId, id, dto, ct);
            return result is null ? NotFound() : Ok(result);
        }
        [HttpPost]
        public async Task<IActionResult> Save(int portfolioId, SaveIncomeDto dto, CancellationToken ct)
        {
            var result = await service.SaveAsync(portfolioId, dto, ct);
            return result is null ? NotFound() : CreatedAtAction(nameof(List), new { portfolioId }, result);
        }
    }
}
