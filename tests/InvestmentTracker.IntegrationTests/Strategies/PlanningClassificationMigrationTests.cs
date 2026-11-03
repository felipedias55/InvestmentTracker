using InvestmentTracker.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InvestmentTracker.IntegrationTests.Strategies
{
    public sealed class PlanningClassificationMigrationTests
    {
        [Fact]
        public async Task MigrationShouldMapOnlyCanonicalLabelsAndKeepUnknownTypesForReview()
        {
            await using var database = new IntegrationTestDatabase();
            await database.InitializeAsync();
            await using var db = database.CreateContext(); var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261015122025_PortfolioStrategies");
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO [AssetType] ([Name]) VALUES (N'FII'), (N'REIT'), (N'Ação'), (N'Fundos personalizados');
                INSERT INTO [Sector] ([Name]) VALUES (N'Imobiliário'), (N'Outro setor');
                INSERT INTO [Country] ([Name]) VALUES (N'Brasil');
                INSERT INTO [Currency] ([Name], [Code], [Symbol]) VALUES (N'Real', N'BRL', N'R$');
                INSERT INTO [AssetCategory] ([Name]) VALUES (N'Categoria antiga');
                INSERT INTO [Portfolio] ([Name], [BaseCurrencyId], [CreatedAt]) SELECT N'Legado', [Id], '2026-01-01' FROM [Currency];
                INSERT INTO [Asset] ([Ticker], [Name], [AssetTypeId], [CountryId], [CurrencyId], [AssetCategoryId], [SectorId], [CreatedAt])
                SELECT 'LEGACY', N'Posição antiga', t.[Id], c.[Id], m.[Id], a.[Id], s.[Id], '2026-01-01'
                FROM [AssetType] t CROSS JOIN [Country] c CROSS JOIN [Currency] m CROSS JOIN [AssetCategory] a CROSS JOIN [Sector] s
                WHERE t.[Name] = N'FII' AND s.[Name] = N'Imobiliário';
                INSERT INTO [PortfolioAsset] ([PortfolioId], [AssetId], [Quantity], [InvestedAmount], [CurrentValue], [Income], [UpdatedOn])
                SELECT p.[Id], a.[Id], 7.123456, 1234.5678, 4321.1234, 5.1234, '2026-01-01' FROM [Portfolio] p CROSS JOIN [Asset] a;
                """);
            await migrator.MigrateAsync();
            var types = await db.AssetTypes.AsNoTracking().ToListAsync();
            Assert.Equal("fii", types.Single(t => t.Name == "FII").InstrumentClass);
            Assert.Equal("reit", types.Single(t => t.Name == "REIT").InstrumentClass);
            Assert.Equal("general", types.Single(t => t.Name == "Ação").InstrumentClass);
            Assert.Equal("unclassified", types.Single(t => t.Name == "Fundos personalizados").InstrumentClass);
            Assert.True((await db.Sectors.SingleAsync(s => s.Name == "Imobiliário")).IsRealEstate);
            Assert.False((await db.Sectors.SingleAsync(s => s.Name == "Outro setor")).IsRealEstate);
            Assert.Equal(4, types.Count);
            var position = await db.PortfolioAssets.AsNoTracking().SingleAsync();
            Assert.Equal(7.123456m, position.Quantity); Assert.Equal(1234.5678m, position.InvestedAmount);
            Assert.Equal(4321.1234m, position.CurrentValue); Assert.Equal(5.1234m, position.Income);
            Assert.Equal(new DateOnly(2026, 1, 1), position.UpdatedOn);
            Assert.Equal("LEGACY", (await db.Assets.SingleAsync()).Ticker);
            Assert.False(db.Database.HasPendingModelChanges());
        }
    }
}
