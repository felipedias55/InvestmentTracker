using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestmentTracker.Infrastructure.Persistence.Configurations
{
    public sealed class IncomeConversionConfiguration : IEntityTypeConfiguration<IncomeConversion>
    {
        public void Configure(EntityTypeBuilder<IncomeConversion> b)
        {
            b.ToTable("IncomeConversion", t => t.HasCheckConstraint("CK_IncomeConversion_Values", "[BaseAmount] > 0 AND [Revision] > 0 AND ([Rate] IS NULL OR [Rate] > 0)"));
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.IncomeReceiptId, x.BaseCurrencyCode, x.Revision }).IsUnique();
            b.HasIndex(x => x.RequestId).IsUnique();
            b.Property(x => x.BaseCurrencyCode).HasMaxLength(3);
            b.Property(x => x.BaseAmount).HasPrecision(19, 4);
            b.Property(x => x.Rate).HasPrecision(38, 18);
            b.Property(x => x.Source).HasMaxLength(32);
            b.Property(x => x.Reason).HasMaxLength(400);
        }
    }
}
