using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestmentTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlanningGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlanningGoalRevision",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PortfolioId = table.Column<int>(type: "int", nullable: false),
                    GoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Target = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: false),
                    Deadline = table.Column<DateOnly>(type: "date", nullable: false),
                    MonthlyContribution = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: false),
                    Archived = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningGoalRevision", x => x.Id);
                    table.CheckConstraint("CK_PlanningGoalRevision_Kind", "[Kind] IN ('wealth', 'monthly-income')");
                    table.CheckConstraint("CK_PlanningGoalRevision_Values", "[Revision] > 0 AND [Target] > 0 AND [MonthlyContribution] >= 0 AND ([Kind] = 'wealth' OR [MonthlyContribution] = 0)");
                    table.ForeignKey(
                        name: "FK_PlanningGoalRevision_Portfolio_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "Portfolio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanningGoalRevision_PortfolioId_GoalId_Revision",
                table: "PlanningGoalRevision",
                columns: new[] { "PortfolioId", "GoalId", "Revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanningGoalRevision");
        }
    }
}
