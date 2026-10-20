using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InvestmentTracker.Application.Quotes;

namespace InvestmentTracker.Infrastructure.Quotes
{
    public sealed record CachedAssetQuote(AssetQuote Quote, DateTimeOffset Expires);

    public sealed class QuoteRequestState
    {
        public List<DateTimeOffset> Attempts { get; set; } = [];
        public DateTimeOffset NextCall { get; set; }
        public DateTimeOffset BlockedUntil { get; set; }
        public string BlockReason { get; set; } = "";
        public Dictionary<string, CachedAssetQuote> Cache { get; set; } = [];
    }

    // The separate lock survives atomic replacement of the data file, including across API processes.
    internal sealed class QuoteRequestStateFile(FileStream lease, string path) : IDisposable
    {
        public static async Task<QuoteRequestStateFile> OpenAsync(string directory, string token, CancellationToken ct)
        {
            Directory.CreateDirectory(directory);
            var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
            var path = Path.Combine(directory, identity + ".json");
            for (var attempt = 0; ; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                try { return new(new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None), path); }
                catch (IOException) when (attempt < 100) { await Task.Delay(200, ct); }
            }
        }

        public async Task<QuoteRequestState> ReadAsync(CancellationToken ct)
        {
            if (!File.Exists(path)) return new();
            var state = JsonSerializer.Deserialize<QuoteRequestState>(await File.ReadAllTextAsync(path, ct))
                ?? throw new JsonException("Invalid quote state.");
            if (state.Attempts is null || state.Cache is null || state.Cache.Values.Any(value => value is null || value.Quote is null))
                throw new JsonException("Invalid quote state.");
            return state;
        }

        public async Task SaveAsync(QuoteRequestState state, CancellationToken ct)
        {
            var temporary = path + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(state), ct);
            File.Move(temporary, path, true);
        }

        public void Dispose() => lease.Dispose();
    }
}
