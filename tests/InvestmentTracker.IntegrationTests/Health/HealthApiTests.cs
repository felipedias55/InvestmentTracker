using System.Data.Common;
using System.Net;
using System.Text.Json;
using InvestmentTracker.Api.Health;
using InvestmentTracker.Infrastructure.Persistence;
using InvestmentTracker.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace InvestmentTracker.IntegrationTests.Health
{
    [Collection(DatabaseCollection.Name)]
    public sealed class HealthApiTests(DatabaseFixture fixture)
    {
        [Fact]
        public async Task HealthyDatabase_ShouldPreserveLivenessContractAndReportReadinessWithoutCaching()
        {
            using var client = fixture.ApiFactory.CreateClient();
            foreach (var path in new[] { "/api/health", "/api/health/live", "/api/health/ready" })
            {
                using var response = await client.GetAsync(path);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.True(response.Headers.CacheControl?.NoStore);
                using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Assert.Equal("ok", payload.RootElement.GetProperty("status").GetString());
                Assert.Equal("InvestmentTracker.Api", payload.RootElement.GetProperty("application").GetString());
                if (path.EndsWith("ready"))
                    Assert.Equal("ok", payload.RootElement.GetProperty("checks").GetProperty("database").GetString());
                else Assert.False(payload.RootElement.TryGetProperty("checks", out _));
            }
        }

        [Fact]
        public async Task DatabaseFailureAfterStartup_ShouldReturn503KeepLivenessAndRecoverWithoutRestart()
        {
            var failure = new ConnectionFailure();
            using var factory = fixture.ApiFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.AddDbContext<InvestmentTrackerDbContext>(options => options.AddInterceptors(failure))));
            using var client = factory.CreateClient(); // Startup schema verification uses the temporary database normally.
            failure.Unavailable = true;
            using var response = await client.GetAsync("/api/health/ready");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            var text = await response.Content.ReadAsStringAsync();
            using var payload = JsonDocument.Parse(text);
            Assert.Equal("unavailable", payload.RootElement.GetProperty("status").GetString());
            Assert.Equal("unavailable", payload.RootElement.GetProperty("checks").GetProperty("database").GetString());
            Assert.DoesNotContain("private-database-details", text);
            Assert.True(response.Headers.CacheControl?.NoStore);
            var attempts = failure.Attempts;
            foreach (var path in new[] { "/api/health", "/api/health/live" })
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
            Assert.Equal(attempts, failure.Attempts);
            failure.Unavailable = false;
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health/ready")).StatusCode);
        }

        [Fact]
        public async Task TimedOutDependency_ShouldObserveCancellationAndReturn503WithoutInternalDetails()
        {
            var slow = new SlowCheck();
            using var factory = fixture.ApiFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.PostConfigure<HealthCheckServiceOptions>(options =>
                {
                    var check = options.Registrations.Single(entry => entry.Name == DatabaseHealthCheck.Name);
                    check.Factory = _ => slow;
                    check.Timeout = TimeSpan.FromMilliseconds(100);
                })));
            using var client = factory.CreateClient();
            using var response = await client.GetAsync("/api/health/ready");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.True(slow.Cancelled);
            var text = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Exception", text);
            using var payload = JsonDocument.Parse(text);
            Assert.Equal("unavailable", payload.RootElement.GetProperty("checks").GetProperty("database").GetString());
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health/live")).StatusCode);
        }

        private sealed class ConnectionFailure : DbConnectionInterceptor
        {
            public bool Unavailable { get; set; }
            public int Attempts { get; private set; }
            public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
                DbConnection connection, ConnectionEventData eventData, InterceptionResult result,
                CancellationToken cancellationToken = default)
            {
                Attempts++;
                if (Unavailable) throw new InvalidOperationException("private-database-details");
                return ValueTask.FromResult(result);
            }
        }

        private sealed class SlowCheck : IHealthCheck
        {
            public bool Cancelled { get; private set; }
            public async Task<HealthCheckResult> CheckHealthAsync(
                HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                { Cancelled = true; throw; }
                return HealthCheckResult.Healthy();
            }
        }
    }
}
