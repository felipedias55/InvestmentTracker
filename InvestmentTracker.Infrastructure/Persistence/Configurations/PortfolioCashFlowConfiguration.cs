using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestmentTracker.Infrastructure.Persistence.Configurations
{
    public sealed class PortfolioCashFlowConfiguration : IEntityTypeConfiguration<PortfolioCashFlow>
    {
        public void Configure(EntityTypeBuilder<PortfolioCashFlow> builder)
        {
            builder.ToTable("PortfolioCashFlow", t =>
            {
                t.HasCheckConstraint("CK_PortfolioCashFlow_Values", "[Amount] > 0 AND ([BaseAmount] IS NULL OR [BaseAmount] > 0)");
                t.HasCheckConstraint("CK_PortfolioCashFlow_Kind", "[Kind] IN ('contribution', 'withdrawal')");
            });
            builder.HasKey(x => x.Id);
            builder.HasOne(x => x.Trade).WithMany().HasForeignKey(x => x.TradeId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => x.TradeId).IsUnique().HasFilter("[TradeId] IS NOT NULL");
            builder.HasIndex(x => new { x.PortfolioId, x.Date });
            builder.HasOne(x => x.Portfolio).WithMany().HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.Currency).WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);
            builder.Property(x => x.Kind).HasMaxLength(12).IsRequired();
            builder.Property(x => x.BaseCurrencyCode).HasMaxLength(3).IsRequired();
            builder.Property(x => x.Amount).HasPrecision(19, 4);
            builder.Property(x => x.BaseAmount).HasPrecision(19, 4);
            builder.Property(x => x.Notes).HasMaxLength(500);
        }
    }
}
