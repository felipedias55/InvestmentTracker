using InvestmentTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace InvestmentTracker.Api.Health
{
    // Operational availability only: no migrations, portfolio reads or external providers.
    public sealed class DatabaseHealthCheck(InvestmentTrackerDbContext database) : IHealthCheck
    {
        public const string Name = "database";
        public const string ReadinessTag = "ready";

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var available = await database.Database.CanConnectAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return available ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Banco indisponível.");
        }
    }
}
