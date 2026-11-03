using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvestmentTracker.Infrastructure.Persistence.Configurations
{
    public class AssetTypeConfiguration : IEntityTypeConfiguration<AssetType>
    {
        public void Configure(EntityTypeBuilder<AssetType> builder)
        {
            builder.ToTable("AssetType", table => table.HasCheckConstraint("CK_AssetType_InstrumentClass",
                "[InstrumentClass] IN ('general', 'fii', 'reit', 'unclassified')"));
            builder.Property(x => x.InstrumentClass).HasMaxLength(20).IsRequired().HasDefaultValue(InstrumentClasses.Unclassified);

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(AssetType.NameMaxLength);

            builder.HasIndex(x => x.Name)
                .IsUnique();
        }
    }
}
