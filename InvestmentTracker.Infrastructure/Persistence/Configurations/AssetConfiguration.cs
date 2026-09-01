using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvestmentTracker.Infrastructure.Persistence.Configurations
{
    public class AssetConfiguration : IEntityTypeConfiguration<Asset>
    {
        public void Configure(EntityTypeBuilder<Asset> builder)
        {
            builder.ToTable("Asset");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Ticker)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.HasIndex(x => x.Ticker)
                .IsUnique();

            builder.HasOne(x => x.AssetType)
                .WithMany(x => x.Assets)
                .HasForeignKey(x => x.AssetTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Country)
                .WithMany(x => x.Assets)
                .HasForeignKey(x => x.CountryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Currency)
                .WithMany(x => x.Assets)
                .HasForeignKey(x => x.CurrencyId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.AssetCategory)
                .WithMany(x => x.Assets)
                .HasForeignKey(x => x.AssetCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Sector)
                .WithMany(x => x.Assets)
                .HasForeignKey(x => x.SectorId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
