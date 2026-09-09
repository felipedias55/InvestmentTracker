using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestmentTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PortfolioHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PortfolioCashFlow",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PortfolioId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    CurrencyId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    BaseCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    BaseAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioCashFlow", x => x.Id);
                    table.CheckConstraint("CK_PortfolioCashFlow_Kind", "[Kind] IN ('contribution', 'withdrawal')");
                    table.CheckConstraint("CK_PortfolioCashFlow_Values", "[Amount] > 0 AND ([BaseAmount] IS NULL OR [BaseAmount] > 0)");
                    table.ForeignKey(
                        name: "FK_PortfolioCashFlow_Currency_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PortfolioCashFlow_Portfolio_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "Portfolio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioSnapshot",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PortfolioId = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    SnapshotDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BaseCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PortfolioValue = table.Column<decimal>(type: "decimal(38,4)", precision: 38, scale: 4, nullable: false),
                    ExternalValue = table.Column<decimal>(type: "decimal(38,4)", precision: 38, scale: 4, nullable: false),
                    TotalWealth = table.Column<decimal>(type: "decimal(38,4)", precision: 38, scale: 4, nullable: false),
                    TotalIncome = table.Column<decimal>(type: "decimal(38,4)", precision: 38, scale: 4, nullable: false),
                    HasStaleRates = table.Column<bool>(type: "bit", nullable: false),
                    HasFallbackRates = table.Column<bool>(type: "bit", nullable: false),
                    PayloadVersion = table.Column<int>(type: "int", nullable: false),
                    DashboardJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioSnapshot", x => x.Id);
                    table.CheckConstraint("CK_PortfolioSnapshot_Json", "ISJSON([DashboardJson]) = 1");
                    table.CheckConstraint("CK_PortfolioSnapshot_Month", "DAY([Month]) = 1 AND YEAR([Month]) = YEAR([SnapshotDate]) AND MONTH([Month]) = MONTH([SnapshotDate])");
                    table.CheckConstraint("CK_PortfolioSnapshot_Values", "[PortfolioValue] >= 0 AND [ExternalValue] >= 0 AND [TotalWealth] >= 0 AND [TotalIncome] >= 0");
                    table.ForeignKey(
                        name: "FK_PortfolioSnapshot_Portfolio_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "Portfolio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioCashFlow_CurrencyId",
                table: "PortfolioCashFlow",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioCashFlow_PortfolioId_Date",
                table: "PortfolioCashFlow",
                columns: new[] { "PortfolioId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioSnapshot_PortfolioId_Month",
                table: "PortfolioSnapshot",
                columns: new[] { "PortfolioId", "Month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM PortfolioSnapshot) OR EXISTS (SELECT 1 FROM PortfolioCashFlow)
                    THROW 51000, 'Rollback would discard saved photographs or cash flow history.', 1;
                """);
            migrationBuilder.DropTable(
                name: "PortfolioCashFlow");

            migrationBuilder.DropTable(
                name: "PortfolioSnapshot");
        }
    }
}
