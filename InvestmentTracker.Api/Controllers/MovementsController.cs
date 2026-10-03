using InvestmentTracker.Application.Movements;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentTracker.Api.Controllers
{
    [ApiController]
    [Route("api/portfolios/{portfolioId:int}/movements")]
    public sealed class MovementsController(IMovementService service, ICorrectionPreviewService corrections, ICorrectionService execution) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> List(int portfolioId, CancellationToken ct) => Ok(await service.ListAsync(portfolioId, ct));
        [HttpGet("{id:int}/correction-draft")]
        public async Task<IActionResult> Draft(int portfolioId, int id, CancellationToken ct)
        {
            var result = await execution.DraftAsync(portfolioId, id, ct);
            return result is null ? NotFound() : Ok(result);
        }
        [HttpPost("correction-simulation")]
        public async Task<IActionResult> Simulate(int portfolioId, CorrectionInput dto, CancellationToken ct)
        {
            var result = await execution.SimulateAsync(portfolioId, dto, ct);
            return result is null ? NotFound() : Ok(result);
        }
        [HttpPost("corrections")]
        public async Task<IActionResult> Apply(int portfolioId, ApplyCorrectionDto dto, CancellationToken ct)
        {
            var result = await execution.ApplyAsync(portfolioId, dto, ct);
            return result is null ? NotFound() : Ok(result);
        }
        [HttpPost("correction-preview")]
        public async Task<IActionResult> Preview(int portfolioId, CorrectionPreviewRequest dto, CancellationToken ct)
        {
            var result = await corrections.PreviewAsync(portfolioId, dto, ct);
            return result is null ? NotFound() : Ok(result);
        }
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
