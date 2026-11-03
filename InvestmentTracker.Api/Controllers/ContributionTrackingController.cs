using InvestmentTracker.Application.Allocation.Tracking;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentTracker.Api.Controllers
{
    [ApiController]
    [Route("api/portfolios/{portfolioId:int}/contribution-plans")]
    public sealed class ContributionTrackingController(ContributionTrackingService service) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Page(int portfolioId, CancellationToken ct, int pageIndex = 0, int pageSize = 10)
        {
            var result = await service.PageAsync(portfolioId, pageIndex, pageSize, ct);
            return result is null ? NotFound() : Ok(result);
        }
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Read(int portfolioId, int id, CancellationToken ct)
        {
            var result = await service.ReadAsync(portfolioId, id, ct);
            return result is null ? NotFound() : Ok(result);
        }
        [HttpPost]
        public async Task<IActionResult> Save(int portfolioId, SaveContributionPlan request, CancellationToken ct)
        {
            var result = await service.SaveAsync(portfolioId, request, ct);
            return result is null ? NotFound() : Ok(result);
        }
        [HttpGet("{id:int}/purchases")]
        public async Task<IActionResult> Candidates(int portfolioId, int id, CancellationToken ct, int pageIndex = 0, int pageSize = 10)
        {
            var result = await service.CandidatesAsync(portfolioId, id, pageIndex, pageSize, ct);
            return result is null ? NotFound() : Ok(result);
        }
        [HttpPost("{id:int}/purchases")]
        public async Task<IActionResult> Link(int portfolioId, int id, LinkPurchase request, CancellationToken ct)
            => await service.LinkAsync(portfolioId, id, request, ct) ? NoContent() : NotFound();
        [HttpDelete("{id:int}/purchases/{linkId:int}")]
        public async Task<IActionResult> Unlink(int portfolioId, int id, int linkId, CancellationToken ct)
            => await service.UnlinkAsync(portfolioId, id, linkId, ct) ? NoContent() : NotFound();
    }
}
