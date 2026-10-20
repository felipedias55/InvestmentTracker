using InvestmentTracker.Application.Common.Exceptions;

namespace InvestmentTracker.Application.Strategies
{
    public sealed class StrategyService(IStrategyRepository repository)
    {
        public Task<IReadOnlyList<SavedStrategy>?> ListAsync(int portfolioId, CancellationToken ct)
            => repository.ListAsync(portfolioId, ct);
        public Task<SavedStrategy?> SaveAsync(int portfolioId, SaveStrategy request, CancellationToken ct)
        {
            StrategyCalculator.Validate(request.Config);
            if (request.ProfileId == Guid.Empty || request.ExpectedRevision < 0)
                throw new InputValidationException("Identificador ou revisão inválidos.");
            return repository.SaveAsync(portfolioId, request, ct);
        }
    }
}
