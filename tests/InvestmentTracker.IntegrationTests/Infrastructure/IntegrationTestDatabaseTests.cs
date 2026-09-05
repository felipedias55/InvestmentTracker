using System;
using System.Collections.Generic;
using System.Text;
using InvestmentTracker.IntegrationTests.Infrastructure;


namespace InvestmentTracker.IntegrationTests.Infrastructure
{
    [Collection(DatabaseCollection.Name)]
    public class IntegrationTestDatabaseTests
    {
        private readonly DatabaseFixture _fixture;

        public IntegrationTestDatabaseTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Database_ShouldBeCreated()
        {
            await using var context = _fixture.Database.CreateContext();

            var canConnect = await context.Database.CanConnectAsync();

            Assert.True(canConnect);
        }
    }
}
