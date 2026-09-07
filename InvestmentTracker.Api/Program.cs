using InvestmentTracker.Api.Exceptions;
using InvestmentTracker.Infrastructure.Persistence;
using InvestmentTracker.Application;
using Microsoft.EntityFrameworkCore;
using InvestmentTracker.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApplicationExceptionHandler>();

builder.Services.AddOpenApi();

var connectionString =
    builder.Configuration.GetConnectionString("InvestmentTracker");

builder.Services.AddDbContext<InvestmentTrackerDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddApplication();
builder.Services.AddInfrastructure();

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

if (builder.Configuration.GetValue<bool>("SeedCatalogs"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<InvestmentTrackerDbContext>();
    await context.Database.MigrateAsync();
    await CatalogSeed.ApplyAsync(context);
    return;
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