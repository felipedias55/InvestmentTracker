using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestmentTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClosingsFeesAndSnapshotVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PortfolioTrade_Values",
                table: "PortfolioTrade");

            migrationBuilder.AddColumn<decimal>(
                name: "Fees",
                table: "PortfolioTrade",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsOutdated",
                table: "PortfolioSnapshot",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsReopened",
                table: "PortfolioSnapshot",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PreviousVersionsJson",
                table: "PortfolioSnapshot",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "PortfolioSnapshot",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql("UPDATE s SET IsOutdated = 1 FROM PortfolioSnapshot s WHERE EXISTS (SELECT 1 FROM FinancialMovement m WHERE m.PortfolioId = s.PortfolioId AND m.Date <= s.SnapshotDate AND m.CreatedAtUtc > s.CapturedAtUtc);");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PortfolioTrade_Values",
                table: "PortfolioTrade",
                sql: "[Fees] >= 0 AND [Quantity] > 0 AND [UnitPrice] > 0 AND [Amount] > 0 AND [RemainingQuantity] >= 0 AND [RemainingCost] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM PortfolioTrade WHERE Fees <> 0) OR EXISTS (SELECT 1 FROM PortfolioSnapshot WHERE Revision > 1 OR IsReopened = 1) THROW 51000, 'Novas taxas ou versões auditáveis impedem remover esta migration.', 1;");
            migrationBuilder.DropCheckConstraint(
                name: "CK_PortfolioTrade_Values",
                table: "PortfolioTrade");

            migrationBuilder.DropColumn(
                name: "Fees",
                table: "PortfolioTrade");

            migrationBuilder.DropColumn(
                name: "IsOutdated",
                table: "PortfolioSnapshot");

            migrationBuilder.DropColumn(
                name: "IsReopened",
                table: "PortfolioSnapshot");

            migrationBuilder.DropColumn(
                name: "PreviousVersionsJson",
                table: "PortfolioSnapshot");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "PortfolioSnapshot");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PortfolioTrade_Values",
                table: "PortfolioTrade",
                sql: "[Quantity] > 0 AND [UnitPrice] > 0 AND [Amount] > 0 AND [RemainingQuantity] >= 0 AND [RemainingCost] >= 0");
        }
    }
}
