using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestmentTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PortfolioBaseCurrencyAndExchangeRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM PortfolioAsset WHERE Quantity < 0 OR InvestedAmount < 0 OR CurrentValue < 0
                    OR Quantity <> ROUND(Quantity, 6) OR InvestedAmount > 999999999999999.9999
                    OR CurrentValue > 999999999999999.9999)
                    THROW 51000, 'Revise as posicoes existentes: valores negativos ou precisao fora dos novos limites.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_PortfolioAsset_Portfolio_PortfolioId",
                table: "PortfolioAsset");

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "PortfolioAsset",
                type: "decimal(19,6)",
                precision: 19,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,8)",
                oldPrecision: 18,
                oldScale: 8);

            migrationBuilder.AlterColumn<decimal>(
                name: "InvestedAmount",
                table: "PortfolioAsset",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "CurrentValue",
                table: "PortfolioAsset",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddColumn<int>(
                name: "BaseCurrencyId",
                table: "Portfolio",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM Portfolio)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM Currency WHERE Code = N'BRL')
                        INSERT INTO Currency (Code, Name, Symbol) VALUES (N'BRL', N'Real brasileiro', N'R$');
                    UPDATE Portfolio SET BaseCurrencyId = (SELECT Id FROM Currency WHERE Code = N'BRL');
                END
                """);
            migrationBuilder.AlterColumn<int>(
                name: "BaseCurrencyId", table: "Portfolio", type: "int", nullable: false,
                oldClrType: typeof(int), oldType: "int", oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ExchangeRate",
                columns: table => new
                {
                    BaseCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    QuoteCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    RateDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FetchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRate", x => new { x.BaseCode, x.QuoteCode });
                    table.CheckConstraint("CK_ExchangeRate_Positive", "[Rate] > 0");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_PortfolioAsset_NonNegative",
                table: "PortfolioAsset",
                sql: "[Quantity] >= 0 AND [InvestedAmount] >= 0 AND [CurrentValue] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_Portfolio_BaseCurrencyId",
                table: "Portfolio",
                column: "BaseCurrencyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Portfolio_Currency_BaseCurrencyId",
                table: "Portfolio",
                column: "BaseCurrencyId",
                principalTable: "Currency",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PortfolioAsset_Portfolio_PortfolioId",
                table: "PortfolioAsset",
                column: "PortfolioId",
                principalTable: "Portfolio",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM PortfolioAsset WHERE Quantity > 9999999999.99999999
                    OR InvestedAmount <> ROUND(InvestedAmount, 2) OR CurrentValue <> ROUND(CurrentValue, 2))
                    THROW 51000, 'O rollback perderia precisao. Revise as posicoes antes de reverter.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_Portfolio_Currency_BaseCurrencyId",
                table: "Portfolio");

            migrationBuilder.DropForeignKey(
                name: "FK_PortfolioAsset_Portfolio_PortfolioId",
                table: "PortfolioAsset");

            migrationBuilder.DropTable(
                name: "ExchangeRate");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PortfolioAsset_NonNegative",
                table: "PortfolioAsset");

            migrationBuilder.DropIndex(
                name: "IX_Portfolio_BaseCurrencyId",
                table: "Portfolio");

            migrationBuilder.DropColumn(
                name: "BaseCurrencyId",
                table: "Portfolio");

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "PortfolioAsset",
                type: "decimal(18,8)",
                precision: 18,
                scale: 8,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,6)",
                oldPrecision: 19,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "InvestedAmount",
                table: "PortfolioAsset",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,4)",
                oldPrecision: 19,
                oldScale: 4);

            migrationBuilder.AlterColumn<decimal>(
                name: "CurrentValue",
                table: "PortfolioAsset",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,4)",
                oldPrecision: 19,
                oldScale: 4);

            migrationBuilder.AddForeignKey(
                name: "FK_PortfolioAsset_Portfolio_PortfolioId",
                table: "PortfolioAsset",
                column: "PortfolioId",
                principalTable: "Portfolio",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
