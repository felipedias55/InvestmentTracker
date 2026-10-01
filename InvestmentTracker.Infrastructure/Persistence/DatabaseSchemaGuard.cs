using Microsoft.EntityFrameworkCore;

namespace InvestmentTracker.Infrastructure.Persistence
{
    public static class DatabaseSchemaGuard
    {
        public static async Task EnsureCurrentAsync(InvestmentTrackerDbContext db, CancellationToken ct = default)
        {
            if (db.Database.HasPendingModelChanges())
                throw new InvalidOperationException("O modelo possui alterações sem migration. Gere e revise a migration antes de iniciar a API.");
            if ((await db.Database.GetPendingMigrationsAsync(ct)).Any())
                throw new InvalidOperationException("Banco desatualizado: existem migrations pendentes. Após o backup, execute: dotnet ef database update --project InvestmentTracker.Infrastructure --startup-project InvestmentTracker.Api");
        }
    }
}
