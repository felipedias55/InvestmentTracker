namespace InvestmentTracker.Domain.Entities
{
    public sealed class PortfolioCashFlow
    {
        public int Id { get; set; }
        public int? TradeId { get; set; }
        public PortfolioTrade? Trade { get; set; }
        public int PortfolioId { get; set; }
        public Portfolio Portfolio { get; set; } = null!;
        public DateOnly Date { get; set; }
        public string Kind { get; set; } = "contribution";
        public int CurrencyId { get; set; }
        public Currency Currency { get; set; } = null!;
        public decimal Amount { get; set; }
        public string BaseCurrencyCode { get; set; } = string.Empty;
        public decimal? BaseAmount { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
