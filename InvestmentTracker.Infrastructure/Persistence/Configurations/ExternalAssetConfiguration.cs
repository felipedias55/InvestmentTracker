using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvestmentTracker.Infrastructure.Persistence.Configurations
{
    public class ExternalAssetConfiguration
    : IEntityTypeConfiguration<ExternalAsset>
    {
        public void Configure(EntityTypeBuilder<ExternalAsset> builder)
        {
            builder.ToTable("ExternalAsset", t => t.HasCheckConstraint("CK_ExternalAsset_Value", "[Value] >= 0"));

            builder.HasKey(x => x.Id);
            builder.Property(x => x.Value).IsConcurrencyToken();
            builder.Property(x => x.UpdatedOn).HasColumnType("date").IsRequired();
            builder.HasOne(x => x.Portfolio).WithMany().HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.Currency).WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(x => x.Value)
                .HasPrecision(19, 4)
                .IsRequired();

            builder.Property(x => x.Description)
                .HasMaxLength(500);
        }
    }
}
