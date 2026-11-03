using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillaresPos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhysicalInventoryCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "physical_inventory_counts",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_physical_inventory_counts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_physical_inventory_counts_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "app",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "physical_inventory_count_items",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhysicalInventoryCountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    PhysicalQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Difference = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_physical_inventory_count_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_physical_inventory_count_items_physical_inventory_counts_Ph~",
                        column: x => x.PhysicalInventoryCountId,
                        principalSchema: "app",
                        principalTable: "physical_inventory_counts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_physical_inventory_count_items_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "app",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_physical_inventory_count_items_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "app",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_physical_inventory_count_items_PhysicalInventoryCountId",
                schema: "app",
                table: "physical_inventory_count_items",
                column: "PhysicalInventoryCountId");

            migrationBuilder.CreateIndex(
                name: "IX_physical_inventory_count_items_ProductId",
                schema: "app",
                table: "physical_inventory_count_items",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_physical_inventory_count_items_TenantId_ProductId",
                schema: "app",
                table: "physical_inventory_count_items",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_physical_inventory_counts_TenantId_CountedAt",
                schema: "app",
                table: "physical_inventory_counts",
                columns: new[] { "TenantId", "CountedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "physical_inventory_count_items",
                schema: "app");

            migrationBuilder.DropTable(
                name: "physical_inventory_counts",
                schema: "app");
        }
    }
}
