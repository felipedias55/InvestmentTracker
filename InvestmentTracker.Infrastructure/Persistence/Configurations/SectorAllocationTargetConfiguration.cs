using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvestmentTracker.Infrastructure.Persistence.Configurations
{
    public class SectorAllocationTargetConfiguration
    : IEntityTypeConfiguration<SectorAllocationTarget>
    {
        public void Configure(EntityTypeBuilder<SectorAllocationTarget> builder)
        {
            builder.ToTable("SectorAllocationTarget");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.TargetPercentage)
                .HasPrecision(5, 4)
                .IsRequired();

            builder.HasIndex(x => new
            {
                x.PortfolioId,
                x.SectorId
            })
            .IsUnique();

            builder.HasOne(x => x.Portfolio)
                .WithMany(x => x.SectorAllocationTargets)
                .HasForeignKey(x => x.PortfolioId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Sector)
                .WithMany(x => x.AllocationTargets)
                .HasForeignKey(x => x.SectorId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
