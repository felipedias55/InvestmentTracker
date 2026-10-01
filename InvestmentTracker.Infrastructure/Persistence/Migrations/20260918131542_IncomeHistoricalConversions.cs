using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestmentTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IncomeHistoricalConversions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BaseCurrencyCode",
                table: "IncomeReceipt",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RequestedBaseAmount",
                table: "IncomeReceipt",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IncomeConversion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IncomeReceiptId = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BaseCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    BaseAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(38,18)", precision: 38, scale: 18, nullable: true),
                    RateDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomeConversion", x => x.Id);
                    table.CheckConstraint("CK_IncomeConversion_Values", "[BaseAmount] > 0 AND [Revision] > 0 AND ([Rate] IS NULL OR [Rate] > 0)");
                    table.ForeignKey(
                        name: "FK_IncomeConversion_IncomeReceipt_IncomeReceiptId",
                        column: x => x.IncomeReceiptId,
                        principalTable: "IncomeReceipt",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IncomeConversion_IncomeReceiptId_BaseCurrencyCode_Revision",
                table: "IncomeConversion",
                columns: new[] { "IncomeReceiptId", "BaseCurrencyCode", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncomeConversion_RequestId",
                table: "IncomeConversion",
                column: "RequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM IncomeConversion) OR EXISTS (SELECT 1 FROM IncomeReceipt WHERE BaseCurrencyCode IS NOT NULL) THROW 51000, 'Conversões de proventos existentes impedem remover esta migration.', 1;");
            migrationBuilder.DropTable(
                name: "IncomeConversion");

            migrationBuilder.DropColumn(
                name: "BaseCurrencyCode",
                table: "IncomeReceipt");

            migrationBuilder.DropColumn(
                name: "RequestedBaseAmount",
                table: "IncomeReceipt");
        }
    }
}
