using System.Data;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Planning;
using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvestmentTracker.Infrastructure.Persistence.Repositories
{
    public sealed class PlanningGoalRepository(InvestmentTrackerDbContext db, TimeProvider clock) : IPlanningGoalRepository
    {
        private IQueryable<PlanningGoalRevision> Rows(int portfolioId) => db.Set<PlanningGoalRevision>().Where(r => r.PortfolioId == portfolioId);
        public async Task<PlanningGoalPageData?> PageAsync(int portfolioId, PlanningGoalQuery query, CancellationToken ct)
        {
            var currency = await db.Portfolios.Where(p => p.Id == portfolioId).Select(p => p.BaseCurrency.Code).SingleOrDefaultAsync(ct);
            if (currency is null) return null;
            var all = Rows(portfolioId).AsNoTracking();
            var rows = all.Where(r => !all.Any(n => n.GoalId == r.GoalId && n.Revision > r.Revision));
            if (query.Status != "all") rows = rows.Where(r => r.Archived == (query.Status == "archived"));
            if (!string.IsNullOrEmpty(query.Kind)) rows = rows.Where(r => r.Kind == query.Kind);
            if (!string.IsNullOrEmpty(query.Search)) rows = rows.Where(r => r.Name.Contains(query.Search));
            var total = await rows.CountAsync(ct); var page = Math.Min(query.PageIndex, Math.Max(0, (total - 1) / query.PageSize));
            return new(await rows.OrderBy(r => r.Deadline).ThenBy(r => r.Name).ThenBy(r => r.GoalId).Skip(page * query.PageSize).Take(query.PageSize).ToListAsync(ct), total, page, query.PageSize, currency);
        }
        public async Task<IReadOnlyList<SavedPlanningGoal>?> HistoryAsync(int portfolioId, Guid goalId, CancellationToken ct)
        {
            var rows = await Rows(portfolioId).AsNoTracking().Where(r => r.GoalId == goalId).OrderByDescending(r => r.Revision).ToListAsync(ct);
            return rows.Count == 0 ? null : rows.Select(PlanningGoalCalculator.Read).ToArray();
        }
        public async Task<SavedPlanningGoal?> SaveAsync(int portfolioId, SavePlanningGoal request, CancellationToken ct)
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var portfolio = await db.Portfolios.FromSqlInterpolated($"SELECT * FROM Portfolio WITH (UPDLOCK, HOLDLOCK) WHERE Id = {portfolioId}")
                .Include(p => p.BaseCurrency).SingleOrDefaultAsync(ct);
            if (portfolio is null) return null;
            var current = await Rows(portfolioId).Where(r => r.GoalId == request.GoalId).OrderByDescending(r => r.Revision).FirstOrDefaultAsync(ct);
            var config = request.Config with { Name = request.Config.Name.Trim() };
            if (current is not null && current.Revision == request.ExpectedRevision + 1 && PlanningGoalCalculator.Read(current).Config == config)
                return PlanningGoalCalculator.Read(current);
            if ((current?.Revision ?? 0) != request.ExpectedRevision)
                throw new ResourceConflictException("O objetivo mudou. Recarregue os objetivos antes de salvar.");
            if (current is not null && current.Currency != config.Currency)
                throw new InputValidationException("A moeda do objetivo é preservada. Crie um novo objetivo para outra moeda.");
            var onlyArchiving = current is not null && config.Archived && config == (PlanningGoalCalculator.Read(current).Config with { Archived = true });
            if (config.Currency != portfolio.BaseCurrency.Code && !onlyArchiving)
                throw new InputValidationException("Use a moeda-base atual da carteira. Objetivos antigos em outra moeda podem ser arquivados sem conversão.");
            var row = new PlanningGoalRevision { PortfolioId = portfolioId, GoalId = request.GoalId, Revision = request.ExpectedRevision + 1,
                Name = config.Name, Kind = config.Kind, Currency = config.Currency, Target = config.Target, Deadline = config.Deadline,
                MonthlyContribution = config.MonthlyContribution, Archived = config.Archived, CreatedAtUtc = clock.GetUtcNow().UtcDateTime };
            db.Add(row); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return PlanningGoalCalculator.Read(row);
        }
    }
}
