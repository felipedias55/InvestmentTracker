using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestmentTracker.Infrastructure.Persistence.Configurations
{
    public sealed class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
    {
        public void Configure(EntityTypeBuilder<ExchangeRate> builder)
        {
            builder.ToTable("ExchangeRate", table => table.HasCheckConstraint("CK_ExchangeRate_Positive", "[Rate] > 0"));
            builder.HasKey(x => new { x.BaseCode, x.QuoteCode });
            builder.Property(x => x.BaseCode).HasMaxLength(3);
            builder.Property(x => x.QuoteCode).HasMaxLength(3);
            builder.Property(x => x.Rate).HasPrecision(28, 12);
        }
    }
}
