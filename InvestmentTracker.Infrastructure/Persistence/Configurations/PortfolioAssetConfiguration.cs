using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvestmentTracker.Infrastructure.Persistence.Configurations
{
    public class PortfolioAssetConfiguration
    : IEntityTypeConfiguration<PortfolioAsset>
    {
        public void Configure(EntityTypeBuilder<PortfolioAsset> builder)
        {
            builder.ToTable("PortfolioAsset");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Quantity)
                .HasPrecision(18, 8)
                .IsRequired();

            builder.Property(x => x.InvestedAmount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(x => x.CurrentValue)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.HasIndex(x => new
            {
                x.PortfolioId,
                x.AssetId
            })
            .IsUnique();

            builder.HasOne(x => x.Portfolio)
                .WithMany(x => x.PortfolioAssets)
                .HasForeignKey(x => x.PortfolioId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Asset)
                .WithMany(x => x.PortfolioAssets)
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
