using InvestmentTracker.Application.AssetTypes.Dtos;
using InvestmentTracker.Application.AssetTypes.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentTracker.Api.Controllers
{
    [ApiController]
    [Route("api/asset-types")]
    public sealed class AssetTypesController(
    IAssetTypeService service) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<AssetTypeDto>>> GetAll(
            CancellationToken cancellationToken)
        {
            var assetTypes = await service.GetAllAsync(cancellationToken);

            return Ok(assetTypes);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<AssetTypeDto>> GetById(
            int id,
            CancellationToken cancellationToken)
        {
            var assetType = await service.GetByIdAsync(
                id,
                cancellationToken);

            if (assetType is null)
            {
                return NotFound();
            }

            return Ok(assetType);
        }

        [HttpPost]
        public async Task<ActionResult<AssetTypeDto>> Create(
            CreateAssetTypeDto dto,
            CancellationToken cancellationToken)
        {
            try
            {
                var assetType = await service.CreateAsync(
                    dto,
                    cancellationToken);

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = assetType.Id },
                    assetType);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new
                {
                    message = ex.Message
                });
            }
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<AssetTypeDto>> Update(
            int id,
            UpdateAssetTypeDto dto,
            CancellationToken cancellationToken)
        {
            try
            {
                var assetType = await service.UpdateAsync(
                    id,
                    dto,
                    cancellationToken);

                if (assetType is null)
                {
                    return NotFound();
                }

                return Ok(assetType);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new
                {
                    message = ex.Message
                });
            }
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
    }
}
