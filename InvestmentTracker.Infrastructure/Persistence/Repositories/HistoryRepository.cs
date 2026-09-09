using System.Data;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.History.Interfaces;
using InvestmentTracker.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace InvestmentTracker.Infrastructure.Persistence.Repositories
{
    public sealed class HistoryRepository(InvestmentTrackerDbContext context) : IHistoryRepository
    {
        public async Task<IReadOnlyList<PortfolioSnapshot>> GetSnapshotsAsync(int portfolioId, CancellationToken cancellationToken)
            => await context.PortfolioSnapshots.AsNoTracking().Where(s => s.PortfolioId == portfolioId).OrderBy(s => s.Month)
                .Select(s => new PortfolioSnapshot { Id = s.Id, PortfolioId = s.PortfolioId, Month = s.Month,
                    SnapshotDate = s.SnapshotDate, CapturedAtUtc = s.CapturedAtUtc, BaseCurrencyCode = s.BaseCurrencyCode,
                    PortfolioValue = s.PortfolioValue, ExternalValue = s.ExternalValue, TotalWealth = s.TotalWealth,
                    TotalIncome = s.TotalIncome, HasStaleRates = s.HasStaleRates, HasFallbackRates = s.HasFallbackRates })
                .ToListAsync(cancellationToken);
        public Task<PortfolioSnapshot?> GetSnapshotAsync(int portfolioId, int id, CancellationToken cancellationToken)
            => context.PortfolioSnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.PortfolioId == portfolioId && s.Id == id, cancellationToken);
        public async Task<IReadOnlyList<PortfolioCashFlow>> GetCashFlowsAsync(int portfolioId, CancellationToken cancellationToken)
            => await context.PortfolioCashFlows.AsNoTracking().Include(f => f.Currency).Where(f => f.PortfolioId == portfolioId)
                .OrderByDescending(f => f.Date).ThenByDescending(f => f.Id).ToListAsync(cancellationToken);
        public Task<PortfolioCashFlow?> GetCashFlowAsync(int portfolioId, int id, CancellationToken cancellationToken)
            => context.PortfolioCashFlows.SingleOrDefaultAsync(f => f.PortfolioId == portfolioId && f.Id == id, cancellationToken);
        public async Task AddCashFlowAsync(PortfolioCashFlow flow, CancellationToken cancellationToken)
            => await context.PortfolioCashFlows.AddAsync(flow, cancellationToken);
        public void RemoveCashFlow(PortfolioCashFlow flow) => context.PortfolioCashFlows.Remove(flow);

        public async Task<PortfolioSnapshot?> CaptureAsync(int portfolioId, bool replace,
            Func<CancellationToken, Task<PortfolioSnapshot>> create, CancellationToken cancellationToken)
        {
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                // Freeze one consistent set of balances and serialize captures for this portfolio.
                var portfolio = await context.Portfolios.FromSqlInterpolated(
                    $"SELECT * FROM Portfolio WITH (UPDLOCK, HOLDLOCK) WHERE Id = {portfolioId}")
                    .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
                if (portfolio is null) return null;
                var snapshot = await create(cancellationToken);
                var existing = await context.PortfolioSnapshots.SingleOrDefaultAsync(s => s.PortfolioId == portfolioId && s.Month == snapshot.Month, cancellationToken);
                if (existing is not null && !replace)
                    throw new ResourceConflictException("Este mês já possui uma fotografia. Use a atualização explícita para substituí-la.");
                if (existing is null && replace)
                    throw new ResourceConflictException("Ainda não existe fotografia neste mês. Registre a primeira fotografia.");
                if (existing is null) context.PortfolioSnapshots.Add(snapshot);
                else
                {
                    snapshot.Id = existing.Id;
                    context.Entry(existing).CurrentValues.SetValues(snapshot);
                }
                await SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return snapshot;
            }
            catch (SqlException ex) when (ex.Number == 1205)
            { throw new ResourceConflictException("A carteira mudou durante o fechamento. Tente novamente.", ex); }
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            try { await context.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateConcurrencyException ex)
            { throw new ResourceConflictException("O registro foi alterado ou removido. Atualize a página.", ex); }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 or 2601 or 2627 or 1205 })
            { throw new ResourceConflictException("O histórico ou suas referências mudaram. Atualize a página e tente novamente.", ex); }
        }
    }
}
