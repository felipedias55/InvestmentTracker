using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestmentTracker.Infrastructure.Persistence.Configurations
{
    public sealed class FinancialMovementConfiguration : IEntityTypeConfiguration<FinancialMovement>
    {
        public void Configure(EntityTypeBuilder<FinancialMovement> b)
        {
            b.ToTable("FinancialMovement"); b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.PortfolioId, x.RequestId }).IsUnique();
            b.HasIndex(x => x.ReversalOfId).IsUnique().HasFilter("[ReversalOfId] IS NOT NULL");
            b.HasIndex(x => x.TradeId).IsUnique().HasFilter("[TradeId] IS NOT NULL");
            b.HasIndex(x => x.IncomeReceiptId).IsUnique().HasFilter("[IncomeReceiptId] IS NOT NULL");
            b.HasOne<Portfolio>().WithMany().HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Trade).WithMany().HasForeignKey(x => x.TradeId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.IncomeReceipt).WithMany().HasForeignKey(x => x.IncomeReceiptId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.ReversalOf).WithMany().HasForeignKey(x => x.ReversalOfId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Effects).WithOne().HasForeignKey(x => x.FinancialMovementId).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Kind).HasMaxLength(32); b.Property(x => x.Description).HasMaxLength(500);
            b.Property(x => x.CurrencyCode).HasMaxLength(3); b.Property(x => x.Amount).HasPrecision(19, 4);
        }
    }
    public sealed class MovementEffectConfiguration : IEntityTypeConfiguration<MovementEffect>
    {
        public void Configure(EntityTypeBuilder<MovementEffect> b)
        {
            b.ToTable("MovementEffect"); b.HasKey(x => x.Id);
            b.HasOne<PortfolioAsset>().WithMany().HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ExternalAsset>().WithMany().HasForeignKey(x => x.CashAssetId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Currency>().WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Name).HasMaxLength(200);
            b.Property(x => x.BeforeValue).HasPrecision(19, 4); b.Property(x => x.AfterValue).HasPrecision(19, 4);
            b.Property(x => x.BeforeQuantity).HasPrecision(19, 6); b.Property(x => x.AfterQuantity).HasPrecision(19, 6);
            b.Property(x => x.BeforeCost).HasPrecision(19, 4); b.Property(x => x.AfterCost).HasPrecision(19, 4);
            b.Property(x => x.BeforeIncome).HasPrecision(19, 4); b.Property(x => x.AfterIncome).HasPrecision(19, 4);
        }
    }
}
