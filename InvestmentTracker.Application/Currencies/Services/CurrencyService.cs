using InvestmentTracker.Application.Common.Validation;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Currencies.Dtos;
using InvestmentTracker.Application.Currencies.Interfaces;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.Currencies.Services
{
    public sealed class CurrencyService(
    ICurrencyRepository repository) : ICurrencyService
    {
        public async Task<IReadOnlyList<CurrencyDto>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            var currencies = await repository.GetAllAsync(cancellationToken);

            return currencies
                .Select(MapToDto)
                .ToList();
        }

        public async Task<CurrencyDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            var currency = await repository.GetByIdAsync(
                id,
                cancellationToken);

            return currency is null
                ? null
                : MapToDto(currency);
        }

        public async Task<CurrencyDto> CreateAsync(
            CreateCurrencyDto dto,
            CancellationToken cancellationToken = default)
        {
            var (code, name, symbol) = Validate(dto.Code, dto.Name, dto.Symbol);

            var exists = await repository.ExistsByCodeAsync(
                code,
                cancellationToken: cancellationToken);

            if (exists)
            {
                throw new ResourceConflictException(
                    "Já existe uma moeda com esse código.");
            }

            var currency = new Currency
            {
                Code = code,
                Name = name,
                Symbol = symbol
            };

            await repository.AddAsync(
                currency,
                cancellationToken);

            await repository.SaveChangesAsync(
                cancellationToken);

            return MapToDto(currency);
        }

        public async Task<CurrencyDto?> UpdateAsync(
            int id,
            UpdateCurrencyDto dto,
            CancellationToken cancellationToken = default)
        {
            var (code, name, symbol) = Validate(dto.Code, dto.Name, dto.Symbol);

            var currency = await repository.GetByIdAsync(
                id,
                cancellationToken);

            if (currency is null)
            {
                return null;
            }

            var exists = await repository.ExistsByCodeAsync(
                code,
                id,
                cancellationToken);

            if (exists)
            {
                throw new ResourceConflictException(
                    "Já existe uma moeda com esse código.");
            }

            if (currency.Code != code && await repository.IsInUseAsync(id, cancellationToken))
                throw new ResourceConflictException("O código de uma moeda em uso não pode ser alterado.");

            currency.Code = code;
            currency.Name = name;
            currency.Symbol = symbol;

            await repository.SaveChangesAsync(
                cancellationToken);

            return MapToDto(currency);
        }

        public async Task<bool> DeleteAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            var currency = await repository.GetByIdAsync(
                id,
                cancellationToken);

            if (currency is null)
            {
                return false;
            }

            await repository.DeleteAsync(
                currency,
                cancellationToken);

            await repository.SaveChangesAsync(
                cancellationToken);

            return true;
        }

        private static (string Code, string Name, string? Symbol) Validate(
            string? code, string? name, string? symbol)
        {
            var normalizedCode = InputRules.RequiredText(code, "O código da moeda", Currency.CodeLength)
                .ToUpperInvariant();
            if (normalizedCode.Length != Currency.CodeLength
                || normalizedCode.Any(character => character < 'A' || character > 'Z'))
            {
                throw new InputValidationException("O código da moeda deve conter três letras de A a Z.");
            }

            return (normalizedCode,
                InputRules.RequiredText(name, "O nome da moeda", Currency.NameMaxLength),
                InputRules.OptionalText(symbol, "O símbolo da moeda", Currency.SymbolMaxLength));
        }

        private static CurrencyDto MapToDto(Currency currency)
        {
            return new CurrencyDto(
                currency.Id,
                currency.Code,
                currency.Name,
                currency.Symbol);
        }
    }
}
