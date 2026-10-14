using System.Net;
using InvestmentTracker.Infrastructure.Quotes;
using Microsoft.Extensions.Configuration;

namespace InvestmentTracker.IntegrationTests.Quotes
{
    public class FreeAssetQuoteProviderTests
    {
        private sealed class Clock : TimeProvider
        {
            private DateTimeOffset now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
            public override DateTimeOffset GetUtcNow() { now = now.AddSeconds(2); return now; }
        }
        private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
        {
            public int Calls { get; private set; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            { Calls++; return Task.FromResult(handle(request)); }
        }
        private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK) { Content = new StringContent(value) };
        private static FreeAssetQuoteProvider Provider(Handler handler, bool configured = true)
            => new(new HttpClient(handler), new ConfigurationBuilder().AddInMemoryCollection(configured
                ? new Dictionary<string, string?> { ["Quotes:BrapiToken"] = "fake-br", ["Quotes:AlphaVantageKey"] = "fake-us" }
                : []).Build(), new Clock());

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
            Assert.Equal(first, await provider.FetchAsync("BR", "PETR4", default));
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
            Assert.Equal(quote, await provider.FetchAsync("US", "MSFT", default));
            Assert.Equal(1, handler.Calls);
        }
        [Fact]
        public async Task ProviderLimitShouldStopFurtherUsRequestsAndNeverExposeProviderMessage()
        {
            var handler = new Handler(_ => Json("""{"Information":"Limit reached; secret or upstream detail"}"""));
            var provider = Provider(handler);
            var result = await provider.FetchAsync("US", "MSFT", default);
            Assert.Equal("limit", result.Status);
            Assert.DoesNotContain("secret", result.Message);
            Assert.Equal("limit", (await provider.FetchAsync("US", "NVDA", default)).Status);
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
    }
}
