using InvestmentTracker.Application.Portfolios;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentTracker.Api.Controllers
{
    [ApiController]
    [Route("api/portfolios/{portfolioId:int}/quotes")]
    public sealed class QuotesController(QuoteBatchService service) : ControllerBase
    {
        [HttpPut]
        public async Task<IActionResult> Save(int portfolioId, QuoteBatchDto dto, CancellationToken ct)
            => await service.SaveAsync(portfolioId, dto, ct) ? NoContent() : NotFound();
    }
}
