using System.Text.RegularExpressions;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.Quotes;

namespace InvestmentTracker.Application.Assets
{
    public sealed record AssetListingChange(string Market, string Symbol);
    public sealed record DiscoveredAsset(string Symbol, string Name, string Market, string? Currency,
        string? Country, string? InstrumentClass, string? Sector, bool HasImage);
    public sealed record AssetDiscoveryResult(string Provider, string Status, string Message, IReadOnlyList<DiscoveredAsset> Items)
    {
        public DateTimeOffset? RetrievedAt { get; init; }
        public DateTimeOffset? RetryAfter { get; init; }
        public bool FromCache { get; init; }
        public QuoteUsage? Usage { get; init; }
    }
    public sealed record SuggestedAssetImage(string Status, string Message, string? Data = null, string? ContentType = null);
    public interface IAssetDiscoveryProvider
    {
        Task<AssetDiscoveryResult> SearchAsync(string market, string query, CancellationToken ct);
        Task<SuggestedAssetImage> ImageAsync(string market, string symbol, CancellationToken ct);
    }
    public sealed class AssetDiscoveryService(IAssetDiscoveryProvider provider)
    {
        public Task<AssetDiscoveryResult> SearchAsync(string market, string query, CancellationToken ct)
        {
            Validate(market, query);
            return provider.SearchAsync(market, query.Trim().ToUpperInvariant(), ct);
        }
        public Task<SuggestedAssetImage> ImageAsync(string market, string symbol, CancellationToken ct)
        {
            Validate(market, symbol);
            return provider.ImageAsync(market, symbol.Trim().ToUpperInvariant(), ct);
        }
        public static void Validate(string market, string symbol)
        {
            if (market is not ("BR" or "US") || symbol is null || !Regex.IsMatch(symbol.Trim(), @"^[A-Za-z0-9][A-Za-z0-9.\-]{0,19}$"))
                throw new InputValidationException("Escolha Brasil ou EUA e informe um ticker de até 20 caracteres.");
        }
    }
    public sealed record ReferenceDefaultsResult(int CountriesAdded, int CurrenciesAdded, string Message);
    public interface IReferenceDefaultsRepository
    {
        Task<ReferenceDefaultsResult> EnsureAsync(CancellationToken ct);
    }
    public sealed class ReferenceDefaultsService(IReferenceDefaultsRepository repository)
    {
        public Task<ReferenceDefaultsResult> EnsureAsync(CancellationToken ct) => repository.EnsureAsync(ct);
    }
}
