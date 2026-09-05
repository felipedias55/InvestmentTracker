using InvestmentTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InvestmentTracker.IntegrationTests.Infrastructure
{
    public sealed class InvestmentTrackerApiFactory
    : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;

        public InvestmentTrackerApiFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        protected override void ConfigureWebHost(
            IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    service =>
                        service.ServiceType ==
                        typeof(DbContextOptions<InvestmentTrackerDbContext>));

                if (descriptor is not null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<InvestmentTrackerDbContext>(
                    options =>
                        options.UseSqlServer(_connectionString));
            });
        }
    }
}
