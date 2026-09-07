using InvestmentTracker.Application.Sectors.Dtos;
using InvestmentTracker.Application.Sectors.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentTracker.Api.Controllers
{
    [ApiController]
    [Route("api/sectors")]
    public sealed class SectorsController(
    ISectorService service) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<SectorDto>>> GetAll(
            CancellationToken cancellationToken)
        {
            var items = await service.GetAllAsync(cancellationToken);

            return Ok(items);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<SectorDto>> GetById(
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
        public async Task<ActionResult<SectorDto>> Create(
            CreateSectorDto dto,
            CancellationToken cancellationToken)
        {
            var item = await service.CreateAsync(dto, cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<SectorDto>> Update(
            int id,
            UpdateSectorDto dto,
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
    }
}
