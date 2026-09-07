using InvestmentTracker.Application.Currencies.Dtos;
using InvestmentTracker.Application.Currencies.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentTracker.Api.Controllers
{
    [ApiController]
    [Route("api/currencies")]
    public sealed class CurrenciesController(
    ICurrencyService service) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<CurrencyDto>>> GetAll(
            CancellationToken cancellationToken)
        {
            var items = await service.GetAllAsync(cancellationToken);

            return Ok(items);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<CurrencyDto>> GetById(
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
        public async Task<ActionResult<CurrencyDto>> Create(
            CreateCurrencyDto dto,
            CancellationToken cancellationToken)
        {
            var item = await service.CreateAsync(dto, cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<CurrencyDto>> Update(
            int id,
            UpdateCurrencyDto dto,
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
