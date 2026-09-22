using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Carpediem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChiefLeaderAppointment : Migration
    {
        private static readonly string[] RecipientCreatedAtColumns = ["RecipientUserId", "CreatedAt"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "notifications");

            migrationBuilder.CreateTable(
                name: "ChiefLeaderAssignments",
                schema: "events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChiefLeaderUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", nullable: false),
                    AppointedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AcceptanceSource = table.Column<string>(type: "varchar(20)", nullable: true),
                    AcceptedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeclinedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_events_ChiefLeaderAssignments", x => x.Id);
                    table.CheckConstraint("CK_ChiefLeaderAssignments_Source", "[AcceptanceSource] IS NULL OR [AcceptanceSource] IN ('Direct','AdminExternal')");
                    table.CheckConstraint("CK_ChiefLeaderAssignments_Status", "[Status] IN ('Pending','Active','Declined')");
                    table.ForeignKey(
                        name: "FK_ChiefLeaderAssignments_AcceptedBy",
                        column: x => x.AcceptedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChiefLeaderAssignments_AppointedBy",
                        column: x => x.AppointedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChiefLeaderAssignments_Events",
                        column: x => x.EventId,
                        principalSchema: "events",
                        principalTable: "Events",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChiefLeaderAssignments_Users",
                        column: x => x.ChiefLeaderUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InPlatformNotifications",
                schema: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NotificationType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ContextPayload = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications_InPlatformNotifications", x => x.Id);
                    table.CheckConstraint("CK_Notifications_ReadAt", "[ReadAt] IS NULL OR [ReadAt] >= [CreatedAt]");
                    table.ForeignKey(
                        name: "FK_Notifications_Events",
                        column: x => x.EventId,
                        principalSchema: "events",
                        principalTable: "Events",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Notifications_Users",
                        column: x => x.RecipientUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ChiefLeaderAppointmentRequests",
                schema: "events",
                columns: table => new
                {
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayloadFingerprint = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_events_ChiefLeaderAppointmentRequests", x => new { x.ActorUserId, x.RequestToken });
                    table.ForeignKey(
                        name: "FK_ChiefLeaderAppointmentRequests_Assignments",
                        column: x => x.AssignmentId,
                        principalSchema: "events",
                        principalTable: "ChiefLeaderAssignments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChiefLeaderAppointmentRequests_Users",
                        column: x => x.ActorUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChiefLeaderAppointmentRequests_AssignmentId",
                schema: "events",
                table: "ChiefLeaderAppointmentRequests",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ChiefLeaderAssignments_AcceptedByUserId",
                schema: "events",
                table: "ChiefLeaderAssignments",
                column: "AcceptedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChiefLeaderAssignments_AppointedByUserId",
                schema: "events",
                table: "ChiefLeaderAssignments",
                column: "AppointedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChiefLeaderAssignments_ChiefLeaderUserId",
                schema: "events",
                table: "ChiefLeaderAssignments",
                column: "ChiefLeaderUserId");

            migrationBuilder.CreateIndex(
                name: "UX_ChiefLeaderAssignments_ActivePerEvent",
                schema: "events",
                table: "ChiefLeaderAssignments",
                column: "EventId",
                unique: true,
                filter: "[Status] = 'Active'");

            migrationBuilder.Sql(
                "CREATE UNIQUE NONCLUSTERED INDEX [UX_ChiefLeaderAssignments_PendingPerEvent] " +
                "ON [events].[ChiefLeaderAssignments] ([EventId]) WHERE [Status] = 'Pending';");

            migrationBuilder.CreateIndex(
                name: "IX_InPlatformNotifications_EventId",
                schema: "notifications",
                table: "InPlatformNotifications",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Recipient_CreatedAt",
                schema: "notifications",
                table: "InPlatformNotifications",
                columns: RecipientCreatedAtColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChiefLeaderAppointmentRequests",
                schema: "events");

            migrationBuilder.DropTable(
                name: "InPlatformNotifications",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "ChiefLeaderAssignments",
                schema: "events");
        }
    }
}
