using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestmentTracker.Infrastructure.Persistence.Configurations
{
    public sealed class PlanningGoalRevisionConfiguration : IEntityTypeConfiguration<PlanningGoalRevision>
    {
        public void Configure(EntityTypeBuilder<PlanningGoalRevision> b)
        {
            b.ToTable("PlanningGoalRevision", t => {
                t.HasCheckConstraint("CK_PlanningGoalRevision_Values", "[Revision] > 0 AND [Target] > 0 AND [MonthlyContribution] >= 0 AND ([Kind] = 'wealth' OR [MonthlyContribution] = 0)");
                t.HasCheckConstraint("CK_PlanningGoalRevision_Kind", "[Kind] IN ('wealth', 'monthly-income')");
            });
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.PortfolioId, x.GoalId, x.Revision }).IsUnique();
            b.Property(x => x.Name).HasMaxLength(100);
            b.Property(x => x.Kind).HasMaxLength(20);
            b.Property(x => x.Currency).HasMaxLength(3);
            b.Property(x => x.Target).HasPrecision(17, 2);
            b.Property(x => x.MonthlyContribution).HasPrecision(17, 2);
            b.HasOne<Portfolio>().WithMany().HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
