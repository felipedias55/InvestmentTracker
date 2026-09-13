using InvestmentTracker.Application.Movements;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentTracker.Api.Controllers
{
    [ApiController]
    [Route("api/portfolios/{portfolioId:int}/movements")]
    public sealed class MovementsController(IMovementService service) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> List(int portfolioId, CancellationToken ct) => Ok(await service.ListAsync(portfolioId, ct));
        [HttpPost]
        public async Task<IActionResult> Save(int portfolioId, SaveMovementDto dto, CancellationToken ct)
        {
            var result = await service.SaveAsync(portfolioId, dto, ct);
            return result is null ? NotFound() : CreatedAtAction(nameof(List), new { portfolioId }, result);
        }
        [HttpPost("corporate-events")]
        public async Task<IActionResult> CorporateEvent(int portfolioId, SaveCorporateEventDto dto, CancellationToken ct)
        {
            var result = await service.CorporateEventAsync(portfolioId, dto, ct);
            return result is null ? NotFound() : CreatedAtAction(nameof(List), new { portfolioId }, result);
        }
        [HttpPost("{id:int}/reversal")]
        public async Task<IActionResult> Reverse(int portfolioId, int id, ReverseMovementDto dto, CancellationToken ct)
        {
            var result = await service.ReverseAsync(portfolioId, id, dto, ct);
            return result is null ? NotFound() : CreatedAtAction(nameof(List), new { portfolioId }, result);
        }
    }
}
