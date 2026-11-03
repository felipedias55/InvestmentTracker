namespace InvestmentTracker.Domain.Entities
{
    public sealed class PlanningGoalRevision
    {
        public int Id { get; set; }
        public int PortfolioId { get; set; }
        public Guid GoalId { get; set; }
        public int Revision { get; set; }
        public string Name { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Currency { get; set; } = "";
        public decimal Target { get; set; }
        public DateOnly Deadline { get; set; }
        public decimal MonthlyContribution { get; set; }
        public bool Archived { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
