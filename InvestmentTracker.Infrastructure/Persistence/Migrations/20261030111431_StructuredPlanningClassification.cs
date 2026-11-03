using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestmentTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StructuredPlanningClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRealEstate",
                table: "Sector",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "InstrumentClass",
                table: "AssetType",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "unclassified");

            // One-time mapping of canonical catalogue labels, never a runtime name heuristic.
            // Unknown/custom types remain explicitly unclassified for the user to review.
            migrationBuilder.Sql("""
                UPDATE [AssetType] SET [InstrumentClass] = CASE
                    WHEN [Name] IN (N'FII', N'FIIs') THEN 'fii'
                    WHEN [Name] IN (N'REIT', N'REITs') THEN 'reit'
                    WHEN [Name] IN (N'Ação', N'Ações', N'Stock', N'Stocks') THEN 'general'
                    ELSE 'unclassified' END;
                UPDATE [Sector] SET [IsRealEstate] = 1 WHERE [Name] IN (N'Imobiliário', N'Real Estate');
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AssetType_InstrumentClass",
                table: "AssetType",
                sql: "[InstrumentClass] IN ('general', 'fii', 'reit', 'unclassified')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AssetType_InstrumentClass",
                table: "AssetType");

            migrationBuilder.DropColumn(
                name: "IsRealEstate",
                table: "Sector");

            migrationBuilder.DropColumn(
                name: "InstrumentClass",
                table: "AssetType");
        }
    }
}
