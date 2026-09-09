namespace InvestmentTracker.Domain.Entities
{
    public sealed class IncomeReceipt
    {
        public int Id { get; set; }
        public int PortfolioId { get; set; }
        public int PositionId { get; set; }
        public int AssetId { get; set; }
        public Guid RequestId { get; set; }
        public DateOnly Date { get; set; }
        public string Ticker { get; set; } = string.Empty;
        public string CurrencyCode { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int? CashAssetId { get; set; }
        public string? CashAssetName { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
