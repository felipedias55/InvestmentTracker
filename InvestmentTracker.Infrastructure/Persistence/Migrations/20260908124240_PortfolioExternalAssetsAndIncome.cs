using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestmentTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PortfolioExternalAssetsAndIncome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM ExternalAsset)
                    THROW 51000, 'Existing external assets need an explicit portfolio and currency mapping before this migration. No financial values were changed.', 1;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_PortfolioAsset_NonNegative",
                table: "PortfolioAsset");

            migrationBuilder.AddColumn<decimal>(
                name: "Income",
                table: "PortfolioAsset",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "Value",
                table: "ExternalAsset",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddColumn<int>(
                name: "CurrencyId",
                table: "ExternalAsset",
                type: "int",
                nullable: false);

            migrationBuilder.AddColumn<int>(
                name: "PortfolioId",
                table: "ExternalAsset",
                type: "int",
                nullable: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PortfolioAsset_NonNegative",
                table: "PortfolioAsset",
                sql: "[Quantity] >= 0 AND [InvestedAmount] >= 0 AND [CurrentValue] >= 0 AND [Income] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAsset_CurrencyId",
                table: "ExternalAsset",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAsset_PortfolioId",
                table: "ExternalAsset",
                column: "PortfolioId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExternalAsset_Value",
                table: "ExternalAsset",
                sql: "[Value] >= 0");

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalAsset_Currency_CurrencyId",
                table: "ExternalAsset",
                column: "CurrencyId",
                principalTable: "Currency",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalAsset_Portfolio_PortfolioId",
                table: "ExternalAsset",
                column: "PortfolioId",
                principalTable: "Portfolio",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM ExternalAsset) OR EXISTS (SELECT 1 FROM PortfolioAsset WHERE Income <> 0)
                    THROW 51000, 'Rollback would discard external asset ownership/currency or position income.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_ExternalAsset_Currency_CurrencyId",
                table: "ExternalAsset");

            migrationBuilder.DropForeignKey(
                name: "FK_ExternalAsset_Portfolio_PortfolioId",
                table: "ExternalAsset");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PortfolioAsset_NonNegative",
                table: "PortfolioAsset");

            migrationBuilder.DropIndex(
                name: "IX_ExternalAsset_CurrencyId",
                table: "ExternalAsset");

            migrationBuilder.DropIndex(
                name: "IX_ExternalAsset_PortfolioId",
                table: "ExternalAsset");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExternalAsset_Value",
                table: "ExternalAsset");

            migrationBuilder.DropColumn(
                name: "Income",
                table: "PortfolioAsset");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "ExternalAsset");

            migrationBuilder.DropColumn(
                name: "PortfolioId",
                table: "ExternalAsset");

            migrationBuilder.AlterColumn<decimal>(
                name: "Value",
                table: "ExternalAsset",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,4)",
                oldPrecision: 19,
                oldScale: 4);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PortfolioAsset_NonNegative",
                table: "PortfolioAsset",
                sql: "[Quantity] >= 0 AND [InvestedAmount] >= 0 AND [CurrentValue] >= 0");
        }
    }
}
