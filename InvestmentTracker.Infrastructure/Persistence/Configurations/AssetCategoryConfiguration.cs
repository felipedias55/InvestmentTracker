using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvestmentTracker.Infrastructure.Persistence.Configurations
{
    public class AssetCategoryConfiguration
    : IEntityTypeConfiguration<AssetCategory>
    {
        public void Configure(EntityTypeBuilder<AssetCategory> builder)
        {
            builder.ToTable("AssetCategory");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.HasIndex(x => x.Name)
                .IsUnique();
        }
    }
}
