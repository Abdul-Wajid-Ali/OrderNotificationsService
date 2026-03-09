using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderNotificationsService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NotificationandOutboxUpdated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeadLetteredAt",
                table: "OutboxEvents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "OutboxEvents",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextRetryAt",
                table: "OutboxEvents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "OutboxEvents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAt",
                table: "Notifications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeliveryAttemptCount",
                table: "Notifications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DeliveryStatus",
                table: "Notifications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastDeliveryError",
                table: "Notifications",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceEventId",
                table: "Notifications",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_OutboxEvents_DeadLetteredAt",
                table: "OutboxEvents",
                column: "DeadLetteredAt");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxEvents_ProcessedAt_DeadLetteredAt_NextRetryAt",
                table: "OutboxEvents",
                columns: new[] { "ProcessedAt", "DeadLetteredAt", "NextRetryAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_SourceEventId_Type",
                table: "Notifications",
                columns: new[] { "SourceEventId", "Type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxEvents_DeadLetteredAt",
                table: "OutboxEvents");

            migrationBuilder.DropIndex(
                name: "IX_OutboxEvents_ProcessedAt_DeadLetteredAt_NextRetryAt",
                table: "OutboxEvents");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_SourceEventId_Type",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "DeadLetteredAt",
                table: "OutboxEvents");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "OutboxEvents");

            migrationBuilder.DropColumn(
                name: "NextRetryAt",
                table: "OutboxEvents");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "OutboxEvents");

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "DeliveryAttemptCount",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "DeliveryStatus",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "LastDeliveryError",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "SourceEventId",
                table: "Notifications");
        }
    }
}
