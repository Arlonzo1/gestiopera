using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillaresPos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Billiards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "billiard_tables",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    HourlyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BillingIncrementMinutes = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billiard_tables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_billiard_tables_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "app",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "billiard_sessions",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BilliardTableId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TotalPausedSeconds = table.Column<long>(type: "bigint", nullable: false),
                    PausedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    HourlyRateSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BillingIncrementMinutesSnapshot = table.Column<int>(type: "integer", nullable: false),
                    OpenedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billiard_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_billiard_sessions_billiard_tables_BilliardTableId",
                        column: x => x.BilliardTableId,
                        principalSchema: "app",
                        principalTable: "billiard_tables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_billiard_sessions_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "app",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "billiard_consumptions",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BilliardSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    DescriptionSnapshot = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitCostAtConsumption = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AddedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billiard_consumptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_billiard_consumptions_billiard_sessions_BilliardSessionId",
                        column: x => x.BilliardSessionId,
                        principalSchema: "app",
                        principalTable: "billiard_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_billiard_consumptions_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "app",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_billiard_consumptions_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "app",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_billiard_consumptions_BilliardSessionId",
                schema: "app",
                table: "billiard_consumptions",
                column: "BilliardSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_billiard_consumptions_ProductId",
                schema: "app",
                table: "billiard_consumptions",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_billiard_consumptions_TenantId_BilliardSessionId",
                schema: "app",
                table: "billiard_consumptions",
                columns: new[] { "TenantId", "BilliardSessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_billiard_sessions_BilliardTableId",
                schema: "app",
                table: "billiard_sessions",
                column: "BilliardTableId");

            migrationBuilder.CreateIndex(
                name: "IX_billiard_sessions_TenantId_BilliardTableId_Status",
                schema: "app",
                table: "billiard_sessions",
                columns: new[] { "TenantId", "BilliardTableId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_billiard_tables_TenantId_Name",
                schema: "app",
                table: "billiard_tables",
                columns: new[] { "TenantId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "billiard_consumptions",
                schema: "app");

            migrationBuilder.DropTable(
                name: "billiard_sessions",
                schema: "app");

            migrationBuilder.DropTable(
                name: "billiard_tables",
                schema: "app");
        }
    }
}
