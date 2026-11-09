using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using InvestmentTracker.Application.Benchmarks;
using InvestmentTracker.Infrastructure.Quotes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InvestmentTracker.Infrastructure.Benchmarks
{
    public sealed class BenchmarkProvider(HttpClient http, IConfiguration configuration, TimeProvider clock, ILogger<BenchmarkProvider>? logger = null, IFredCsvFallback? fredFallback = null) : IBenchmarkProvider
    {
        private readonly SemaphoreSlim gate = new(1, 1);
        private readonly Dictionary<string, (BenchmarkSeries Series, DateTimeOffset Expires)> cache = [];
        public async Task<BenchmarkSeries> FetchAsync(string code, DateOnly start, DateOnly end, CancellationToken ct)
        {
            var source = code == "SP500" ? "FRED / S&P Dow Jones Indices" : code == "IBOV" ? "brapi / B3" : "BCB / SGS " + (code == "CDI" ? "12" : code == "IPCA" ? "433" : "1");
            if (code is not ("CDI" or "IPCA" or "IBOV" or "SP500" or "USDBRL") || start < new DateOnly(2000, 1, 1) || end <= start || end.DayNumber - start.DayNumber > 1830)
                return new(code, source, [], "Código ou intervalo não suportado. ");
            var key = $"{code}:{start:yyyy-MM-dd}:{end:yyyy-MM-dd}";
            await gate.WaitAsync(ct);
            try
            {
                var now = clock.GetUtcNow();
                if (cache.TryGetValue(key, out var stored) && stored.Expires > now) return stored.Series with { FromCache = true };
                BenchmarkSeries result;
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(code == "SP500" ? 30 : 10));
                try
                {
                    var from = start.AddDays(-10);
                    if (code == "IBOV") result = await Ibovespa(start, end, timeout.Token);
                    else
                    {
                        var uri = code == "SP500"
                            ? $"https://fred.stlouisfed.org/graph/fredgraph.csv?id=SP500&cosd={from:yyyy-MM-dd}&coed={end:yyyy-MM-dd}"
                            : $"https://api.bcb.gov.br/dados/serie/bcdata.sgs.{(code == "CDI" ? 12 : code == "IPCA" ? 433 : 1)}/dados?formato=json&dataInicial={from:dd/MM/yyyy}&dataFinal={end:dd/MM/yyyy}";
                        var body = code == "SP500" ? await Fred(from, end, timeout.Token) : await PublicDocument(uri, timeout.Token);
                        var points = code == "SP500" ? ParseFred(body) : ParseBcb(body);
                        result = new(code, source, points);
                    }
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) { result = new(code, source, [], "A fonte demorou para responder. Tente novamente mais tarde. "); }
                catch (Exception error) when (error is HttpRequestException or IOException or JsonException or FormatException or InvalidOperationException or ArgumentOutOfRangeException or OverflowException or UnauthorizedAccessException or KeyNotFoundException)
                {
                    logger?.LogWarning("Benchmark {Code}: {ErrorType}, HTTP {StatusCode}, transporte {TransportError}, causa {Cause}", code, error.GetType().Name,
                        error is HttpRequestException requestError ? (int?)requestError.StatusCode : null,
                        error is HttpRequestException transport ? transport.HttpRequestError.ToString() : "none", error.InnerException?.GetType().Name ?? "none");
                    result = new(code, source, [], "Fonte indisponível, resposta inválida ou cobertura não liberada. Nenhum retorno foi presumido. ");
                }
                result = result with { RetrievedAt = result.RetrievedAt ?? now };
                if (cache.Count >= 80) cache.Remove(cache.MinBy(p => p.Value.Expires).Key);
                cache[key] = (result, now.Add(result.Error is null && result.Points.Count > 0 ? TimeSpan.FromHours(24) : TimeSpan.FromMinutes(5)));
                return result;
            }
            finally { gate.Release(); }
        }
        private async Task<string> PublicDocument(string uri, CancellationToken ct)
        {
            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode(); return await Read(response.Content, ct);
        }
        private async Task<string> Fred(DateOnly start, DateOnly end, CancellationToken ct)
        {
            using var managed = CancellationTokenSource.CreateLinkedTokenSource(ct);
            managed.CancelAfter(TimeSpan.FromSeconds(fredFallback is null ? 30 : 5));
            try { return await PublicDocument($"https://fred.stlouisfed.org/graph/fredgraph.csv?id=SP500&cosd={start:yyyy-MM-dd}&coed={end:yyyy-MM-dd}", managed.Token); }
            catch (Exception error) when (fredFallback is not null && !ct.IsCancellationRequested && (error is OperationCanceledException || error is HttpRequestException request && request.StatusCode is null))
            {
                logger?.LogInformation("FRED: usando transporte HTTPS alternativo após falha de conexão.");
                return await fredFallback.FetchAsync(start, end, ct);
            }
        }
        private async Task<BenchmarkSeries> Ibovespa(DateOnly start, DateOnly end, CancellationToken ct)
        {
            BenchmarkSeries Missing(string message) => new("IBOV", "brapi / B3", [], message + " ");
            var token = configuration["Quotes:BrapiToken"];
            if (string.IsNullOrWhiteSpace(token)) return Missing("Configure a chave brapi já usada nas cotações para consultar o Ibovespa.");
            var directory = configuration["Quotes:StateDirectory"] ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InvestmentTracker", "QuoteState");
            using var file = await QuoteRequestStateFile.OpenAsync(directory, token, ct, "brapi-");
            var state = await file.ReadAsync(ct); var now = clock.GetUtcNow();
            var key = $"benchmark:IBOV:{start:yyyy-MM-dd}:{end:yyyy-MM-dd}";
            if (state.Documents.TryGetValue(key, out var saved) && saved.Expires > now)
                return new("IBOV", "brapi / B3", ParseBrapi(saved.Json)) { FromCache = true, RetrievedAt = saved.RetrievedAt };
            state.Attempts.RemoveAll(at => at <= now.AddDays(-31));
            if (state.BlockedUntil > now) return Missing("A brapi pediu uma pausa. Aguarde a liberação das consultas.");
            if (state.Attempts.Count >= 15000) return Missing("Proteção local brapi: limite de tentativas dos últimos 31 dias atingido.");
            state.Attempts.Add(now); await file.SaveAsync(state, ct);
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://brapi.dev/api/v2/stocks/historical?symbols=%5EBVSP&startDate={start.AddDays(-10):yyyy-MM-dd}&endDate={end.AddDays(1):yyyy-MM-dd}&interval=1d&sortOrder=asc");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                state.BlockReason = "temporary-limit";
                state.BlockedUntil = response.Headers.RetryAfter?.Date ?? now.Add(response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(1));
                await file.SaveAsync(state, CancellationToken.None); return Missing("A brapi pediu uma pausa. Aguarde a liberação das consultas.");
            }
            if (!response.IsSuccessStatusCode) return Missing("Histórico do Ibovespa indisponível ou não liberado no plano brapi. Não foi ativado plano pago.");
            var body = await Read(response.Content, ct); var points = ParseBrapi(body);
            foreach (var expired in state.Documents.Where(p => p.Value.Expires <= now).Select(p => p.Key).ToArray()) state.Documents.Remove(expired);
            if (state.Documents.Count >= 200) state.Documents.Remove(state.Documents.MinBy(p => p.Value.Expires).Key);
            state.Documents[key] = new(body, now, now.AddHours(24)); await file.SaveAsync(state, CancellationToken.None);
            return new("IBOV", "brapi / B3", points) { RetrievedAt = now };
        }
        private static List<BenchmarkPoint> ParseBcb(string body)
        {
            using var json = JsonDocument.Parse(body); var points = new List<BenchmarkPoint>();
            foreach (var row in json.RootElement.EnumerateArray())
                points.Add(new(DateOnly.ParseExact(row.GetProperty("data").GetString()!, "dd/MM/yyyy", CultureInfo.InvariantCulture),
                    decimal.Parse(row.GetProperty("valor").GetString()!, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture)));
            return points;
        }
        private static List<BenchmarkPoint> ParseFred(string body)
        {
            var lines = body.TrimStart('\uFEFF').Trim().Split('\n');
            if (lines.Length < 2 || lines[0].Trim() is not ("observation_date,SP500" or "DATE,SP500")) throw new FormatException();
            var points = new List<BenchmarkPoint>();
            foreach (var line in lines.Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                var columns = line.Trim().Split(','); if (columns.Length != 2) throw new FormatException();
                var date = DateOnly.ParseExact(columns[0], "yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (columns[1] is "" or ".") continue;
                points.Add(new(date, decimal.Parse(columns[1], NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture)));
            }
            return points;
        }
        private static List<BenchmarkPoint> ParseBrapi(string body)
        {
            using var json = JsonDocument.Parse(body);
            var result = json.RootElement.GetProperty("results").EnumerateArray().Single(r => r.GetProperty("symbol").GetString() == "^BVSP");
            var data = result.GetProperty("data");
            if (data.GetProperty("usedInterval").GetString() != "1d") throw new FormatException();
            return data.GetProperty("historicalDataPrice").EnumerateArray().Select(row => new BenchmarkPoint(
                DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(row.GetProperty("date").GetInt64()).UtcDateTime), row.GetProperty("close").GetDecimal())).ToList();
        }
        private static async Task<string> Read(HttpContent content, CancellationToken ct)
        {
            const int limit = 1048576;
            if (content.Headers.ContentLength > limit) throw new IOException();
            await using var stream = await content.ReadAsStreamAsync(ct); using var bytes = new MemoryStream(); var buffer = new byte[8192];
            int count; while ((count = await stream.ReadAsync(buffer, ct)) > 0) { if (bytes.Length + count > limit) throw new IOException(); bytes.Write(buffer, 0, count); }
            return Encoding.UTF8.GetString(bytes.ToArray());
        }
    }
}
