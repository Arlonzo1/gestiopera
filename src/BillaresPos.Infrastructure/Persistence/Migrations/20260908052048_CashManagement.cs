using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillaresPos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CashManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cash_shifts",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OpeningCash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpectedCash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CountedCash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Difference = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_shifts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cash_shifts_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "app",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "expenses",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CashShiftId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_expenses_cash_shifts_CashShiftId",
                        column: x => x.CashShiftId,
                        principalSchema: "app",
                        principalTable: "cash_shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_expenses_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "app",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cash_shifts_TenantId_OpenedAt",
                schema: "app",
                table: "cash_shifts",
                columns: new[] { "TenantId", "OpenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_cash_shifts_TenantId_UserId_Status",
                schema: "app",
                table: "cash_shifts",
                columns: new[] { "TenantId", "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_expenses_CashShiftId",
                schema: "app",
                table: "expenses",
                column: "CashShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_expenses_TenantId_CashShiftId",
                schema: "app",
                table: "expenses",
                columns: new[] { "TenantId", "CashShiftId" });

            migrationBuilder.CreateIndex(
                name: "IX_expenses_TenantId_CreatedAt",
                schema: "app",
                table: "expenses",
                columns: new[] { "TenantId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "expenses",
                schema: "app");

            migrationBuilder.DropTable(
                name: "cash_shifts",
                schema: "app");
        }
    }
}
