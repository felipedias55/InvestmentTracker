using System.Text.Json;
using InvestmentTracker.Application.Allocation.Dtos;
using InvestmentTracker.Application.Allocation.Interfaces;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Common.Validation;
using InvestmentTracker.Application.Currencies.Interfaces;
using InvestmentTracker.Application.History.Dtos;
using InvestmentTracker.Application.History.Interfaces;
using InvestmentTracker.Application.Portfolios.Interfaces;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.History.Services
{
    public sealed class HistoryService(IHistoryRepository repository, IPortfolioRepository portfolios,
        IAllocationService dashboard, ICurrencyRepository currencies, TimeProvider clock) : IHistoryService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private static readonly TimeZoneInfo BusinessZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        private DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), BusinessZone).DateTime);

        public async Task<HistoryDto?> GetAsync(int portfolioId, CancellationToken cancellationToken = default)
        {
            var portfolio = await portfolios.GetByIdAsync(portfolioId, cancellationToken);
            if (portfolio is null) return null;
            var snapshots = await repository.GetSnapshotsAsync(portfolioId, cancellationToken);
            var flows = await repository.GetCashFlowsAsync(portfolioId, cancellationToken);
            var today = Today;
            return new HistoryDto(new(portfolio.Id, portfolio.Name, portfolio.Description, portfolio.BaseCurrencyId, portfolio.BaseCurrency.Code),
                today, HistoryCalculator.Months(snapshots, flows, today, portfolio.BaseCurrency.Code),
                HistoryCalculator.Years(snapshots, flows, today, portfolio.BaseCurrency.Code),
                flows.Select(f => new CashFlowDto(f.Id, f.Date, f.Kind, f.CurrencyId, f.Currency.Code,
                    f.Amount, f.BaseCurrencyCode, f.BaseAmount, f.Notes) { TradeId = f.TradeId }).ToList());
        }

        public async Task<SnapshotDetailDto?> GetSnapshotAsync(int portfolioId, int id, CancellationToken cancellationToken = default)
        {
            var snapshot = await repository.GetSnapshotAsync(portfolioId, id, cancellationToken);
            return snapshot is null ? null : Map(snapshot);
        }

        public async Task<SnapshotDetailDto?> CaptureAsync(int portfolioId, bool replace, CancellationToken cancellationToken = default)
        {
            var snapshot = await repository.CaptureAsync(portfolioId, replace, async ct =>
            {
                var current = await dashboard.GetDashboardAsync(portfolioId, ct)
                    ?? throw new ResourceConflictException("A carteira não está mais disponível.");
                if (!current.TotalWealth.HasValue || !current.Summary.ConversionAvailable || current.ExternalAssets is null
                    || !current.ExternalAssets.ConversionAvailable || !current.Summary.TotalIncome.HasValue)
                    throw new ResourceConflictException("Atualize o câmbio antes de fotografar. Não é possível guardar um patrimônio parcial.");
                var today = Today;
                return new PortfolioSnapshot
                {
                    PortfolioId = portfolioId, Month = new DateOnly(today.Year, today.Month, 1), SnapshotDate = today,
                    CapturedAtUtc = clock.GetUtcNow().UtcDateTime, BaseCurrencyCode = current.Summary.Portfolio.BaseCurrencyCode,
                    PortfolioValue = decimal.Round(current.Summary.CurrentValue!.Value, 4),
                    ExternalValue = decimal.Round(current.ExternalAssets.TotalValue!.Value, 4),
                    TotalWealth = decimal.Round(current.TotalWealth.Value, 4), TotalIncome = decimal.Round(current.Summary.TotalIncome.Value, 4),
                    HasStaleRates = current.Summary.HasStaleRates || current.ExternalAssets.HasStaleRates,
                    HasFallbackRates = current.Summary.HasFallbackRates || current.ExternalAssets.HasFallbackRates,
                    DashboardJson = JsonSerializer.Serialize(current, JsonOptions)
                };
            }, cancellationToken);
            return snapshot is null ? null : Map(snapshot);
        }

        public async Task<int?> CreateCashFlowAsync(int portfolioId, SaveCashFlowDto dto, CancellationToken cancellationToken = default)
        {
            var portfolio = await portfolios.GetByIdAsync(portfolioId, cancellationToken);
            if (portfolio is null) return null;
            var flow = new PortfolioCashFlow { PortfolioId = portfolioId, BaseCurrencyCode = portfolio.BaseCurrency.Code,
                CreatedAtUtc = clock.GetUtcNow().UtcDateTime };
            await ApplyAsync(flow, dto, cancellationToken);
            await repository.AddCashFlowAsync(flow, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
            return flow.Id;
        }

        public async Task<bool> UpdateCashFlowAsync(int portfolioId, int id, SaveCashFlowDto dto, CancellationToken cancellationToken = default)
        {
            var flow = await repository.GetCashFlowAsync(portfolioId, id, cancellationToken);
            if (flow is null) return false;
            if (flow.TradeId.HasValue) throw new ResourceConflictException("Este movimento foi gerado por uma operação e não pode ser alterado separadamente.");
            await ApplyAsync(flow, dto, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> DeleteCashFlowAsync(int portfolioId, int id, CancellationToken cancellationToken = default)
        {
            var flow = await repository.GetCashFlowAsync(portfolioId, id, cancellationToken);
            if (flow is null) return false;
            if (flow.TradeId.HasValue) throw new ResourceConflictException("Este movimento foi gerado por uma operação e não pode ser alterado separadamente.");
            repository.RemoveCashFlow(flow);
            await repository.SaveChangesAsync(cancellationToken);
            return true;
        }

        private async Task ApplyAsync(PortfolioCashFlow flow, SaveCashFlowDto dto, CancellationToken cancellationToken)
        {
            if (dto.Date < new DateOnly(1900, 1, 1) || dto.Date > Today)
                throw new InputValidationException("Informe uma data válida, entre 01/01/1900 e hoje.");
            if (dto.Kind is not ("contribution" or "withdrawal"))
                throw new InputValidationException("Selecione aporte ou retirada.");
            ValidateAmount(dto.Amount);
            if (dto.BaseAmount.HasValue) ValidateAmount(dto.BaseAmount.Value);
            var currency = await currencies.GetByIdAsync(dto.CurrencyId, cancellationToken)
                ?? throw new InputValidationException("Selecione uma moeda existente.");
            if (currency.Code == flow.BaseCurrencyCode && dto.BaseAmount.HasValue && dto.BaseAmount != dto.Amount)
                throw new InputValidationException("Na mesma moeda, o valor equivalente deve ser igual ao valor original.");
            flow.Date = dto.Date; flow.Kind = dto.Kind; flow.Amount = dto.Amount;
            flow.CurrencyId = currency.Id; flow.Currency = currency;
            flow.BaseAmount = currency.Code == flow.BaseCurrencyCode ? dto.Amount : dto.BaseAmount;
            flow.Notes = InputRules.OptionalText(dto.Notes, "A observação", 500);
            flow.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        }

        private static void ValidateAmount(decimal value)
        {
            if (value <= 0m || value > 999999999999999.9999m || decimal.Round(value, 4) != value)
                throw new InputValidationException("O valor deve ser maior que zero, com até 15 inteiros e quatro casas decimais.");
        }

        private static SnapshotDetailDto Map(PortfolioSnapshot s) => new(s.Id, s.Month, s.SnapshotDate, s.CapturedAtUtc, s.PayloadVersion,
            JsonSerializer.Deserialize<DashboardDto>(s.DashboardJson, JsonOptions)
                ?? throw new InvalidDataException("Fotografia inválida."));
    }
}
