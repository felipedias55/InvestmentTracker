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
            QuoteRequestStateFile? file = null;
            var requested = false;
            try
            {
                var now = clock.GetUtcNow();
                QuoteRequestState? state = null;
                foreach (var expired in cache.Where(x => x.Value.Expires <= now).Select(x => x.Key).ToArray()) cache.Remove(expired);
                var key = market + ":" + symbol;
                if (market == "BR" && cache.TryGetValue(key, out var cached))
                    return cached.Quote with { FromCache = true, Requested = false };
                if (market == "US")
                {
                    var directory = configuration["Quotes:StateDirectory"] ?? Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InvestmentTracker", "QuoteState");
                    file = await QuoteRequestStateFile.OpenAsync(directory, token, ct);
                    state = await file.ReadAsync(ct);
                    now = clock.GetUtcNow();
                    foreach (var expired in state.Cache.Where(x => x.Value.Expires <= now).Select(x => x.Key).ToArray()) state.Cache.Remove(expired);
                    if (state.Cache.TryGetValue(symbol, out var saved))
                        return saved.Quote with { FromCache = true, Requested = false };
                    if (state.BlockedUntil > now)
                        return Failure(state.BlockReason, state.BlockReason == "daily-limit"
                            ? "O provedor informou limite diário. Aguarde a liberação indicada."
                            : "O provedor pediu uma pausa temporária. Tente novamente após o horário indicado.")
                            with { RetryAfter = state.BlockedUntil };
                    // A rolling window avoids assuming the provider resets its quota at UTC midnight.
                    state.Attempts.RemoveAll(at => at <= now.AddHours(-24));
                    if (state.Attempts.Count >= 25)
                        return Failure("daily-limit", "Proteção local: 25 tentativas nas últimas 24 horas. Aguarde a liberação indicada.")
                            with { RetryAfter = state.Attempts.Min().AddHours(24) };
                    var wait = state.NextCall - now;
                    if (wait > TimeSpan.Zero) await Task.Delay(wait, clock, ct);
                    now = clock.GetUtcNow();
                    state.Attempts.Add(now);
                    // Conservative pacing, not a claim about a guaranteed provider minute allowance.
                    state.NextCall = now.AddSeconds(13);
                    // Persist BEFORE sending, including timeouts/cancellation and uncertain responses.
                    await file.SaveAsync(state, ct);
                }
                using var request = new HttpRequestMessage(HttpMethod.Get, market == "BR"
                    ? $"https://brapi.dev/api/v2/stocks/quote?symbols={Uri.EscapeDataString(symbol)}"
                    : $"https://www.alphavantage.co/query?function=GLOBAL_QUOTE&symbol={Uri.EscapeDataString(symbol)}&apikey={Uri.EscapeDataString(token)}");
                if (market == "BR") request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                AssetQuote result;
                try
                {
                    requested = true;
                    using var response = await http.SendAsync(request, ct);
                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        var retry = response.Headers.RetryAfter;
                        var until = retry?.Date ?? clock.GetUtcNow().Add(retry?.Delta ?? TimeSpan.FromMinutes(1));
                        result = Failure("temporary-limit", "O provedor pediu uma pausa temporária; a cota diária não foi marcada como esgotada.")
                            with { RetryAfter = until > clock.GetUtcNow() ? until : clock.GetUtcNow().AddMinutes(1) };
                    }
                    else if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                        result = Failure("unavailable", "Chave inválida ou ativo não liberado no plano gratuito. Nenhum plano pago será ativado.");
                    else if (!response.IsSuccessStatusCode)
                        result = Failure("unavailable", "Cotação indisponível no provedor.");
                    else
                    {
                        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                        result = market == "BR" ? ParseBrapi(json.RootElement, symbol) : ParseAlpha(json.RootElement, symbol);
                    }
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                { result = Failure("unavailable", "O provedor demorou para responder. Tente novamente mais tarde."); }
                catch (HttpRequestException) { result = Failure("unavailable", "Não foi possível consultar o provedor."); }
                catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException or OverflowException or KeyNotFoundException)
                { result = Failure("invalid", "O provedor retornou dados incompletos ou incompatíveis."); }
                now = clock.GetUtcNow();
                result = result with { RetrievedAt = now, Requested = true };
                if (state is not null)
                {
                    if (result.Status is "daily-limit" or "temporary-limit")
                    {
                        state.BlockReason = result.Status;
                        state.BlockedUntil = result.RetryAfter ?? now.Add(
                            result.Status == "daily-limit" ? TimeSpan.FromHours(24) : TimeSpan.FromMinutes(1));
                        result = result with { RetryAfter = state.BlockedUntil };
                    }
                    if (result.Price.HasValue) state.Cache[symbol] = new(result, now.AddHours(12));
                    // Complete durable bookkeeping even if the browser disconnected after receiving the quote.
                    await file!.SaveAsync(state, CancellationToken.None);
                }
                else cache[key] = (result, now.Add(result.Price.HasValue ? TimeSpan.FromMinutes(30) : TimeSpan.FromMinutes(2)));
                return result;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                return Failure("state-unavailable", "Não foi possível ler ou salvar o controle local de consultas. Confira a pasta de cache da API antes de tentar novamente.")
                    with { Requested = requested };
            }
            finally { file?.Dispose(); gate.Release(); }
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
            AssetQuote Failure(string status, string message) => new("Alpha Vantage", symbol, "USD", null, null, status, message);
            if (root.TryGetProperty("Note", out var note) || root.TryGetProperty("Information", out note))
            {
                // Never forward raw upstream text: it can contain the API key or promotional links.
                var message = (note.GetString() ?? "").ToLowerInvariant();
                if (message.Contains("api key") && (message.Contains("invalid") || message.Contains("missing")))
                    return Failure("invalid-key", "A chave da Alpha Vantage foi recusada. Confira a configuração da API.");
                if (message.Contains("per minute") || message.Contains("per second") || message.Contains("frequency") && !message.Contains("per day"))
                    return Failure("temporary-limit", "Consultas muito próximas. Aguarde a pausa indicada e consulte novamente os campos vazios.");
                if (message.Contains("per day") || message.Contains("daily limit") || message.Contains("daily rate"))
                    return Failure("daily-limit", "O provedor informou que o limite diário desta chave foi atingido.");
                if (message.Contains("premium") || message.Contains("entitlement"))
                    return Failure("restricted", "O provedor restringiu este dado no plano gratuito. Nenhum serviço pago foi solicitado.");
                return Failure("provider-notice", "O provedor retornou um aviso sem cotação. O limite diário não foi presumido; tente novamente mais tarde.");
            }
            if (root.TryGetProperty("Error Message", out _))
                return Failure("unsupported", "O provedor não reconheceu a consulta deste ativo. Confira o ticker cadastrado.");
            if (!root.TryGetProperty("Global Quote", out var data) || data.ValueKind != JsonValueKind.Object || !data.EnumerateObject().Any())
                return Failure("unavailable", "O provedor não retornou cotação para este ativo.");
            var quote = root.GetProperty("Global Quote");
            if (quote.GetProperty("01. symbol").GetString() != symbol) throw new JsonException();
            var price = decimal.Parse(quote.GetProperty("05. price").GetString()!, CultureInfo.InvariantCulture);
            var date = DateOnly.ParseExact(quote.GetProperty("07. latest trading day").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            return new("Alpha Vantage", symbol, "USD", price, date, "available", "Preço de fechamento diário; não é uma cotação em tempo real.");
        }
    }
}
