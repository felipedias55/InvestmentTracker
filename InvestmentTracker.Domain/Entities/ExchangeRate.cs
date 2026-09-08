namespace InvestmentTracker.Domain.Entities
{
    public sealed class ExchangeRate
    {
        public string BaseCode { get; set; } = string.Empty;
        public string QuoteCode { get; set; } = string.Empty;
        public decimal Rate { get; set; }
        public DateOnly RateDate { get; set; }
        public DateTime FetchedAtUtc { get; set; }
    }
}
