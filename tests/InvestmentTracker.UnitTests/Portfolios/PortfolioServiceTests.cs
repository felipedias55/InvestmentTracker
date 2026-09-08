using InvestmentTracker.Application.Assets.Interfaces;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Currencies.Interfaces;
using InvestmentTracker.Application.ExchangeRates.Interfaces;
using InvestmentTracker.Application.Portfolios;
using InvestmentTracker.Application.Portfolios.Dtos;
using InvestmentTracker.Application.Portfolios.Interfaces;
using InvestmentTracker.Application.Portfolios.Services;
using InvestmentTracker.Domain.Entities;
using Moq;

namespace InvestmentTracker.UnitTests.Portfolios
{
    public class PortfolioServiceTests
    {
        private readonly Mock<IPortfolioRepository> _repository = new();
        private readonly Mock<ICurrencyRepository> _currencies = new();
        private readonly Mock<IAssetRepository> _assets = new();
        private readonly Mock<IExchangeRateService> _rates = new();
        private PortfolioService Service => new(_repository.Object, _currencies.Object, _assets.Object, _rates.Object, new PortfolioDefaults());
        private static readonly Currency Brl = new() { Id = 1, Code = "BRL" };

        private void PortfolioWith(params PortfolioAsset[] positions)
        {
            _repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Portfolio { Id = 1, Name = "Principal", BaseCurrencyId = 1, BaseCurrency = Brl });
            _repository.Setup(r => r.GetPositionsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(positions);
        }
        private static PortfolioAsset Position(string code, decimal invested, decimal current) => new()
        {
            Id = 1, AssetId = 1, PortfolioId = 1, Quantity = 2m, InvestedAmount = invested, CurrentValue = current,
            Asset = new Asset { Ticker = code, Currency = new Currency { Code = code } }
        };

        [Fact]
        public async Task Summary_ShouldConvertBeforeSummingAndKeepOriginalSubtotals()
        {
            PortfolioWith(Position("BRL", 100m, 120m), Position("USD", 10m, 20m), Position("USD", 5m, 10m));
            _rates.Setup(r => r.GetAsync("USD", "BRL", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ExchangeRateQuote("USD", "BRL", 5m, new DateOnly(2026, 9, 7), false, false));
            var result = (await Service.GetSummaryAsync(1))!;
            Assert.Equal(175m, result.TotalInvested);
            Assert.Equal(270m, result.CurrentValue);
            Assert.Equal(30m, result.OriginalSubtotals.Single(s => s.CurrencyCode == "USD").CurrentValue);
            Assert.Equal(20m, result.Positions[1].CurrentValue);
            _rates.Verify(r => r.GetAsync("USD", "BRL", It.IsAny<CancellationToken>()), Times.Once);
            _rates.Verify(r => r.GetAsync("BRL", "BRL", It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Summary_ShouldNotReturnPartialTotalWhenRateIsMissing()
        {
            PortfolioWith(Position("BRL", 100m, 120m), Position("USD", 10m, 20m));
            var result = (await Service.GetSummaryAsync(1))!;
            Assert.False(result.ConversionAvailable);
            Assert.Null(result.CurrentValue);
            Assert.Null(result.TotalInvested);
            Assert.Equal(120m, result.Positions[0].BaseCurrentValue);
            Assert.Null(result.Positions[1].BaseCurrentValue);
            Assert.Equal(20m, result.Positions[1].CurrentValue);
        }

        [Fact]
        public async Task Summary_ShouldExposeStaleFallbackDate()
        {
            PortfolioWith(Position("USD", 10m, 20m));
            var date = new DateOnly(2026, 9, 4);
            _rates.Setup(r => r.GetAsync("USD", "BRL", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ExchangeRateQuote("USD", "BRL", 5m, date, true, true));
            var result = (await Service.GetSummaryAsync(1))!;
            Assert.True(result.HasStaleRates);
            Assert.True(result.HasFallbackRates);
            Assert.Equal(date, result.Positions[0].RateDate);
            Assert.Equal(100m, result.CurrentValue);
        }

        [Fact]
        public async Task EmptyPortfolio_ShouldHaveZeroTotalsWithoutFetchingRates()
        {
            PortfolioWith();
            var result = (await Service.GetSummaryAsync(1))!;
            Assert.True(result.ConversionAvailable);
            Assert.Equal(0m, result.CurrentValue);
            Assert.Empty(result.Positions);
            _rates.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Create_ShouldUseSiteDefaultCurrency()
        {
            _currencies.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { Brl });
            _currencies.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Brl);
            var result = await Service.CreateAsync(new CreatePortfolioDto(" Principal "));
            Assert.Equal("BRL", result.BaseCurrencyCode);
            Assert.Equal("Principal", result.Name);
        }

        [Theory]
        [InlineData("-1", "0", "0")]
        [InlineData("0", "-1", "0")]
        [InlineData("0", "0", "-1")]
        [InlineData("0.0000001", "0", "0")]
        [InlineData("1", "0.00001", "0")]
        [InlineData("1", "0", "0.00001")]
        [InlineData("10000000000000", "0", "0")]
        public async Task Position_ShouldRejectInvalidPrecisionAndNegativeValues(string quantity, string invested, string current)
        {
            var dto = new SavePositionDto(1, decimal.Parse(quantity, System.Globalization.CultureInfo.InvariantCulture),
                decimal.Parse(invested, System.Globalization.CultureInfo.InvariantCulture),
                decimal.Parse(current, System.Globalization.CultureInfo.InvariantCulture));
            await Assert.ThrowsAsync<InputValidationException>(() => Service.AddPositionAsync(1, dto));
            await Assert.ThrowsAsync<InputValidationException>(() => Service.UpdatePositionAsync(1, 1, dto));
            _repository.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Position_ShouldRejectDuplicateWithinPortfolio()
        {
            PortfolioWith();
            _assets.Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(new Asset { Id = 2 });
            _repository.Setup(r => r.HasAssetAsync(1, 2, null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            await Assert.ThrowsAsync<ResourceConflictException>(() => Service.AddPositionAsync(1, new(2, 1, 1, 1)));
            _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Position_ShouldNotAllowChangingAssetDuringEdit()
        {
            _repository.Setup(r => r.GetPositionAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(Position("BRL", 10, 20));
            await Assert.ThrowsAsync<InputValidationException>(() => Service.UpdatePositionAsync(1, 1, new(2, 1, 1, 1)));
            _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
