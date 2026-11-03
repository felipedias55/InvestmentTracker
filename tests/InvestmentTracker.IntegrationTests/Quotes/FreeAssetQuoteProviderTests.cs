using System.Net;
using InvestmentTracker.Infrastructure.Quotes;
using Microsoft.Extensions.Configuration;

namespace InvestmentTracker.IntegrationTests.Quotes
{
    public class FreeAssetQuoteProviderTests : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "InvestmentTracker-quote-tests-" + Guid.NewGuid());
        public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        private sealed class Clock : TimeProvider
        {
            private DateTimeOffset now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
            public void Advance(TimeSpan interval) => now += interval;
            public override DateTimeOffset GetUtcNow() { now = now.AddSeconds(30); return now; }
        }
        private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
        {
            public int Calls { get; private set; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            { Calls++; return Task.FromResult(handle(request)); }
        }
        private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK) { Content = new StringContent(value) };
        private FreeAssetQuoteProvider Provider(Handler handler, bool configured = true, Clock? clock = null)
            => new(new HttpClient(handler), new ConfigurationBuilder().AddInMemoryCollection(configured
                ? new Dictionary<string, string?> { ["Quotes:BrapiToken"] = "fake-br", ["Quotes:AlphaVantageKey"] = "fake-us", ["Quotes:StateDirectory"] = directory }
                : []).Build(), clock ?? new Clock());

        [Fact]
        public async Task MissingKeysShouldNotCallNetwork()
        {
            var handler = new Handler(_ => throw new Exception("Unexpected network"));
            Assert.Equal("not-configured", (await Provider(handler, false).FetchAsync("US", "MSFT", default)).Status);
            Assert.Equal(0, handler.Calls);
        }
        [Fact]
        public async Task BrazilShouldUseBearerAndRejectAutomaticSymbolChanges()
        {
            var handler = new Handler(request =>
            {
                Assert.Equal("brapi.dev", request.RequestUri!.Host);
                Assert.Equal("fake-br", request.Headers.Authorization!.Parameter);
                Assert.DoesNotContain("fake-br", request.RequestUri.ToString());
                return Json("""{"results":[{"symbol":"PETR4","requestedSymbol":"PETR4","changed":false,"data":{"currency":"BRL","regularMarketPrice":31.23,"regularMarketTime":"2026-10-06T20:00:00Z"}}]}""");
            });
            var provider = Provider(handler);
            var first = await provider.FetchAsync("BR", "PETR4", default);
            Assert.Equal(31.23m, first.Price);
            Assert.Equal(DateTimeOffset.Parse("2026-10-06T20:00:00Z"), first.MarketTime);
            Assert.NotNull(first.RetrievedAt);
            Assert.True((await provider.FetchAsync("BR", "PETR4", default)).FromCache);
            await provider.FetchAsync("BR", "PETR4", default);
            Assert.Equal(1, handler.Calls);
            var wrong = Provider(new Handler(_ => Json("""{"results":[{"symbol":"NEW3","requestedSymbol":"OLD3","changed":true}]}""")));
            Assert.Null((await wrong.FetchAsync("BR", "OLD3", default)).Price);
        }
        [Fact]
        public async Task AlphaShouldUseDailyFreeEndpointAndCacheItsResult()
        {
            var handler = new Handler(request =>
            {
                Assert.Contains("GLOBAL_QUOTE", request.RequestUri!.Query);
                Assert.DoesNotContain("entitlement", request.RequestUri.Query);
                return Json("""{"Global Quote":{"01. symbol":"MSFT","05. price":"100.1234","07. latest trading day":"2026-10-06"}}""");
            });
            var provider = Provider(handler);
            var quote = await provider.FetchAsync("US", "MSFT", default);
            Assert.Equal(100.1234m, quote.Price);
            Assert.Null(quote.MarketTime);
            Assert.NotNull(quote.RetrievedAt);
            Assert.Equal(new DateOnly(2026, 10, 6), quote.Date);
            Assert.True((await provider.FetchAsync("US", "MSFT", default)).FromCache);
            Assert.Equal(1, handler.Calls);
        }
        [Fact]
        public async Task DailyLimitShouldSurviveRestartAndNeverExposeProviderMessage()
        {
            var handler = new Handler(_ => Json("""{"Information":"25 requests per day; subscribe premium; secret"}"""));
            var provider = Provider(handler);
            var result = await provider.FetchAsync("US", "MSFT", default);
            Assert.Equal("daily-limit", result.Status);
            Assert.DoesNotContain("secret", result.Message);
            Assert.Equal("daily-limit", (await Provider(handler).FetchAsync("US", "NVDA", default)).Status);
            Assert.Equal(1, handler.Calls);
        }
        private static HttpResponseMessage Quote(string symbol) => Json(
            $$$"""{"Global Quote":{"01. symbol":"{{{symbol}}}","05. price":"100.1234","07. latest trading day":"2026-10-06"}}""");

        private FreeAssetQuoteProvider MappedProvider(Handler handler, string mapped)
            => new(new HttpClient(handler), new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Quotes:AlphaVantageKey"] = "fake-us",
                    ["Quotes:StateDirectory"] = directory,
                    ["Quotes:Symbols:US:LOCAL"] = mapped
                }).Build(), new Clock());

        [Fact]
        public async Task ExplicitMappingShouldValidateProviderSymbolAndShareDurableCache()
        {
            var handler = new Handler(request =>
            {
                Assert.Contains("symbol=MSFT", request.RequestUri!.Query);
                return Quote("MSFT");
            });
            var quote = await MappedProvider(handler, "MSFT").FetchAsync("US", "LOCAL", default);
            Assert.Equal("LOCAL", quote.Symbol);
            Assert.NotNull(quote.Price);
            Assert.Contains("Símbolo consultado: MSFT", quote.Message);
            var cached = await Provider(handler).FetchAsync("US", "MSFT", default);
            Assert.Equal("MSFT", cached.Symbol);
            Assert.True(cached.FromCache);
            Assert.DoesNotContain("LOCAL", cached.Message);
            Assert.Equal(1, handler.Calls);
        }

        [Theory]
        [InlineData("")]
        [InlineData("msft")]
        [InlineData("MSFT&apikey=other")]
        public async Task InvalidMappingShouldNotConsumeQuota(string mapped)
        {
            var handler = new Handler(_ => throw new Exception("Unexpected network"));
            var quote = await MappedProvider(handler, mapped).FetchAsync("US", "LOCAL", default);
            Assert.Equal("invalid-mapping", quote.Status);
            Assert.False(quote.Requested);
            Assert.Null(quote.Price);
            Assert.Equal(0, handler.Calls);
            Assert.False(Directory.Exists(directory));
        }

        [Fact]
        public async Task MappingShouldNotAcceptAnUnexpectedReturnedSymbol()
        {
            var handler = new Handler(_ => Quote("OTHER"));
            var quote = await MappedProvider(handler, "MSFT").FetchAsync("US", "LOCAL", default);
            Assert.Equal("LOCAL", quote.Symbol);
            Assert.Equal("invalid", quote.Status);
            Assert.Null(quote.Price);
        }

        [Fact]
        public async Task ChangedMappingShouldNotReuseThePreviousSymbolsPrice()
        {
            var handler = new Handler(request => Quote(request.RequestUri!.Query.Contains("MSFT") ? "MSFT" : "NVDA"));
            await MappedProvider(handler, "MSFT").FetchAsync("US", "LOCAL", default);
            var quote = await MappedProvider(handler, "NVDA").FetchAsync("US", "LOCAL", default);
            Assert.False(quote.FromCache);
            Assert.Contains("Símbolo consultado: NVDA", quote.Message);
            Assert.Equal(2, handler.Calls);
        }

        [Fact]
        public async Task BrazilMappingShouldKeepExactIdentityAndRejectProviderRenames()
        {
            var handler = new Handler(request =>
            {
                Assert.Contains("symbols=PETR4", request.RequestUri!.Query);
                return Json("""{"results":[{"symbol":"PETR4","requestedSymbol":"PETR4","changed":false,"data":{"currency":"BRL","regularMarketPrice":31.23,"regularMarketTime":"2026-10-06T20:00:00Z"}}]}""");
            });
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Quotes:BrapiToken"] = "fake-br",
                ["Quotes:Symbols:BR:LOCAL"] = "PETR4",
                ["Quotes:StateDirectory"] = directory
            }).Build();
            var provider = new FreeAssetQuoteProvider(new HttpClient(handler), configuration, new Clock());
            var quote = await provider.FetchAsync("BR", "LOCAL", default);
            Assert.Equal("LOCAL", quote.Symbol);
            Assert.Equal(31.23m, quote.Price);
            Assert.True((await provider.FetchAsync("BR", "LOCAL", default)).FromCache);
            Assert.Equal(1, handler.Calls);
            var wrong = new Handler(_ => Json("""{"results":[{"symbol":"OTHER3","requestedSymbol":"PETR4","changed":true}]}"""));
            configuration["Quotes:StateDirectory"] = Path.Combine(directory, "rejected");
            var rejected = await new FreeAssetQuoteProvider(new HttpClient(wrong), configuration, new Clock())
                .FetchAsync("BR", "LOCAL", default);
            Assert.Null(rejected.Price);
            Assert.Equal("invalid", rejected.Status);
        }

        [Fact]
        public async Task UnknownNoticeShouldNotBlockOtherSymbols()
        {
            var handler = new Handler(request => request.RequestUri!.Query.Contains("MSFT")
                ? Json("""{"Information":"Unexpected notice with secret"}""") : Quote("NVDA"));
            var provider = Provider(handler);
            var first = await provider.FetchAsync("US", "MSFT", default);
            Assert.Equal("provider-notice", first.Status);
            Assert.DoesNotContain("secret", first.Message);
            Assert.NotNull((await provider.FetchAsync("US", "NVDA", default)).Price);
            Assert.Equal(2, handler.Calls);
        }

        [Fact]
        public async Task TemporaryLimitShouldHonorRetryAfterAndResumeWithoutExhaustingDailyBudget()
        {
            var clock = new Clock();
            var calls = 0;
            var handler = new Handler(_ =>
            {
                if (++calls > 1) return Quote("NVDA");
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.RetryAfter = new(System.TimeSpan.FromMinutes(10));
                return response;
            });
            var first = await Provider(handler, clock: clock).FetchAsync("US", "MSFT", default);
            Assert.Equal("temporary-limit", first.Status);
            var blocked = await Provider(handler, clock: clock).FetchAsync("US", "NVDA", default);
            Assert.False(blocked.Requested);
            Assert.Equal(first.RetryAfter, blocked.RetryAfter);
            Assert.Equal(1, handler.Calls);
            clock.Advance(TimeSpan.FromMinutes(11));
            Assert.NotNull((await Provider(handler, clock: clock).FetchAsync("US", "NVDA", default)).Price);
            Assert.Equal(2, handler.Calls);
        }

        [Fact]
        public async Task CacheAndBudgetShouldSurviveRestart()
        {
            var clock = new Clock();
            var handler = new Handler(_ => Quote("MSFT"));
            await Provider(handler, clock: clock).FetchAsync("US", "MSFT", default);
            var cached = await Provider(handler, clock: clock).FetchAsync("US", "MSFT", default);
            Assert.True(cached.FromCache);
            Assert.False(cached.Requested);
            Assert.Equal(1, handler.Calls);
            // Failed attempts also consume the local allowance; they must not reset on restart.
            var failing = new Handler(_ => new(HttpStatusCode.InternalServerError));
            for (var i = 0; i < 24; i++) await Provider(failing, clock: clock).FetchAsync("US", "FAIL", default);
            var blocked = await Provider(failing, clock: clock).FetchAsync("US", "FAIL", default);
            Assert.Equal("daily-limit", blocked.Status);
            Assert.Equal(24, failing.Calls);
            clock.Advance(TimeSpan.FromHours(24));
            await Provider(failing, clock: clock).FetchAsync("US", "FAIL", default);
            Assert.Equal(25, failing.Calls);
        }

        [Theory]
        [InlineData("API key is invalid", "invalid-key")]
        [InlineData("premium endpoint", "restricted")]
        [InlineData("5 requests per minute; 25 requests per day", "temporary-limit")]
        public async Task NoticesShouldHaveSpecificSafeReasons(string message, string status)
        {
            var handler = new Handler(_ => Json(System.Text.Json.JsonSerializer.Serialize(new { Information = message })));
            Assert.Equal(status, (await Provider(handler).FetchAsync("US", "MSFT", default)).Status);
        }

        [Fact]
        public async Task ConcurrentProvidersShouldShareCacheWithoutDuplicateRequests()
        {
            var handler = new Handler(_ => Quote("MSFT"));
            var results = await Task.WhenAll(Provider(handler).FetchAsync("US", "MSFT", default),
                Provider(handler).FetchAsync("US", "MSFT", default));
            Assert.Equal(1, handler.Calls);
            Assert.Single(results, r => r.FromCache);
        }
        [Fact]
        public async Task CorruptPersistedStateShouldNotSilentlyResetTheBudget()
        {
            var handler = new Handler(_ => Quote("MSFT"));
            await Provider(handler).FetchAsync("US", "MSFT", default);
            var path = Assert.Single(Directory.GetFiles(directory, "*.json"));
            Assert.DoesNotContain("fake-us", await File.ReadAllTextAsync(path));
            await File.WriteAllTextAsync(path, """{"Attempts":null}""");
            var result = await Provider(handler).FetchAsync("US", "NVDA", default);
            Assert.Equal("state-unavailable", result.Status);
            Assert.False(result.Requested);
            Assert.Equal(1, handler.Calls);
        }
        [Theory]
        [InlineData(403)]
        [InlineData(429)]
        [InlineData(500)]
        public async Task HttpFailuresShouldReturnUnavailableWithoutInventingPrices(int status)
        {
            var provider = Provider(new Handler(_ => new((HttpStatusCode)status)));
            Assert.Null((await provider.FetchAsync("BR", "PETR4", default)).Price);
        }
        [Fact]
        public async Task InvalidJsonShouldBeIsolated()
        {
            var provider = Provider(new Handler(_ => Json("not json")));
            Assert.Equal("invalid", (await provider.FetchAsync("BR", "PETR4", default)).Status);
        }

        private static HttpResponseMessage BrazilQuote() => Json("""{"results":[{"symbol":"PETR4","requestedSymbol":"PETR4","changed":false,"data":{"currency":"BRL","regularMarketPrice":31.23,"regularMarketTime":"2026-10-06T20:00:00Z"}}]}""");

        [Fact]
        public async Task BrazilBudgetShouldPersistFailuresBlockAndReleaseAfterRollingWindow()
        {
            var clock = new Clock();
            var handler = new Handler(_ => new(HttpStatusCode.InternalServerError));
            var first = await Provider(handler, clock: clock).FetchAsync("BR", "PETR4", default);
            Assert.Equal(1, first.Usage!.Attempts);
            var path = Assert.Single(Directory.GetFiles(directory, "brapi-*.json"));
            var state = System.Text.Json.JsonSerializer.Deserialize<QuoteRequestState>(await File.ReadAllTextAsync(path))!;
            state.Attempts = Enumerable.Repeat(clock.GetUtcNow(), 15000).ToList();
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(state));
            var blocked = await Provider(handler, clock: clock).FetchAsync("BR", "VALE3", default);
            Assert.Equal("monthly-limit", blocked.Status);
            Assert.Equal(15000, blocked.Usage!.Attempts);
            Assert.NotNull(blocked.RetryAfter);
            Assert.False(blocked.Requested);
            Assert.Equal(1, handler.Calls);
            var cached = await Provider(handler, clock: clock).FetchAsync("BR", "PETR4", default);
            Assert.True(cached.FromCache);
            Assert.Equal(15000, cached.Usage!.Attempts);
            clock.Advance(TimeSpan.FromDays(32));
            var resumed = await Provider(handler, clock: clock).FetchAsync("BR", "VALE3", default);
            Assert.True(resumed.Requested);
            Assert.Equal(1, resumed.Usage!.Attempts);
            Assert.Equal(2, handler.Calls);
        }

        [Fact]
        public async Task BrazilCacheShouldSurviveRestartAndExpireWithoutChangingQuoteTimes()
        {
            var clock = new Clock();
            var handler = new Handler(_ => BrazilQuote());
            var first = await Provider(handler, clock: clock).FetchAsync("BR", "PETR4", default);
            var cached = await Provider(handler, clock: clock).FetchAsync("BR", "PETR4", default);
            Assert.True(cached.FromCache);
            Assert.False(cached.Requested);
            Assert.Equal(first.RetrievedAt, cached.RetrievedAt);
            Assert.Equal(first.MarketTime, cached.MarketTime);
            Assert.Equal(1, handler.Calls);
            clock.Advance(TimeSpan.FromMinutes(31));
            Assert.False((await Provider(handler, clock: clock).FetchAsync("BR", "PETR4", default)).FromCache);
            Assert.Equal(2, handler.Calls);
            var content = await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(directory, "brapi-*.json")));
            Assert.DoesNotContain("fake-br", content);
        }

        [Fact]
        public async Task BrazilPauseShouldSurviveRestartAndApplyToOtherSymbols()
        {
            var clock = new Clock();
            var handler = new Handler(_ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.RetryAfter = new(TimeSpan.FromMinutes(10));
                return response;
            });
            var first = await Provider(handler, clock: clock).FetchAsync("BR", "PETR4", default);
            var blocked = await Provider(handler, clock: clock).FetchAsync("BR", "VALE3", default);
            Assert.Equal("temporary-limit", blocked.Status);
            Assert.Equal(first.RetryAfter, blocked.RetryAfter);
            Assert.False(blocked.Requested);
            Assert.Equal(1, handler.Calls);
            clock.Advance(TimeSpan.FromMinutes(11));
            Assert.True((await Provider(handler, clock: clock).FetchAsync("BR", "VALE3", default)).Requested);
            Assert.Equal(2, handler.Calls);
        }

        [Fact]
        public async Task BrazilFailuresShouldBeCachedBrieflyAndCorruptStateShouldStopRequests()
        {
            var clock = new Clock();
            var handler = new Handler(_ => new(HttpStatusCode.InternalServerError));
            await Provider(handler, clock: clock).FetchAsync("BR", "PETR4", default);
            Assert.True((await Provider(handler, clock: clock).FetchAsync("BR", "PETR4", default)).FromCache);
            clock.Advance(TimeSpan.FromMinutes(3));
            Assert.True((await Provider(handler, clock: clock).FetchAsync("BR", "PETR4", default)).Requested);
            var path = Assert.Single(Directory.GetFiles(directory, "brapi-*.json"));
            await File.WriteAllTextAsync(path, "invalid");
            var result = await Provider(handler, clock: clock).FetchAsync("BR", "VALE3", default);
            Assert.Equal("state-unavailable", result.Status);
            Assert.False(result.Requested);
            Assert.Equal(2, handler.Calls);
        }

        [Fact]
        public async Task ProvidersShouldKeepSeparateStateEvenWithIdenticalKeys()
        {
            var handler = new Handler(request => request.RequestUri!.Host == "brapi.dev" ? BrazilQuote() : Quote("MSFT"));
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Quotes:BrapiToken"] = "same-key", ["Quotes:AlphaVantageKey"] = "same-key",
                ["Quotes:StateDirectory"] = directory
            }).Build();
            var provider = new FreeAssetQuoteProvider(new HttpClient(handler), configuration, new Clock());
            await provider.FetchAsync("US", "MSFT", default);
            await provider.FetchAsync("BR", "PETR4", default);
            Assert.Equal(2, Directory.GetFiles(directory, "*.json").Length);
            Assert.Equal(2, handler.Calls);
        }
    }
}
