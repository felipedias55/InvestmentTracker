using InvestmentTracker.Application.Income;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentTracker.Api.Controllers
{
    [ApiController]
    [Route("api/portfolios/{portfolioId:int}/income/analysis")]
    public sealed class IncomeAnalysisController(IIncomeAnalysisService service) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Get(int portfolioId, CancellationToken ct) => Ok(await service.GetAsync(portfolioId, ct));
    }
}
