using InvestmentTracker.Application.ExternalAssets.Interfaces;
using InvestmentTracker.Application.Allocation;
using InvestmentTracker.Application.Allocation.Dtos;
using InvestmentTracker.Application.Allocation.Interfaces;
using InvestmentTracker.Application.Allocation.Services;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Portfolios.Dtos;
using InvestmentTracker.Application.Portfolios.Interfaces;
using InvestmentTracker.Domain.Entities;
using Moq;

namespace InvestmentTracker.UnitTests.Allocation
{
    public class AllocationServiceTests
    {
        private readonly Mock<IAllocationRepository> _repository = new();
        private readonly Mock<IPortfolioRepository> _portfolios = new();
        private readonly Mock<IPortfolioService> _summary = new();
        private AllocationService Service => new(_repository.Object, _portfolios.Object, _summary.Object, Mock.Of<IExternalAssetService>());

        public AllocationServiceTests()
        {
            _portfolios.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Portfolio { Id = 1 });
            _repository.Setup(r => r.GetTargetsAsync(1, It.IsAny<AllocationDimension>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<AllocationTargetDto>());
            _repository.Setup(r => r.GroupsExistAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<AllocationDimension>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        }

        [Theory]
        [InlineData(AllocationDimension.Category)]
        [InlineData(AllocationDimension.Sector)]
        public async Task Save_ShouldReplaceTheWholeValidSetWithExactFractionPrecision(AllocationDimension dimension)
        {
            var entries = new[] { new TargetEntryDto(1, 0.333333m), new TargetEntryDto(2, 0.666667m) };
            Assert.True(await Service.SaveTargetsAsync(1, dimension, new(entries)));
            _repository.Verify(r => r.ReplaceTargetsAsync(1, dimension, entries, It.IsAny<CancellationToken>()), Times.Once);
        }

        public static IEnumerable<object[]> InvalidTargets => new[]
        {
            new object[] { new TargetEntryDto[] { } },
            new object[] { new[] { new TargetEntryDto(1, 0.5m) } },
            new object[] { new[] { new TargetEntryDto(1, 0.5m), new TargetEntryDto(1, 0.5m) } },
            new object[] { new[] { new TargetEntryDto(1, -0.1m), new TargetEntryDto(2, 1.1m) } },
            new object[] { new[] { new TargetEntryDto(1, 0.3333333m), new TargetEntryDto(2, 0.6666667m) } },
            new object[] { new[] { new TargetEntryDto(0, 1m) } },
            new object[] { new TargetEntryDto[] { null! } },
        };
        [Theory]
        [MemberData(nameof(InvalidTargets))]
        public async Task Save_ShouldRejectInvalidSetWithoutWriting(TargetEntryDto[] entries)
        {
            await Assert.ThrowsAsync<InputValidationException>(() => Service.SaveTargetsAsync(1, AllocationDimension.Category, new(entries)));
            _repository.Verify(r => r.ReplaceTargetsAsync(It.IsAny<int>(), It.IsAny<AllocationDimension>(),
                It.IsAny<IReadOnlyList<TargetEntryDto>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Save_ShouldRejectNullAndUnknownReferencesAndRespectMissingPortfolio()
        {
            await Assert.ThrowsAsync<InputValidationException>(() => Service.SaveTargetsAsync(1, AllocationDimension.Category, new(null!)));
            _repository.Setup(r => r.GroupsExistAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<AllocationDimension>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            await Assert.ThrowsAsync<InputValidationException>(() => Service.SaveTargetsAsync(1, AllocationDimension.Category, new([new(999, 1m)])));
            Assert.False(await Service.SaveTargetsAsync(2, AllocationDimension.Category, new([new(1, 1m)])));
            Assert.Null(await Service.GetTargetsAsync(2, AllocationDimension.Category));
            Assert.Null(await Service.GetDashboardAsync(2));
            Assert.Null(await Service.AnalyzeContributionAsync(2, new("category", 10m)));
        }

        private void Summary(decimal? total, params PositionDto[] positions)
        {
            _summary.Setup(s => s.GetSummaryAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(
                new PortfolioSummaryDto(new(1, "Principal", null, 1, "BRL"), positions, [], total, total,
                    total.HasValue, true, false));
        }
        private static PositionDto Position(int id, decimal? value, int category, int sector, int country)
            => new(id, id, $"A{id}", "Ativo", "USD", 1m, 10m, 20m, value, value, value.HasValue ? 5m : null, null, true, false)
            {
                AssetCategoryId = category, AssetCategoryName = $"Categoria {category}",
                SectorId = sector, SectorName = $"Setor {sector}", CountryId = country, CountryName = $"País {country}"
            };

        [Fact]
        public async Task Dashboard_ShouldGroupConvertedValuesAndIncludeTargetsWithoutPositions()
        {
            Summary(200m, Position(1, 100m, 1, 1, 1), Position(2, 50m, 1, 2, 1), Position(3, 50m, 2, 2, 2));
            _repository.Setup(r => r.GetTargetsAsync(1, AllocationDimension.Category, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new AllocationTargetDto(1, "Categoria 1", 0.5m), new AllocationTargetDto(3, "Categoria 3", 0.5m) });
            var dashboard = (await Service.GetDashboardAsync(1))!;
            var categories = dashboard.Allocation.Categories;
            Assert.Equal(150m, categories.Single(r => r.GroupId == 1).CurrentValue);
            Assert.Equal(0.75m, categories.Single(r => r.GroupId == 1).CurrentPercentage);
            Assert.Equal(-0.25m, categories.Single(r => r.GroupId == 1).Difference);
            Assert.Equal(0m, categories.Single(r => r.GroupId == 2).TargetPercentage);
            Assert.Equal(0.5m, categories.Single(r => r.GroupId == 3).Difference);
            Assert.Equal(0m, categories.Single(r => r.GroupId == 3).CurrentValue);
            Assert.All(dashboard.Allocation.Sectors, r => Assert.Null(r.TargetPercentage));
            Assert.Equal(150m, dashboard.Allocation.Countries.Single(r => r.GroupId == 1).CurrentValue);
            Assert.True(dashboard.Summary.HasStaleRates);
        }

        [Fact]
        public async Task MissingRate_ShouldSuppressAllTotalsPercentagesAndDifferences()
        {
            Summary(null, Position(1, 100m, 1, 1, 1), Position(2, null, 2, 2, 2));
            _repository.Setup(r => r.GetTargetsAsync(1, AllocationDimension.Category, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new AllocationTargetDto(1, "Categoria", 1m) });
            var result = (await Service.GetDashboardAsync(1))!;
            Assert.All(result.Allocation.Categories.Concat(result.Allocation.Sectors).Concat(result.Allocation.Countries), row =>
            { Assert.Null(row.CurrentValue); Assert.Null(row.CurrentPercentage); Assert.Null(row.Difference); });
            await Assert.ThrowsAsync<ResourceConflictException>(() => Service.AnalyzeContributionAsync(1, new("category", 100m)));
        }

        [Fact]
        public async Task EmptyPortfolio_ShouldUseZeroPercentagesAndDistributeFirstContributionByTargets()
        {
            Summary(0m);
            _repository.Setup(r => r.GetTargetsAsync(1, AllocationDimension.Category, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new AllocationTargetDto(1, "Categoria 1", 0.3m), new AllocationTargetDto(2, "Categoria 2", 0.7m) });
            var result = (await Service.AnalyzeContributionAsync(1, new("category", 100m)))!;
            Assert.Equal(30m, result.Rows.Single(r => r.GroupId == 1).SuggestedContribution);
            Assert.Equal(70m, result.Rows.Single(r => r.GroupId == 2).SuggestedContribution);
            Assert.All(result.Rows, r => Assert.Equal(0m, r.CurrentPercentage));
        }
    }
}
