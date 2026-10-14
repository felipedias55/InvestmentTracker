using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using InvestmentTracker.Application.Quotes;
using Microsoft.Extensions.Configuration;

namespace InvestmentTracker.Infrastructure.Quotes
{
    // Shared singleton: serialize calls for brapi's free tier and reuse quotes across previews.
    public sealed class FreeAssetQuoteProvider(HttpClient http, IConfiguration configuration, TimeProvider clock) : IAssetQuoteProvider
    {
        private readonly SemaphoreSlim gate = new(1, 1);
        private readonly Dictionary<string, (AssetQuote Quote, DateTimeOffset Expires)> cache = [];
        private DateOnly budgetDay;
        private int usRequests;
        private DateTimeOffset nextUsCall;

        public async Task<AssetQuote> FetchAsync(string market, string symbol, CancellationToken ct)
        {
            var provider = market == "BR" ? "brapi" : "Alpha Vantage";
            var currency = market == "BR" ? "BRL" : "USD";
            AssetQuote Failure(string status, string message) => new(provider, symbol, currency, null, null, status, message);
            if (market is not ("BR" or "US") || !Regex.IsMatch(symbol, @"^[A-Z0-9][A-Z0-9.\-]{0,19}$"))
                return Failure("unsupported", "Mercado ou ticker não suportado.");
            var token = configuration[market == "BR" ? "Quotes:BrapiToken" : "Quotes:AlphaVantageKey"];
            if (string.IsNullOrWhiteSpace(token))
                return Failure("not-configured", $"Configure a chave gratuita de {provider} nos User Secrets da API.");
            await gate.WaitAsync(ct);
            try
            {
                var now = clock.GetUtcNow();
                foreach (var expired in cache.Where(x => x.Value.Expires <= now).Select(x => x.Key).ToArray()) cache.Remove(expired);
                var key = market + ":" + symbol;
                if (cache.TryGetValue(key, out var cached)) return cached.Quote;
                if (market == "US")
                {
                    var day = DateOnly.FromDateTime(now.UtcDateTime);
                    if (day != budgetDay) { budgetDay = day; usRequests = 0; }
                    if (usRequests >= 25) return Failure("limit", "Limite local de 25 consultas diárias atingido. Tente no próximo dia ou preencha manualmente.");
                    var wait = nextUsCall - now;
                    if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
                    usRequests++;
                    nextUsCall = clock.GetUtcNow().AddSeconds(1.1);
                }
                using var request = new HttpRequestMessage(HttpMethod.Get, market == "BR"
                    ? $"https://brapi.dev/api/v2/stocks/quote?symbols={Uri.EscapeDataString(symbol)}"
                    : $"https://www.alphavantage.co/query?function=GLOBAL_QUOTE&symbol={Uri.EscapeDataString(symbol)}&apikey={Uri.EscapeDataString(token)}");
                if (market == "BR") request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                AssetQuote result;
                try
                {
                    using var response = await http.SendAsync(request, ct);
                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        if (market == "US") usRequests = 25;
                        result = Failure("limit", "Limite gratuito do provedor atingido. Aguarde a renovação ou preencha manualmente.");
                    }
                    else if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                        result = Failure("unavailable", "Chave inválida ou ativo não liberado no plano gratuito. Nenhum plano pago será ativado.");
                    else if (!response.IsSuccessStatusCode)
                        result = Failure("unavailable", "Cotação indisponível no provedor.");
                    else
                    {
                        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                        result = market == "BR" ? ParseBrapi(json.RootElement, symbol) : ParseAlpha(json.RootElement, symbol);
                        if (market == "US" && result.Status == "limit") usRequests = 25;
                    }
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                { result = Failure("unavailable", "O provedor demorou para responder. Tente novamente mais tarde."); }
                catch (HttpRequestException) { result = Failure("unavailable", "Não foi possível consultar o provedor."); }
                catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException or OverflowException or KeyNotFoundException)
                { result = Failure("invalid", "O provedor retornou dados incompletos ou incompatíveis."); }
                result = result with { RetrievedAt = clock.GetUtcNow() };
                cache[key] = (result, clock.GetUtcNow().Add(result.Price.HasValue
                    ? market == "BR" ? TimeSpan.FromMinutes(30) : TimeSpan.FromHours(12)
                    : TimeSpan.FromMinutes(2)));
                return result;
            }
            finally { gate.Release(); }
        }

        private static AssetQuote ParseBrapi(JsonElement root, string symbol)
        {
            var rows = root.GetProperty("results");
            if (rows.GetArrayLength() != 1) throw new JsonException();
            var row = rows[0];
            if (row.GetProperty("symbol").GetString() != symbol || row.GetProperty("requestedSymbol").GetString() != symbol ||
                row.GetProperty("changed").GetBoolean()) throw new JsonException();
            var data = row.GetProperty("data");
            if (data.GetProperty("currency").GetString() != "BRL") throw new JsonException();
            var price = data.GetProperty("regularMarketPrice").GetDecimal();
            var marketTime = data.GetProperty("regularMarketTime").GetDateTimeOffset();
            var date = DateOnly.FromDateTime(marketTime.UtcDateTime);
            return new("brapi", symbol, "BRL", price, date, "available", "Cotação com atraso do plano gratuito; confira a data antes de aplicar.") { MarketTime = marketTime };
        }
        private static AssetQuote ParseAlpha(JsonElement root, string symbol)
        {
            if (root.TryGetProperty("Note", out _) || root.TryGetProperty("Information", out _))
                return new("Alpha Vantage", symbol, "USD", null, null, "limit", "O provedor limitou a consulta gratuita. Aguarde ou preencha manualmente.");
            var quote = root.GetProperty("Global Quote");
            if (quote.GetProperty("01. symbol").GetString() != symbol) throw new JsonException();
            var price = decimal.Parse(quote.GetProperty("05. price").GetString()!, CultureInfo.InvariantCulture);
            var date = DateOnly.ParseExact(quote.GetProperty("07. latest trading day").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            return new("Alpha Vantage", symbol, "USD", price, date, "available", "Preço de fechamento diário; não é uma cotação em tempo real.");
        }
    }
}
