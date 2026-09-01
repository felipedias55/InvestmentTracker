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
            builder.ToTable("ExternalAsset");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(x => x.Value)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(x => x.Description)
                .HasMaxLength(500);
        }
    }
}
