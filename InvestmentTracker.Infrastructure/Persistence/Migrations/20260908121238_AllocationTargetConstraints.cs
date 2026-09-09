using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestmentTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllocationTargetConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CategoryAllocationTarget_Portfolio_PortfolioId",
                table: "CategoryAllocationTarget");

            migrationBuilder.DropForeignKey(
                name: "FK_SectorAllocationTarget_Portfolio_PortfolioId",
                table: "SectorAllocationTarget");

            migrationBuilder.AlterColumn<decimal>(
                name: "TargetPercentage",
                table: "SectorAllocationTarget",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,4)",
                oldPrecision: 5,
                oldScale: 4);

            migrationBuilder.AlterColumn<decimal>(
                name: "TargetPercentage",
                table: "CategoryAllocationTarget",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,4)",
                oldPrecision: 5,
                oldScale: 4);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SectorAllocationTarget_Percentage",
                table: "SectorAllocationTarget",
                sql: "[TargetPercentage] >= 0 AND [TargetPercentage] <= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CategoryAllocationTarget_Percentage",
                table: "CategoryAllocationTarget",
                sql: "[TargetPercentage] >= 0 AND [TargetPercentage] <= 1");

            migrationBuilder.AddForeignKey(
                name: "FK_CategoryAllocationTarget_Portfolio_PortfolioId",
                table: "CategoryAllocationTarget",
                column: "PortfolioId",
                principalTable: "Portfolio",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SectorAllocationTarget_Portfolio_PortfolioId",
                table: "SectorAllocationTarget",
                column: "PortfolioId",
                principalTable: "Portfolio",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM CategoryAllocationTarget WHERE TargetPercentage <> ROUND(TargetPercentage, 4))
                   OR EXISTS (SELECT 1 FROM SectorAllocationTarget WHERE TargetPercentage <> ROUND(TargetPercentage, 4))
                    THROW 51000, 'Rollback would lose allocation target precision.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_CategoryAllocationTarget_Portfolio_PortfolioId",
                table: "CategoryAllocationTarget");

            migrationBuilder.DropForeignKey(
                name: "FK_SectorAllocationTarget_Portfolio_PortfolioId",
                table: "SectorAllocationTarget");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SectorAllocationTarget_Percentage",
                table: "SectorAllocationTarget");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CategoryAllocationTarget_Percentage",
                table: "CategoryAllocationTarget");

            migrationBuilder.AlterColumn<decimal>(
                name: "TargetPercentage",
                table: "SectorAllocationTarget",
                type: "decimal(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,6)",
                oldPrecision: 9,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "TargetPercentage",
                table: "CategoryAllocationTarget",
                type: "decimal(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,6)",
                oldPrecision: 9,
                oldScale: 6);

            migrationBuilder.AddForeignKey(
                name: "FK_CategoryAllocationTarget_Portfolio_PortfolioId",
                table: "CategoryAllocationTarget",
                column: "PortfolioId",
                principalTable: "Portfolio",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SectorAllocationTarget_Portfolio_PortfolioId",
                table: "SectorAllocationTarget",
                column: "PortfolioId",
                principalTable: "Portfolio",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
