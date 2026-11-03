using InvestmentTracker.Application.Planning;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentTracker.Api.Controllers
{
    [ApiController]
    [Route("api/portfolios/{portfolioId:int}/planning-goals")]
    public sealed class PlanningGoalsController(PlanningGoalService service) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Page(int portfolioId, [FromQuery] PlanningGoalQuery query, CancellationToken ct)
        {
            var result = await service.PageAsync(portfolioId, query, ct);
            return result is null ? NotFound() : Ok(result);
        }
        [HttpPost]
        public async Task<IActionResult> Save(int portfolioId, SavePlanningGoal request, CancellationToken ct)
        {
            var result = await service.SaveAsync(portfolioId, request, ct);
            return result is null ? NotFound() : Ok(result);
        }
        [HttpGet("{goalId:guid}/revisions")]
        public async Task<IActionResult> History(int portfolioId, Guid goalId, CancellationToken ct)
        {
            var result = await service.HistoryAsync(portfolioId, goalId, ct);
            return result is null ? NotFound() : Ok(result);
        }
    }
}
