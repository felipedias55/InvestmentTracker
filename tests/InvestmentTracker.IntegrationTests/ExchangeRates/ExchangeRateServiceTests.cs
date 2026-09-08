using System.Net;
using System.Text;
using InvestmentTracker.Application.ExchangeRates.Interfaces;
using InvestmentTracker.Domain.Entities;
using InvestmentTracker.Infrastructure.ExchangeRates;
using Microsoft.Extensions.Logging.Abstractions;

namespace InvestmentTracker.IntegrationTests.ExchangeRates
{
    public class ExchangeRateServiceTests
    {
        private sealed class Clock : TimeProvider
        {
            public DateTimeOffset Now { get; set; } = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
            public override DateTimeOffset GetUtcNow() => Now;
        }
        private sealed class MemoryCache : IExchangeRateCache
        {
            public ExchangeRate? Value { get; set; }
            public Task<ExchangeRate?> GetAsync(string b, string q, CancellationToken ct) => Task.FromResult(Value);
            public Task StoreAsync(ExchangeRate rate, CancellationToken ct) { Value = rate; return Task.CompletedTask; }
        }
        private sealed class Provider : IExchangeRateProvider
        {
            public int Calls { get; private set; }
            public ExchangeRate? Value { get; set; }
            public Exception? Error { get; set; }
            public Task<ExchangeRate?> FetchAsync(string b, string q, CancellationToken ct)
            { Calls++; return Error is null ? Task.FromResult(Value) : Task.FromException<ExchangeRate?>(Error); }
        }
        private static ExchangeRate Rate(DateTime utc, int daysOld = 0) => new() { BaseCode = "USD", QuoteCode = "BRL",
            Rate = 5.1211m, RateDate = DateOnly.FromDateTime(utc.AddDays(-daysOld)), FetchedAtUtc = utc };

        [Fact]
        public async Task Cache_ShouldFetchOnlyOncePerUtcDayAndReturnReferenceDate()
        {
            var clock = new Clock(); var cache = new MemoryCache(); var provider = new Provider { Value = Rate(clock.Now.UtcDateTime, 3) };
            var coordinator = new ExchangeRateRefreshCoordinator();
            var service = new CachedExchangeRateService(provider, cache, coordinator, clock, NullLogger<CachedExchangeRateService>.Instance);
            var first = await service.GetAsync("USD", "BRL");
            var second = await service.GetAsync("USD", "BRL");
            Assert.Equal(1, provider.Calls);
            Assert.Equal(new DateOnly(2026, 9, 4), second!.RateDate);
            Assert.True(first!.IsStale);
            Assert.False(first.IsFallback);
            clock.Now = clock.Now.AddDays(1);
            provider.Value = Rate(clock.Now.UtcDateTime);
            await service.GetAsync("USD", "BRL");
            Assert.Equal(2, provider.Calls);
        }

        [Fact]
        public async Task Outage_ShouldReusePersistentCacheAndBackOffRetries()
        {
            var clock = new Clock(); var cache = new MemoryCache { Value = Rate(clock.Now.UtcDateTime.AddDays(-1)) };
            var provider = new Provider { Error = new HttpRequestException("offline") };
            var service = new CachedExchangeRateService(provider, cache, new(), clock, NullLogger<CachedExchangeRateService>.Instance);
            var result = await service.GetAsync("USD", "BRL");
            await service.GetAsync("USD", "BRL");
            Assert.True(result!.IsFallback); Assert.True(result.IsStale); Assert.Equal(5.1211m, result.Rate);
            Assert.Equal(1, provider.Calls);
            clock.Now = clock.Now.AddMinutes(16);
            provider.Error = null; provider.Value = Rate(clock.Now.UtcDateTime);
            Assert.False((await service.GetAsync("USD", "BRL"))!.IsFallback);
            Assert.Equal(2, provider.Calls);
        }

        [Fact]
        public async Task NoCacheAndNoRate_ShouldReturnUnavailableRatherThanIdentity()
        {
            var provider = new Provider();
            var service = new CachedExchangeRateService(provider, new MemoryCache(), new(), new Clock(), NullLogger<CachedExchangeRateService>.Instance);
            Assert.Null(await service.GetAsync("USD", "BRL"));
            Assert.Equal(1m, (await service.GetAsync("BRL", "BRL"))!.Rate);
            Assert.Equal(1, provider.Calls);
        }

        [Fact]
        public async Task CallerCancellation_ShouldPropagate()
        {
            using var cts = new CancellationTokenSource(); cts.Cancel();
            var service = new CachedExchangeRateService(new Provider(), new MemoryCache(), new(), new Clock(), NullLogger<CachedExchangeRateService>.Instance);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetAsync("USD", "BRL", cts.Token));
        }

        private sealed class Handler(string json, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
        {
            public Uri? RequestedUri { get; private set; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            { RequestedUri = request.RequestUri; return Task.FromResult(new HttpResponseMessage(status)
                { Content = new StringContent(json, Encoding.UTF8, "application/json") }); }
        }

        [Fact]
        public async Task Frankfurter_ShouldParseV2ContractAndSendOnlyCurrencyCodes()
        {
            using var handler = new Handler("""{"date":"2026-09-07","base":"USD","quote":"BRL","rate":5.1211}""");
            using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.frankfurter.dev/") };
            var rate = await new FrankfurterClient(http, new Clock()).FetchAsync("USD", "BRL", default);
            Assert.Equal(5.1211m, rate!.Rate);
            Assert.Equal("https://api.frankfurter.dev/v2/rate/USD/BRL", handler.RequestedUri!.AbsoluteUri);
        }

        [Theory]
        [InlineData("""{"date":"2026-09-07","base":"BRL","quote":"USD","rate":5}""")]
        [InlineData("""{"date":"2026-09-07","base":"USD","quote":"BRL","rate":0}""")]
        [InlineData("""{"date":"2030-01-01","base":"USD","quote":"BRL","rate":5}""")]
        [InlineData("""{}""")]
        public async Task Frankfurter_ShouldRejectInvalidResponses(string json)
        {
            using var handler = new Handler(json);
            using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.frankfurter.dev/") };
            await Assert.ThrowsAsync<InvalidDataException>(() => new FrankfurterClient(http, new Clock()).FetchAsync("USD", "BRL", default));
        }
    }
}
