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
builder.Services.AddHttpClient("AssetQuotes", client => client.Timeout = TimeSpan.FromSeconds(15)).RemoveAllLoggers();
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
