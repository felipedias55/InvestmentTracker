using InvestmentTracker.Application.Portfolios;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentTracker.Api.Controllers
{
    [ApiController]
    [Route("api/portfolios/{portfolioId:int}/quotes")]
    public sealed class QuotesController(QuoteBatchService service, InvestmentTracker.Application.Quotes.AssetQuoteService quotes) : ControllerBase
    {
        [HttpPost("preview")]
        public async Task<IActionResult> Preview(int portfolioId, InvestmentTracker.Application.Quotes.AssetQuoteRequest dto, CancellationToken ct)
        {
            var result = await quotes.PreviewAsync(portfolioId, dto, ct);
            return result is null ? NotFound() : Ok(result);
        }
        [HttpPut]
        public async Task<IActionResult> Save(int portfolioId, QuoteBatchDto dto, CancellationToken ct)
            => await service.SaveAsync(portfolioId, dto, ct) ? NoContent() : NotFound();
    }
}
