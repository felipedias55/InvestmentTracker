using System.Text.Json.Serialization;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Application.History.Interfaces;

namespace InvestmentTracker.Application.Benchmarks
{
    public sealed record BenchmarkPoint(DateOnly Date, decimal Value);
    public sealed record BenchmarkSeries(string Code, string Source, IReadOnlyList<BenchmarkPoint> Points, string? Error = null)
    {
        public DateTimeOffset? RetrievedAt { get; init; }
        public bool FromCache { get; init; }
    }
    public interface IBenchmarkProvider
    {
        Task<BenchmarkSeries> FetchAsync(string code, DateOnly start, DateOnly end, CancellationToken ct);
    }
    public sealed record BenchmarkResult(string Code, string Name, string Source, string CurrencyCode,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal? Return,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal? Difference,
        DateOnly? FirstObservation, DateOnly? LastObservation, string Note, DateTimeOffset? RetrievedAt, bool FromCache);
    public sealed record BenchmarkComparison(string Period, DateOnly? Start, DateOnly? End, string CurrencyCode,
        [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal? PortfolioReturn,
        string Method, IReadOnlyList<BenchmarkResult> Items);

    public sealed class BenchmarkService(IHistoryService history, IBenchmarkProvider provider)
    {
        private static readonly (string Code, string Name, string Currency, string Note)[] Definitions = [
            ("CDI", "CDI", "BRL", "100% do CDI bruto, sem impostos ou custos; taxas diárias compostas."),
            ("IPCA", "IPCA", "BRL", "Inflação mensal composta; exige fotografias em fins de mês e todos os meses publicados."),
            ("IBOV", "Ibovespa", "BRL", "Variação do índice Ibovespa; cobertura sujeita ao plano brapi."),
            ("SP500", "S&P 500", "USD", "Índice de preços, sem dividendos reinvestidos; histórico FRED limitado pela fonte.")
        ];
        public async Task<BenchmarkComparison?> CompareAsync(int portfolioId, string mode, string period, CancellationToken ct)
        {
            if (mode is not ("months" or "years") || period is null || period.Length > 7)
                throw new InputValidationException("Escolha mês ou ano e um período do histórico.");
            var data = await history.GetAsync(portfolioId, ct, includeCashFlows: false);
            if (data is null) return null;
            var row = (mode == "months" ? data.Months : data.Years).SingleOrDefault(r => r.Period == period)
                ?? throw new InputValidationException("Selecione um período existente no histórico desta carteira.");
            var start = row.ComparisonStart; var end = row.SnapshotDate; var currency = row.CurrencyCode;
            var blocked = row.IsOutdated || row.IsReopened || row.Returns?.ModifiedDietz is null || start is null || end is null || end <= start
                ? "Comparação suspensa: são necessárias fotografias válidas e Dietz disponível no intervalo. " + row.Returns?.Note
                : currency is not ("BRL" or "USD") ? "Comparação disponível somente em BRL ou USD."
                : start < new DateOnly(2000, 1, 1) || end.Value.DayNumber - start.Value.DayNumber > 1830 ? "Selecione um intervalo entre 2000 e hoje, de até cinco anos." : null;
            var items = new List<BenchmarkResult>();
            BenchmarkSeries? fx = null;
            foreach (var definition in Definitions)
            {
                BenchmarkResult Missing(string reason) => new(definition.Code, definition.Name, definition.Code == "SP500" ? "FRED / S&P Dow Jones Indices" : definition.Code == "IBOV" ? "brapi / B3" : "BCB / SGS", currency,
                    null, null, null, null, reason + " " + definition.Note, null, false);
                if (blocked is not null) { items.Add(Missing(blocked)); continue; }
                if (definition.Code == "IPCA" && (currency != "BRL" || start!.Value.Day != DateTime.DaysInMonth(start.Value.Year, start.Value.Month) || end!.Value.Day != DateTime.DaysInMonth(end.Value.Year, end.Value.Month)))
                { items.Add(Missing("IPCA compara inflação em BRL somente entre fins de mês; não é rateado por dia nem convertido em inflação de outra moeda.")); continue; }
                var series = await provider.FetchAsync(definition.Code, start!.Value, end!.Value, ct);
                var measured = BenchmarkCalculator.Measure(definition.Code, start.Value, end.Value, series.Points);
                var note = series.Error ?? measured.Error;
                decimal? value = note is null ? measured.Factor : null;
                var conversionNote = "";
                if (value.HasValue && definition.Currency != currency)
                {
                    fx ??= await provider.FetchAsync("USDBRL", start.Value, end.Value, ct);
                    var exchange = BenchmarkCalculator.Measure("USDBRL", start.Value, end.Value, fx.Points);
                    if (fx.Error is not null || exchange.Error is not null) { note = "Câmbio histórico insuficiente para comparar na moeda da fotografia."; value = null; }
                    else
                    {
                        try { value = definition.Currency == "USD" ? value * exchange.Factor : value / exchange.Factor; conversionNote = $" Observações USD/BRL: {exchange.First:dd/MM/yyyy} → {exchange.Last:dd/MM/yyyy}. "; }
                        catch (Exception error) when (error is OverflowException or DivideByZeroException) { value = null; note = "Conversão fora dos limites numéricos suportados. "; }
                    }
                }
                var rate = value - 1m;
                items.Add(new(definition.Code, definition.Name, series.Source, currency, rate,
                    rate.HasValue ? row.Returns!.ModifiedDietz - rate : null, measured.First, measured.Last,
                    (note ?? (definition.Currency != currency ? "Convertido com USD/BRL histórico (BCB/SGS 1); inclui efeito cambial. " : "")) + conversionNote + " " + definition.Note,
                    series.RetrievedAt, series.FromCache && (definition.Currency == currency || fx?.FromCache == true)));
            }
            return new(period, start, end, currency, blocked is null ? row.Returns!.ModifiedDietz : null,
                "Dietz modificado estimado do patrimônio total das fotografias, incluindo saldos e patrimônio externo, versus variação da referência no mesmo intervalo. Diferença em pontos percentuais; não é alfa nem comparação com XIRR anualizada. As referências não replicam os aportes, retiradas, impostos ou custos da carteira. "
                    + (row.HasStaleRates || row.HasFallbackRates ? "As fotografias usam câmbio anterior ou alternativo." : ""), items);
        }
    }

    public static class BenchmarkCalculator
    {
        public sealed record Measurement(decimal? Factor, DateOnly? First, DateOnly? Last, string? Error);
        public static Measurement Measure(string code, DateOnly start, DateOnly end, IReadOnlyList<BenchmarkPoint> points)
        {
            Measurement Missing() => new(null, null, null, "Série sem cobertura suficiente ou com observações inválidas; comparação indisponível. ");
            if (end <= start || points.GroupBy(p => p.Date).Any(g => g.Count() > 1)) return Missing();
            var sorted = points.OrderBy(p => p.Date).ToArray();
            try
            {
                if (code == "IPCA")
                {
                    if (start.Day != DateTime.DaysInMonth(start.Year, start.Month) || end.Day != DateTime.DaysInMonth(end.Year, end.Month)) return Missing();
                    var month = new DateOnly(start.Year, start.Month, 1).AddMonths(1); var finish = new DateOnly(end.Year, end.Month, 1);
                    decimal factor = 1;
                    while (month <= finish)
                    {
                        var rows = sorted.Where(p => p.Date.Year == month.Year && p.Date.Month == month.Month).ToArray();
                        if (rows.Length != 1 || rows[0].Value <= -100 || rows[0].Value > 100) return Missing();
                        factor *= 1 + rows[0].Value / 100; month = month.AddMonths(1);
                    }
                    var observations = sorted.Where(p => p.Date.Year * 12 + p.Date.Month > start.Year * 12 + start.Month && p.Date.Year * 12 + p.Date.Month <= end.Year * 12 + end.Month).ToArray();
                    return new(factor, observations[0].Date, observations[^1].Date, null);
                }
                if (code == "CDI")
                {
                    var rows = sorted.Where(p => p.Date > start && p.Date <= end).ToArray();
                    if (rows.Length == 0 || rows[0].Date.DayNumber - start.DayNumber > 7 || end.DayNumber - rows[^1].Date.DayNumber > 7
                        || rows.Any(p => p.Value <= -100 || p.Value > 100) || rows.Zip(rows.Skip(1)).Any(pair => pair.Second.Date.DayNumber - pair.First.Date.DayNumber > 7)) return Missing();
                    return new(rows.Aggregate(1m, (factor, p) => factor * (1 + p.Value / 100)), rows[0].Date, rows[^1].Date, null);
                }
                var first = sorted.LastOrDefault(p => p.Date <= start); var last = sorted.LastOrDefault(p => p.Date <= end);
                if (first is null || last is null || first.Value <= 0 || last.Value <= 0 || start.DayNumber - first.Date.DayNumber > 7 || end.DayNumber - last.Date.DayNumber > 7) return Missing();
                var indexFactor = last.Value / first.Value;
                return indexFactor > 0 ? new(indexFactor, first.Date, last.Date, null) : Missing();
            }
            catch (OverflowException) { return Missing(); }
        }
    }
}
