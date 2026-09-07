using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvestmentTracker.Infrastructure.Persistence.Configurations
{
    public class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
    {
        public void Configure(EntityTypeBuilder<Currency> builder)
        {
            builder.ToTable("Currency");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(Currency.CodeLength);

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(Currency.NameMaxLength);

            builder.Property(x => x.Symbol)
                .HasMaxLength(Currency.SymbolMaxLength);

            builder.HasIndex(x => x.Code)
                .IsUnique();
        }
    }
}
