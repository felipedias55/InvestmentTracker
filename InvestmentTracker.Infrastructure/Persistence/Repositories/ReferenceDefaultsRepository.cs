using System.Data;
using System.Globalization;
using System.Text;
using InvestmentTracker.Application.Assets;
using InvestmentTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvestmentTracker.Infrastructure.Persistence.Repositories
{
    public sealed class ReferenceDefaultsRepository(InvestmentTrackerDbContext db) : IReferenceDefaultsRepository
    {
        public async Task<ReferenceDefaultsResult> EnsureAsync(CancellationToken ct)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var countries = await db.Countries.ToListAsync(ct);
            var currencies = await db.Currencies.ToListAsync(ct);
            var addedCountries = 0; var addedCurrencies = 0;
            foreach (var (name, aliases) in new[] { ("Brasil", new[] { "BRASIL", "BRAZIL" }), ("Estados Unidos", new[] { "ESTADOS UNIDOS", "UNITED STATES", "UNITED STATES OF AMERICA", "EUA", "USA" }) })
                if (!countries.Any(c => aliases.Contains(Normalize(c.Name)))) { db.Countries.Add(new Country { Name = name }); addedCountries++; }
            foreach (var item in new[] { new Currency { Code = "BRL", Name = "Real brasileiro", Symbol = "R$" }, new Currency { Code = "USD", Name = "Dólar americano", Symbol = "US$" } })
                if (!currencies.Any(c => c.Code.Trim().Equals(item.Code, StringComparison.OrdinalIgnoreCase))) { db.Currencies.Add(item); addedCurrencies++; }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(addedCountries, addedCurrencies, "Referências BR/US disponíveis. Cadastros existentes foram preservados; confira opções equivalentes antes de criar outras.");
        }
        private static string Normalize(string value) => string.Concat(value.Trim().Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToUpperInvariant();
    }
}
