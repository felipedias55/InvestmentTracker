using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestmentTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IncomeReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IncomeReceipt",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PortfolioId = table.Column<int>(type: "int", nullable: false),
                    PositionId = table.Column<int>(type: "int", nullable: false),
                    AssetId = table.Column<int>(type: "int", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Ticker = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CashAssetId = table.Column<int>(type: "int", nullable: true),
                    CashAssetName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomeReceipt", x => x.Id);
                    table.CheckConstraint("CK_IncomeReceipt_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_IncomeReceipt_Asset_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Asset",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IncomeReceipt_ExternalAsset_CashAssetId",
                        column: x => x.CashAssetId,
                        principalTable: "ExternalAsset",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IncomeReceipt_PortfolioAsset_PositionId",
                        column: x => x.PositionId,
                        principalTable: "PortfolioAsset",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IncomeReceipt_Portfolio_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "Portfolio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IncomeReceipt_AssetId",
                table: "IncomeReceipt",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeReceipt_CashAssetId",
                table: "IncomeReceipt",
                column: "CashAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeReceipt_PortfolioId_Date",
                table: "IncomeReceipt",
                columns: new[] { "PortfolioId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_IncomeReceipt_PortfolioId_RequestId",
                table: "IncomeReceipt",
                columns: new[] { "PortfolioId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncomeReceipt_PositionId",
                table: "IncomeReceipt",
                column: "PositionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IncomeReceipt");
        }
    }
}
