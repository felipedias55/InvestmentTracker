namespace InvestmentTracker.Domain.Entities
{
    public sealed class PortfolioTrade
    {
        public int Id { get; set; }
        public int PortfolioId { get; set; }
        public int AssetId { get; set; }
        public Guid RequestId { get; set; }
        public DateOnly Date { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string Ticker { get; set; } = string.Empty;
        public string CurrencyCode { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
        public decimal RemainingQuantity { get; set; }
        public decimal RemainingCost { get; set; }
        public int? CashAssetId { get; set; }
        public string? CashAssetName { get; set; }
        public decimal? RequestedBaseAmount { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
