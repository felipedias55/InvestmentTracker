using System.Text.Json.Serialization;
using InvestmentTracker.Application.Common.Exceptions;

namespace InvestmentTracker.Application.Portfolios
{
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public sealed record QuoteUpdate(int PositionId, string CurrencyCode, decimal UnitPrice,
        decimal ExpectedQuantity, decimal ExpectedValue, DateOnly ExpectedUpdatedOn);
    public sealed record QuoteBatchDto(Guid RequestId, IReadOnlyList<QuoteUpdate> Items);
    public interface IQuoteBatchRepository
    {
        Task<bool> SaveAsync(int portfolioId, QuoteBatchDto batch, DateOnly today, CancellationToken ct);
    }
    public sealed class QuoteBatchService(IQuoteBatchRepository repository, TimeProvider clock)
    {
        public Task<bool> SaveAsync(int portfolioId, QuoteBatchDto batch, CancellationToken ct)
        {
            if (batch.RequestId == Guid.Empty || batch.Items is null || batch.Items.Count is < 1 or > 200)
                throw new InputValidationException("Informe de 1 a 200 cotações e um identificador de solicitação válido.");
            if (batch.Items.Any(x => x is null) || batch.Items.Select(x => x.PositionId).Distinct().Count() != batch.Items.Count)
                throw new InputValidationException("Uma posição não pode aparecer duas vezes no lote.");
            foreach (var item in batch.Items)
            {
                if (item.PositionId <= 0 || string.IsNullOrWhiteSpace(item.CurrencyCode) || item.ExpectedQuantity <= 0
                    || item.ExpectedQuantity > 9999999999999.999999m || decimal.Round(item.ExpectedQuantity, 6) != item.ExpectedQuantity
                    || item.ExpectedValue < 0 || item.ExpectedValue > 999999999999999.9999m || decimal.Round(item.ExpectedValue, 4) != item.ExpectedValue)
                    throw new InputValidationException("Posição ou valores de referência inválidos. Recarregue a carteira.");
                CalculateValue(item.ExpectedQuantity, item.UnitPrice);
            }
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(),
                TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);
            return repository.SaveAsync(portfolioId, batch, today, ct);
        }
        public static decimal CalculateValue(decimal quantity, decimal unitPrice)
        {
            if (unitPrice <= 0 || unitPrice > 999999999999999.9999m || decimal.Round(unitPrice, 4) != unitPrice)
                throw new InputValidationException("A cotação unitária deve ser positiva e ter até quatro casas decimais.");
            decimal value;
            try { value = decimal.Round(quantity * unitPrice, 4, MidpointRounding.ToEven); }
            catch (OverflowException) { throw new InputValidationException("O valor total calculado excede o limite da posição."); }
            if (value > 999999999999999.9999m)
                throw new InputValidationException("O valor total calculado excede o limite da posição.");
            return value;
        }
    }
}
