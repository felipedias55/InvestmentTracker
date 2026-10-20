namespace InvestmentTracker.Domain.Entities
{
    public sealed class PortfolioStrategyRevision
    {
        public int Id { get; set; }
        public int PortfolioId { get; set; }
        public Guid ProfileId { get; set; }
        public int Revision { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string ConfigurationJson { get; set; } = "";
    }
}
