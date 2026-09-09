namespace InvestmentTracker.Domain.Entities
{
    public sealed class PortfolioSnapshot
    {
        public int Id { get; set; }
        public int PortfolioId { get; set; }
        public Portfolio Portfolio { get; set; } = null!;
        public DateOnly Month { get; set; }
        public DateOnly SnapshotDate { get; set; }
        public DateTime CapturedAtUtc { get; set; }
        public string BaseCurrencyCode { get; set; } = string.Empty;
        public decimal PortfolioValue { get; set; }
        public decimal ExternalValue { get; set; }
        public decimal TotalWealth { get; set; }
        public decimal TotalIncome { get; set; }
        public bool HasStaleRates { get; set; }
        public bool HasFallbackRates { get; set; }
        public int PayloadVersion { get; set; } = 1;
        public string DashboardJson { get; set; } = string.Empty;
    }
}
