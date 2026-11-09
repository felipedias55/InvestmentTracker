using System.Net;
using System.Net.Http.Json;
using InvestmentTracker.Application.Assets;
using InvestmentTracker.Application.Assets.Dtos;
using InvestmentTracker.Domain.Entities;
using InvestmentTracker.Infrastructure.Persistence;
using InvestmentTracker.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InvestmentTracker.IntegrationTests.Catalogs
{
    [Collection(DatabaseCollection.Name)]
    public class AssetDiscoveryApiTests(DatabaseFixture fixture)
    {
        private sealed class Provider : IAssetDiscoveryProvider
        {
            public int Calls { get; private set; }
            public Task<AssetDiscoveryResult> SearchAsync(string market, string query, CancellationToken ct) { Calls++; return Task.FromResult(new AssetDiscoveryResult("Synthetic", "available", "Review", [new(query, "Synthetic", market, "USD", null, null, null, false)])); }
            public Task<SuggestedAssetImage> ImageAsync(string market, string symbol, CancellationToken ct) => Task.FromResult(new SuggestedAssetImage("unavailable", "Synthetic"));
        }
        [Fact]
        public async Task SearchIsReadOnlyAndRejectsInvalidMarketsBeforeCallingProvider()
        {
            await fixture.ResetAsync(); var provider = new Provider();
            await using var factory = fixture.ApiFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<IAssetDiscoveryProvider>(provider)));
            using var client = factory.CreateClient();
            var result = await client.GetFromJsonAsync<AssetDiscoveryResult>("/api/asset-discovery?market=US&query=test"); Assert.Equal("TEST", Assert.Single(result!.Items).Symbol);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/asset-discovery?market=XX&query=TEST")).StatusCode); Assert.Equal(1, provider.Calls);
            await using var db = fixture.Database.CreateContext(); Assert.Empty(await db.Assets.ToListAsync()); Assert.Empty(await db.Portfolios.ToListAsync()); Assert.Empty(await db.Countries.ToListAsync());
        }
        [Fact]
        public async Task ReferenceDefaultsAreIdempotentAndPreserveEquivalentCustomRecords()
        {
            await fixture.ResetAsync(); int countryId, currencyId;
            await using (var db = fixture.Database.CreateContext())
            {
                var country = new Country { Name = "Brazil" }; var currency = new Currency { Name = "Meu real", Code = "BRL", Symbol = "R" }; db.AddRange(country, currency); await db.SaveChangesAsync(); countryId = country.Id; currencyId = currency.Id;
            }
            using var client = fixture.ApiFactory.CreateClient(); var first = await client.PostAsJsonAsync("/api/reference-defaults", new { }); first.EnsureSuccessStatusCode();
            var created = await first.Content.ReadFromJsonAsync<ReferenceDefaultsResult>(); Assert.Equal(1, created!.CountriesAdded); Assert.Equal(1, created.CurrenciesAdded);
            var second = await (await client.PostAsJsonAsync("/api/reference-defaults", new { })).Content.ReadFromJsonAsync<ReferenceDefaultsResult>(); Assert.Equal(0, second!.CountriesAdded); Assert.Equal(0, second.CurrenciesAdded);
            await using var check = fixture.Database.CreateContext(); Assert.Equal("Brazil", (await check.Countries.FindAsync(countryId))!.Name); Assert.Equal("R", (await check.Currencies.FindAsync(currencyId))!.Symbol); Assert.Empty(await check.Portfolios.ToListAsync()); Assert.Empty(await check.Assets.ToListAsync()); Assert.Equal(2, await check.Countries.CountAsync());
        }
        [Fact]
        public async Task ListingIsOptionalPreservedForOldClientsAndValidatedAtomically()
        {
            await fixture.ResetAsync(); CreateAssetDto input;
            await using (var db = fixture.Database.CreateContext())
            {
                await CatalogSeed.ApplyAsync(db); input = new("MYLOCAL", "Ativo", (await db.AssetTypes.FirstAsync()).Id, (await db.Countries.FirstAsync()).Id, (await db.Currencies.SingleAsync(c => c.Code == "USD")).Id, (await db.AssetCategories.FirstAsync()).Id, (await db.Sectors.FirstAsync()).Id) { Listing = new("US", "BRK.B") };
            }
            using var client = fixture.ApiFactory.CreateClient(); var create = await client.PostAsJsonAsync("/api/assets", input); create.EnsureSuccessStatusCode(); var asset = (await create.Content.ReadFromJsonAsync<AssetDto>())!;
            Assert.Equal(input.Listing, asset.Listing);
            var update = new UpdateAssetDto(input.Ticker, "Nome confirmado", input.AssetTypeId, input.CountryId, input.CurrencyId, input.AssetCategoryId, input.SectorId);
            (await client.PutAsJsonAsync($"/api/assets/{asset.Id}", update)).EnsureSuccessStatusCode();
            Assert.Equal(input.Listing, (await client.GetFromJsonAsync<AssetDto>($"/api/assets/{asset.Id}"))!.Listing);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/assets/{asset.Id}", update with { Name = "Não aplicar", Listing = new("XX", "INVALID") })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/assets/{asset.Id}", update with { Name = "Não aplicar", Listing = new("BR", "TEST3") })).StatusCode);
            var preserved = (await client.GetFromJsonAsync<AssetDto>($"/api/assets/{asset.Id}"))!; Assert.Equal("Nome confirmado", preserved.Name);
            (await client.PutAsJsonAsync($"/api/assets/{asset.Id}", update with { Listing = new("", "") })).EnsureSuccessStatusCode();
            Assert.Null((await client.GetFromJsonAsync<AssetDto>($"/api/assets/{asset.Id}"))!.Listing);
            await using var check = fixture.Database.CreateContext(); Assert.Empty(await check.PortfolioAssets.ToListAsync()); Assert.Empty(await check.Set<FinancialMovement>().ToListAsync()); Assert.False(check.Database.HasPendingModelChanges());
        }
    }
}
