using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestmentTracker.Infrastructure.Persistence.Configurations
{
    public sealed class PortfolioTradeConfiguration : IEntityTypeConfiguration<PortfolioTrade>
    {
        public void Configure(EntityTypeBuilder<PortfolioTrade> b)
        {
            b.ToTable("PortfolioTrade", t => {
                t.HasCheckConstraint("CK_PortfolioTrade_Values", "[Quantity] > 0 AND [UnitPrice] > 0 AND [Amount] > 0 AND [RemainingQuantity] >= 0 AND [RemainingCost] >= 0");
                t.HasCheckConstraint("CK_PortfolioTrade_Kind", "[Kind] IN ('buy', 'sell')");
            });
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.PortfolioId, x.RequestId }).IsUnique();
            b.HasIndex(x => new { x.PortfolioId, x.Date });
            b.HasOne<Portfolio>().WithMany().HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ExternalAsset>().WithMany().HasForeignKey(x => x.CashAssetId).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Kind).HasMaxLength(4);
            b.Property(x => x.Ticker).HasMaxLength(20);
            b.Property(x => x.CurrencyCode).HasMaxLength(3);
            b.Property(x => x.CashAssetName).HasMaxLength(200);
            b.Property(x => x.Quantity).HasPrecision(19, 6);
            b.Property(x => x.RemainingQuantity).HasPrecision(19, 6);
            b.Property(x => x.UnitPrice).HasPrecision(19, 4);
            b.Property(x => x.Amount).HasPrecision(19, 4);
            b.Property(x => x.RemainingCost).HasPrecision(19, 4);
            b.Property(x => x.RequestedBaseAmount).HasPrecision(19, 4);
        }
    }
}
