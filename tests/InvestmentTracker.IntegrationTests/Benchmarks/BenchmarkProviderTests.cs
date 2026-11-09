using System.Net;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using InvestmentTracker.Infrastructure.Benchmarks;
using InvestmentTracker.Infrastructure.Quotes;
using Microsoft.Extensions.Configuration;

namespace InvestmentTracker.IntegrationTests.Benchmarks
{
    public sealed class BenchmarkProviderTests : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "InvestmentTracker-benchmark-" + Guid.NewGuid());
        private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero); }
        private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
        {
            public int Calls { get; private set; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { Calls++; return Task.FromResult(handle(request)); }
        }
        private IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Quotes:BrapiToken"] = "fake-benchmark", ["Quotes:StateDirectory"] = directory }).Build();
        private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        private static readonly DateOnly Start = new(2026, 1, 31), End = new(2026, 2, 28);
        private string StateFile => Path.Combine(directory, "brapi-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("fake-benchmark"))) + ".json");
        [Fact]
        public async Task BcbUsesFixedSeriesDateBoundsInvariantDecimalsAndCacheWithoutKeys()
        {
            var handler = new Handler(request => { Assert.Equal("api.bcb.gov.br", request.RequestUri!.Host); Assert.Contains("bcdata.sgs.12", request.RequestUri.AbsolutePath); Assert.Contains("dataInicial", request.RequestUri.Query); Assert.Null(request.Headers.Authorization); return Json("""[{"data":"02/02/2026","valor":"0.054321"}]"""); });
            var provider = new BenchmarkProvider(new(handler), Config(), new Clock());
            Assert.Equal(.054321m, Assert.Single((await provider.FetchAsync("CDI", Start, End, default)).Points).Value);
            Assert.True((await provider.FetchAsync("CDI", Start, End, default)).FromCache); Assert.Equal(1, handler.Calls);
            Assert.NotNull((await provider.FetchAsync("arbitrary", Start, End, default)).Error); Assert.Equal(1, handler.Calls);
        }
        [Fact]
        public async Task FredUsesPriceIndexSkipsClosedDaysAndNeverInventsPrices()
        {
            var handler = new Handler(request => { Assert.Equal("fred.stlouisfed.org", request.RequestUri!.Host); Assert.Contains("id=SP500", request.RequestUri.Query); return new(HttpStatusCode.OK) { Content = new StringContent("observation_date,SP500\n2026-01-30,100\n2026-02-02,.\n2026-02-27,110\n") }; });
            var result = await new BenchmarkProvider(new(handler), Config(), new Clock()).FetchAsync("SP500", Start, End, default);
            Assert.Null(result.Error); Assert.Equal(2, result.Points.Count); Assert.Equal(110, result.Points[^1].Value);
        }
        private static string Ibov() => """{"results":[{"symbol":"^BVSP","data":{"usedInterval":"1d","historicalDataPrice":[{"date":1769731200,"close":100},{"date":1772150400,"close":105}]}}]}""";
        [Fact]
        public async Task IbovespaUsesDurableSharedQuotaAndCacheAcrossInstances()
        {
            var handler = new Handler(request => { Assert.Contains("/api/v2/stocks/historical", request.RequestUri!.AbsolutePath); Assert.Contains("%5EBVSP", request.RequestUri.Query, StringComparison.OrdinalIgnoreCase); Assert.Equal("fake-benchmark", request.Headers.Authorization!.Parameter); return Json(Ibov()); });
            var first = await new BenchmarkProvider(new(handler), Config(), new Clock()).FetchAsync("IBOV", Start, End, default);
            var second = await new BenchmarkProvider(new(handler), Config(), new Clock()).FetchAsync("IBOV", Start, End, default);
            Assert.Null(first.Error); Assert.True(second.FromCache); Assert.Equal(1, handler.Calls);
            var state = JsonSerializer.Deserialize<QuoteRequestState>(await File.ReadAllTextAsync(StateFile))!; Assert.Single(state.Attempts);
        }
        [Fact]
        public async Task MonthlyProtectionPreventsAnotherRequestAndRateLimitBlocksQuoteClientToo()
        {
            Directory.CreateDirectory(directory); var now = new Clock().GetUtcNow();
            await File.WriteAllTextAsync(StateFile, JsonSerializer.Serialize(new QuoteRequestState { Attempts = Enumerable.Repeat(now, 15000).ToList() }));
            var handler = new Handler(_ => new(HttpStatusCode.TooManyRequests));
            var limited = await new BenchmarkProvider(new(handler), Config(), new Clock()).FetchAsync("IBOV", Start, End, default);
            Assert.NotNull(limited.Error); Assert.Equal(0, handler.Calls);
            await File.WriteAllTextAsync(StateFile, JsonSerializer.Serialize(new QuoteRequestState()));
            Assert.NotNull((await new BenchmarkProvider(new(handler), Config(), new Clock()).FetchAsync("IBOV", Start, End, default)).Error);
            var quote = await new FreeAssetQuoteProvider(new(handler), Config(), new Clock()).FetchAsync("BR", "TEST3", default);
            Assert.Equal("temporary-limit", quote.Status); Assert.Equal(1, handler.Calls);
        }
        [Theory]
        [InlineData("{\"error\":\"private upstream detail\"}")]
        [InlineData("<html>unavailable</html>")]
        public async Task InvalidResponsesReturnAnUnavailableSeriesWithoutLeakingBody(string body)
        {
            var result = await new BenchmarkProvider(new(new Handler(_ => Json(body))), Config(), new Clock()).FetchAsync("CDI", Start, End, default);
            Assert.Empty(result.Points); Assert.NotNull(result.Error); Assert.DoesNotContain("private", result.Error);
        }
        [Fact]
        public async Task UnsupportedBrapiIntervalIsNotMistakenForDailyHistory()
        {
            var result = await new BenchmarkProvider(new(new Handler(_ => Json(Ibov().Replace("1d", "1mo")))), Config(), new Clock()).FetchAsync("IBOV", Start, End, default);
            Assert.Empty(result.Points); Assert.NotNull(result.Error);
        }
        [Fact]
        public async Task ProviderFailureIsCachedBrieflyInsteadOfRepeatedOnEveryClick()
        {
            var handler = new Handler(_ => new(HttpStatusCode.ServiceUnavailable)); var provider = new BenchmarkProvider(new(handler), Config(), new Clock());
            Assert.NotNull((await provider.FetchAsync("SP500", Start, End, default)).Error);
            var retry = await provider.FetchAsync("SP500", Start, End, default);
            Assert.True(retry.FromCache); Assert.NotNull(retry.Error); Assert.Equal(1, handler.Calls);
        }
        [Fact]
        public async Task CallerCancellationIsNotConvertedIntoAnUnavailableBenchmark()
        {
            var handler = new Handler(_ => throw new Exception("No request expected")); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new BenchmarkProvider(new(handler), Config(), new Clock()).FetchAsync("CDI", Start, End, cancellation.Token));
            Assert.Equal(0, handler.Calls);
        }
        private sealed class FredFallback : IFredCsvFallback
        {
            public int Calls { get; private set; }
            public Task<string> FetchAsync(DateOnly start, DateOnly end, CancellationToken ct)
            {
                Calls++; Assert.Equal(Start.AddDays(-10), start); Assert.Equal(End, end);
                return Task.FromResult("observation_date,SP500\n2026-01-30,100\n2026-02-27,110\n");
            }
        }
        [Fact]
        public async Task FredFallsBackOnlyOnTransportFailureAndStillUsesTheSameParserAndCache()
        {
            var fallback = new FredFallback(); var handler = new Handler(_ => throw new HttpRequestException("Synthetic reset"));
            var provider = new BenchmarkProvider(new(handler), Config(), new Clock(), fredFallback: fallback);
            Assert.Equal(2, (await provider.FetchAsync("SP500", Start, End, default)).Points.Count);
            Assert.True((await provider.FetchAsync("SP500", Start, End, default)).FromCache); Assert.Equal(1, fallback.Calls);
        }
        [Theory]
        [InlineData(HttpStatusCode.Forbidden)]
        [InlineData(HttpStatusCode.TooManyRequests)]
        public async Task FredDoesNotUseFallbackToBypassAnHttpRefusal(HttpStatusCode status)
        {
            var fallback = new FredFallback(); var provider = new BenchmarkProvider(new(new Handler(_ => new(status))), Config(), new Clock(), fredFallback: fallback);
            Assert.NotNull((await provider.FetchAsync("SP500", Start, End, default)).Error); Assert.Equal(0, fallback.Calls);
        }
        public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
