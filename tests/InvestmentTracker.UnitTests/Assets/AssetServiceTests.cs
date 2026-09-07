using InvestmentTracker.Application.Assets.Dtos;
using InvestmentTracker.Application.Assets.Interfaces;
using InvestmentTracker.Application.Assets.Services;
using InvestmentTracker.Application.AssetTypes.Interfaces;
using InvestmentTracker.Application.Countries.Interfaces;
using InvestmentTracker.Application.Currencies.Interfaces;
using InvestmentTracker.Application.AssetCategories.Interfaces;
using InvestmentTracker.Application.Sectors.Interfaces;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Domain.Entities;
using Moq;

namespace InvestmentTracker.UnitTests.Assets
{
    public class AssetServiceTests
    {
        private readonly Mock<IAssetRepository> _repository = new();
        private readonly Mock<IAssetTypeRepository> _types = new();
        private readonly Mock<ICountryRepository> _countries = new();
        private readonly Mock<ICurrencyRepository> _currencies = new();
        private readonly Mock<IAssetCategoryRepository> _categories = new();
        private readonly Mock<ISectorRepository> _sectors = new();
        private AssetService Service => new(_repository.Object, _types.Object, _countries.Object,
            _currencies.Object, _categories.Object, _sectors.Object);

        public AssetServiceTests()
        {
            _types.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new AssetType { Id = 1 });
            _countries.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Country { Id = 1 });
            _currencies.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Currency { Id = 1 });
            _categories.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new AssetCategory { Id = 1 });
            _sectors.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Sector { Id = 1 });
        }

        [Theory]
        [InlineData(99, 1, 1, 1, 1)]
        [InlineData(1, 99, 1, 1, 1)]
        [InlineData(1, 1, 99, 1, 1)]
        [InlineData(1, 1, 1, 99, 1)]
        [InlineData(1, 1, 1, 1, 99)]
        [InlineData(0, 1, 1, 1, 1)]
        public async Task CreateAndUpdate_ShouldRejectMissingClassification(int type, int country, int currency, int category, int sector)
        {
            var entity = new Asset { Id = 1, Ticker = "ORIGINAL" };
            _repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
            await Assert.ThrowsAsync<InputValidationException>(() => Service.CreateAsync(new("TEST", "Ativo", type, country, currency, category, sector)));
            await Assert.ThrowsAsync<InputValidationException>(() => Service.UpdateAsync(1, new("TEST", "Ativo", type, country, currency, category, sector)));
            Assert.Equal("ORIGINAL", entity.Ticker);
            _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData(null, "Ativo")]
        [InlineData(" ", "Ativo")]
        [InlineData("TEST", null)]
        [InlineData("TEST", " ")]
        public async Task Create_ShouldRequireTickerAndName(string? ticker, string? name)
        {
            await Assert.ThrowsAsync<InputValidationException>(() => Service.CreateAsync(new(ticker!, name!, 1, 1, 1, 1, 1)));
        }

        [Fact]
        public async Task Create_ShouldRejectLongFields()
        {
            await Assert.ThrowsAsync<InputValidationException>(() => Service.CreateAsync(new(new string('A', 21), "Ativo", 1, 1, 1, 1, 1)));
            await Assert.ThrowsAsync<InputValidationException>(() => Service.CreateAsync(new("TEST", new string('A', 201), 1, 1, 1, 1, 1)));
        }

        [Fact]
        public async Task Create_ShouldNormalizeTickerAndSetUtcCreationDate()
        {
            var before = DateTime.UtcNow;
            var result = await Service.CreateAsync(new(" petr4 ", " Petrobras ", 1, 1, 1, 1, 1));
            Assert.Equal("PETR4", result.Ticker);
            Assert.Equal("Petrobras", result.Name);
            Assert.Equal(DateTimeKind.Utc, result.CreatedAt.Kind);
            Assert.InRange(result.CreatedAt, before, DateTime.UtcNow);
        }

        [Fact]
        public async Task Update_ShouldPreserveCreationDateAndExcludeItsOwnTicker()
        {
            var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            _repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Asset { Id = 1, CreatedAt = createdAt });
            var result = await Service.UpdateAsync(1, new("test", "Ativo", 1, 1, 1, 1, 1));
            Assert.Equal(createdAt, result!.CreatedAt);
            _repository.Verify(r => r.ExistsByTickerAsync("TEST", 1, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Create_ShouldRejectDuplicateTicker()
        {
            _repository.Setup(r => r.ExistsByTickerAsync("TEST", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            await Assert.ThrowsAsync<ResourceConflictException>(() => Service.CreateAsync(new("test", "Ativo", 1, 1, 1, 1, 1)));
            _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
