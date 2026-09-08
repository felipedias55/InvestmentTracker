using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InvestmentTracker.Application.Assets.Dtos;
using InvestmentTracker.Domain.Entities;
using InvestmentTracker.Infrastructure.Persistence;
using InvestmentTracker.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace InvestmentTracker.IntegrationTests.Catalogs
{
    [Collection(DatabaseCollection.Name)]
    public class CatalogApiTests(DatabaseFixture fixture)
    {
        [Theory]
        [InlineData("countries")]
        [InlineData("asset-categories")]
        [InlineData("sectors")]
        [InlineData("currencies")]
        public async Task Catalog_ShouldSupportCrudAndRejectDuplicate(string route)
        {
            await fixture.ResetAsync();
            using var client = fixture.ApiFactory.CreateClient();
            var path = $"/api/{route}";
            var data = new { name = " Primeiro ", code = " brl ", symbol = " R$ " };
            using var created = await client.PostAsJsonAsync(path, data);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            Assert.NotNull(created.Headers.Location);
            var item = await created.Content.ReadFromJsonAsync<JsonElement>();
            var id = item.GetProperty("id").GetInt32();
            Assert.Equal("Primeiro", item.GetProperty("name").GetString());
            if (route == "currencies")
                Assert.Equal("BRL", item.GetProperty("code").GetString());
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(created.Headers.Location)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, data)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"{path}/{id}",
                new { name = "Alterado", code = "BRL", symbol = (string?)null })).StatusCode);
            var persisted = await client.GetFromJsonAsync<JsonElement>($"{path}/{id}");
            Assert.Equal("Alterado", persisted.GetProperty("name").GetString());
            Assert.Single((await client.GetFromJsonAsync<JsonElement[]>(path))!);
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{path}/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{path}/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"{path}/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"{path}/{id}", data)).StatusCode);
        }

        [Theory]
        [InlineData("countries")]
        [InlineData("asset-categories")]
        [InlineData("sectors")]
        [InlineData("currencies")]
        public async Task Catalog_ShouldRejectInvalidNamesAndDuplicateUpdate(string route)
        {
            await fixture.ResetAsync();
            using var client = fixture.ApiFactory.CreateClient();
            var path = $"/api/{route}";
            foreach (var name in new[] { null, "", "   ", new string('a', 101) })
            {
                var response = await client.PostAsJsonAsync(path, new { name, code = "BRL" });
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }
            var first = await client.PostAsJsonAsync(path, new { name = "Primeiro", code = "BRL" });
            var second = await client.PostAsJsonAsync(path, new { name = "Segundo", code = "USD" });
            second.EnsureSuccessStatusCode();
            var responseUpdate = await client.PutAsJsonAsync(second.Headers.Location,
                new { name = "Primeiro", code = "BRL" });
            Assert.Equal(HttpStatusCode.Conflict, responseUpdate.StatusCode);
            var unchanged = await client.GetFromJsonAsync<JsonElement>(second.Headers.Location);
            Assert.Equal("Segundo", unchanged.GetProperty("name").GetString());
            first.EnsureSuccessStatusCode();
        }

        [Fact]
        public async Task Asset_ShouldSupportCrudWithIndependentCountryAndCurrency()
        {
            await fixture.ResetAsync();
            await using var context = fixture.Database.CreateContext();
            await CatalogSeed.ApplyAsync(context);
            var request = await RequestAsync(context);
            using var client = fixture.ApiFactory.CreateClient();
            var created = await client.PostAsJsonAsync("/api/assets", request);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var asset = (await created.Content.ReadFromJsonAsync<AssetDto>())!;
            Assert.Equal("TEST3", asset.Ticker);
            Assert.NotEqual(default, asset.CreatedAt);
            Assert.Equal(request.CountryId, asset.CountryId);
            Assert.Equal(request.CurrencyId, asset.CurrencyId);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/assets", request)).StatusCode);
            var update = new UpdateAssetDto("test4", "Alterado", asset.AssetTypeId, asset.CountryId,
                asset.CurrencyId, asset.AssetCategoryId, asset.SectorId);
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(created.Headers.Location, update)).StatusCode);
            var saved = (await client.GetFromJsonAsync<AssetDto>(created.Headers.Location))!;
            Assert.Equal("TEST4", saved.Ticker);
            Assert.Equal(asset.CreatedAt, saved.CreatedAt);
            Assert.Single((await client.GetFromJsonAsync<AssetDto[]>("/api/assets"))!);
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(created.Headers.Location)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(created.Headers.Location)).StatusCode);
        }

        [Theory]
        [InlineData("type")]
        [InlineData("country")]
        [InlineData("currency")]
        [InlineData("category")]
        [InlineData("sector")]
        public async Task Asset_ShouldRejectMissingReferences(string field)
        {
            await fixture.ResetAsync();
            await using var context = fixture.Database.CreateContext();
            await CatalogSeed.ApplyAsync(context);
            var request = await RequestAsync(context);
            request = field switch
            {
                "type" => request with { AssetTypeId = int.MaxValue },
                "country" => request with { CountryId = int.MaxValue },
                "currency" => request with { CurrencyId = int.MaxValue },
                "category" => request with { AssetCategoryId = int.MaxValue },
                _ => request with { SectorId = int.MaxValue }
            };
            using var client = fixture.ApiFactory.CreateClient();
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/assets", request)).StatusCode);
            Assert.Empty(await context.Assets.ToListAsync());
        }

        [Fact]
        public async Task ReferencedCatalogsAndAsset_ShouldRejectDeletion()
        {
            await fixture.ResetAsync();
            await using var context = fixture.Database.CreateContext();
            await CatalogSeed.ApplyAsync(context);
            var request = await RequestAsync(context);
            using var client = fixture.ApiFactory.CreateClient();
            var created = await client.PostAsJsonAsync("/api/assets", request);
            created.EnsureSuccessStatusCode();
            var asset = (await created.Content.ReadFromJsonAsync<AssetDto>())!;
            foreach (var path in new[] { $"asset-types/{asset.AssetTypeId}", $"countries/{asset.CountryId}",
                $"currencies/{asset.CurrencyId}", $"asset-categories/{asset.AssetCategoryId}", $"sectors/{asset.SectorId}" })
            {
                var response = await client.DeleteAsync($"/api/{path}");
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/{path}")).StatusCode);
            }
            context.PortfolioAssets.Add(new PortfolioAsset
            {
                AssetId = asset.Id, Portfolio = new Portfolio { Name = "Teste", CreatedAt = DateTime.UtcNow, BaseCurrencyId = asset.CurrencyId }
            });
            await context.SaveChangesAsync();
            Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync(created.Headers.Location)).StatusCode);
        }

        [Fact]
        public async Task Seed_ShouldBeRepeatableAndPreserveExistingRecords()
        {
            await fixture.ResetAsync();
            await using var context = fixture.Database.CreateContext();
            context.Currencies.Add(new Currency { Code = "BRL", Name = "Nome personalizado" });
            await context.SaveChangesAsync();
            await CatalogSeed.ApplyAsync(context);
            var originalIds = await context.Countries.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync();
            await CatalogSeed.ApplyAsync(context);
            Assert.Equal(originalIds, await context.Countries.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync());
            Assert.Equal(3, await context.AssetTypes.CountAsync());
            Assert.Equal(2, await context.Countries.CountAsync());
            Assert.Equal(2, await context.Currencies.CountAsync());
            Assert.Equal(4, await context.AssetCategories.CountAsync());
            Assert.Equal(8, await context.Sectors.CountAsync());
            Assert.Equal("Nome personalizado", (await context.Currencies.SingleAsync(x => x.Code == "BRL")).Name);
            Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync());
        }

        private static async Task<CreateAssetDto> RequestAsync(InvestmentTrackerDbContext context)
        {
            return new CreateAssetDto(" test3 ", " Ativo de teste ",
                (await context.AssetTypes.FirstAsync()).Id,
                (await context.Countries.SingleAsync(x => x.Name == "Brasil")).Id,
                (await context.Currencies.SingleAsync(x => x.Code == "USD")).Id,
                (await context.AssetCategories.FirstAsync()).Id,
                (await context.Sectors.FirstAsync()).Id);
        }
    }
}
