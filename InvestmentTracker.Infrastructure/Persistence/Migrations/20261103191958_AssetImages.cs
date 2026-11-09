using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestmentTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssetImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageVersion",
                table: "Asset",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AssetImage",
                columns: table => new
                {
                    AssetId = table.Column<int>(type: "int", nullable: false),
                    Data = table.Column<byte[]>(type: "varbinary(max)", maxLength: 262144, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetImage", x => x.AssetId);
                    table.CheckConstraint("CK_AssetImage_Size", "DATALENGTH([Data]) > 0 AND DATALENGTH([Data]) <= 262144");
                    table.ForeignKey(
                        name: "FK_AssetImage_Asset_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Asset",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetImage");

            migrationBuilder.DropColumn(
                name: "ImageVersion",
                table: "Asset");
        }
    }
}
