using InvestmentTracker.Application.Portfolios;
using InvestmentTracker.Application.Portfolios.Dtos;
using InvestmentTracker.Application.Portfolios.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentTracker.Api.Controllers
{
    [ApiController]
    [Route("api/portfolios")]
    public sealed class PortfoliosController(IPortfolioService service, PortfolioDefaults defaults) : ControllerBase
    {
        [HttpGet("defaults")]
        public IActionResult Defaults() => Ok(new { currencyCode = defaults.CurrencyCode });
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<PortfolioDto>>> GetAll(CancellationToken cancellationToken)
            => Ok(await service.GetAllAsync(cancellationToken));
        [HttpGet("{id:int}")]
        public async Task<ActionResult<PortfolioSummaryDto>> GetById(int id, CancellationToken cancellationToken)
        {
            var summary = await service.GetSummaryAsync(id, cancellationToken);
            return summary is null ? NotFound() : Ok(summary);
        }
        [HttpGet("{id:int}/assets")]
        public Task<ActionResult<PortfolioSummaryDto>> GetPositions(int id, CancellationToken cancellationToken)
            => GetById(id, cancellationToken);
        [HttpPost]
        public async Task<ActionResult<PortfolioDto>> Create(CreatePortfolioDto dto, CancellationToken cancellationToken)
        {
            var portfolio = await service.CreateAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = portfolio.Id }, portfolio);
        }
        [HttpPut("{id:int}")]
        public async Task<ActionResult<PortfolioDto>> Update(int id, UpdatePortfolioDto dto, CancellationToken cancellationToken)
        {
            var portfolio = await service.UpdateAsync(id, dto, cancellationToken);
            return portfolio is null ? NotFound() : Ok(portfolio);
        }
        [HttpPost("{id:int}/assets")]
        public async Task<IActionResult> AddPosition(int id, SavePositionDto dto, CancellationToken cancellationToken)
        {
            var positionId = await service.AddPositionAsync(id, dto, cancellationToken);
            return positionId is null ? NotFound() : CreatedAtAction(nameof(GetPositions), new { id }, new { id = positionId.Value });
        }
        [HttpPut("{id:int}/assets/{positionId:int}")]
        public async Task<IActionResult> UpdatePosition(int id, int positionId, SavePositionDto dto, CancellationToken cancellationToken)
            => await service.UpdatePositionAsync(id, positionId, dto, cancellationToken) ? NoContent() : NotFound();
        [HttpDelete("{id:int}/assets/{positionId:int}")]
        public async Task<IActionResult> DeletePosition(int id, int positionId, CancellationToken cancellationToken)
            => await service.DeletePositionAsync(id, positionId, cancellationToken) ? NoContent() : NotFound();
    }
}
