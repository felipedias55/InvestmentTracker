using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestmentTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReversalCashFlowClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsReversal",
                table: "PortfolioCashFlow",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM PortfolioCashFlow WHERE IsReversal = 1) THROW 51000, 'Rollback would lose reversal classification.', 1;");
            migrationBuilder.DropColumn(
                name: "IsReversal",
                table: "PortfolioCashFlow");
        }
    }
}
