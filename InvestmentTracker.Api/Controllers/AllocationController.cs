using InvestmentTracker.Application.Allocation;
using InvestmentTracker.Application.Allocation.Dtos;
using InvestmentTracker.Application.Allocation.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentTracker.Api.Controllers
{
    [ApiController]
    [Route("api/portfolios/{id:int}")]
    public sealed class AllocationController(IAllocationService service) : ControllerBase
    {
        [HttpGet("category-targets")]
        public async Task<IActionResult> GetCategories(int id, CancellationToken cancellationToken)
        {
            var result = await service.GetTargetsAsync(id, AllocationDimension.Category, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        [HttpGet("sector-targets")]
        public async Task<IActionResult> GetSectors(int id, CancellationToken cancellationToken)
        {
            var result = await service.GetTargetsAsync(id, AllocationDimension.Sector, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        [HttpPut("category-targets")]
        public async Task<IActionResult> SaveCategories(int id, SaveTargetsDto dto, CancellationToken cancellationToken)
            => await service.SaveTargetsAsync(id, AllocationDimension.Category, dto, cancellationToken) ? NoContent() : NotFound();
        [HttpPut("sector-targets")]
        public async Task<IActionResult> SaveSectors(int id, SaveTargetsDto dto, CancellationToken cancellationToken)
            => await service.SaveTargetsAsync(id, AllocationDimension.Sector, dto, cancellationToken) ? NoContent() : NotFound();
        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard(int id, CancellationToken cancellationToken)
        {
            var result = await service.GetDashboardAsync(id, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        [HttpGet("allocation")]
        public async Task<IActionResult> Allocation(int id, CancellationToken cancellationToken)
        {
            var result = await service.GetDashboardAsync(id, cancellationToken);
            return result is null ? NotFound() : Ok(result.Allocation);
        }
        [HttpPost("contribution-analysis")]
        public async Task<IActionResult> Contribution(int id, ContributionRequestDto dto, CancellationToken cancellationToken)
        {
            var result = await service.AnalyzeContributionAsync(id, dto, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
    }
}
