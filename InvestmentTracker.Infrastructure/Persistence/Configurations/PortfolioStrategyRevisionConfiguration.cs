using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestmentTracker.Infrastructure.Persistence.Configurations
{
    public sealed class PortfolioStrategyRevisionConfiguration : IEntityTypeConfiguration<PortfolioStrategyRevision>
    {
        public void Configure(EntityTypeBuilder<PortfolioStrategyRevision> builder)
        {
            builder.ToTable("PortfolioStrategyRevision");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.PortfolioId, x.ProfileId, x.Revision }).IsUnique();
            builder.Property(x => x.ConfigurationJson).IsRequired();
            builder.HasOne<Portfolio>().WithMany().HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
