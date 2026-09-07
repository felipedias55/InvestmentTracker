using InvestmentTracker.Application.Countries.Dtos;
using InvestmentTracker.Application.Countries.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentTracker.Api.Controllers
{
    [ApiController]
    [Route("api/countries")]
    public sealed class CountriesController(
    ICountryService service) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<CountryDto>>> GetAll(
            CancellationToken cancellationToken)
        {
            var items = await service.GetAllAsync(cancellationToken);

            return Ok(items);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<CountryDto>> GetById(
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
        public async Task<ActionResult<CountryDto>> Create(
            CreateCountryDto dto,
            CancellationToken cancellationToken)
        {
            var item = await service.CreateAsync(dto, cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<CountryDto>> Update(
            int id,
            UpdateCountryDto dto,
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
