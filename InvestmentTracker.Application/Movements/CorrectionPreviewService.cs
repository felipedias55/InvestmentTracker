using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.ExternalAssets.Interfaces;
using InvestmentTracker.Application.History.Interfaces;
using InvestmentTracker.Application.Portfolios.Interfaces;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.Movements
{
    public sealed record CorrectionPreviewRequest(DateOnly Date, int? MovementId = null, int? PositionId = null, int? CashAssetId = null, int? DestinationId = null);
    public sealed record CorrectionMovementDto(int Id, DateOnly Date, string Kind, string Description, string Dependency, bool IsReversed);
    public sealed record CorrectionSnapshotDto(int Id, DateOnly Month, DateOnly SnapshotDate, int Revision, bool IsOutdated, bool IsReopened, bool RequiresReopening);
    public sealed record CorrectionPreviewDto(DateOnly FromDate, int? MovementId,
        IReadOnlyList<CorrectionMovementDto> Movements, IReadOnlyList<CorrectionSnapshotDto> Snapshots,
        IReadOnlyList<int> ReviewOrder, IReadOnlyList<string> Warnings);

    public interface ICorrectionPreviewService
    {
        Task<CorrectionPreviewDto?> PreviewAsync(int portfolioId, CorrectionPreviewRequest request, CancellationToken ct);
    }

    public sealed class CorrectionPreviewService(IPortfolioRepository portfolios, IMovementRepository movements,
        IExternalAssetRepository cash, IHistoryRepository history, TimeProvider clock) : ICorrectionPreviewService
    {
        public async Task<CorrectionPreviewDto?> PreviewAsync(int portfolioId, CorrectionPreviewRequest request, CancellationToken ct)
        {
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);
            if (request.Date < new DateOnly(1900, 1, 1) || request.Date > today)
                throw new InputValidationException("Informe uma data entre 01/01/1900 e hoje.");
            if (await portfolios.GetByIdAsync(portfolioId, ct) is null) return null;
            if (request.PositionId.HasValue && await portfolios.GetPositionAsync(portfolioId, request.PositionId.Value, ct) is null)
                throw new InputValidationException("Selecione uma posição desta carteira.");
            if (request.CashAssetId.HasValue && await cash.GetByIdAsync(portfolioId, request.CashAssetId.Value, ct) is null)
                throw new InputValidationException("Selecione um saldo desta carteira.");
            if (request.DestinationId.HasValue && await cash.GetByIdAsync(portfolioId, request.DestinationId.Value, ct) is null)
                throw new InputValidationException("Selecione um destino desta carteira.");
            var all = await movements.ListAsync(portfolioId, ct);
            if (request.MovementId.HasValue && !all.Any(x => x.Id == request.MovementId))
                throw new InputValidationException("Movimentação não encontrada nesta carteira.");
            if (!request.MovementId.HasValue && !request.PositionId.HasValue && !request.CashAssetId.HasValue && !request.DestinationId.HasValue)
                throw new InputValidationException("Selecione a posição e/ou os saldos envolvidos no lançamento atrasado.");
            return Calculate(request, all, await history.GetSnapshotsAsync(portfolioId, ct));
        }

        public static CorrectionPreviewDto Calculate(CorrectionPreviewRequest request, IReadOnlyList<FinancialMovement> all,
            IReadOnlyList<PortfolioSnapshot> snapshots)
        {
            var original = request.MovementId.HasValue ? all.Single(x => x.Id == request.MovementId) : null;
            if (original?.Kind is "income-conversion" or "reopen" or "correction")
                throw new InputValidationException("Este registro não altera saldos. Use o fluxo próprio de conversão ou fechamento.");
            var from = original is not null && original.Date < request.Date ? original.Date : request.Date;
            var positions = new HashSet<int>();
            var balances = new HashSet<int>();
            void Include(FinancialMovement m)
            {
                positions.UnionWith(m.Effects.Where(e => e.PositionId.HasValue).Select(e => e.PositionId!.Value));
                balances.UnionWith(m.Effects.Where(e => e.CashAssetId.HasValue).Select(e => e.CashAssetId!.Value));
            }
            if (original is not null) Include(original);
            if (request.PositionId.HasValue) positions.Add(request.PositionId.Value);
            if (request.CashAssetId.HasValue) balances.Add(request.CashAssetId.Value);
            if (request.DestinationId.HasValue) balances.Add(request.DestinationId.Value);
            var directPositions = positions.ToHashSet();
            var directBalances = balances.ToHashSet();
            static bool Unknown(FinancialMovement m) => !m.Effects.Any(e => e.PositionId.HasValue || e.CashAssetId.HasValue);
            static bool Shares(FinancialMovement m, HashSet<int> p, HashSet<int> b) => m.Effects.Any(e =>
                (e.PositionId.HasValue && p.Contains(e.PositionId.Value)) || (e.CashAssetId.HasValue && b.Contains(e.CashAssetId.Value)));

            // Conservative closure: include the whole day (no intraday operation time) and registration dependencies.
            var candidates = all.Where(x => x.Id != original?.Id && x.Kind is not ("income-conversion" or "reopen" or "correction")
                && (x.Date >= from || (original is not null && x.Id > original.Id))).ToArray();
            var affected = new Dictionary<int, FinancialMovement>();
            var unknownOriginal = original is not null && Unknown(original);
            bool changed;
            do
            {
                changed = false;
                foreach (var candidate in candidates)
                {
                    if (affected.ContainsKey(candidate.Id)) continue;
                    if (!unknownOriginal && !Unknown(candidate) && !Shares(candidate, positions, balances)) continue;
                    affected.Add(candidate.Id, candidate); Include(candidate); changed = true;
                }
            } while (changed);
            if (original is not null) affected.Add(original.Id, original);
            var reversed = all.Where(x => x.ReversalOfId.HasValue).Select(x => x.ReversalOfId!.Value).ToHashSet();
            var ordered = affected.Values.OrderBy(x => x.Date).ThenBy(x => x.Id).ToArray();
            // Include an earlier registration discovered through dependencies when listing affected snapshots.
            var impactFrom = ordered.Select(x => x.Date).Append(from).Min();
            var photos = snapshots.Where(x => x.SnapshotDate >= impactFrom).OrderBy(x => x.SnapshotDate)
                .Select(x => new CorrectionSnapshotDto(x.Id, x.Month, x.SnapshotDate, x.Revision, x.IsOutdated,
                    x.IsReopened, x.SnapshotDate > impactFrom && !x.IsReopened)).ToArray();
            var warnings = new List<string> {
                "Prévia de dependências, sem alteração de dados. Não calcula novos saldos nem confirma que a correção pode ser executada.",
                "A análise é conservadora: inclui operações no mesmo dia e dependências indiretas. Se a correção envolver outra posição ou saldo, inclua-os na consulta.",
                "Quantidades, custos, taxas, câmbio e saldo disponível precisam ser validados com os valores corrigidos antes de qualquer execução. Atualize a prévia se houver novos lançamentos."
            };
            if (ordered.Any(Unknown)) warnings.Add("Há registros sem efeitos auditáveis suficientes. A cadeia pode estar incompleta e exige conferência manual.");
            if (ordered.Any(x => reversed.Contains(x.Id) || x.ReversalOfId.HasValue)) warnings.Add("Há estornos na cadeia. Originais estornados são exibidos para auditoria e não devem ser estornados novamente.");
            if (ordered.Any(x => x.Kind is "opening" or "position-adjustment" or "adjustment")) warnings.Add("Há saldos iniciais ou ajustes absolutos. Não é seguro reaplicar diferenças sem conferir o saldo e a intenção de cada ajuste.");
            if (photos.Length > 0) warnings.Add("Fotografias listadas precisam de revisão. Nenhuma será reconstruída com preços ou câmbio atuais; versões anteriores devem ser preservadas.");
            return new CorrectionPreviewDto(impactFrom, original?.Id,
                ordered.Select(x => new CorrectionMovementDto(x.Id, x.Date, x.Kind, x.Description,
                    x.Id == original?.Id ? "original" : Unknown(x) ? "unknown" : Shares(x, directPositions, directBalances) ? "direct" : "indirect",
                    reversed.Contains(x.Id))).ToArray(), photos,
                ordered.Where(x => !reversed.Contains(x.Id) && x.ReversalOfId is null && x.Kind is not ("opening" or "historical"))
                    .OrderByDescending(x => x.Id).Select(x => x.Id).ToArray(), warnings);
        }
    }
}
