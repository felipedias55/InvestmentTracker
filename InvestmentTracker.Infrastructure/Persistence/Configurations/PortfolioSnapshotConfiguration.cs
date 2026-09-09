using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestmentTracker.Infrastructure.Persistence.Configurations
{
    public sealed class PortfolioSnapshotConfiguration : IEntityTypeConfiguration<PortfolioSnapshot>
    {
        public void Configure(EntityTypeBuilder<PortfolioSnapshot> builder)
        {
            builder.ToTable("PortfolioSnapshot", t =>
            {
                t.HasCheckConstraint("CK_PortfolioSnapshot_Month", "DAY([Month]) = 1 AND YEAR([Month]) = YEAR([SnapshotDate]) AND MONTH([Month]) = MONTH([SnapshotDate])");
                t.HasCheckConstraint("CK_PortfolioSnapshot_Values", "[PortfolioValue] >= 0 AND [ExternalValue] >= 0 AND [TotalWealth] >= 0 AND [TotalIncome] >= 0");
                t.HasCheckConstraint("CK_PortfolioSnapshot_Json", "ISJSON([DashboardJson]) = 1");
            });
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.PortfolioId, x.Month }).IsUnique();
            builder.HasOne(x => x.Portfolio).WithMany().HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.Restrict);
            builder.Property(x => x.BaseCurrencyCode).HasMaxLength(3).IsRequired();
            builder.Property(x => x.DashboardJson).HasColumnType("nvarchar(max)").IsRequired();
            builder.Property(x => x.PortfolioValue).HasPrecision(38, 4);
            builder.Property(x => x.ExternalValue).HasPrecision(38, 4);
            builder.Property(x => x.TotalWealth).HasPrecision(38, 4);
            builder.Property(x => x.TotalIncome).HasPrecision(38, 4);
        }
    }
}
