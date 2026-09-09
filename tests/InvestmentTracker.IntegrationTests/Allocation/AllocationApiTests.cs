using System.Net;
using System.Net.Http.Json;
using InvestmentTracker.Application.Allocation.Dtos;
using InvestmentTracker.Application.ExchangeRates.Interfaces;
using InvestmentTracker.Application.Portfolios.Dtos;
using InvestmentTracker.Domain.Entities;
using InvestmentTracker.Infrastructure.Persistence;
using InvestmentTracker.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InvestmentTracker.IntegrationTests.Allocation
{
    [Collection(DatabaseCollection.Name)]
    public class AllocationApiTests(DatabaseFixture fixture)
    {
        private sealed class UnavailableProvider : IExchangeRateProvider
        {
            public Task<ExchangeRate?> FetchAsync(string b, string q, CancellationToken ct) => Task.FromResult<ExchangeRate?>(null);
        }

        private async Task<(int Id, int OtherId, int[] Categories, int[] Sectors)> SeedAsync()
        {
            await fixture.ResetAsync();
            await using var db = fixture.Database.CreateContext();
            await CatalogSeed.ApplyAsync(db);
            var portfolio = await db.Portfolios.SingleAsync();
            var other = new Portfolio { Name = "Outra", BaseCurrencyId = portfolio.BaseCurrencyId, CreatedAt = DateTime.UtcNow };
            db.Portfolios.Add(other);
            await db.SaveChangesAsync();
            return (portfolio.Id, other.Id, await db.AssetCategories.OrderBy(c => c.Id).Select(c => c.Id).ToArrayAsync(),
                await db.Sectors.OrderBy(c => c.Id).Select(c => c.Id).ToArrayAsync());
        }
        private static SaveTargetsDto Targets(int first, int second, decimal percentage = 0.3m)
            => new([new(first, percentage), new(second, 1m - percentage)]);

        [Theory]
        [InlineData("category")]
        [InlineData("sector")]
        public async Task Targets_ShouldRoundTripReplaceValidateAndStayWithinPortfolio(string dimension)
        {
            var (id, other, categories, sectors) = await SeedAsync();
            var groups = dimension == "category" ? categories : sectors;
            using var client = fixture.ApiFactory.CreateClient();
            var path = $"/api/portfolios/{id}/{dimension}-targets";
            Assert.Empty((await client.GetFromJsonAsync<AllocationTargetDto[]>(path))!);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(path, Targets(groups[0], groups[1], 0.333333m))).StatusCode);
            var loaded = (await client.GetFromJsonAsync<AllocationTargetDto[]>(path))!;
            Assert.Equal(0.333333m, loaded.Single(t => t.GroupId == groups[0]).TargetPercentage);
            Assert.Equal(1m, loaded.Sum(t => t.TargetPercentage));
            Assert.Empty((await client.GetFromJsonAsync<AllocationTargetDto[]>($"/api/portfolios/{other}/{dimension}-targets"))!);
            foreach (var invalid in new SaveTargetsDto[] {
                new([]), new([new(groups[0], 0.4m)]), new([new(groups[0], 0.5m), new(groups[0], 0.5m)]),
                new([new(int.MaxValue, 1m)]), new([new(groups[0], -1m), new(groups[1], 2m)]),
                new([new(groups[0], 0.3333333m), new(groups[1], 0.6666667m)]), new(null!) })
            {
                var response = await client.PutAsJsonAsync(path, invalid);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
                Assert.Equal(loaded.Select(t => t.TargetPercentage), (await client.GetFromJsonAsync<AllocationTargetDto[]>(path))!.Select(t => t.TargetPercentage));
            }
            Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(path, new SaveTargetsDto([new(groups[1], 1m)]))).StatusCode);
            Assert.Equal(groups[1], Assert.Single((await client.GetFromJsonAsync<AllocationTargetDto[]>(path))!).GroupId);
            var catalogPath = dimension == "category" ? "asset-categories" : "sectors";
            Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/{catalogPath}/{groups[1]}")).StatusCode);
        }

        [Fact]
        public async Task DashboardAndContribution_ShouldUseCurrencyConversionAndPreservePositions()
        {
            var (id, _, categories, sectors) = await SeedAsync();
            await using (var db = fixture.Database.CreateContext())
            {
                var brl = await db.Currencies.SingleAsync(c => c.Code == "BRL");
                var usd = await db.Currencies.SingleAsync(c => c.Code == "USD");
                var countries = await db.Countries.OrderBy(c => c.Id).ToArrayAsync();
                var type = await db.AssetTypes.FirstAsync();
                var a = new Asset { Ticker = "AAA", Name = "Em reais", AssetTypeId = type.Id, CountryId = countries[0].Id,
                    CurrencyId = brl.Id, AssetCategoryId = categories[0], SectorId = sectors[0], CreatedAt = DateTime.UtcNow };
                var b = new Asset { Ticker = "BBB", Name = "Em dólares", AssetTypeId = type.Id, CountryId = countries[1].Id,
                    CurrencyId = usd.Id, AssetCategoryId = categories[1], SectorId = sectors[1], CreatedAt = DateTime.UtcNow };
                db.PortfolioAssets.AddRange(
                    new PortfolioAsset { PortfolioId = id, Asset = a, Quantity = 1m, InvestedAmount = 50m, CurrentValue = 100m },
                    new PortfolioAsset { PortfolioId = id, Asset = b, Quantity = 1m, InvestedAmount = 10m, CurrentValue = 20m });
                db.ExchangeRates.Add(new ExchangeRate { BaseCode = "USD", QuoteCode = "BRL", Rate = 5m,
                    RateDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), FetchedAtUtc = DateTime.UtcNow });
                await db.SaveChangesAsync();
            }
            using var client = fixture.ApiFactory.CreateClient();
            (await client.PutAsJsonAsync($"/api/portfolios/{id}/category-targets", Targets(categories[0], categories[1], 0.75m))).EnsureSuccessStatusCode();
            var path = $"/api/portfolios/{id}";
            var before = (await client.GetFromJsonAsync<PortfolioSummaryDto>(path))!;
            var dashboard = (await client.GetFromJsonAsync<DashboardDto>(path + "/dashboard"))!;
            Assert.Equal(200m, dashboard.Summary.CurrentValue);
            Assert.Equal(100m, dashboard.Summary.TotalInvested);
            Assert.True(dashboard.Summary.HasStaleRates);
            Assert.All(dashboard.Allocation.Categories, r => Assert.Equal(0.5m, r.CurrentPercentage));
            Assert.Equal(2, dashboard.Allocation.Countries.Count);
            Assert.Equal(2, dashboard.Allocation.Sectors.Count);
            var response = await client.PostAsJsonAsync(path + "/contribution-analysis", new ContributionRequestDto("category", 1234.56m));
            response.EnsureSuccessStatusCode();
            var result = (await response.Content.ReadFromJsonAsync<ContributionAnalysisDto>())!;
            Assert.Equal(1234.56m, result.Rows.Single(r => r.GroupId == categories[0]).SuggestedContribution);
            Assert.Equal(0m, result.Rows.Single(r => r.GroupId == categories[1]).SuggestedContribution);
            Assert.True(result.HasStaleRates);
            Assert.Equal("BRL", result.CurrencyCode);
            var after = (await client.GetFromJsonAsync<PortfolioSummaryDto>(path))!;
            Assert.Equal(before.Positions, after.Positions);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path + "/contribution-analysis", new ContributionRequestDto("sector", 100m))).StatusCode);
            var allocation = (await client.GetFromJsonAsync<AllocationDto>(path + "/allocation"))!;
            Assert.Equal(dashboard.Allocation.Categories, allocation.Categories);
        }

        [Fact]
        public async Task MissingCurrencyRate_ShouldKeepTargetsVisibleButBlockPartialSimulation()
        {
            var (id, _, categories, sectors) = await SeedAsync();
            await using (var db = fixture.Database.CreateContext())
            {
                var asset = new Asset { Ticker = "USD", Name = "Sem cotação", AssetTypeId = (await db.AssetTypes.FirstAsync()).Id,
                    CurrencyId = (await db.Currencies.SingleAsync(c => c.Code == "USD")).Id, CountryId = (await db.Countries.FirstAsync()).Id,
                    AssetCategoryId = categories[0], SectorId = sectors[0], CreatedAt = DateTime.UtcNow };
                db.PortfolioAssets.Add(new PortfolioAsset { PortfolioId = id, Asset = asset, Quantity = 1m, CurrentValue = 1m });
                await db.SaveChangesAsync();
            }
            using var factory = fixture.ApiFactory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IExchangeRateProvider, UnavailableProvider>()));
            using var client = factory.CreateClient();
            (await client.PutAsJsonAsync($"/api/portfolios/{id}/category-targets", new SaveTargetsDto([new(categories[0], 1m)]))).EnsureSuccessStatusCode();
            var dashboard = (await client.GetFromJsonAsync<DashboardDto>($"/api/portfolios/{id}/dashboard"))!;
            var row = Assert.Single(dashboard.Allocation.Categories);
            Assert.Equal(1m, row.TargetPercentage);
            Assert.Null(row.CurrentValue); Assert.Null(row.CurrentPercentage); Assert.Null(row.Difference);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/portfolios/{id}/contribution-analysis", new ContributionRequestDto("category", 100m))).StatusCode);
        }

        [Fact]
        public async Task ParallelReplacements_ShouldPersistOneCompleteSet()
        {
            var (id, _, categories, _) = await SeedAsync();
            using var client = fixture.ApiFactory.CreateClient();
            var path = $"/api/portfolios/{id}/category-targets";
            var responses = await Task.WhenAll(
                client.PutAsJsonAsync(path, Targets(categories[0], categories[1], 0.25m)),
                client.PutAsJsonAsync(path, Targets(categories[1], categories[2], 0.4m)));
            Assert.All(responses, r => Assert.Equal(HttpStatusCode.NoContent, r.StatusCode));
            var targets = (await client.GetFromJsonAsync<AllocationTargetDto[]>(path))!;
            Assert.Equal(2, targets.Length);
            Assert.Equal(1m, targets.Sum(t => t.TargetPercentage));
            Assert.True(targets.Any(t => t.TargetPercentage == 0.25m) || targets.Any(t => t.TargetPercentage == 0.4m));
        }

        [Fact]
        public async Task Database_ShouldRejectOutOfRangeTarget()
        {
            var (id, _, categories, _) = await SeedAsync();
            await using var db = fixture.Database.CreateContext();
            db.CategoryAllocationTargets.Add(new CategoryAllocationTarget { PortfolioId = id, AssetCategoryId = categories[0], TargetPercentage = 1.1m });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        [Fact]
        public async Task MissingPortfolio_ShouldReturnNotFoundForAllAnalysisRoutes()
        {
            await SeedAsync();
            using var client = fixture.ApiFactory.CreateClient();
            var path = "/api/portfolios/2147483647";
            foreach (var route in new[] { "category-targets", "sector-targets", "dashboard", "allocation" })
                Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path + "/" + route)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(path + "/contribution-analysis", new ContributionRequestDto("category", 100m))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(path + "/category-targets", new SaveTargetsDto([new(1, 1m)]))).StatusCode);
        }
    }
}
