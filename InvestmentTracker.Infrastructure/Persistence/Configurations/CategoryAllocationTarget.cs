using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvestmentTracker.Infrastructure.Persistence.Configurations
{
    public class CategoryAllocationTargetConfiguration
    : IEntityTypeConfiguration<CategoryAllocationTarget>
    {
        public void Configure(EntityTypeBuilder<CategoryAllocationTarget> builder)
        {
            builder.ToTable("CategoryAllocationTarget", t => t.HasCheckConstraint("CK_CategoryAllocationTarget_Percentage", "[TargetPercentage] >= 0 AND [TargetPercentage] <= 1"));

            builder.HasKey(x => x.Id);

            builder.Property(x => x.TargetPercentage)
                .HasPrecision(9, 6)
                .IsRequired();

            builder.HasIndex(x => new
            {
                x.PortfolioId,
                x.AssetCategoryId
            })
            .IsUnique();

            builder.HasOne(x => x.Portfolio)
                .WithMany(x => x.CategoryAllocationTargets)
                .HasForeignKey(x => x.PortfolioId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.AssetCategory)
                .WithMany(x => x.AllocationTargets)
                .HasForeignKey(x => x.AssetCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
