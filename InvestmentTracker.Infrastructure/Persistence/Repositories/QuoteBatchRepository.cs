using System.Data;
using System.Text.Json;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Portfolios;
using InvestmentTracker.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace InvestmentTracker.Infrastructure.Persistence.Repositories
{
    public sealed class QuoteBatchRepository(InvestmentTrackerDbContext db, TimeProvider clock) : IQuoteBatchRepository
    {
        public async Task<bool> SaveAsync(int portfolioId, QuoteBatchDto batch, DateOnly today, CancellationToken ct)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var portfolio = await db.Portfolios.FromSqlInterpolated($"SELECT * FROM Portfolio WITH (UPDLOCK, HOLDLOCK) WHERE Id = {portfolioId}")
                    .Include(x => x.BaseCurrency).SingleOrDefaultAsync(ct);
                if (portfolio is null) return false;
                var payload = JsonSerializer.Serialize(batch.Items.OrderBy(x => x.PositionId));
                var existing = await db.Set<FinancialMovement>().SingleOrDefaultAsync(x => x.PortfolioId == portfolioId && x.RequestId == batch.RequestId, ct);
                if (existing is not null)
                {
                    if (existing.Kind != "position-adjustment" || existing.RequestPayload != payload)
                        throw new ResourceConflictException("Esta solicitação já foi usada com outros dados.");
                    return true;
                }
                var ids = batch.Items.Select(x => x.PositionId).ToArray();
                var positions = await db.PortfolioAssets.Where(x => x.PortfolioId == portfolioId && ids.Contains(x.Id))
                    .Include(x => x.Asset).ThenInclude(x => x.Currency).ToListAsync(ct);
                if (positions.Count != ids.Length) throw new ResourceConflictException("Uma posição foi removida ou pertence a outra carteira. Recarregue os dados.");
                foreach (var item in batch.Items)
                {
                    var position = positions.Single(x => x.Id == item.PositionId);
                    if (position.Quantity != item.ExpectedQuantity || position.CurrentValue != item.ExpectedValue
                        || position.UpdatedOn != item.ExpectedUpdatedOn || position.Asset.Currency.Code != item.CurrencyCode)
                        throw new ResourceConflictException($"A posição {position.Asset.Ticker} mudou desde a revisão. Nenhuma cotação foi salva. Recarregue os dados.");
                    position.CurrentValue = QuoteBatchService.CalculateValue(position.Quantity, item.UnitPrice);
                    position.UpdatedOn = today;
                }
                await MovementRecorder.SaveAsync(db, new FinancialMovement
                {
                    PortfolioId = portfolioId, RequestId = batch.RequestId, Date = today, Kind = "position-adjustment",
                    CurrencyCode = portfolio.BaseCurrency.Code, Amount = 0, Description = $"Atualização manual de cotações: {positions.Count} posições",
                    CreatedAtUtc = clock.GetUtcNow().UtcDateTime, RequestPayload = payload
                }, ct);
                await transaction.CommitAsync(ct);
                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            { throw new ResourceConflictException("A carteira mudou durante a gravação. Nenhuma cotação foi salva. Recarregue os dados.", ex); }
            catch (Exception ex) when (ex is SqlException { Number: 1205 } || ex is DbUpdateException { InnerException: SqlException { Number: 1205 or 2601 or 2627 } })
            { throw new ResourceConflictException("Outra operação alterou a carteira. Confira os dados antes de tentar novamente.", ex); }
            finally { db.ChangeTracker.Clear(); }
        }
    }
}
