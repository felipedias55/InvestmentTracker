using InvestmentTracker.Application.AssetTypes.Exceptions;
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
        public async Task SaveChanges_ShouldTranslateDuplicateAfterBothChecksPass()
        {
            await _fixture.ResetAsync();
            await using var firstContext = _fixture.Database.CreateContext();
            await using var secondContext = _fixture.Database.CreateContext();
            var first = new AssetTypeRepository(firstContext);
            var second = new AssetTypeRepository(secondContext);

            Assert.False(await first.ExistsByNameAsync("Duplicado"));
            Assert.False(await second.ExistsByNameAsync("Duplicado"));
            await first.AddAsync(new AssetType { Name = "Duplicado" });
            await second.AddAsync(new AssetType { Name = "Duplicado" });
            await first.SaveChangesAsync();

            var exception = await Assert.ThrowsAsync<AssetTypeConflictException>(
                () => second.SaveChangesAsync());
            Assert.Equal("Já existe um tipo de ativo com esse nome.", exception.Message);
        }

        [Fact]
        public async Task SaveChanges_ShouldTranslateDeletionOfReferencedType()
        {
            await _fixture.ResetAsync();
            int typeId;
            await using (var seed = _fixture.Database.CreateContext())
            {
                var type = new AssetType { Name = "Ação" };
                seed.Assets.Add(new Asset
                {
                    Name = "Ativo", Ticker = "TEST3", AssetType = type,
                    Country = new Country { Name = "Brasil" },
                    Currency = new Currency { Code = "BRL", Name = "Real" },
                    AssetCategory = new AssetCategory { Name = "Renda variável" },
                    Sector = new Sector { Name = "Tecnologia" },
                    CreatedAt = DateTime.UtcNow
                });
                await seed.SaveChangesAsync();
                typeId = type.Id;
            }

            await using var context = _fixture.Database.CreateContext();
            var repository = new AssetTypeRepository(context);
            var entity = await repository.GetByIdAsync(typeId);
            await repository.DeleteAsync(entity!);
            var exception = await Assert.ThrowsAsync<AssetTypeConflictException>(
                () => repository.SaveChangesAsync());
            Assert.Equal("O tipo de ativo está em uso e não pode ser excluído.", exception.Message);
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
