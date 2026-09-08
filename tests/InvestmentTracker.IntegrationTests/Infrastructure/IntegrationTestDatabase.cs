using InvestmentTracker.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace InvestmentTracker.IntegrationTests.Infrastructure
{
    public sealed class IntegrationTestDatabase : IAsyncDisposable
    {
        private readonly string _databaseName = $"InvestmentTracker_Tests_{Guid.NewGuid():N}";
        public string ConnectionString { get; }

        public IntegrationTestDatabase()
        {
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets<IntegrationTestDatabase>()
                .AddEnvironmentVariables()
                .Build();
            var configured = configuration.GetConnectionString("TestDatabase")
                ?? throw new InvalidOperationException("A connection string 'TestDatabase' não foi configurada.");
            var builder = new SqlConnectionStringBuilder(configured)
            {
                InitialCatalog = _databaseName,
                AttachDBFilename = string.Empty
            };
            ConnectionString = builder.ConnectionString;
        }

        public InvestmentTrackerDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<InvestmentTrackerDbContext>()
                .UseSqlServer(ConnectionString).Options;
            return new InvestmentTrackerDbContext(options);
        }

        public async Task InitializeAsync()
        {
            await using var context = CreateContext();
            await context.Database.MigrateAsync();
        }

        public async Task ResetAsync()
        {
            VerifyDatabase();
            await using var context = CreateContext();
            // Only data in the newly generated database is reset; migrations are exercised on initialization.
            await context.Database.ExecuteSqlRawAsync("""
                DELETE FROM ExchangeRate;
                DELETE FROM PortfolioAsset;
                DELETE FROM CategoryAllocationTarget;
                DELETE FROM SectorAllocationTarget;
                DELETE FROM Asset;
                DELETE FROM Portfolio;
                DELETE FROM ExternalAsset;
                DELETE FROM AssetType;
                DELETE FROM Country;
                DELETE FROM Currency;
                DELETE FROM AssetCategory;
                DELETE FROM Sector;
                """);
        }

        public async ValueTask DisposeAsync()
        {
            VerifyDatabase();
            await using var context = CreateContext();
            await context.Database.EnsureDeletedAsync();
        }

        private void VerifyDatabase()
        {
            if (new SqlConnectionStringBuilder(ConnectionString).InitialCatalog != _databaseName)
                throw new InvalidOperationException("O banco de testes não corresponde ao banco isolado desta execução.");
        }
    }
}
