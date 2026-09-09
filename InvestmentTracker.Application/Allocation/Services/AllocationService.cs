using InvestmentTracker.Application.ExternalAssets.Interfaces;
using InvestmentTracker.Application.Allocation.Dtos;
using InvestmentTracker.Application.Allocation.Interfaces;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Portfolios.Dtos;
using InvestmentTracker.Application.Portfolios.Interfaces;

namespace InvestmentTracker.Application.Allocation.Services
{
    public sealed class AllocationService(IAllocationRepository repository, IPortfolioRepository portfolios,
        IPortfolioService portfolioService, IExternalAssetService externalAssets) : IAllocationService
    {
        public async Task<IReadOnlyList<AllocationTargetDto>?> GetTargetsAsync(int portfolioId, AllocationDimension dimension,
            CancellationToken cancellationToken = default)
        {
            if (await portfolios.GetByIdAsync(portfolioId, cancellationToken) is null) return null;
            return await repository.GetTargetsAsync(portfolioId, dimension, cancellationToken);
        }

        public async Task<bool> SaveTargetsAsync(int portfolioId, AllocationDimension dimension, SaveTargetsDto dto,
            CancellationToken cancellationToken = default)
        {
            if (await portfolios.GetByIdAsync(portfolioId, cancellationToken) is null) return false;
            var targets = dto.Targets;
            if (targets is null || targets.Count == 0 || targets.Any(t => t is null))
                throw new InputValidationException("Informe as metas; o conjunto deve somar 100%.");
            if (targets.Any(t => t.GroupId <= 0 || t.TargetPercentage < 0m || t.TargetPercentage > 1m
                || decimal.Round(t.TargetPercentage, 6) != t.TargetPercentage))
                throw new InputValidationException("Cada meta deve estar entre 0% e 100%, com até quatro casas decimais no percentual.");
            if (targets.Select(t => t.GroupId).Distinct().Count() != targets.Count)
                throw new InputValidationException("Uma categoria ou setor não pode aparecer duas vezes nas metas.");
            if (targets.Sum(t => t.TargetPercentage) != 1m)
                throw new InputValidationException("A soma das metas deve ser exatamente 100%.");
            if (!await repository.GroupsExistAsync(targets.Select(t => t.GroupId).ToList(), dimension, cancellationToken))
                throw new InputValidationException("Selecione apenas categorias ou setores existentes.");
            await repository.ReplaceTargetsAsync(portfolioId, dimension, targets, cancellationToken);
            return true;
        }

        public async Task<DashboardDto?> GetDashboardAsync(int portfolioId, CancellationToken cancellationToken = default)
        {
            var dashboard = await GetAllocationDashboardAsync(portfolioId, cancellationToken);
            if (dashboard is null) return null;
            var external = await externalAssets.GetSummaryAsync(portfolioId, cancellationToken);
            return dashboard with { ExternalAssets = external, TotalWealth = dashboard.Summary.CurrentValue + external?.TotalValue };
        }

        private async Task<DashboardDto?> GetAllocationDashboardAsync(int portfolioId, CancellationToken cancellationToken)
        {
            var summary = await portfolioService.GetSummaryAsync(portfolioId, cancellationToken);
            if (summary is null) return null;
            var categories = await repository.GetTargetsAsync(portfolioId, AllocationDimension.Category, cancellationToken);
            var sectors = await repository.GetTargetsAsync(portfolioId, AllocationDimension.Sector, cancellationToken);
            return new DashboardDto(summary, new AllocationDto(categories.Count > 0 && categories.Sum(t => t.TargetPercentage) == 1m,
                sectors.Count > 0 && sectors.Sum(t => t.TargetPercentage) == 1m,
                BuildRows(summary, p => (p.AssetCategoryId, p.AssetCategoryName), categories),
                BuildRows(summary, p => (p.SectorId, p.SectorName), sectors),
                BuildRows(summary, p => (p.CountryId, p.CountryName), [])));
        }

        public async Task<ContributionAnalysisDto?> AnalyzeContributionAsync(int portfolioId, ContributionRequestDto dto,
            CancellationToken cancellationToken = default)
        {
            ContributionCalculator.ValidateAmount(dto.Amount);
            var dashboard = await GetAllocationDashboardAsync(portfolioId, cancellationToken);
            return dashboard is null ? null : ContributionCalculator.Calculate(dashboard, dto);
        }

        private static IReadOnlyList<AllocationRowDto> BuildRows(PortfolioSummaryDto summary,
            Func<PositionDto, (int Id, string Name)> classify, IReadOnlyList<AllocationTargetDto> targets)
        {
            var groups = summary.Positions.GroupBy(p => classify(p).Id).ToDictionary(g => g.Key, g => g.ToList());
            var configured = targets.Count > 0;
            return groups.Keys.Union(targets.Select(t => t.GroupId)).Select(id =>
            {
                var target = targets.SingleOrDefault(t => t.GroupId == id);
                groups.TryGetValue(id, out var positions);
                var name = target?.Name ?? classify(positions![0]).Name;
                decimal? value = summary.ConversionAvailable ? positions?.Sum(p => p.BaseCurrentValue!.Value) ?? 0m : null;
                decimal? current = summary.CurrentValue > 0m ? value / summary.CurrentValue : summary.ConversionAvailable ? 0m : null;
                decimal? desired = configured ? target?.TargetPercentage ?? 0m : null;
                return new AllocationRowDto(id, name, value, current, desired, desired - current);
            }).OrderBy(r => r.Name).ThenBy(r => r.GroupId).ToList();
        }
    }
}
