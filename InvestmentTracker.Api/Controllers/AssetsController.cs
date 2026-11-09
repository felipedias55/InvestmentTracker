using InvestmentTracker.Application.Assets.Dtos;
using InvestmentTracker.Application.Assets.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentTracker.Api.Controllers
{
    [ApiController]
    [Route("api/assets")]
    public sealed class AssetsController(
    IAssetService service) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<AssetDto>>> GetAll(
            CancellationToken cancellationToken)
        {
            var items = await service.GetAllAsync(cancellationToken);

            return Ok(items);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<AssetDto>> GetById(
            int id,
            CancellationToken cancellationToken)
        {
            var item = await service.GetByIdAsync(
                id,
                cancellationToken);

            if (item is null)
            {
                return NotFound();
            }

            return Ok(item);
        }

        [HttpPost]
        [RequestSizeLimit(400000)]
        public async Task<ActionResult<AssetDto>> Create(
            CreateAssetDto dto,
            CancellationToken cancellationToken)
        {
            var item = await service.CreateAsync(dto, cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
        }

        [HttpPut("{id:int}")]
        [RequestSizeLimit(400000)]
        public async Task<ActionResult<AssetDto>> Update(
            int id,
            UpdateAssetDto dto,
            CancellationToken cancellationToken)
        {
            var item = await service.UpdateAsync(id, dto, cancellationToken);

            if (item is null)
            {
                return NotFound();
            }

            return Ok(item);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(
            int id,
            CancellationToken cancellationToken)
        {
            var deleted = await service.DeleteAsync(
                id,
                cancellationToken);

            if (!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }
        [HttpGet("{id:int}/image")]
        public async Task<IActionResult> Image(int id, string? v, CancellationToken cancellationToken)
        {
            var file = await service.GetImageAsync(id, cancellationToken);
            if (file is null || (v is not null && v != file.Version)) return NotFound();
            Response.Headers.CacheControl = "private,max-age=3600";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(file.Data, "image/png", lastModified: null,
                entityTag: new Microsoft.Net.Http.Headers.EntityTagHeaderValue('"' + file.Version + '"'));
        }
    }
}
