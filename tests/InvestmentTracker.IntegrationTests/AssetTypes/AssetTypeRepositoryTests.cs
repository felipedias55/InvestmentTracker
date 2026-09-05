using InvestmentTracker.Domain.Entities;
using InvestmentTracker.Infrastructure.Persistence.Repositories;
using InvestmentTracker.IntegrationTests.Infrastructure;

namespace InvestmentTracker.IntegrationTests.AssetTypes
{
    [Collection(DatabaseCollection.Name)]
    public class AssetTypeRepositoryTests
    {
        private readonly DatabaseFixture _fixture;

        public AssetTypeRepositoryTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task AddAsync_ShouldPersistAssetType()
        {
            await _fixture.ResetAsync();

            await using var context = _fixture.Database.CreateContext();

            var repository = new AssetTypeRepository(context);

            var assetType = new AssetType
            {
                Name = "Ação"
            };

            await repository.AddAsync(assetType);
            await repository.SaveChangesAsync();

            var result = await repository.GetByIdAsync(assetType.Id);

            Assert.NotNull(result);
            Assert.Equal(assetType.Id, result.Id);
            Assert.Equal("Ação", result.Name);
        }

        [Fact]
        public async Task GetAllAsync_ShouldReturnAssetTypesOrderedByName()
        {
            await _fixture.ResetAsync();

            await using var context = _fixture.Database.CreateContext();

            var repository = new AssetTypeRepository(context);

            await repository.AddAsync(new AssetType
            {
                Name = "REIT"
            });

            await repository.AddAsync(new AssetType
            {
                Name = "Ação"
            });

            await repository.AddAsync(new AssetType
            {
                Name = "FII"
            });

            await repository.SaveChangesAsync();

            var result = await repository.GetAllAsync();

            Assert.Equal(3, result.Count);
            Assert.Equal("Ação", result[0].Name);
            Assert.Equal("FII", result[1].Name);
            Assert.Equal("REIT", result[2].Name);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnAssetType_WhenAssetTypeExists()
        {
            await _fixture.ResetAsync();

            await using var context = _fixture.Database.CreateContext();

            var repository = new AssetTypeRepository(context);

            var assetType = new AssetType
            {
                Name = "Ação"
            };

            await repository.AddAsync(assetType);
            await repository.SaveChangesAsync();

            var result = await repository.GetByIdAsync(assetType.Id);

            Assert.NotNull(result);
            Assert.Equal(assetType.Id, result.Id);
            Assert.Equal("Ação", result.Name);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnNull_WhenAssetTypeDoesNotExist()
        {
            await _fixture.ResetAsync();

            await using var context = _fixture.Database.CreateContext();

            var repository = new AssetTypeRepository(context);

            var result = await repository.GetByIdAsync(999);

            Assert.Null(result);
        }

        [Fact]
        public async Task ExistsByNameAsync_ShouldReturnTrue_WhenNameExists()
        {
            await _fixture.ResetAsync();

            await using var context = _fixture.Database.CreateContext();

            var repository = new AssetTypeRepository(context);

            await repository.AddAsync(new AssetType
            {
                Name = "Ação"
            });

            await repository.SaveChangesAsync();

            var result = await repository.ExistsByNameAsync("Ação");

            Assert.True(result);
        }

        [Fact]
        public async Task ExistsByNameAsync_ShouldReturnFalse_WhenNameDoesNotExist()
        {
            await _fixture.ResetAsync();

            await using var context = _fixture.Database.CreateContext();

            var repository = new AssetTypeRepository(context);

            var result = await repository.ExistsByNameAsync("FII");

            Assert.False(result);
        }

        [Fact]
        public async Task ExistsByNameAsync_ShouldIgnoreAssetType_WhenIdIsExcluded()
        {
            await _fixture.ResetAsync();

            await using var context = _fixture.Database.CreateContext();

            var repository = new AssetTypeRepository(context);

            var assetType = new AssetType
            {
                Name = "Ação"
            };

            await repository.AddAsync(assetType);
            await repository.SaveChangesAsync();

            var result = await repository.ExistsByNameAsync(
                "Ação",
                assetType.Id);

            Assert.False(result);
        }

        [Fact]
        public async Task ExistsByNameAsync_ShouldReturnTrue_WhenAnotherAssetTypeHasSameName()
        {
            await _fixture.ResetAsync();

            await using var context = _fixture.Database.CreateContext();

            var repository = new AssetTypeRepository(context);

            var action = new AssetType
            {
                Name = "Ação"
            };

            var fii = new AssetType
            {
                Name = "FII"
            };

            await repository.AddAsync(action);
            await repository.AddAsync(fii);
            await repository.SaveChangesAsync();

            var result = await repository.ExistsByNameAsync(
                "Ação",
                fii.Id);

            Assert.True(result);
        }

        [Fact]
        public async Task DeleteAsync_ShouldRemoveAssetType()
        {
            await _fixture.ResetAsync();

            await using var context = _fixture.Database.CreateContext();

            var repository = new AssetTypeRepository(context);

            var assetType = new AssetType
            {
                Name = "Ação"
            };

            await repository.AddAsync(assetType);
            await repository.SaveChangesAsync();

            await repository.DeleteAsync(assetType);
            await repository.SaveChangesAsync();

            var result = await repository.GetByIdAsync(assetType.Id);

            Assert.Null(result);
        }
    }
}
