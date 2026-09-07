using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Domain.Entities;
using InvestmentTracker.Infrastructure.Persistence;
using InvestmentTracker.Infrastructure.Persistence.Repositories;
using InvestmentTracker.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace InvestmentTracker.IntegrationTests.Catalogs
{
    [Collection(DatabaseCollection.Name)]
    public class CatalogRepositoryTests(DatabaseFixture fixture)
    {
        [Theory]
        [InlineData("country")]
        [InlineData("currency")]
        [InlineData("category")]
        [InlineData("sector")]
        public async Task Save_ShouldTranslateDatabaseUniqueViolation(string kind)
        {
            await fixture.ResetAsync();
            await using var first = fixture.Database.CreateContext();
            await using var second = fixture.Database.CreateContext();
            // Two contexts have pending inserts before either writer commits.
            // The database, rather than a preflight check, resolves the collision.
            Func<Task> saveFirst;
            Func<Task> saveSecond;
            switch (kind)
            {
                case "country":
                    var countryFirst = new CountryRepository(first);
                    var countrySecond = new CountryRepository(second);
                    await countryFirst.AddAsync(new Country { Name = "Brasil" });
                    await countrySecond.AddAsync(new Country { Name = "Brasil" });
                    saveFirst = () => countryFirst.SaveChangesAsync();
                    saveSecond = () => countrySecond.SaveChangesAsync();
                    break;
                case "currency":
                    var currencyFirst = new CurrencyRepository(first);
                    var currencySecond = new CurrencyRepository(second);
                    await currencyFirst.AddAsync(new Currency { Code = "BRL", Name = "Real" });
                    await currencySecond.AddAsync(new Currency { Code = "BRL", Name = "Outro nome" });
                    saveFirst = () => currencyFirst.SaveChangesAsync();
                    saveSecond = () => currencySecond.SaveChangesAsync();
                    break;
                case "category":
                    var categoryFirst = new AssetCategoryRepository(first);
                    var categorySecond = new AssetCategoryRepository(second);
                    await categoryFirst.AddAsync(new AssetCategory { Name = "Ações" });
                    await categorySecond.AddAsync(new AssetCategory { Name = "Ações" });
                    saveFirst = () => categoryFirst.SaveChangesAsync();
                    saveSecond = () => categorySecond.SaveChangesAsync();
                    break;
                default:
                    var sectorFirst = new SectorRepository(first);
                    var sectorSecond = new SectorRepository(second);
                    await sectorFirst.AddAsync(new Sector { Name = "Energia" });
                    await sectorSecond.AddAsync(new Sector { Name = "Energia" });
                    saveFirst = () => sectorFirst.SaveChangesAsync();
                    saveSecond = () => sectorSecond.SaveChangesAsync();
                    break;
            }
            await saveFirst();
            var exception = await Assert.ThrowsAsync<ResourceConflictException>(saveSecond);
            Assert.IsType<DbUpdateException>(exception.InnerException);
        }

        [Fact]
        public async Task Asset_ShouldTranslateUniqueAndMissingReferenceViolations()
        {
            await fixture.ResetAsync();
            await using var seed = fixture.Database.CreateContext();
            await CatalogSeed.ApplyAsync(seed);
            var template = new Asset
            {
                Name = "Ativo", Ticker = "TEST", CreatedAt = DateTime.UtcNow,
                AssetTypeId = (await seed.AssetTypes.FirstAsync()).Id,
                CountryId = (await seed.Countries.FirstAsync()).Id,
                CurrencyId = (await seed.Currencies.FirstAsync()).Id,
                AssetCategoryId = (await seed.AssetCategories.FirstAsync()).Id,
                SectorId = (await seed.Sectors.FirstAsync()).Id
            };
            seed.Assets.Add(template);
            await seed.SaveChangesAsync();

            await using var context = fixture.Database.CreateContext();
            var repository = new AssetRepository(context);
            var duplicate = new Asset
            {
                Name = template.Name, Ticker = template.Ticker, CreatedAt = DateTime.UtcNow,
                AssetTypeId = template.AssetTypeId, CountryId = template.CountryId,
                CurrencyId = template.CurrencyId, AssetCategoryId = template.AssetCategoryId,
                SectorId = template.SectorId
            };
            await repository.AddAsync(duplicate);
            var conflict = await Assert.ThrowsAsync<ResourceConflictException>(() => repository.SaveChangesAsync());
            Assert.Contains("ticker", conflict.Message);
            context.ChangeTracker.Clear();
            duplicate.Ticker = "OTHER";
            duplicate.CountryId = int.MaxValue;
            await repository.AddAsync(duplicate);
            conflict = await Assert.ThrowsAsync<ResourceConflictException>(() => repository.SaveChangesAsync());
            Assert.Contains("classificação", conflict.Message);
        }
    }
}
