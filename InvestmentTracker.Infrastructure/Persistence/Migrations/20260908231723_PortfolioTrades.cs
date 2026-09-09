using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestmentTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PortfolioTrades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TradeId",
                table: "PortfolioCashFlow",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PortfolioTrade",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PortfolioId = table.Column<int>(type: "int", nullable: false),
                    AssetId = table.Column<int>(type: "int", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Ticker = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    RemainingCost = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CashAssetId = table.Column<int>(type: "int", nullable: true),
                    CashAssetName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RequestedBaseAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioTrade", x => x.Id);
                    table.CheckConstraint("CK_PortfolioTrade_Kind", "[Kind] IN ('buy', 'sell')");
                    table.CheckConstraint("CK_PortfolioTrade_Values", "[Quantity] > 0 AND [UnitPrice] > 0 AND [Amount] > 0 AND [RemainingQuantity] >= 0 AND [RemainingCost] >= 0");
                    table.ForeignKey(
                        name: "FK_PortfolioTrade_Asset_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Asset",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PortfolioTrade_ExternalAsset_CashAssetId",
                        column: x => x.CashAssetId,
                        principalTable: "ExternalAsset",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PortfolioTrade_Portfolio_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "Portfolio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioCashFlow_TradeId",
                table: "PortfolioCashFlow",
                column: "TradeId",
                unique: true,
                filter: "[TradeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioTrade_AssetId",
                table: "PortfolioTrade",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioTrade_CashAssetId",
                table: "PortfolioTrade",
                column: "CashAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioTrade_PortfolioId_Date",
                table: "PortfolioTrade",
                columns: new[] { "PortfolioId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioTrade_PortfolioId_RequestId",
                table: "PortfolioTrade",
                columns: new[] { "PortfolioId", "RequestId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PortfolioCashFlow_PortfolioTrade_TradeId",
                table: "PortfolioCashFlow",
                column: "TradeId",
                principalTable: "PortfolioTrade",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PortfolioCashFlow_PortfolioTrade_TradeId",
                table: "PortfolioCashFlow");

            migrationBuilder.DropTable(
                name: "PortfolioTrade");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioCashFlow_TradeId",
                table: "PortfolioCashFlow");

            migrationBuilder.DropColumn(
                name: "TradeId",
                table: "PortfolioCashFlow");
        }
    }
}
