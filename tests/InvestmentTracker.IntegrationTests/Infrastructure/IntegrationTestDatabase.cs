using InvestmentTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvestmentTracker.IntegrationTests.Infrastructure
{
    public sealed class IntegrationTestDatabase : IAsyncDisposable
    {
        private readonly string _connectionString;
        public string ConnectionString => _connectionString;

        public IntegrationTestDatabase()
        {
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets<IntegrationTestDatabase>()
                .Build();

            _connectionString = configuration
                .GetConnectionString("TestDatabase")
                ?? throw new InvalidOperationException(
                    "A connection string 'TestDatabase' não foi configurada.");
        }

        public InvestmentTrackerDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<InvestmentTrackerDbContext>()
                .UseSqlServer(_connectionString)
                .Options;

            return new InvestmentTrackerDbContext(options);
        }

        public async Task InitializeAsync()
        {
            await using var context = CreateContext();

            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
        }

        public async Task ResetAsync()
        {
            await using var context = CreateContext();

            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
