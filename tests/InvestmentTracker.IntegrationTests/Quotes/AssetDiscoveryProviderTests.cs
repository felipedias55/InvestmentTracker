using System.Net;
using System.Text;
using InvestmentTracker.Infrastructure.Quotes;
using Microsoft.Extensions.Configuration;

namespace InvestmentTracker.IntegrationTests.Quotes
{
    public class AssetDiscoveryProviderTests : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "InvestmentTracker-discovery-tests-" + Guid.NewGuid());
        private sealed class Clock : TimeProvider
        {
            private DateTimeOffset now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
            public override DateTimeOffset GetUtcNow() => now = now.AddSeconds(30);
        }
        private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
        {
            public int Calls { get; private set; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { Calls++; return Task.FromResult(handle(request)); }
        }
        private IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Quotes:BrapiToken"] = "fake-br", ["Quotes:AlphaVantageKey"] = "fake-us", ["Quotes:StateDirectory"] = directory }).Build();
        private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        private static string Brazil(string logo = "https://icons.brapi.dev/icons/TEST11.svg") => $$$"""{"results":[{"symbol":"TEST11","name":"Fundo sintético","exchange":"B3","currency":"BRL","subType":"fii","sector":"Imobiliário","logoUrl":"{{{logo}}}","isActive":true}]}""";
        [Fact]
        public async Task BrazilianResultsAreStructuredCachedAndDoNotInferIssuerCountry()
        {
            var handler = new Handler(r => { Assert.Contains("/api/v2/tickers?search=TEST&limit=20", r.RequestUri!.ToString()); Assert.Equal("fake-br", r.Headers.Authorization!.Parameter); return Json(Brazil()); });
            var provider = new AssetDiscoveryProvider(new(handler), new(new Handler(_ => throw new Exception())), Configuration(), new Clock());
            var result = await provider.SearchAsync("BR", "TEST", default);
            var item = Assert.Single(result.Items); Assert.Equal("fii", item.InstrumentClass); Assert.Null(item.Country); Assert.True(item.HasImage);
            Assert.Equal(1, result.Usage!.Attempts); Assert.True((await provider.SearchAsync("BR", "TEST", default)).FromCache); Assert.Equal(1, handler.Calls);
        }
        [Fact]
        public async Task DiscoveryAndQuotesShareDurableQuotaAcrossInstances()
        {
            var clock = new Clock(); var config = Configuration();
            var handler = new Handler(r => r.RequestUri!.Query.Contains("GLOBAL_QUOTE") ? Json("{}") : Json("""{"bestMatches":[{"1. symbol":"TEST","2. name":"Synthetic","3. type":"Equity","4. region":"United States","8. currency":"USD"}]}"""));
            var http = new HttpClient(handler); var provider = new AssetDiscoveryProvider(http, http, config, clock);
            Assert.Equal("available", (await provider.SearchAsync("US", "TEST", default)).Status);
            var quotes = new FreeAssetQuoteProvider(http, config, clock);
            for (var i = 0; i < 24; i++) await quotes.FetchAsync("US", "Q" + i, default);
            var restarted = new AssetDiscoveryProvider(http, http, config, clock);
            var blocked = await restarted.SearchAsync("US", "OTHER", default); Assert.Equal("daily-limit", blocked.Status); Assert.NotNull(blocked.RetryAfter); Assert.Equal(25, handler.Calls);
        }
        [Fact]
        public async Task UpstreamPausesAreSharedAndPrivateMessagesAreNotExposed()
        {
            var config = Configuration(); var clock = new Clock(); var handler = new Handler(_ => Json("""{"Information":"daily limit per day fake-secret"}""")); var http = new HttpClient(handler);
            var result = await new AssetDiscoveryProvider(http, http, config, clock).SearchAsync("US", "TEST", default);
            Assert.Equal("daily-limit", result.Status); Assert.DoesNotContain("fake-secret", result.Message);
            Assert.Equal("daily-limit", (await new FreeAssetQuoteProvider(http, config, clock).FetchAsync("US", "TEST", default)).Status); Assert.Equal(1, handler.Calls);
        }
        [Theory]
        [InlineData("https://127.0.0.1/logo.png")]
        [InlineData("https://icons.brapi.dev.evil.example/icons/test.svg")]
        [InlineData("http://icons.brapi.dev/icons/test.svg")]
        [InlineData("https://icons.brapi.dev/icons/test.svg?token=secret")]
        public async Task ImagesCannotFetchArbitraryAddresses(string url)
        {
            var images = new Handler(_ => throw new Exception("Unexpected download")); var provider = new AssetDiscoveryProvider(new(new Handler(_ => Json(Brazil(url)))), new(images), Configuration(), new Clock());
            Assert.Equal("unavailable", (await provider.ImageAsync("BR", "TEST11", default)).Status); Assert.Equal(0, images.Calls);
        }
        [Fact]
        public async Task SuggestedSvgIsRestrictedAndImageDownloadsAreCached()
        {
            var images = new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 80 40"><path d="M0 0h80v40H0z" fill="#315e99" onclick="bad()"/></svg>""", Encoding.UTF8, "image/svg+xml") });
            var provider = new AssetDiscoveryProvider(new(new Handler(_ => Json(Brazil()))), new(images), Configuration(), new Clock());
            var result = await provider.ImageAsync("BR", "TEST11", default); Assert.Equal("available", result.Status); Assert.Equal("image/svg+xml", result.ContentType);
            var svg = Encoding.UTF8.GetString(Convert.FromBase64String(result.Data!)); Assert.DoesNotContain("onclick", svg); Assert.Contains("width=\"128\"", svg);
            await provider.ImageAsync("BR", "TEST11", default); Assert.Equal(1, images.Calls);
        }
        [Fact]
        public async Task ActiveSvgAndRedirectsDoNotBecomeImageSuggestions()
        {
            var imageHandler = new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent("""<svg xmlns="http://www.w3.org/2000/svg"><script>bad()</script></svg>""", Encoding.UTF8, "image/svg+xml") });
            var provider = new AssetDiscoveryProvider(new(new Handler(_ => Json(Brazil()))), new(imageHandler), Configuration(), new Clock());
            Assert.Equal("unavailable", (await provider.ImageAsync("BR", "TEST11", default)).Status);
            provider = new(new(new Handler(_ => Json(Brazil()))), new(new Handler(_ => new(HttpStatusCode.Redirect) { Headers = { Location = new("https://127.0.0.1/secret") } })), Configuration(), new Clock());
            Assert.Equal("unavailable", (await provider.ImageAsync("BR", "TEST11", default)).Status);
        }
        [Fact]
        public async Task UsSearchKeepsOnlyUsdListingsAndDoesNotInferCountryOrReitClassification()
        {
            var handler = new Handler(r => { Assert.Contains("SYMBOL_SEARCH&keywords=TEST", r.RequestUri!.Query); return Json("""{"bestMatches":[{"1. symbol":"TEST","2. name":"Synthetic","3. type":"Equity","4. region":"United States","8. currency":"USD"},{"1. symbol":"TEST.TO","2. name":"Foreign","4. region":"Canada","8. currency":"CAD"}]}"""); });
            var provider = new AssetDiscoveryProvider(new(handler), new(handler), Configuration(), new Clock());
            var item = Assert.Single((await provider.SearchAsync("US", "TEST", default)).Items); Assert.Equal("TEST", item.Symbol); Assert.Null(item.Country); Assert.Null(item.InstrumentClass);
        }
        [Fact]
        public async Task AlphaLogoFieldsAreReadWithoutLeakingTheProviderLocation()
        {
            var data = new Handler(_ => Json("""{"symbol":"TEST","logo_url_png":"https://www.alphavantage.co/logo/TEST.png","logo_url_svg":null}"""));
            var images = new Handler(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(InvestmentTracker.Testing.TestPng.Create()) { Headers = { ContentType = new("image/png") } } });
            var provider = new AssetDiscoveryProvider(new(data), new(images), Configuration(), new Clock());
            var result = await provider.ImageAsync("US", "TEST", default); Assert.Equal("available", result.Status); Assert.Equal("image/png", result.ContentType); Assert.DoesNotContain("https://", result.Message); Assert.Equal(1, images.Calls);
        }
        [Fact]
        public async Task MissingKeysInvalidResponsesAndWrongSymbolsAllowManualEntry()
        {
            var handler = new Handler(_ => Json("""{"Symbol":"WRONG","Currency":"USD","Name":"wrong"}"""));
            var provider = new AssetDiscoveryProvider(new(handler), new(handler), new ConfigurationBuilder().Build(), new Clock());
            Assert.Equal("not-configured", (await provider.SearchAsync("US", "TEST", default)).Status); Assert.Equal(0, handler.Calls);
            provider = new(new(handler), new(handler), Configuration(), new Clock());
            Assert.Empty((await provider.SearchAsync("US", "TEST", default)).Items);
            provider = new(new(new Handler(_ => Json("""{"results":[null,"invalid",{}]}"""))), new(handler), Configuration(), new Clock());
            Assert.Empty((await provider.SearchAsync("BR", "MALFORMED", default)).Items);
            provider = new(new(new Handler(_ => Json(new string('x', 1048577)))), new(handler), Configuration(), new Clock());
            Assert.Equal("unavailable", (await provider.SearchAsync("BR", "OVERSIZED", default)).Status);
        }
        public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
