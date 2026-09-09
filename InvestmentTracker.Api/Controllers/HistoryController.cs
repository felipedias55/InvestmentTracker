using InvestmentTracker.Application.History.Dtos;
using InvestmentTracker.Application.History.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentTracker.Api.Controllers
{
    [ApiController]
    [Route("api/portfolios/{portfolioId:int}/history")]
    public sealed class HistoryController(IHistoryService service) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Get(int portfolioId, CancellationToken cancellationToken)
        {
            var result = await service.GetAsync(portfolioId, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        [HttpGet("snapshots/{id:int}")]
        public async Task<IActionResult> Snapshot(int portfolioId, int id, CancellationToken cancellationToken)
        {
            var result = await service.GetSnapshotAsync(portfolioId, id, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        [HttpPost("snapshots")]
        public async Task<IActionResult> Capture(int portfolioId, CancellationToken cancellationToken)
        {
            var result = await service.CaptureAsync(portfolioId, false, cancellationToken);
            return result is null ? NotFound() : CreatedAtAction(nameof(Snapshot), new { portfolioId, id = result.Id }, result);
        }
        [HttpPut("snapshots/current")]
        public async Task<IActionResult> ReplaceCurrent(int portfolioId, CancellationToken cancellationToken)
        {
            var result = await service.CaptureAsync(portfolioId, true, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        [HttpPost("cash-flows")]
        public async Task<IActionResult> CreateFlow(int portfolioId, SaveCashFlowDto dto, CancellationToken cancellationToken)
        {
            var id = await service.CreateCashFlowAsync(portfolioId, dto, cancellationToken);
            return id is null ? NotFound() : CreatedAtAction(nameof(Get), new { portfolioId }, new { id = id.Value });
        }
        [HttpPut("cash-flows/{id:int}")]
        public async Task<IActionResult> UpdateFlow(int portfolioId, int id, SaveCashFlowDto dto, CancellationToken cancellationToken)
            => await service.UpdateCashFlowAsync(portfolioId, id, dto, cancellationToken) ? NoContent() : NotFound();
        [HttpDelete("cash-flows/{id:int}")]
        public async Task<IActionResult> DeleteFlow(int portfolioId, int id, CancellationToken cancellationToken)
            => await service.DeleteCashFlowAsync(portfolioId, id, cancellationToken) ? NoContent() : NotFound();
    }
}
