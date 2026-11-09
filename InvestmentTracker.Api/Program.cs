using InvestmentTracker.Application.Portfolios;
using InvestmentTracker.Application.ExchangeRates.Interfaces;
using InvestmentTracker.Infrastructure.ExchangeRates;
using InvestmentTracker.Api.Exceptions;
using InvestmentTracker.Api.Health;
using InvestmentTracker.Infrastructure.Persistence;
using InvestmentTracker.Application;
using Microsoft.EntityFrameworkCore;
using InvestmentTracker.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApplicationExceptionHandler>();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>(
    DatabaseHealthCheck.Name,
    tags: [DatabaseHealthCheck.ReadinessTag],
    timeout: TimeSpan.FromSeconds(5));

builder.Services.AddOpenApi();

var connectionString =
    builder.Configuration.GetConnectionString("InvestmentTracker");

builder.Services.AddDbContext<InvestmentTrackerDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddHttpClient("Benchmarks", client => { client.Timeout = TimeSpan.FromSeconds(35); client.DefaultRequestHeaders.UserAgent.ParseAdd("FDInvestmentTracker/1.0"); client.DefaultRequestHeaders.Accept.ParseAdd("*/*"); })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, AutomaticDecompression = System.Net.DecompressionMethods.All }).RemoveAllLoggers();
builder.Services.AddSingleton<InvestmentTracker.Infrastructure.Benchmarks.IFredCsvFallback, InvestmentTracker.Infrastructure.Benchmarks.FredCsvFallback>();
builder.Services.AddSingleton<InvestmentTracker.Application.Benchmarks.IBenchmarkProvider>(services =>
    new InvestmentTracker.Infrastructure.Benchmarks.BenchmarkProvider(services.GetRequiredService<IHttpClientFactory>().CreateClient("Benchmarks"),
        services.GetRequiredService<IConfiguration>(), services.GetRequiredService<TimeProvider>(), services.GetRequiredService<ILogger<InvestmentTracker.Infrastructure.Benchmarks.BenchmarkProvider>>(), services.GetRequiredService<InvestmentTracker.Infrastructure.Benchmarks.IFredCsvFallback>()));
