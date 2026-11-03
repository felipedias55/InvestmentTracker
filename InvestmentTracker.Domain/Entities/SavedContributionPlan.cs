namespace InvestmentTracker.Domain.Entities
{
    public sealed class SavedContributionPlan
    {
        public int Id { get; set; }
        public int PortfolioId { get; set; }
        public Guid RequestId { get; set; }
        public string Name { get; set; } = "";
        public DateOnly From { get; set; }
        public DateOnly To { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string RequestJson { get; set; } = "";
        public string SnapshotJson { get; set; } = "";
        public List<ContributionPurchaseLink> Purchases { get; set; } = [];
    }

    public sealed class ContributionPurchaseLink
    {
        public int Id { get; set; }
        public int SavedContributionPlanId { get; set; }
        public int TradeId { get; set; }
        public string RowKey { get; set; } = "";
        public decimal BaseAmount { get; set; }
        public string ConversionSource { get; set; } = "";
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? RemovedAtUtc { get; set; }
    }
}
