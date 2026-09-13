using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestmentTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditableMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MovementId",
                table: "PortfolioCashFlow",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FinancialMovement",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PortfolioId = table.Column<int>(type: "int", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TradeId = table.Column<int>(type: "int", nullable: true),
                    IncomeReceiptId = table.Column<int>(type: "int", nullable: true),
                    ReversalOfId = table.Column<int>(type: "int", nullable: true),
                    RequestPayload = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialMovement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialMovement_FinancialMovement_ReversalOfId",
                        column: x => x.ReversalOfId,
                        principalTable: "FinancialMovement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialMovement_IncomeReceipt_IncomeReceiptId",
                        column: x => x.IncomeReceiptId,
                        principalTable: "IncomeReceipt",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialMovement_PortfolioTrade_TradeId",
                        column: x => x.TradeId,
                        principalTable: "PortfolioTrade",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialMovement_Portfolio_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "Portfolio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovementEffect",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FinancialMovementId = table.Column<int>(type: "int", nullable: false),
                    PositionId = table.Column<int>(type: "int", nullable: true),
                    CashAssetId = table.Column<int>(type: "int", nullable: true),
                    CurrencyId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BeforeValue = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    AfterValue = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    BeforeQuantity = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    AfterQuantity = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    BeforeCost = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    AfterCost = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    BeforeIncome = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    AfterIncome = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovementEffect", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovementEffect_Currency_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovementEffect_ExternalAsset_CashAssetId",
                        column: x => x.CashAssetId,
                        principalTable: "ExternalAsset",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovementEffect_FinancialMovement_FinancialMovementId",
                        column: x => x.FinancialMovementId,
                        principalTable: "FinancialMovement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovementEffect_PortfolioAsset_PositionId",
                        column: x => x.PositionId,
                        principalTable: "PortfolioAsset",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioCashFlow_MovementId",
                table: "PortfolioCashFlow",
                column: "MovementId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialMovement_IncomeReceiptId",
                table: "FinancialMovement",
                column: "IncomeReceiptId",
                unique: true,
                filter: "[IncomeReceiptId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialMovement_PortfolioId_RequestId",
                table: "FinancialMovement",
                columns: new[] { "PortfolioId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialMovement_ReversalOfId",
                table: "FinancialMovement",
                column: "ReversalOfId",
                unique: true,
                filter: "[ReversalOfId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialMovement_TradeId",
                table: "FinancialMovement",
                column: "TradeId",
                unique: true,
                filter: "[TradeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MovementEffect_CashAssetId",
                table: "MovementEffect",
                column: "CashAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_MovementEffect_CurrencyId",
                table: "MovementEffect",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_MovementEffect_FinancialMovementId",
                table: "MovementEffect",
                column: "FinancialMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_MovementEffect_PositionId",
                table: "MovementEffect",
                column: "PositionId");

            migrationBuilder.AddForeignKey(
                name: "FK_PortfolioCashFlow_FinancialMovement_MovementId",
                table: "PortfolioCashFlow",
                column: "MovementId",
                principalTable: "FinancialMovement",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            // Preserve legacy records without inventing unavailable before/after balances.
            migrationBuilder.Sql("""
                INSERT INTO FinancialMovement (PortfolioId, RequestId, Date, Kind, Amount, CurrencyCode, Description, CreatedAtUtc, TradeId)
                SELECT PortfolioId, RequestId, Date, Kind, Amount, CurrencyCode, Ticker, CreatedAtUtc, Id FROM PortfolioTrade;
                INSERT INTO FinancialMovement (PortfolioId, RequestId, Date, Kind, Amount, CurrencyCode, Description, CreatedAtUtc, IncomeReceiptId)
                SELECT PortfolioId, NEWID(), Date, 'income', Amount, CurrencyCode, Ticker, CreatedAtUtc, Id FROM IncomeReceipt;
                UPDATE f SET MovementId = m.Id FROM PortfolioCashFlow f JOIN FinancialMovement m ON m.TradeId = f.TradeId;
                DECLARE @flowId int, @movementId int;
                DECLARE flows CURSOR LOCAL FAST_FORWARD FOR SELECT Id FROM PortfolioCashFlow WHERE MovementId IS NULL;
                OPEN flows; FETCH NEXT FROM flows INTO @flowId;
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    INSERT INTO FinancialMovement (PortfolioId, RequestId, Date, Kind, Amount, CurrencyCode, Description, CreatedAtUtc)
                    SELECT f.PortfolioId, NEWID(), f.Date, 'historical', f.Amount, c.Code,
                        COALESCE(f.Notes, N'Registro histórico sem alteração de saldo'), f.CreatedAtUtc
                    FROM PortfolioCashFlow f JOIN Currency c ON c.Id = f.CurrencyId WHERE f.Id = @flowId;
                    SET @movementId = SCOPE_IDENTITY();
                    UPDATE PortfolioCashFlow SET MovementId = @movementId WHERE Id = @flowId;
                    FETCH NEXT FROM flows INTO @flowId;
                END;
                CLOSE flows; DEALLOCATE flows;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM FinancialMovement) THROW 51000, 'Rollback would discard the movement audit trail.', 1;");
            migrationBuilder.DropForeignKey(
                name: "FK_PortfolioCashFlow_FinancialMovement_MovementId",
                table: "PortfolioCashFlow");

            migrationBuilder.DropTable(
                name: "MovementEffect");

            migrationBuilder.DropTable(
                name: "FinancialMovement");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioCashFlow_MovementId",
                table: "PortfolioCashFlow");

            migrationBuilder.DropColumn(
                name: "MovementId",
                table: "PortfolioCashFlow");
        }
    }
}