builder.Services.AddHttpClient("AssetQuotes", client => client.Timeout = TimeSpan.FromSeconds(15)).RemoveAllLoggers();
builder.Services.AddHttpClient("AssetDiscovery", client => client.Timeout = TimeSpan.FromSeconds(15))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }).RemoveAllLoggers();
builder.Services.AddHttpClient("AssetImages", client => client.Timeout = TimeSpan.FromSeconds(10))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }).RemoveAllLoggers();
builder.Services.AddSingleton<InvestmentTracker.Application.Assets.IAssetDiscoveryProvider>(services =>
    new InvestmentTracker.Infrastructure.Quotes.AssetDiscoveryProvider(
        services.GetRequiredService<IHttpClientFactory>().CreateClient("AssetDiscovery"),
        services.GetRequiredService<IHttpClientFactory>().CreateClient("AssetImages"),
        services.GetRequiredService<IConfiguration>(), services.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton<InvestmentTracker.Application.Quotes.IAssetQuoteProvider>(services =>
    new InvestmentTracker.Infrastructure.Quotes.FreeAssetQuoteProvider(
        services.GetRequiredService<IHttpClientFactory>().CreateClient("AssetQuotes"),
        services.GetRequiredService<IConfiguration>(), services.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton(new PortfolioDefaults(
    (builder.Configuration["Portfolio:DefaultCurrencyCode"] ?? "BRL").Trim().ToUpperInvariant()));
builder.Services.AddHttpClient<IExchangeRateProvider, FrankfurterClient>(client =>
{
    client.BaseAddress = new Uri("https://api.frankfurter.dev/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient<IHistoricalExchangeRateProvider, FrankfurterClient>(client =>
{
    client.BaseAddress = new Uri("https://api.frankfurter.dev/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("BenchmarkCheck"))
{
    if (!DateOnly.TryParseExact(builder.Configuration["BenchmarkStart"], "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var start)
        || !DateOnly.TryParseExact(builder.Configuration["BenchmarkEnd"], "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var end)
        || start < new DateOnly(2000, 1, 1) || end <= start || end.DayNumber - start.DayNumber > 1830 || end > DateOnly.FromDateTime(DateTime.UtcNow))
        throw new InvalidOperationException("Informe BenchmarkStart e BenchmarkEnd no formato yyyy-MM-dd, em intervalo passado de até cinco anos.");
    var output = builder.Configuration["BenchmarkOutput"];
    if (string.IsNullOrWhiteSpace(output) || !Directory.Exists(Path.GetDirectoryName(Path.GetFullPath(output))))
        throw new InvalidOperationException("Informe BenchmarkOutput em uma pasta já existente.");
    var provider = app.Services.GetRequiredService<InvestmentTracker.Application.Benchmarks.IBenchmarkProvider>();
    var results = new List<object>();
    var codes = new[] { "CDI", "IPCA", "IBOV", "SP500", "USDBRL" };
    if (builder.Configuration["BenchmarkCode"] is { Length: > 0 } requestedCode)
    {
        if (!codes.Contains(requestedCode)) throw new InvalidOperationException("BenchmarkCode inválido.");
        codes = [requestedCode];
    }
    foreach (var code in codes)
    {
        var series = await provider.FetchAsync(code, start, end, CancellationToken.None);
        var measurement = InvestmentTracker.Application.Benchmarks.BenchmarkCalculator.Measure(code, start, end, series.Points);
        results.Add(new { code, series.Source, series.Error, series.RetrievedAt, series.FromCache, Count = series.Points.Count,
            measurement.First, measurement.Last, measurement.Factor, MeasurementError = measurement.Error });
    }
    await File.WriteAllTextAsync(output, System.Text.Json.JsonSerializer.Serialize(results, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine("Conferência de benchmarks concluída, sem acessar a base financeira. O relatório não contém chaves.");
    return;
}

if (builder.Configuration.GetValue<bool>("AssetDiscoveryCheck"))
{
    var market = builder.Configuration["AssetDiscoveryMarket"] ?? "BR";
    var symbol = (builder.Configuration["AssetDiscoverySymbol"] ?? "PETR4").Trim().ToUpperInvariant();
    var output = builder.Configuration["AssetDiscoveryOutput"];
    if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("Informe AssetDiscoveryOutput.");
    if (!Directory.Exists(Path.GetDirectoryName(Path.GetFullPath(output)))) throw new InvalidOperationException("A pasta de AssetDiscoveryOutput deve existir antes da consulta.");
    await using var scope = app.Services.CreateAsyncScope();
    var discovery = scope.ServiceProvider.GetRequiredService<InvestmentTracker.Application.Assets.AssetDiscoveryService>();
    var result = await discovery.SearchAsync(market, symbol, CancellationToken.None);
    var image = result.Items.Any(item => item.Symbol == symbol) ? await discovery.ImageAsync(market, symbol, CancellationToken.None) : null;
    await File.WriteAllTextAsync(output, System.Text.Json.JsonSerializer.Serialize(new
    {
        CheckedAt = DateTimeOffset.UtcNow,
        Result = result,
        Image = image is null ? null : new { image.Status, image.Message, image.ContentType }
    }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine("Conferência de cadastro concluída. O relatório não contém chaves nem imagens em base64.");
    return;
}

if (builder.Configuration.GetValue<bool>("QuoteCoverage"))
{
    var output = builder.Configuration["QuoteCoverageOutput"];
    if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("Informe QuoteCoverageOutput.");
    await using var scope = app.Services.CreateAsyncScope();
    var rows = await scope.ServiceProvider.GetRequiredService<InvestmentTracker.Application.Quotes.QuoteCoverageService>()
        .CheckAsync(CancellationToken.None);
    await File.WriteAllTextAsync(output, System.Text.Json.JsonSerializer.Serialize(new
    {
        CheckedAt = DateTimeOffset.UtcNow,
        Assets = rows
    }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Cobertura conferida: {rows.Count} ativos. Relatório salvo no arquivo indicado.");
    return;
}

if (builder.Configuration.GetValue<bool>("SeedCatalogs"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<InvestmentTrackerDbContext>();
    await context.Database.MigrateAsync();
    await CatalogSeed.ApplyAsync(context);
    return;
}

await using (var scope = app.Services.CreateAsyncScope())
{
    await DatabaseSchemaGuard.EnsureCurrentAsync(scope.ServiceProvider.GetRequiredService<InvestmentTrackerDbContext>());
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors("Frontend");

app.MapControllers();

app.Run();
