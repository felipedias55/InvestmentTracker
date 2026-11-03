using InvestmentTracker.Application.Allocation.Interfaces;
using InvestmentTracker.Application.Common.Exceptions;

namespace InvestmentTracker.Application.Allocation.Tracking
{
    public sealed class ContributionTrackingService(IContributionTrackingRepository repository, IAllocationService allocation)
    {
        public Task<TrackingPlanPage?> PageAsync(int portfolioId, int page, int size, CancellationToken ct)
        { ValidatePage(page, size); return repository.PageAsync(portfolioId, page, size, ct); }
        public async Task<TrackingPlanDetail?> ReadAsync(int portfolioId, int id, CancellationToken ct)
        {
            var data = await repository.ReadAsync(portfolioId, id, ct);
            return data is null ? null : ContributionTrackingCalculator.Detail(data);
        }
        public async Task<TrackingPlanDetail?> SaveAsync(int portfolioId, SaveContributionPlan request, CancellationToken ct)
        {
            if (request.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100
                || request.From < new DateOnly(1900, 1, 1) || request.To > new DateOnly(2100, 12, 31) || request.From > request.To
                || request.Simulation is null || string.IsNullOrEmpty(request.ExpectedToken) || request.ExpectedToken.Length != 64)
                throw new InputValidationException("Informe nome, período válido e simule o aporte antes de salvar o plano.");
            var id = await repository.SaveAsync(portfolioId, request, async () =>
            {
                var simulation = await allocation.PlanContributionAsync(portfolioId, request.Simulation, ct)
                    ?? throw new ResourceConflictException("A carteira não está disponível.");
                if (ContributionTrackingCalculator.Token(simulation) != request.ExpectedToken)
                    throw new ResourceConflictException("A simulação mudou. Simule novamente e confira o plano antes de salvar.");
                if (simulation.Plan is null) throw new InputValidationException("A simulação não possui um plano conjunto.");
                return new ContributionPlanSnapshot(simulation.Dashboard.Summary.Portfolio.BaseCurrencyCode,
                    request.Simulation, simulation.Plan, simulation.StrategyNotes);
            }, ct);
            return id.HasValue ? await ReadAsync(portfolioId, id.Value, ct) : null;
        }
        public Task<PurchasePage?> CandidatesAsync(int portfolioId, int id, int page, int size, CancellationToken ct)
        { ValidatePage(page, size); return repository.CandidatesAsync(portfolioId, id, page, size, ct); }
        public Task<bool> LinkAsync(int portfolioId, int id, LinkPurchase request, CancellationToken ct)
        {
            if (request.TradeId <= 0 || string.IsNullOrWhiteSpace(request.RowKey) || request.RowKey.Length > 100)
                throw new InputValidationException("Selecione a compra e o grupo do plano.");
            return repository.LinkAsync(portfolioId, id, request, ct);
        }
        public Task<bool> UnlinkAsync(int portfolioId, int id, int linkId, CancellationToken ct) => repository.UnlinkAsync(portfolioId, id, linkId, ct);
        private static void ValidatePage(int page, int size)
        {
            if (page < 0 || size is not (10 or 25 or 50)) throw new InputValidationException("Informe paginação válida.");
        }
    }
}
