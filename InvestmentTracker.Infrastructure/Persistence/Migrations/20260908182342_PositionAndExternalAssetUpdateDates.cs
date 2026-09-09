using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestmentTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PositionAndExternalAssetUpdateDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "UpdatedOn",
                table: "PortfolioAsset",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "UpdatedOn",
                table: "ExternalAsset",
                type: "date",
                nullable: true);
            // Initial date explicitly agreed for existing records; do not rewrite frozen snapshots.
            migrationBuilder.Sql("UPDATE [PortfolioAsset] SET [UpdatedOn] = '20260908'; UPDATE [ExternalAsset] SET [UpdatedOn] = '20260908';");
            migrationBuilder.AlterColumn<DateOnly>(name: "UpdatedOn", table: "PortfolioAsset", type: "date",
                nullable: false, oldClrType: typeof(DateOnly), oldType: "date", oldNullable: true);
            migrationBuilder.AlterColumn<DateOnly>(name: "UpdatedOn", table: "ExternalAsset", type: "date",
                nullable: false, oldClrType: typeof(DateOnly), oldType: "date", oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdatedOn",
                table: "PortfolioAsset");

            migrationBuilder.DropColumn(
                name: "UpdatedOn",
                table: "ExternalAsset");
        }
    }
}
