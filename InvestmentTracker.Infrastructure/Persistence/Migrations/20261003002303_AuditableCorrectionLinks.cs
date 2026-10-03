using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestmentTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditableCorrectionLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CorrectionId",
                table: "FinancialMovement",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReplacesMovementId",
                table: "FinancialMovement",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialMovement_CorrectionId",
                table: "FinancialMovement",
                column: "CorrectionId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialMovement_ReplacesMovementId",
                table: "FinancialMovement",
                column: "ReplacesMovementId");

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialMovement_FinancialMovement_CorrectionId",
                table: "FinancialMovement",
                column: "CorrectionId",
                principalTable: "FinancialMovement",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialMovement_FinancialMovement_ReplacesMovementId",
                table: "FinancialMovement",
                column: "ReplacesMovementId",
                principalTable: "FinancialMovement",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM FinancialMovement WHERE CorrectionId IS NOT NULL OR ReplacesMovementId IS NOT NULL OR Kind = 'correction') THROW 51000, 'Correções auditáveis existentes impedem remover esta migration.', 1;");
            migrationBuilder.DropForeignKey(
                name: "FK_FinancialMovement_FinancialMovement_CorrectionId",
                table: "FinancialMovement");

            migrationBuilder.DropForeignKey(
                name: "FK_FinancialMovement_FinancialMovement_ReplacesMovementId",
                table: "FinancialMovement");

            migrationBuilder.DropIndex(
                name: "IX_FinancialMovement_CorrectionId",
                table: "FinancialMovement");

            migrationBuilder.DropIndex(
                name: "IX_FinancialMovement_ReplacesMovementId",
                table: "FinancialMovement");

            migrationBuilder.DropColumn(
                name: "CorrectionId",
                table: "FinancialMovement");

            migrationBuilder.DropColumn(
                name: "ReplacesMovementId",
                table: "FinancialMovement");
        }
    }
}
