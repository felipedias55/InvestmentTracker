using System;
using System.Collections.Generic;
using System.Text;
using InvestmentTracker.Application.Currencies.Dtos;

namespace InvestmentTracker.Application.Currencies.Interfaces
{
    public interface ICurrencyService
    {
        Task<IReadOnlyList<CurrencyDto>> GetAllAsync(
            CancellationToken cancellationToken = default);

        Task<CurrencyDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<CurrencyDto> CreateAsync(
            CreateCurrencyDto dto,
            CancellationToken cancellationToken = default);

        Task<CurrencyDto?> UpdateAsync(
            int id,
            UpdateCurrencyDto dto,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteAsync(
            int id,
            CancellationToken cancellationToken = default);
    }
}
