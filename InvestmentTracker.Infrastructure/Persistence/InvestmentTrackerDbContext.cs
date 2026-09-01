using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvestmentTracker.Infrastructure.Persistence;

public class InvestmentTrackerDbContext : DbContext
{
    public InvestmentTrackerDbContext(
        DbContextOptions<InvestmentTrackerDbContext> options)
        : base(options)
    {
    }

    public DbSet<Asset> Assets => Set<Asset>();

    public DbSet<AssetType> AssetTypes => Set<AssetType>();

    public DbSet<Country> Countries => Set<Country>();

    public DbSet<Currency> Currencies => Set<Currency>();

    public DbSet<AssetCategory> AssetCategories => Set<AssetCategory>();

    public DbSet<Sector> Sectors => Set<Sector>();

    public DbSet<Portfolio> Portfolios => Set<Portfolio>();

    public DbSet<PortfolioAsset> PortfolioAssets => Set<PortfolioAsset>();

    public DbSet<CategoryAllocationTarget> CategoryAllocationTargets =>
        Set<CategoryAllocationTarget>();

    public DbSet<SectorAllocationTarget> SectorAllocationTargets =>
        Set<SectorAllocationTarget>();

    public DbSet<ExternalAsset> ExternalAssets => Set<ExternalAsset>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(InvestmentTrackerDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}