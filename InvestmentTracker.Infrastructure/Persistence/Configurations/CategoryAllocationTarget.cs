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
            builder.ToTable("CategoryAllocationTarget");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.TargetPercentage)
                .HasPrecision(5, 4)
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
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.AssetCategory)
                .WithMany(x => x.AllocationTargets)
                .HasForeignKey(x => x.AssetCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
