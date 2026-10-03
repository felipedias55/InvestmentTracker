using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Movements;
using InvestmentTracker.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace InvestmentTracker.Infrastructure.Persistence.Repositories
{
    public sealed class CorrectionRepository(InvestmentTrackerDbContext db, TimeProvider clock) : ICorrectionRepository
    {
        private sealed record Receipt(CorrectionInput Input, string Token, CorrectionSimulationDto Result);
        public async Task<CorrectionOperation?> DraftAsync(int portfolioId, int movementId, CancellationToken ct)
        {
            var m = await db.Set<FinancialMovement>().AsNoTracking().Include(x => x.Trade).Include(x => x.IncomeReceipt)
                .SingleOrDefaultAsync(x => x.PortfolioId == portfolioId && x.Id == movementId, ct);
            if (m is null) return null;
            if (await db.Set<FinancialMovement>().AnyAsync(x => x.ReversalOfId == m.Id, ct)) throw new InputValidationException("O lançamento já foi estornado. Selecione seu substituto no histórico.");
            var flows = await db.PortfolioCashFlows.AsNoTracking().Where(x => x.MovementId == m.Id).ToListAsync(ct);
            var op = CorrectionCalculator.ReadOperation(m, flows);
            var currency = await db.Portfolios.Where(x => x.Id == portfolioId).Select(x => x.BaseCurrency.Code).SingleAsync(ct);
            if (m.CurrencyCode == currency) op = op with { BaseAmount = null };
            else if (flows.Any(f => f.BaseCurrencyCode != currency)) op = op with { BaseAmount = null };
            if (m.IncomeReceipt is { } income)
            {
                var equivalent = await db.Set<IncomeConversion>().Where(x => x.IncomeReceiptId == income.Id && x.BaseCurrencyCode == currency)
                    .OrderByDescending(x => x.Revision).Select(x => (decimal?)x.BaseAmount).FirstOrDefaultAsync(ct);
                op = op with { BaseAmount = income.CurrencyCode == currency ? null : equivalent };
            }
            return op;
        }
        public async Task<CorrectionSimulationDto?> RunAsync(int portfolioId, CorrectionInput input, Guid? requestId, string? token,
            Func<CorrectionState, CorrectionPlan> calculate, CancellationToken ct)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var portfolio = await db.Portfolios.FromSqlInterpolated($"SELECT * FROM Portfolio WITH (UPDLOCK, HOLDLOCK) WHERE Id = {portfolioId}")
                    .Include(x => x.BaseCurrency).SingleOrDefaultAsync(ct);
                if (portfolio is null) return null;
                if (requestId.HasValue)
                {
                    var existing = await db.Set<FinancialMovement>().AsNoTracking().SingleOrDefaultAsync(x => x.PortfolioId == portfolioId && x.RequestId == requestId, ct);
                    if (existing is not null)
                    {
                        if (existing.Kind != "correction") throw new ResourceConflictException("Solicitação já utilizada em outra operação.");
                        var receipt = JsonSerializer.Deserialize<Receipt>(existing.RequestPayload!)!;
                        if (receipt.Input != input || receipt.Token != token) throw new ResourceConflictException("Solicitação já utilizada com outros dados.");
                        return receipt.Result;
                    }
                }
                var state = await ReadStateAsync(portfolio, ct);
                var expected = CorrectionCalculator.Token(state.Fingerprint, input);
                if (requestId.HasValue && token != expected) throw new ResourceConflictException("A carteira ou os dados da correção mudaram. Simule novamente antes de confirmar.");
                var plan = calculate(state);
                if (!requestId.HasValue) return plan.Preview;
                if (!plan.Preview.CanApply) throw new ResourceConflictException(string.Join(" ", plan.Preview.Blockers));
                var now = clock.GetUtcNow().UtcDateTime;
                var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);
                var header = new FinancialMovement { PortfolioId = portfolioId, RequestId = requestId.Value, Date = today, Kind = "correction",
                    CurrencyCode = portfolio.BaseCurrency.Code, Description = input.Reason, CreatedAtUtc = now };
                db.Add(header); await db.SaveChangesAsync(ct);
                var idMap = new Dictionary<int, int>();
                foreach (var p in plan.Positions)
                {
                    PortfolioAsset entity;
                    if (p.Id < 0) { entity = new PortfolioAsset { PortfolioId = portfolioId, AssetId = p.AssetId }; db.Add(entity); }
                    else entity = await db.PortfolioAssets.SingleAsync(x => x.PortfolioId == portfolioId && x.Id == p.Id, ct);
                    entity.Quantity = p.Quantity; entity.InvestedAmount = p.InvestedAmount; entity.CurrentValue = p.CurrentValue;
                    entity.Income = p.Income; entity.UpdatedOn = today;
                    if (p.Id < 0) { await db.SaveChangesAsync(ct); idMap.Add(p.Id, entity.Id); }
                }
                foreach (var b in plan.Cash)
                {
                    var entity = await db.ExternalAssets.SingleAsync(x => x.PortfolioId == portfolioId && x.Id == b.Id, ct);
                    entity.Value = b.Value; entity.UpdatedOn = today;
                }
                foreach (var step in plan.Steps)
                {
                    var movement = step.Movement; movement.CorrectionId = header.Id; movement.CreatedAtUtc = now;
                    foreach (var effect in movement.Effects)
                        if (effect.PositionId is < 0) effect.PositionId = idMap[effect.PositionId.Value];
                    if (movement.Trade is { } trade) trade.CreatedAtUtc = now;
                    if (movement.IncomeReceipt is { } income)
                    {
                        if (income.PositionId < 0) income.PositionId = idMap[income.PositionId];
                        income.CreatedAtUtc = now;
                        foreach (var conversion in income.Conversions) if (conversion.CreatedAtUtc == default) conversion.CreatedAtUtc = now;
                    }
                    db.Add(movement);
                    foreach (var flow in step.Flows)
                    {
                        flow.Movement = movement; flow.Trade = movement.Trade;
                        flow.CreatedAtUtc = now; flow.UpdatedAtUtc = now; db.Add(flow);
                    }
                    // Dependency checks use movement IDs; persist replay steps in their effective order.
                    await db.SaveChangesAsync(ct);
                }
                foreach (var snapshot in await db.PortfolioSnapshots.Where(x => x.PortfolioId == portfolioId && x.SnapshotDate >= plan.Preview.FromDate).ToListAsync(ct))
                { snapshot.IsReopened = true; snapshot.IsOutdated = true; }
                var result = plan.Preview with { CorrectionId = header.Id };
                header.RequestPayload = JsonSerializer.Serialize(new Receipt(input, token!, result));
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return result;
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 or 2601 or 2627 or 1205 })
            { throw new ResourceConflictException("As referências mudaram. Nenhuma parte da correção foi aplicada; simule novamente.", ex); }
            catch (SqlException ex) when (ex.Number == 1205)
            { throw new ResourceConflictException("Outra operação modificou a carteira. Simule novamente.", ex); }
            finally { db.ChangeTracker.Clear(); }
        }
        private async Task<CorrectionState> ReadStateAsync(Portfolio portfolio, CancellationToken ct)
        {
            var id = portfolio.Id;
            var positions = await db.PortfolioAssets.AsNoTracking().Where(x => x.PortfolioId == id).OrderBy(x => x.Id).ToListAsync(ct);
            var cash = await db.ExternalAssets.AsNoTracking().Where(x => x.PortfolioId == id).OrderBy(x => x.Id).ToListAsync(ct);
            var assets = await db.Assets.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
            var currencies = await db.Currencies.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
            var movements = await db.Set<FinancialMovement>().AsNoTracking().Include(x => x.Effects).Include(x => x.Trade)
                .Include(x => x.IncomeReceipt)!.ThenInclude(x => x!.Conversions).Where(x => x.PortfolioId == id).OrderBy(x => x.Id).ToListAsync(ct);
            var flows = await db.PortfolioCashFlows.AsNoTracking().Where(x => x.PortfolioId == id).OrderBy(x => x.Id).ToListAsync(ct);
            var photos = await db.PortfolioSnapshots.AsNoTracking().Where(x => x.PortfolioId == id).OrderBy(x => x.Id).ToListAsync(ct);
            var serialized = JsonSerializer.Serialize(new {
                portfolio.Id, portfolio.Name, portfolio.BaseCurrencyId,
                Positions = positions.Select(x => new { x.Id, x.AssetId, x.Quantity, x.InvestedAmount, x.CurrentValue, x.Income, x.UpdatedOn }),
                Cash = cash.Select(x => new { x.Id, x.CurrencyId, x.Name, x.Value, x.UpdatedOn }),
                Assets = assets.Select(x => new { x.Id, x.CurrencyId, x.Ticker }), Currencies = currencies.Select(x => new { x.Id, x.Code }),
                Movements = movements.Select(x => new { x.Id, x.Date, x.Kind, x.RequestId, x.RequestPayload, x.ReversalOfId, x.CorrectionId, x.ReplacesMovementId,
                    Effects = x.Effects.OrderBy(e => e.Id), x.Trade, Income = x.IncomeReceipt is null ? null : new { x.IncomeReceipt.Amount, x.IncomeReceipt.AssetId,
                        x.IncomeReceipt.CashAssetId, x.IncomeReceipt.BaseCurrencyCode, Conversions = x.IncomeReceipt.Conversions.OrderBy(c => c.Id) } }),
                Flows = flows.Select(x => new { x.Id, x.MovementId, x.Date, x.Kind, x.IsReversal, x.CurrencyId, x.Amount, x.BaseAmount, x.BaseCurrencyCode }),
                Photos = photos.Select(x => new { x.Id, x.SnapshotDate, x.Revision, x.IsOutdated, x.IsReopened, x.DashboardJson, x.PreviousVersionsJson })
            });
            return new(portfolio, positions, cash, assets, currencies, movements, flows, photos, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serialized))));
        }
    }
}
