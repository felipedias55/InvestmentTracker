using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestmentTracker.Infrastructure.Persistence.Configurations
{
    public sealed class SavedContributionPlanConfiguration : IEntityTypeConfiguration<SavedContributionPlan>
    {
        public void Configure(EntityTypeBuilder<SavedContributionPlan> b)
        {
            b.ToTable("SavedContributionPlan", t => t.HasCheckConstraint("CK_SavedContributionPlan_Period", "[From] <= [To]"));
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.PortfolioId, x.RequestId }).IsUnique();
            b.Property(x => x.Name).HasMaxLength(100);
            b.HasOne<Portfolio>().WithMany().HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Purchases).WithOne().HasForeignKey(x => x.SavedContributionPlanId).OnDelete(DeleteBehavior.Restrict);
        }
    }
    public sealed class ContributionPurchaseLinkConfiguration : IEntityTypeConfiguration<ContributionPurchaseLink>
    {
        public void Configure(EntityTypeBuilder<ContributionPurchaseLink> b)
        {
            b.ToTable("ContributionPurchaseLink", t => t.HasCheckConstraint("CK_ContributionPurchaseLink_Amount", "[BaseAmount] > 0"));
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.TradeId).IsUnique().HasFilter("[RemovedAtUtc] IS NULL");
            b.Property(x => x.RowKey).HasMaxLength(100);
            b.Property(x => x.ConversionSource).HasMaxLength(20);
            b.Property(x => x.BaseAmount).HasPrecision(19, 4);
            b.HasOne<PortfolioTrade>().WithMany().HasForeignKey(x => x.TradeId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
