using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestmentTracker.Infrastructure.Persistence.Configurations
{
    public sealed class AssetImageConfiguration : IEntityTypeConfiguration<AssetImage>
    {
        public void Configure(EntityTypeBuilder<AssetImage> b)
        {
            b.ToTable("AssetImage", t => t.HasCheckConstraint("CK_AssetImage_Size", "DATALENGTH([Data]) > 0 AND DATALENGTH([Data]) <= 262144"));
            b.HasKey(x => x.AssetId);
            b.Property(x => x.Data).HasMaxLength(262144).IsRequired();
            b.HasOne(x => x.Asset).WithOne().HasForeignKey<AssetImage>(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
