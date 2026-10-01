using System.Net;
using System.Net.Http.Json;
using InvestmentTracker.Application.Income;
using InvestmentTracker.Application.Movements;
using InvestmentTracker.Application.Trades;
using InvestmentTracker.Application.Common.Exceptions;
using InvestmentTracker.Infrastructure.Persistence.Repositories;
using InvestmentTracker.Application.History.Dtos;
using InvestmentTracker.Application.ExchangeRates.Interfaces;
using InvestmentTracker.Domain.Entities;
using InvestmentTracker.Infrastructure.Persistence;
using InvestmentTracker.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InvestmentTracker.IntegrationTests.Movements
{
    [Collection(DatabaseCollection.Name)]
    public class MovementsApiTests(DatabaseFixture fixture)
    {
        private static readonly DateOnly Today = new(2026, 9, 8);
        private sealed class Clock : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => new(2026, 9, 8, 15, 0, 0, TimeSpan.Zero);
        }
        private sealed class Rates : IExchangeRateProvider
        {
            public Task<ExchangeRate?> FetchAsync(string b, string q, CancellationToken ct) => Task.FromResult<ExchangeRate?>(
                new ExchangeRate { BaseCode = b, QuoteCode = q, Rate = 5m, RateDate = Today, FetchedAtUtc = DateTime.UtcNow });
        }
        private HttpClient Client()
            => fixture.ApiFactory.WithWebHostBuilder(b => b.ConfigureTestServices(s => {
                s.AddSingleton<TimeProvider, Clock>(); s.AddSingleton<IExchangeRateProvider, Rates>();
            })).CreateClient();
        private async Task<(int Portfolio, int Asset, int Cash)> Seed(bool foreign = false)
        {
            await fixture.ResetAsync();
            await using var db = fixture.Database.CreateContext();
            await CatalogSeed.ApplyAsync(db);
            var portfolio = await db.Portfolios.SingleAsync();
            var currency = foreign ? (await db.Currencies.SingleAsync(c => c.Code == "USD")).Id : portfolio.BaseCurrencyId;
            var asset = new Asset { Ticker = "TRADE", Name = "Operação", CurrencyId = currency,
                AssetTypeId = (await db.AssetTypes.FirstAsync()).Id, CountryId = (await db.Countries.FirstAsync()).Id,
                AssetCategoryId = (await db.AssetCategories.FirstAsync()).Id, SectorId = (await db.Sectors.FirstAsync()).Id };
            var cash = new ExternalAsset { PortfolioId = portfolio.Id, CurrencyId = currency, Name = "Saldo disponível", Value = 1000m, UpdatedOn = Today };
            db.Assets.Add(asset); db.ExternalAssets.Add(cash); await db.SaveChangesAsync();
            db.PortfolioAssets.Add(new PortfolioAsset { PortfolioId = portfolio.Id, AssetId = asset.Id, Quantity = 0, Income = 15m, UpdatedOn = Today });
            await db.SaveChangesAsync();
            return (portfolio.Id, asset.Id, cash.Id);
        }
        private static async Task<MovementDto> Post(HttpClient client, string path, object dto)
        {
            var response = await client.PostAsJsonAsync(path, dto);
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<MovementDto>())!;
        }
        [Fact]
        public async Task FeesAndCorporateEvents_ShouldPreserveCostAndReverseExactBalances()
        {
            var (portfolio, asset, cash) = await Seed(); using var client = Client();
            var path = $"/api/portfolios/{portfolio}";
            var buy = new SaveTradeDto(Guid.NewGuid(), Today, "buy", asset, 10, 10, cash, Fees: 2.5m);
            var response = await client.PostAsJsonAsync(path + "/trades", buy); response.EnsureSuccessStatusCode();
            var trade = (await response.Content.ReadFromJsonAsync<TradeDto>())!;
            Assert.Equal(102.5m, trade.Amount); Assert.Equal(2.5m, trade.Fees);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + "/trades", buy with { Fees = 3 })).StatusCode);
            int positionId;
            await using (var db = fixture.Database.CreateContext())
            { var p = await db.PortfolioAssets.SingleAsync(); positionId = p.Id; Assert.Equal(102.5m, p.InvestedAmount); Assert.Equal(897.5m, (await db.ExternalAssets.SingleAsync()).Value); }
            var splitInput = new SaveCorporateEventDto(Guid.NewGuid(), Today, "split", positionId, 20, 0, "Duas ações para cada ação");
            var split = await Post(client, path + "/movements/corporate-events", splitInput);
            Assert.Equal(20m, Assert.Single(split.Effects).AfterQuantity); Assert.Equal(102.5m, split.Effects[0].AfterCost);
            Assert.Equal(100m, split.Effects[0].AfterValue);
            Assert.Equal(split.Id, (await Post(client, path + "/movements/corporate-events", splitInput)).Id);
            var group = await Post(client, path + "/movements/corporate-events", splitInput with { RequestId = Guid.NewGuid(), Kind = "reverse-split", Quantity = 10 });
            Assert.Equal(102.5m, Assert.Single(group.Effects).AfterCost);
            var bonus = await Post(client, path + "/movements/corporate-events", splitInput with { RequestId = Guid.NewGuid(), Kind = "bonus", Quantity = 2, Cost = 3 });
            Assert.Equal(12m, bonus.Effects[0].AfterQuantity); Assert.Equal(105.5m, bonus.Effects[0].AfterCost);
            await Post(client, path + $"/movements/{bonus.Id}/reversal", new ReverseMovementDto(Guid.NewGuid(), "Correção do comunicado"));
            var sell = await client.PostAsJsonAsync(path + "/trades", new SaveTradeDto(Guid.NewGuid(), Today, "sell", asset, 2, 12, cash, Fees: 1));
            sell.EnsureSuccessStatusCode(); Assert.Equal(23m, (await sell.Content.ReadFromJsonAsync<TradeDto>())!.Amount);
            await using var check = fixture.Database.CreateContext(); var position = await check.PortfolioAssets.SingleAsync();
            Assert.Equal(8m, position.Quantity); Assert.Equal(82m, position.InvestedAmount);
            Assert.Equal(920.5m, (await check.ExternalAssets.SingleAsync()).Value);
            Assert.Empty(await check.PortfolioCashFlows.ToListAsync());
        }
        [Fact]
        public async Task Reopening_ShouldPreserveVersionsAndAllowLateOperationOnlyAfterDependentReversals()
        {
            var (portfolio, asset, cash) = await Seed(); using var client = Client(); var path = $"/api/portfolios/{portfolio}";
            (await client.PostAsJsonAsync(path + "/trades", new SaveTradeDto(Guid.NewGuid(), Today, "buy", asset, 1, 10, cash))).EnsureSuccessStatusCode();
            var capture = await client.PostAsync(path + "/history/snapshots", null); capture.EnsureSuccessStatusCode();
            var first = (await capture.Content.ReadFromJsonAsync<SnapshotDetailDto>())!;
            var late = new SaveTradeDto(Guid.NewGuid(), Today.AddDays(-1), "buy", asset, 1, 8, cash);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + "/trades", late)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path + "/movements", new SaveMovementDto(Guid.NewGuid(), Today.AddDays(-1), "reopen", 0, 0, Reason: ""))).StatusCode);
            var reopenInput = new SaveMovementDto(Guid.NewGuid(), Today.AddDays(-1), "reopen", 0, 0, Reason: "Operação pendente na corretora");
            var reopen = await Post(client, path + "/movements", reopenInput);
            Assert.Equal(reopen.Id, (await Post(client, path + "/movements", reopenInput)).Id);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + "/trades", late)).StatusCode);
            var all = (await client.GetFromJsonAsync<List<MovementDto>>(path + "/movements"))!;
            var buy = all.Single(x => x.Kind == "buy");
            await Post(client, path + $"/movements/{buy.Id}/reversal", new ReverseMovementDto(Guid.NewGuid(), "Reordenar lançamento atrasado"));
            (await client.PostAsJsonAsync(path + "/trades", late)).EnsureSuccessStatusCode();
            var outdated = (await client.GetFromJsonAsync<SnapshotDetailDto>(path + $"/history/snapshots/{first.Id}"))!;
            Assert.True(outdated.IsOutdated); Assert.True(outdated.IsReopened);
            Assert.Equal(first.Dashboard.TotalWealth, outdated.Dashboard.TotalWealth);
            var refresh = await client.PutAsync(path + "/history/snapshots/current", null); refresh.EnsureSuccessStatusCode();
            var revised = (await refresh.Content.ReadFromJsonAsync<SnapshotDetailDto>())!;
            Assert.Equal(2, revised.Revision); Assert.False(revised.IsOutdated); Assert.False(revised.IsReopened);
            Assert.Equal(first.Dashboard.TotalWealth, Assert.Single(revised.PreviousVersions).Dashboard.TotalWealth);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + "/trades", late with { RequestId = Guid.NewGuid() })).StatusCode);
        }
        [Fact]
        public async Task LateIndependentBalance_ShouldBeAcceptedAndSameDaySnapshotMarkedOutdated()
        {
            var (portfolio, _, cash) = await Seed(); using var client = Client(); var path = $"/api/portfolios/{portfolio}";
            int other;
            await using (var db = fixture.Database.CreateContext())
            { var balance = new ExternalAsset { PortfolioId = portfolio, CurrencyId = (await db.Portfolios.SingleAsync()).BaseCurrencyId, Name = "Outro saldo", Value = 0, UpdatedOn = Today }; db.Add(balance); await db.SaveChangesAsync(); other = balance.Id; }
            await Post(client, path + "/movements", new SaveMovementDto(Guid.NewGuid(), Today, "deposit", cash, 5));
            await Post(client, path + "/movements", new SaveMovementDto(Guid.NewGuid(), Today.AddDays(-1), "deposit", other, 3));
            (await client.PostAsync(path + "/history/snapshots", null)).EnsureSuccessStatusCode();
            await Post(client, path + "/movements", new SaveMovementDto(Guid.NewGuid(), Today, "deposit", other, 2));
            var history = (await client.GetFromJsonAsync<HistoryDto>(path + "/history"))!;
            Assert.True(Assert.Single(history.Months).IsOutdated);
        }
        [Fact]
        public async Task IncomeAnalysis_ShouldSeparateCurrenciesAndRetainReversalAudit()
        {
            var (portfolio, asset, cash) = await Seed(); using var client = Client(); var path = $"/api/portfolios/{portfolio}";
            (await client.PostAsJsonAsync(path + "/income", new SaveIncomeDto(Guid.NewGuid(), Today, asset, 10, cash))).EnsureSuccessStatusCode();
            var movement = Assert.Single((await client.GetFromJsonAsync<List<MovementDto>>(path + "/movements"))!);
            await Post(client, path + $"/movements/{movement.Id}/reversal", new ReverseMovementDto(Guid.NewGuid(), "Duplicado"));
            var report = (await client.GetFromJsonAsync<IncomeAnalysisDto>(path + "/income/analysis"))!;
            var month = Assert.Single(report.Months); Assert.Equal(10m, month.Received); Assert.Equal(10m, month.Reversed); Assert.Equal(0m, month.Net);
            Assert.Equal(0m, Assert.Single(report.Assets).Net);
        }
        [Fact]
        public async Task Migration_ShouldIndexLegacyRecordsWithoutInventingBalancesOrChangingAmounts()
        {
            var (portfolio, asset, _) = await Seed();
            await using var db = fixture.Database.CreateContext();
            var migrator = db.GetService<IMigrator>();
            try
            {
                await migrator.MigrateAsync("20260909135902_IncomeReceipts");
                var currency = await db.Currencies.SingleAsync(x => x.Code == "BRL");
                var trade = new PortfolioTrade { PortfolioId = portfolio, AssetId = asset, RequestId = Guid.NewGuid(), Date = Today,
                    Kind = "buy", Ticker = "LEGACY", CurrencyCode = "BRL", Quantity = 1, UnitPrice = 20, Amount = 20,
                    RemainingQuantity = 1, RemainingCost = 20, CreatedAtUtc = DateTime.UtcNow };
                var income = new IncomeReceipt { PortfolioId = portfolio, PositionId = (await db.PortfolioAssets.SingleAsync()).Id,
                    AssetId = asset, RequestId = Guid.NewGuid(), Date = Today, Ticker = "LEGACY", CurrencyCode = "BRL", Amount = 5,
                    CreatedAtUtc = DateTime.UtcNow };
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO PortfolioTrade (PortfolioId, AssetId, RequestId, Date, Kind, Ticker, CurrencyCode, Quantity, UnitPrice, Amount, RemainingQuantity, RemainingCost, CreatedAtUtc)
                    VALUES ({portfolio}, {asset}, {trade.RequestId}, {Today}, 'buy', 'LEGACY', 'BRL', 1, 20, 20, 1, 20, {DateTime.UtcNow});
                    """);
                trade.Id = await db.Database.SqlQuery<int>($"SELECT Id AS Value FROM PortfolioTrade WHERE RequestId = {trade.RequestId}").SingleAsync();
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO IncomeReceipt (PortfolioId, PositionId, AssetId, RequestId, Date, Ticker, CurrencyCode, Amount, CreatedAtUtc)
                    VALUES ({portfolio}, {income.PositionId}, {asset}, {income.RequestId}, {Today}, 'LEGACY', 'BRL', 5, {DateTime.UtcNow});
                    """);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO PortfolioCashFlow (PortfolioId, TradeId, Date, Kind, CurrencyId, Amount, BaseCurrencyCode, BaseAmount, CreatedAtUtc, UpdatedAtUtc)
                    VALUES ({portfolio}, {trade.Id}, {Today}, 'contribution', {currency.Id}, 20, 'BRL', 20, {DateTime.UtcNow}, {DateTime.UtcNow});
                    INSERT INTO PortfolioCashFlow (PortfolioId, Date, Kind, CurrencyId, Amount, BaseCurrencyCode, BaseAmount, CreatedAtUtc, UpdatedAtUtc)
                    VALUES ({portfolio}, {Today}, 'withdrawal', {currency.Id}, 3, 'BRL', 3, {DateTime.UtcNow}, {DateTime.UtcNow});
                    """);
                await migrator.MigrateAsync();
                db.ChangeTracker.Clear();
                Assert.Equal(3, await db.Set<FinancialMovement>().CountAsync());
                Assert.Empty(await db.Set<MovementEffect>().ToListAsync());
                Assert.All(await db.PortfolioCashFlows.ToListAsync(), x => Assert.NotNull(x.MovementId));
                Assert.Equal(23m, await db.PortfolioCashFlows.SumAsync(x => x.Amount));
                Assert.Equal(15m, (await db.PortfolioAssets.SingleAsync()).Income);
                Assert.Equal(1000m, (await db.ExternalAssets.SingleAsync()).Value);
                using var client = Client();
                var movements = (await client.GetFromJsonAsync<List<MovementDto>>($"/api/portfolios/{portfolio}/movements"))!;
                Assert.All(movements.Where(x => x.TradeId.HasValue || x.IncomeReceiptId.HasValue), x => Assert.False(x.CanReverse));
                Assert.True(movements.Single(x => x.Kind == "historical").CanReverse);
            }
            finally { await migrator.MigrateAsync(); }
        }
        [Fact]
        public async Task SellReversal_ShouldRestoreExactCostAndQuantityAndNotCountAsANewContribution()
        {
            var (portfolio, asset, _) = await Seed(); using var client = Client();
            await using (var db = fixture.Database.CreateContext())
            {
                var p = await db.PortfolioAssets.SingleAsync(); p.Quantity = 3m; p.InvestedAmount = 100m; p.CurrentValue = 120m;
                await db.SaveChangesAsync();
            }
            var path = $"/api/portfolios/{portfolio}";
            (await client.PostAsJsonAsync(path + "/trades", new SaveTradeDto(Guid.NewGuid(), Today, "sell", asset, 1m, 50m))).EnsureSuccessStatusCode();
            var sale = Assert.Single((await client.GetFromJsonAsync<List<MovementDto>>(path + "/movements"))!);
            await Post(client, path + $"/movements/{sale.Id}/reversal", new ReverseMovementDto(Guid.NewGuid(), "Venda incorreta"));
            var history = (await client.GetFromJsonAsync<HistoryDto>(path + "/history"))!;
            Assert.Equal(0m, Assert.Single(history.Months).Contributions); Assert.Equal(0m, history.Months[0].Withdrawals);
            await using var check = fixture.Database.CreateContext(); var position = await check.PortfolioAssets.SingleAsync();
            Assert.Equal(3m, position.Quantity); Assert.Equal(100m, position.InvestedAmount); Assert.Equal(120m, position.CurrentValue);
        }
        [Fact]
        public async Task ForeignDeposit_ShouldFreezeEquivalentAndRejectTransferAcrossCurrencies()
        {
            var (portfolio, _, cash) = await Seed(true); using var client = Client();
            var path = $"/api/portfolios/{portfolio}/movements";
            var deposit = await Post(client, path, new SaveMovementDto(Guid.NewGuid(), Today, "deposit", cash, 10, BaseAmount: 47));
            int destination;
            await using (var db = fixture.Database.CreateContext())
            {
                var balance = new ExternalAsset { PortfolioId = portfolio, CurrencyId = (await db.Currencies.SingleAsync(x => x.Code == "BRL")).Id,
                    Name = "Reais", Value = 0, UpdatedOn = Today };
                db.ExternalAssets.Add(balance); await db.SaveChangesAsync(); destination = balance.Id;
            }
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, new SaveMovementDto(Guid.NewGuid(), Today, "transfer", cash, 10, destination))).StatusCode);
            await Post(client, path + $"/{deposit.Id}/reversal", new ReverseMovementDto(Guid.NewGuid(), "Valor incorreto"));
            await using var check = fixture.Database.CreateContext(); var flows = await check.PortfolioCashFlows.ToListAsync();
            Assert.Equal(2, flows.Count); Assert.All(flows, f => Assert.Equal(47m, f.BaseAmount));
            Assert.Single(flows, f => f.IsReversal);
            Assert.Equal(1000m, (await check.ExternalAssets.SingleAsync(x => x.Id == cash)).Value);
        }
        [Fact]
        public async Task MoneyMovements_ShouldUpdateBalancesAndFlowsAndReverseInDependencyOrder()
        {
            var (portfolio, _, cash) = await Seed(); using var client = Client();
            int destination;
            await using (var db = fixture.Database.CreateContext())
            {
                var balance = new ExternalAsset { PortfolioId = portfolio, CurrencyId = (await db.ExternalAssets.SingleAsync()).CurrencyId,
                    Name = "Outro saldo", Value = 0, UpdatedOn = Today };
                db.ExternalAssets.Add(balance); await db.SaveChangesAsync(); destination = balance.Id;
            }
            var path = $"/api/portfolios/{portfolio}/movements";
            var input = new SaveMovementDto(Guid.NewGuid(), Today, "deposit", cash, 100m);
            var deposit = await Post(client, path, input);
            Assert.Equal(deposit.Id, (await Post(client, path, input)).Id);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, input with { Amount = 101m })).StatusCode);
            var transfer = await Post(client, path, new SaveMovementDto(Guid.NewGuid(), Today, "transfer", cash, 50m, destination));
            var adjustment = await Post(client, path, new SaveMovementDto(Guid.NewGuid(), Today, "adjustment", destination, 60m, Reason: "Correção de saldo"));
            await using (var db = fixture.Database.CreateContext())
            {
                Assert.Equal(1050m, (await db.ExternalAssets.SingleAsync(x => x.Id == cash)).Value);
                Assert.Equal(60m, (await db.ExternalAssets.SingleAsync(x => x.Id == destination)).Value);
                Assert.Single(await db.PortfolioCashFlows.ToListAsync());
            }
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + $"/{deposit.Id}/reversal", new ReverseMovementDto(Guid.NewGuid(), "Erro"))).StatusCode);
            await Post(client, path + $"/{adjustment.Id}/reversal", new ReverseMovementDto(Guid.NewGuid(), "Valor incorreto"));
            await Post(client, path + $"/{transfer.Id}/reversal", new ReverseMovementDto(Guid.NewGuid(), "Destino incorreto"));
            var reversalInput = new ReverseMovementDto(Guid.NewGuid(), "Depósito duplicado");
            var reversal = await Post(client, path + $"/{deposit.Id}/reversal", reversalInput);
            Assert.Equal(reversal.Id, (await Post(client, path + $"/{deposit.Id}/reversal", reversalInput)).Id);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + $"/{deposit.Id}/reversal", reversalInput with { RequestId = Guid.NewGuid() })).StatusCode);
            await using var check = fixture.Database.CreateContext();
            Assert.Equal(1000m, (await check.ExternalAssets.SingleAsync(x => x.Id == cash)).Value);
            Assert.Equal(0m, (await check.ExternalAssets.SingleAsync(x => x.Id == destination)).Value);
            Assert.Equal(2, await check.PortfolioCashFlows.CountAsync());
            Assert.Equal(6, await check.Set<FinancialMovement>().CountAsync());
            Assert.Equal(deposit.Id, reversal.ReversalOfId);
        }
        [Fact]
        public async Task TradeAndIncomeReversal_ShouldRestorePositionAndCashWithoutDeletingOriginals()
        {
            var (portfolio, asset, cash) = await Seed(); using var client = Client();
            var path = $"/api/portfolios/{portfolio}";
            (await client.PostAsJsonAsync(path + "/trades", new SaveTradeDto(Guid.NewGuid(), Today, "buy", asset, 10, 20))).EnsureSuccessStatusCode();
            (await client.PostAsJsonAsync(path + "/income", new SaveIncomeDto(Guid.NewGuid(), Today, asset, 25, cash))).EnsureSuccessStatusCode();
            var movements = (await client.GetFromJsonAsync<List<MovementDto>>(path + "/movements"))!;
            var buy = movements.Single(x => x.Kind == "buy"); var income = movements.Single(x => x.Kind == "income");
            Assert.False(buy.CanReverse); Assert.True(income.CanReverse);
            await Post(client, path + $"/movements/{income.Id}/reversal", new ReverseMovementDto(Guid.NewGuid(), "Recebimento incorreto"));
            await Post(client, path + $"/movements/{buy.Id}/reversal", new ReverseMovementDto(Guid.NewGuid(), "Compra incorreta"));
            await using var db = fixture.Database.CreateContext();
            var position = await db.PortfolioAssets.SingleAsync();
            Assert.Equal(0m, position.Quantity); Assert.Equal(0m, position.CurrentValue); Assert.Equal(0m, position.InvestedAmount);
            Assert.Equal(15m, position.Income); Assert.Equal(1000m, (await db.ExternalAssets.SingleAsync()).Value);
            Assert.Single(await db.Set<PortfolioTrade>().ToListAsync()); Assert.Single(await db.Set<IncomeReceipt>().ToListAsync());
            Assert.Equal(2, await db.PortfolioCashFlows.CountAsync());
            Assert.Equal(200m, (await db.PortfolioCashFlows.SingleAsync(x => x.IsReversal)).Amount);
        }
        [Fact]
        public async Task Reversal_ShouldRequireReasonAndPreserveSnapshotAndRejectWrongPortfolio()
        {
            var (portfolio, _, cash) = await Seed(); using var client = Client();
            var path = $"/api/portfolios/{portfolio}";
            var deposit = await Post(client, path + "/movements", new SaveMovementDto(Guid.NewGuid(), Today, "deposit", cash, 10));
            var capture = await client.PostAsync(path + "/history/snapshots", null); capture.EnsureSuccessStatusCode();
            using var json = System.Text.Json.JsonDocument.Parse(await capture.Content.ReadAsStringAsync());
            var snapshotPath = path + "/history/snapshots/" + json.RootElement.GetProperty("id").GetInt32();
            var before = await client.GetStringAsync(snapshotPath);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path + $"/movements/{deposit.Id}/reversal", new ReverseMovementDto(Guid.NewGuid(), " "))).StatusCode);
            var otherResponse = await client.PostAsJsonAsync("/api/portfolios", new { name = "Outra carteira" });
            using var other = System.Text.Json.JsonDocument.Parse(await otherResponse.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/portfolios/{other.RootElement.GetProperty("id").GetInt32()}/movements/{deposit.Id}/reversal", new ReverseMovementDto(Guid.NewGuid(), "Erro"))).StatusCode);
            await Post(client, path + $"/movements/{deposit.Id}/reversal", new ReverseMovementDto(Guid.NewGuid(), "Duplicado"));
            Assert.Equal(before.Replace("\"isOutdated\":false", "\"isOutdated\":true"), await client.GetStringAsync(snapshotPath));
        }
        [Fact]
        public async Task InvalidTransfersAndInsufficientBalance_ShouldLeaveNoPartialWrites()
        {
            var (portfolio, _, cash) = await Seed(); using var client = Client(); var path = $"/api/portfolios/{portfolio}/movements";
            foreach (var dto in new[] {
                new SaveMovementDto(Guid.NewGuid(), Today, "withdrawal", cash, 1001),
                new SaveMovementDto(Guid.NewGuid(), Today, "transfer", cash, 1, cash),
                new SaveMovementDto(Guid.NewGuid(), Today, "adjustment", cash, 30),
                new SaveMovementDto(Guid.NewGuid(), Today.AddDays(1), "deposit", cash, 1) })
                Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, dto)).StatusCode);
            await using var db = fixture.Database.CreateContext();
            Assert.Equal(1000m, (await db.ExternalAssets.SingleAsync()).Value);
            Assert.Empty(await db.Set<FinancialMovement>().ToListAsync()); Assert.Empty(await db.PortfolioCashFlows.ToListAsync());
        }
        [Fact]
        public async Task ConcurrentReversals_ShouldApplyOnlyOnce()
        {
            var (portfolio, _, cash) = await Seed(); using var client = Client(); var path = $"/api/portfolios/{portfolio}/movements";
            var deposit = await Post(client, path, new SaveMovementDto(Guid.NewGuid(), Today, "deposit", cash, 10));
            var results = await Task.WhenAll(client.PostAsJsonAsync(path + $"/{deposit.Id}/reversal", new ReverseMovementDto(Guid.NewGuid(), "Erro")),
                client.PostAsJsonAsync(path + $"/{deposit.Id}/reversal", new ReverseMovementDto(Guid.NewGuid(), "Erro")));
            Assert.Single(results, x => x.IsSuccessStatusCode); Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
            await using var db = fixture.Database.CreateContext();
            Assert.Equal(1000m, (await db.ExternalAssets.SingleAsync()).Value);
            Assert.Equal(2, await db.Set<FinancialMovement>().CountAsync());
        }
    }
}
