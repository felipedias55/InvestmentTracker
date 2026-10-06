using System.Net;
using System.Net.Http.Json;
using InvestmentTracker.Application.Portfolios;
using InvestmentTracker.Domain.Entities;
using InvestmentTracker.Infrastructure.Persistence;
using InvestmentTracker.Infrastructure.Persistence.Repositories;
using InvestmentTracker.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace InvestmentTracker.IntegrationTests.Portfolios
{
    [Collection(DatabaseCollection.Name)]
    public class QuoteBatchApiTests(DatabaseFixture fixture)
    {
        private async Task<(int Portfolio, QuoteBatchDto Batch)> Seed()
        {
            await fixture.ResetAsync();
            await using var db = fixture.Database.CreateContext();
            await CatalogSeed.ApplyAsync(db);
            var portfolio = await db.Portfolios.SingleAsync();
            var type = await db.AssetTypes.FirstAsync(); var country = await db.Countries.FirstAsync();
            var category = await db.AssetCategories.FirstAsync(); var sector = await db.Sectors.FirstAsync();
            var currencies = await db.Currencies.OrderBy(x => x.Id).Take(2).ToListAsync();
            var positions = currencies.Select((currency, i) => new PortfolioAsset
            {
                PortfolioId = portfolio.Id, Quantity = 2, CurrentValue = 20, InvestedAmount = 15, Income = 3, UpdatedOn = new(2026, 9, 1),
                Asset = new Asset { Ticker = $"BATCH{i}", Name = "Synthetic quote test", CurrencyId = currency.Id,
                    AssetTypeId = type.Id, CountryId = country.Id, AssetCategoryId = category.Id, SectorId = sector.Id }
            }).ToList();
            db.PortfolioAssets.AddRange(positions); await db.SaveChangesAsync();
            var items = positions.Select((p, i) => new QuoteUpdate(p.Id, currencies[i].Code, 15, p.Quantity, p.CurrentValue, p.UpdatedOn)).ToList();
            return (portfolio.Id, new(Guid.NewGuid(), items));
        }
        [Fact]
        public async Task Batch_UpdatesOnlyValuationsAndRecordsAuditOnceEvenOnRetry()
        {
            var (portfolio, batch) = await Seed(); using var client = fixture.ApiFactory.CreateClient();
            var path = $"/api/portfolios/{portfolio}/quotes";
            Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(path, batch)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(path, batch)).StatusCode);
            await using var db = fixture.Database.CreateContext();
            Assert.All(await db.PortfolioAssets.ToListAsync(), p => { Assert.Equal(30, p.CurrentValue); Assert.Equal(2, p.Quantity); Assert.Equal(15, p.InvestedAmount); Assert.Equal(3, p.Income); });
            var movement = Assert.Single(await db.Set<FinancialMovement>().Include(x => x.Effects).ToListAsync());
            Assert.Equal(2, movement.Effects.Count); Assert.All(movement.Effects, e => { Assert.Equal(20, e.BeforeValue); Assert.Equal(30, e.AfterValue); });
            Assert.Empty(await db.PortfolioCashFlows.ToListAsync()); Assert.Empty(await db.Set<PortfolioTrade>().ToListAsync());
        }
        [Fact]
        public async Task StaleSecondPosition_RejectsTheEntireBatch()
        {
            var (portfolio, batch) = await Seed(); using var client = fixture.ApiFactory.CreateClient();
            var items = batch.Items.ToArray(); items[1] = items[1] with { ExpectedQuantity = 3 };
            Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/portfolios/{portfolio}/quotes", batch with { Items = items })).StatusCode);
            await using var db = fixture.Database.CreateContext();
            Assert.All(await db.PortfolioAssets.ToListAsync(), p => Assert.Equal(20, p.CurrentValue));
            Assert.Empty(await db.Set<FinancialMovement>().ToListAsync());
        }
        [Fact]
        public async Task SameDaySnapshot_IsFlaggedAndDifferentCurrencyIsRejected()
        {
            var (portfolio, batch) = await Seed(); using var client = fixture.ApiFactory.CreateClient();
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);
            await using (var db = fixture.Database.CreateContext())
            {
                db.PortfolioSnapshots.Add(new PortfolioSnapshot { PortfolioId = portfolio, Month = new(today.Year, today.Month, 1), SnapshotDate = today,
                    TotalWealth = 40, BaseCurrencyCode = "BRL", DashboardJson = "{}" }); await db.SaveChangesAsync();
            }
            var invalid = batch with { Items = [batch.Items[0] with { CurrencyCode = "XXX" }] };
            Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/portfolios/{portfolio}/quotes", invalid)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/portfolios/{portfolio}/quotes", batch)).StatusCode);
            await using var check = fixture.Database.CreateContext();
            var photo = await check.PortfolioSnapshots.SingleAsync(); Assert.True(photo.IsOutdated); Assert.Equal(40, photo.TotalWealth); Assert.Equal("{}", photo.DashboardJson);
        }
        [Fact]
        public async Task ConcurrentConfirmations_KeepOneAuditRecord()
        {
            var (portfolio, batch) = await Seed(); using var client = fixture.ApiFactory.CreateClient();
            var responses = await Task.WhenAll(client.PutAsJsonAsync($"/api/portfolios/{portfolio}/quotes", batch), client.PutAsJsonAsync($"/api/portfolios/{portfolio}/quotes", batch));
            Assert.All(responses, r => Assert.Equal(HttpStatusCode.NoContent, r.StatusCode));
            await using var db = fixture.Database.CreateContext(); Assert.Single(await db.Set<FinancialMovement>().ToListAsync());
        }
        private sealed class FailSecondSave : SaveChangesInterceptor
        {
            private int count;
            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                if (++count == 2) throw new InvalidOperationException("Synthetic failure after the first save");
                return base.SavingChangesAsync(eventData, result, cancellationToken);
            }
        }
        [Fact]
        public async Task FailureAfterFirstSave_RollsBackValuesAndAudit()
        {
            var (portfolio, batch) = await Seed();
            var options = new DbContextOptionsBuilder<InvestmentTrackerDbContext>().UseSqlServer(fixture.Database.ConnectionString).AddInterceptors(new FailSecondSave()).Options;
            await using (var failingDb = new InvestmentTrackerDbContext(options))
            {
                var repository = new QuoteBatchRepository(failingDb, TimeProvider.System);
                await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveAsync(portfolio, batch, new(2026, 10, 4), default));
            }
            await using var db = fixture.Database.CreateContext();
            Assert.All(await db.PortfolioAssets.ToListAsync(), p => Assert.Equal(20, p.CurrentValue));
            Assert.Empty(await db.Set<FinancialMovement>().ToListAsync()); Assert.Empty(await db.Set<MovementEffect>().ToListAsync());
        }
    }
}
