using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillaresPos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WhatsAppPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "WhatsAppEnabled",
                schema: "app",
                table: "NotificationPreferences",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppPhoneNumber",
                schema: "app",
                table: "NotificationPreferences",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WhatsAppEnabled",
                schema: "app",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "WhatsAppPhoneNumber",
                schema: "app",
                table: "NotificationPreferences");
        }
    }
}
