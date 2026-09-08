using System.Data;
using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvestmentTracker.Infrastructure.Persistence
{
    public static class CatalogSeed
    {
        public static async Task ApplyAsync(InvestmentTrackerDbContext context, CancellationToken cancellationToken = default)
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            foreach (var name in new[] { "Ação", "FII", "REIT" })
            {
                if (!await context.AssetTypes.AnyAsync(x => x.Name == name, cancellationToken))
                    context.AssetTypes.Add(new AssetType { Name = name });
            }
            foreach (var name in new[] { "Brasil", "Estados Unidos" })
            {
                if (!await context.Countries.AnyAsync(x => x.Name == name, cancellationToken))
                    context.Countries.Add(new Country { Name = name });
            }
            foreach (var currency in new[]
            {
                new Currency { Code = "BRL", Name = "Real brasileiro", Symbol = "R$" },
                new Currency { Code = "USD", Name = "Dólar americano", Symbol = "US$" }
            })
            {
                if (!await context.Currencies.AnyAsync(x => x.Code == currency.Code, cancellationToken))
                    context.Currencies.Add(currency);
            }
            foreach (var name in new[] { "Ações BR", "Ações EUA", "FIIs", "REITs" })
            {
                if (!await context.AssetCategories.AnyAsync(x => x.Name == name, cancellationToken))
                    context.AssetCategories.Add(new AssetCategory { Name = name });
            }
            foreach (var name in new[] { "Consumo", "Energia", "Financeiro", "Imobiliário",
                "Materiais Básicos", "Saneamento", "Saúde", "Tecnologia" })
            {
                if (!await context.Sectors.AnyAsync(x => x.Name == name, cancellationToken))
                    context.Sectors.Add(new Sector { Name = name });
            }
            await context.SaveChangesAsync(cancellationToken);
            if (!await context.Portfolios.AnyAsync(cancellationToken))
            {
                var brl = await context.Currencies.SingleAsync(x => x.Code == "BRL", cancellationToken);
                context.Portfolios.Add(new Portfolio { Name = "Carteira Principal", BaseCurrencyId = brl.Id,
                    CreatedAt = DateTime.UtcNow });
                await context.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
    }
}
