using Xunit;

namespace InvestmentTracker.IntegrationTests.Infrastructure
{
    public sealed class DatabaseFixture : IAsyncLifetime
    {
        public IntegrationTestDatabase Database { get; } = new();

        public InvestmentTrackerApiFactory ApiFactory { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            await Database.InitializeAsync();

            ApiFactory = new InvestmentTrackerApiFactory(
                Database.ConnectionString);
        }

        public async Task DisposeAsync()
        {
            ApiFactory.Dispose();

            await Database.DisposeAsync();
        }

        public Task ResetAsync()
        {
            return Database.ResetAsync();
        }
    }
}