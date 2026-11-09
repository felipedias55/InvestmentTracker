using InvestmentTracker.Application.Assets;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentTracker.Api.Controllers
{
    [ApiController]
    [Route("api/asset-discovery")]
    public sealed class AssetDiscoveryController(AssetDiscoveryService service) : ControllerBase
    {
        [HttpGet]
        public Task<AssetDiscoveryResult> Search(string market, string query, CancellationToken ct) => service.SearchAsync(market, query, ct);
        [HttpGet("image")]
        public async Task<SuggestedAssetImage> Image(string market, string symbol, CancellationToken ct)
        {
            Response.Headers.CacheControl = "no-store";
            return await service.ImageAsync(market, symbol, ct);
        }
    }
    [ApiController]
    [Route("api/reference-defaults")]
    public sealed class ReferenceDefaultsController(ReferenceDefaultsService service) : ControllerBase
    {
        [HttpPost]
        public Task<ReferenceDefaultsResult> Ensure(CancellationToken ct) => service.EnsureAsync(ct);
    }
}
