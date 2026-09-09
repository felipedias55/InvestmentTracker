using InvestmentTracker.Application.ExternalAssets.Dtos;
using InvestmentTracker.Application.ExternalAssets.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentTracker.Api.Controllers
{
    [ApiController]
    [Route("api/portfolios/{portfolioId:int}/external-assets")]
    public sealed class ExternalAssetsController(IExternalAssetService service) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetAll(int portfolioId, CancellationToken cancellationToken)
        {
            var summary = await service.GetSummaryAsync(portfolioId, cancellationToken);
            return summary is null ? NotFound() : Ok(summary);
        }
        [HttpPost]
        public async Task<IActionResult> Create(int portfolioId, SaveExternalAssetDto dto, CancellationToken cancellationToken)
        {
            var id = await service.CreateAsync(portfolioId, dto, cancellationToken);
            return id is null ? NotFound() : CreatedAtAction(nameof(GetAll), new { portfolioId }, new { id = id.Value });
        }
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int portfolioId, int id, SaveExternalAssetDto dto, CancellationToken cancellationToken)
            => await service.UpdateAsync(portfolioId, id, dto, cancellationToken) ? NoContent() : NotFound();
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int portfolioId, int id, CancellationToken cancellationToken)
            => await service.DeleteAsync(portfolioId, id, cancellationToken) ? NoContent() : NotFound();
    }
}
