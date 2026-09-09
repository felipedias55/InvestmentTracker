using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestmentTracker.Infrastructure.Persistence.Configurations
{
    public sealed class IncomeReceiptConfiguration : IEntityTypeConfiguration<IncomeReceipt>
    {
        public void Configure(EntityTypeBuilder<IncomeReceipt> b)
        {
            b.ToTable("IncomeReceipt", t => t.HasCheckConstraint("CK_IncomeReceipt_Amount", "[Amount] > 0"));
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.PortfolioId, x.RequestId }).IsUnique();
            b.HasIndex(x => new { x.PortfolioId, x.Date });
            b.HasOne<Portfolio>().WithMany().HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<PortfolioAsset>().WithMany().HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ExternalAsset>().WithMany().HasForeignKey(x => x.CashAssetId).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Amount).HasPrecision(19, 4);
            b.Property(x => x.Ticker).HasMaxLength(20);
            b.Property(x => x.CurrencyCode).HasMaxLength(3);
            b.Property(x => x.CashAssetName).HasMaxLength(200);
            b.Property(x => x.Notes).HasMaxLength(500);
        }
    }
}
