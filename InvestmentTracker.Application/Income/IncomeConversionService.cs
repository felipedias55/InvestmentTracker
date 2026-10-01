using System.Text.Json;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Common.Validation;
using InvestmentTracker.Application.Currencies.Interfaces;
using InvestmentTracker.Application.ExchangeRates.Interfaces;
using InvestmentTracker.Application.Movements;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.Income
{
    public interface IIncomeConversionService
    {
        Task<IncomeConversion?> BuildAsync(IncomeReceipt receipt, string currency, decimal? amount, Guid requestId, int revision, string reason, CancellationToken ct);
        Task<IncomeDto?> SaveAsync(int portfolioId, int receiptId, SaveIncomeConversionDto dto, CancellationToken ct);
    }
    public sealed class IncomeConversionService(IHistoricalExchangeRateProvider provider, IIncomeRepository income,
        IMovementRepository movements, ICurrencyRepository currencies, TimeProvider clock) : IIncomeConversionService
    {
        public static void ValidateAmount(decimal? amount)
        {
            if (amount.HasValue && (amount <= 0 || amount > 999999999999999.9999m || decimal.Round(amount.Value, 4) != amount))
                throw new InputValidationException("O equivalente deve ser positivo, com até 15 inteiros e 4 casas decimais.");
        }
        public async Task<IncomeConversion?> BuildAsync(IncomeReceipt receipt, string currency, decimal? amount,
            Guid requestId, int revision, string reason, CancellationToken ct)
        {
            ValidateAmount(amount);
            var result = new IncomeConversion { RequestId = requestId, Revision = revision, BaseCurrencyCode = currency,
                RateDate = receipt.Date, Reason = reason, CreatedAtUtc = clock.GetUtcNow().UtcDateTime };
            if (receipt.CurrencyCode == currency)
            {
                if (amount.HasValue && amount != receipt.Amount) throw new InputValidationException("Na mesma moeda, o equivalente deve ser igual ao recebimento.");
                result.BaseAmount = receipt.Amount; result.Rate = 1; result.Source = "same-currency";
            }
            else if (amount.HasValue)
            {
                result.BaseAmount = amount.Value;
                var rate = decimal.Round(amount.Value / receipt.Amount, 18, MidpointRounding.AwayFromZero);
                result.Rate = rate > 0 ? rate : null; result.Source = "manual";
            }
            else
            {
                ExchangeRate? quote;
                try { quote = await provider.FetchAsync(receipt.CurrencyCode, currency, receipt.Date, ct); }
                catch (HttpRequestException) { return null; }
                catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return null; }
                catch (InvalidDataException) { return null; }
                catch (JsonException) { return null; }
                if (quote is null || quote.BaseCode != receipt.CurrencyCode || quote.QuoteCode != currency || quote.Rate <= 0
                    || quote.Rate > 1000000000000m || quote.RateDate > receipt.Date || quote.RateDate < receipt.Date.AddDays(-7)) return null;
                try { result.BaseAmount = decimal.Round(receipt.Amount * quote.Rate, 4, MidpointRounding.AwayFromZero); }
                catch (OverflowException) { return null; }
                if (result.BaseAmount <= 0 || result.BaseAmount > 999999999999999.9999m) return null;
                result.Rate = decimal.Round(quote.Rate, 18, MidpointRounding.AwayFromZero);
                if (result.Rate <= 0) return null;
                result.RateDate = quote.RateDate; result.Source = "Frankfurter v2";
            }
            return result;
        }
        public async Task<IncomeDto?> SaveAsync(int portfolioId, int receiptId, SaveIncomeConversionDto dto, CancellationToken ct)
        {
            if (dto.RequestId == Guid.Empty || dto.ExpectedRevision < 0) throw new InputValidationException("Solicitação de conversão inválida.");
            var code = InputRules.RequiredText(dto.BaseCurrencyCode, "A moeda-base", 3).ToUpperInvariant();
            var reason = InputRules.RequiredText(dto.Reason, "O motivo", 400);
            ValidateAmount(dto.BaseAmount);
            var payload = JsonSerializer.Serialize(new { ReceiptId = receiptId, Currency = code, dto.ExpectedRevision, Reason = reason, dto.BaseAmount });
            var saved = await movements.ExecuteAsync(portfolioId, dto.RequestId, async (_, existing, token) =>
            {
                if (existing is not null)
                {
                    if (existing.Kind != "income-conversion" || existing.RequestPayload != payload)
                        throw new ResourceConflictException("Solicitação já utilizada com outros dados.");
                    return existing;
                }
                var receipt = await income.GetAsync(portfolioId, receiptId, token)
                    ?? throw new InputValidationException("Recebimento não encontrado nesta carteira.");
                if (!await currencies.ExistsByCodeAsync(code, cancellationToken: token)) throw new InputValidationException("Selecione uma moeda cadastrada.");
                var revision = receipt.Conversions.Where(x => x.BaseCurrencyCode == code).Select(x => x.Revision).DefaultIfEmpty().Max();
                if (revision != dto.ExpectedRevision) throw new ResourceConflictException("A conversão mudou. Atualize os recebimentos antes de confirmar.");
                var conversion = await BuildAsync(receipt, code, dto.BaseAmount, dto.RequestId, revision + 1, reason, token)
                    ?? throw new ResourceConflictException("Não há cotação histórica adequada disponível. Informe o equivalente real ou tente novamente mais tarde.");
                receipt.Conversions.Add(conversion);
                return new FinancialMovement { PortfolioId = portfolioId, RequestId = dto.RequestId,
                    Date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime),
                    Kind = "income-conversion", CurrencyCode = code, Amount = 0, Description = $"Câmbio do provento #{receipt.Id} ({receipt.Ticker}), versão {revision + 1}: {reason}",
                    RequestPayload = payload, CreatedAtUtc = clock.GetUtcNow().UtcDateTime };
            }, ct);
            return saved is null ? null : IncomeService.Map((await income.GetAsync(portfolioId, receiptId, ct))!);
        }
        public static IncomeConversionDto Map(IncomeConversion c) => new(c.Id, c.Revision, c.BaseCurrencyCode, c.BaseAmount, c.Rate, c.RateDate, c.Source, c.Reason, c.CreatedAtUtc);
    }
}
