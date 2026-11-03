using System.Text.Json.Serialization;
using InvestmentTracker.Application.Allocation.Dtos;
using InvestmentTracker.Application.Strategies;
using InvestmentTracker.Domain.Entities;

namespace InvestmentTracker.Application.Allocation.Tracking
{
    public sealed record SaveContributionPlan(Guid RequestId, string Name, DateOnly From, DateOnly To,
        ContributionPlanRequestDto Simulation, string ExpectedToken);
    public sealed record ContributionPlanSnapshot(string Currency, ContributionPlanRequestDto Simulation,
        ContributionJointPlanDto Plan, IReadOnlyList<string> Notes);
    public sealed record LinkPurchase(int TradeId, string RowKey,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? BaseAmount = null);
    public sealed record TrackingPlanSummary(int Id, string Name, DateOnly From, DateOnly To, DateTime CreatedAtUtc);
    public sealed record TrackingPlanPage(IReadOnlyList<TrackingPlanSummary> Items, int Total, int PageIndex, int PageSize);
    public sealed record PurchaseCandidate(int TradeId, DateOnly Date, string Ticker, string Currency, int CategoryId, int SectorId,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Amount,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? BaseAmount,
        bool Active);
    public sealed record PurchasePage(IReadOnlyList<PurchaseCandidate> Items, int Total, int PageIndex, int PageSize);
    public sealed record TrackedPurchase(int Id, int TradeId, string RowKey, DateOnly Date, string Ticker, string Currency,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Amount,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal BaseAmount,
        string ConversionSource, DateTime CreatedAtUtc, DateTime? RemovedAtUtc, bool Active);
    public sealed record TrackingRow(ContributionPlanRowDto Planned,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Realized,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Difference,
        string Status);
    public sealed record TrackingPlanDetail(TrackingPlanSummary Plan, ContributionPlanSnapshot Snapshot,
        IReadOnlyList<TrackingRow> Rows, IReadOnlyList<TrackedPurchase> Purchases,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Realized,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Difference,
        string Status);
    public sealed record TrackingData(SavedContributionPlan Plan, IReadOnlyList<TrackedPurchase> Purchases);
    public interface IContributionTrackingRepository
    {
        Task<TrackingPlanPage?> PageAsync(int portfolioId, int page, int size, CancellationToken ct);
        Task<TrackingData?> ReadAsync(int portfolioId, int id, CancellationToken ct);
        Task<int?> SaveAsync(int portfolioId, SaveContributionPlan request, Func<Task<ContributionPlanSnapshot>> create, CancellationToken ct);
        Task<PurchasePage?> CandidatesAsync(int portfolioId, int planId, int page, int size, CancellationToken ct);
        Task<bool> LinkAsync(int portfolioId, int planId, LinkPurchase request, CancellationToken ct);
        Task<bool> UnlinkAsync(int portfolioId, int planId, int linkId, CancellationToken ct);
    }
}
