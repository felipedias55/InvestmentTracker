using Xunit;


namespace InvestmentTracker.IntegrationTests.Infrastructure
{
    [CollectionDefinition(Name)]
    public sealed class DatabaseCollection
    : ICollectionFixture<DatabaseFixture>
    {
        public const string Name = "Database collection";
    }
}
