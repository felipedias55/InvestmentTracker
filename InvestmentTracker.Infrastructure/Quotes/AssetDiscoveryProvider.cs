using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using InvestmentTracker.Application.Assets;
using InvestmentTracker.Application.Quotes;
using Microsoft.Extensions.Configuration;

namespace InvestmentTracker.Infrastructure.Quotes
{
    public sealed class AssetDiscoveryProvider(HttpClient http, HttpClient images, IConfiguration configuration, TimeProvider clock) : IAssetDiscoveryProvider
    {
        private sealed record Document(string? Json, string Status, string Message, DateTimeOffset? At = null, bool Cached = false, DateTimeOffset? Retry = null, QuoteUsage? Usage = null);
        private readonly SemaphoreSlim gate = new(1, 1);
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (SuggestedAssetImage Image, DateTimeOffset Expires)> imageCache = new();
        private static string? Text(JsonElement value, string key) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.String && field.GetString() is { Length: > 0 and <= 500 } text && text != "None" ? text : null;
        private static bool Symbol(string? value) => value is not null && Regex.IsMatch(value, @"^[A-Z0-9][A-Z0-9.\-]{0,19}$");
        private static string Source(string market) => market == "BR" ? "brapi" : "Alpha Vantage";
        public async Task<AssetDiscoveryResult> SearchAsync(string market, string query, CancellationToken ct)
        {
            AssetDiscoveryService.Validate(market, query);
            var document = await RequestAsync(market, query, false, ct);
            var rows = new List<DiscoveredAsset>();
            if (document.Json is not null)
            {
                try
                {
                    using var json = JsonDocument.Parse(document.Json);
                    if (market == "BR")
                    {
                        foreach (var row in json.RootElement.GetProperty("results").EnumerateArray().Take(20))
                        {
                            var symbol = Text(row, "symbol"); var name = Text(row, "longName") ?? Text(row, "name");
                            if (!Symbol(symbol) || name is null || Text(row, "exchange") != "B3" || Text(row, "currency") != "BRL") continue;
                            if (row.TryGetProperty("isActive", out var active) && active.ValueKind == JsonValueKind.False) continue;
                            var kind = Text(row, "subType") switch { "fii" => "fii", "stock" or "unit" or "etf" or "bdr" => "general", _ => null };
                            // Listing venue does not establish the issuer's country (e.g. BDRs).
                            rows.Add(new(symbol!, name[..Math.Min(name.Length, 200)], market, "BRL", null, kind, Text(row, "sector"), SafeImageUrl(Text(row, "logoUrl")) is not null));
                        }
                    }
                    else
                    {
                        foreach (var row in json.RootElement.GetProperty("bestMatches").EnumerateArray().Take(20))
                        {
                            var symbol = Text(row, "1. symbol"); var name = Text(row, "2. name");
                            if (!Symbol(symbol) || name is null || Text(row, "4. region") != "United States" || Text(row, "8. currency") != "USD") continue;
                            // Search region denotes listing venue, and Equity does not distinguish REITs.
                            rows.Add(new(symbol!, name[..Math.Min(name.Length, 200)], market, "USD", null, null, null, true));
                        }
                    }
                }
                catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException)
                { return new(Source(market), "invalid", "A fonte retornou dados incompatíveis. Continue com o preenchimento manual.", []); }
            }
            return new(Source(market), document.Json is null ? document.Status : rows.Count == 0 ? "empty" : "available",
                document.Json is null ? document.Message : rows.Count == 0 ? "Nenhum resultado compatível. Confira o ticker ou preencha manualmente." : "Escolha o instrumento e revise os campos antes de aplicar. País e categoria precisam de conferência.", rows)
            { RetrievedAt = document.At, FromCache = document.Cached, RetryAfter = document.Retry, Usage = document.Usage };
        }
        public async Task<SuggestedAssetImage> ImageAsync(string market, string symbol, CancellationToken ct)
        {
            AssetDiscoveryService.Validate(market, symbol);
            var imageKey = market + ":" + symbol;
            if (imageCache.TryGetValue(imageKey, out var saved) && saved.Expires > clock.GetUtcNow()) return saved.Image;
            var document = await RequestAsync(market, symbol, market == "US", ct);
            if (document.Json is null) return new(document.Status, document.Message);
            string? url = null;
            try
            {
                using var json = JsonDocument.Parse(document.Json);
                if (market == "BR")
                {
                    var row = json.RootElement.GetProperty("results").EnumerateArray().FirstOrDefault(r => Text(r, "symbol") == symbol);
                    if (row.ValueKind == JsonValueKind.Object) url = Text(row, "logoUrl");
                }
                else if (Text(json.RootElement, "symbol") == symbol || Text(json.RootElement, "Symbol") == symbol)
                    url = Text(json.RootElement, "logo_url_png") ?? Text(json.RootElement, "png") ?? Text(json.RootElement, "logo_png") ?? Text(json.RootElement, "url") ?? Text(json.RootElement, "logo_url") ?? Text(json.RootElement, "logo") ?? Text(json.RootElement, "logo_url_svg") ?? Text(json.RootElement, "svg");
            }
            catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException) { }
            var uri = SafeImageUrl(url);
            if (uri is null) return new("unavailable", "A fonte não disponibilizou uma imagem compatível. Você pode selecionar um arquivo.");
            try
            {
                using var response = await images.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!response.IsSuccessStatusCode) return new("unavailable", "Imagem indisponível. A imagem atual foi preservada.");
                var bytes = await ReadBoundedAsync(response.Content, 262144, ct);
                var type = response.Content.Headers.ContentType?.MediaType;
                if (type == "image/svg+xml") bytes = SafeAssetSvg.Normalize(bytes);
                else if (type is not ("image/png" or "image/jpeg" or "image/webp")) return new("unavailable", "Formato de imagem não suportado pela fonte.");
                var result = new SuggestedAssetImage("available", "Imagem sugerida pela fonte. Revise e aplique antes de salvar o ativo.", Convert.ToBase64String(bytes), type);
                if (imageCache.Count >= 32) imageCache.TryRemove(imageCache.First().Key, out _);
                imageCache[imageKey] = (result, clock.GetUtcNow().AddHours(24));
                return result;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new("unavailable", "A imagem demorou para responder. Tente outro arquivo."); }
            catch (Exception error) when (error is HttpRequestException or IOException or System.Xml.XmlException or InvalidOperationException)
            { return new("unavailable", "Não foi possível preparar a imagem da fonte. A imagem atual foi preservada."); }
        }
        internal static Uri? SafeImageUrl(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0 &&
            (uri.Host == "icons.brapi.dev" && uri.AbsolutePath.StartsWith("/icons/", StringComparison.Ordinal) || uri.Host == "www.alphavantage.co" && (uri.AbsolutePath.StartsWith("/images/", StringComparison.Ordinal) || uri.AbsolutePath.StartsWith("/logo/", StringComparison.Ordinal))) ? uri : null;
        private async Task<Document> RequestAsync(string market, string query, bool logo, CancellationToken ct)
        {
            var token = configuration[market == "BR" ? "Quotes:BrapiToken" : "Quotes:AlphaVantageKey"];
            if (market == "US" && string.IsNullOrWhiteSpace(token)) return new(null, "not-configured", "Configure a chave Alpha Vantage na API ou preencha manualmente.");
            await gate.WaitAsync(ct);
            try
            {
                var directory = configuration["Quotes:StateDirectory"] ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InvestmentTracker", "QuoteState");
                // Same lease, attempts, pacing and provider pauses as the quote adapter.
                using var file = await QuoteRequestStateFile.OpenAsync(directory, token ?? "brapi-public-discovery", ct, market == "BR" ? "brapi-" : "");
                var state = await file.ReadAsync(ct); var now = clock.GetUtcNow();
                state.Attempts.RemoveAll(at => at <= now.AddDays(market == "BR" ? -31 : -1));
                var usage = new QuoteUsage(state.Attempts.Count, market == "BR" ? 15000 : 25, market == "BR" ? 31 : 1);
                state.Documents ??= [];
                foreach (var key in state.Documents.Where(x => x.Value.Expires <= now).Select(x => x.Key).ToArray()) state.Documents.Remove(key);
                var cacheKey = $"{(logo ? "logo" : market == "US" ? "search" : "discovery")}:{query}";
                if (state.Documents.TryGetValue(cacheKey, out var saved)) return new(saved.Json, "available", "Dados em cache.", saved.RetrievedAt, true, Usage: usage);
                if (state.BlockedUntil > now) return new(null, state.BlockReason, "O provedor está em pausa. Aguarde e tente novamente; o preenchimento manual está disponível.", Retry: state.BlockedUntil, Usage: usage);
                if (state.Attempts.Count >= usage.Limit) return new(null, market == "BR" ? "monthly-limit" : "daily-limit", "Proteção local de consultas atingida. Aguarde a liberação.", Retry: state.Attempts.Min().AddDays(usage.WindowDays), Usage: usage);
                if (market == "US" && state.NextCall > now) await Task.Delay(state.NextCall - now, clock, ct);
                now = clock.GetUtcNow(); state.Attempts.Add(now); if (market == "US") state.NextCall = now.AddSeconds(13);
                await file.SaveAsync(state, ct);
                using var request = new HttpRequestMessage(HttpMethod.Get, market == "BR" ? $"https://brapi.dev/api/v2/tickers?search={Uri.EscapeDataString(query)}&limit=20"
                    : $"https://www.alphavantage.co/query?function={(logo ? "COMPANY_LOGO&symbol" : "SYMBOL_SEARCH&keywords")}={Uri.EscapeDataString(query)}&apikey={Uri.EscapeDataString(token!)}");
                if (market == "BR" && !string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                Document result;
                try
                {
                    using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                        result = new(null, "temporary-limit", "O provedor pediu uma pausa temporária.", Retry: response.Headers.RetryAfter?.Date ?? now.Add(response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(1)));
                    else if (!response.IsSuccessStatusCode) result = new(null, "unavailable", "Consulta indisponível ou não liberada no plano. Continue manualmente.");
                    else
                    {
                        var body = System.Text.Encoding.UTF8.GetString(await ReadBoundedAsync(response.Content, 1048576, ct));
                        using var json = JsonDocument.Parse(body);
                        if (json.RootElement.ValueKind != JsonValueKind.Object || market == "BR" && (!json.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)) throw new JsonException();
                        if (market == "US" && (json.RootElement.TryGetProperty("Information", out _) || json.RootElement.TryGetProperty("Note", out _) || json.RootElement.TryGetProperty("Error Message", out _)))
                        {
                            var notice = FreeAssetQuoteProvider.ParseAlpha(json.RootElement, query);
                            result = new(null, notice.Status, notice.Message);
                        }
                        else result = new(body, "available", "Sugestão disponível.", now);
                    }
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) { result = new(null, "unavailable", "O provedor demorou para responder. Continue manualmente."); }
                catch (Exception error) when (error is HttpRequestException or IOException or JsonException or InvalidOperationException) { result = new(null, "unavailable", "Não foi possível consultar a fonte. Continue manualmente."); }
                if (result.Status is "daily-limit" or "temporary-limit")
                {
                    state.BlockReason = result.Status;
                    state.BlockedUntil = result.Retry ?? now.Add(result.Status == "daily-limit" ? TimeSpan.FromHours(24) : TimeSpan.FromMinutes(1));
                    result = result with { Retry = state.BlockedUntil };
                }
                if (result.Json is not null)
                {
                    if (state.Documents.Count >= 200) state.Documents.Remove(state.Documents.MinBy(x => x.Value.Expires).Key);
                    state.Documents[cacheKey] = new(result.Json, now, now.AddHours(24));
                    // A selected Brazilian result already contains its logo; avoid another data request.
                    if (market == "BR")
                    {
                        using var json = JsonDocument.Parse(result.Json);
                        if (json.RootElement.TryGetProperty("results", out var rows) && rows.ValueKind == JsonValueKind.Array)
                            foreach (var row in rows.EnumerateArray().Take(20))
                                if (Symbol(Text(row, "symbol")) && !state.Documents.ContainsKey("discovery:" + Text(row, "symbol")))
                                {
                                    if (state.Documents.Count >= 200) state.Documents.Remove(state.Documents.MinBy(x => x.Value.Expires).Key);
                                    state.Documents["discovery:" + Text(row, "symbol")] = new(JsonSerializer.Serialize(new { results = new[] { row.Clone() } }), now, now.AddHours(24));
                                }
                    }
                }
                await file.SaveAsync(state, CancellationToken.None);
                return result with { Usage = usage with { Attempts = state.Attempts.Count } };
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
            { return new(null, "state-unavailable", "Não foi possível acessar o controle local de consultas. Continue manualmente."); }
            finally { gate.Release(); }
        }
        private static async Task<byte[]> ReadBoundedAsync(HttpContent content, int limit, CancellationToken ct)
        {
            if (content.Headers.ContentLength > limit) throw new IOException("Response too large.");
            await using var stream = await content.ReadAsStreamAsync(ct); using var data = new MemoryStream(); var buffer = new byte[8192];
            int read; while ((read = await stream.ReadAsync(buffer, ct)) > 0) { if (data.Length + read > limit) throw new IOException("Response too large."); data.Write(buffer, 0, read); }
            return data.ToArray();
        }
    }
}
