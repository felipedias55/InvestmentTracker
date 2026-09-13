namespace InvestmentTracker.Domain.Entities
{
    public sealed class FinancialMovement
    {
        public int Id { get; set; }
        public int PortfolioId { get; set; }
        public Guid RequestId { get; set; }
        public DateOnly Date { get; set; }
        public string Kind { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public int? TradeId { get; set; }
        public PortfolioTrade? Trade { get; set; }
        public int? IncomeReceiptId { get; set; }
        public IncomeReceipt? IncomeReceipt { get; set; }
        public int? ReversalOfId { get; set; }
        public FinancialMovement? ReversalOf { get; set; }
        public string? RequestPayload { get; set; }
        public List<MovementEffect> Effects { get; set; } = [];
    }

    public sealed class MovementEffect
    {
        public int Id { get; set; }
        public int FinancialMovementId { get; set; }
        public int? PositionId { get; set; }
        public int? CashAssetId { get; set; }
        public int CurrencyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal BeforeValue { get; set; }
        public decimal AfterValue { get; set; }
        public decimal BeforeQuantity { get; set; }
        public decimal AfterQuantity { get; set; }
        public decimal BeforeCost { get; set; }
        public decimal AfterCost { get; set; }
        public decimal BeforeIncome { get; set; }
        public decimal AfterIncome { get; set; }
    }
}
