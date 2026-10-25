using InvestmentTracker.Application.Trades;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentTracker.Api.Controllers
{
    [ApiController]
    [Route("api/portfolios/{portfolioId:int}/trades")]
    public sealed class TradesController(ITradeService service) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<TradeDto>>> List(int portfolioId, CancellationToken ct)
            => Ok(await service.ListAsync(portfolioId, ct));
        [HttpGet("page")]
        public async Task<ActionResult<TradePageDto>> Page(int portfolioId, [FromQuery] TradePageQuery query, CancellationToken ct)
            => Ok(await service.PageAsync(portfolioId, query, ct));
        [HttpPost]
        public async Task<ActionResult<TradeDto>> Save(int portfolioId, SaveTradeDto dto, CancellationToken ct)
        {
            var trade = await service.SaveAsync(portfolioId, dto, ct);
            return trade is null ? NotFound() : Ok(trade);
        }
    }
}
