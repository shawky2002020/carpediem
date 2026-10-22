using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Carpediem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedbackAndIncidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "engagement");

            migrationBuilder.CreateTable(
                name: "EventFeedback",
                schema: "engagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RaterUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaderUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventFeedback", x => x.Id);
                    table.CheckConstraint("CK_EventFeedback_Kind", "[Kind] IN ('Event','Leader')");
                    table.CheckConstraint("CK_EventFeedback_Rating", "[Rating] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_EventFeedback_Subject", "([Kind] = 'Event' AND [LeaderUserId] IS NULL) OR ([Kind] = 'Leader' AND [LeaderUserId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_EventFeedback_Events_EventId",
                        column: x => x.EventId,
                        principalSchema: "events",
                        principalTable: "Events",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EventFeedback_Users_LeaderUserId",
                        column: x => x.LeaderUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EventFeedback_Users_RaterUserId",
                        column: x => x.RaterUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "IncidentReports",
                schema: "engagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReporterUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReportedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentReports_Events_EventId",
                        column: x => x.EventId,
                        principalSchema: "events",
                        principalTable: "Events",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_IncidentReports_Users_ReporterUserId",
                        column: x => x.ReporterUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_IncidentReports_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventFeedback_EventId_RaterUserId_Kind_LeaderUserId",
                schema: "engagement",
                table: "EventFeedback",
                columns: new[] { "EventId", "RaterUserId", "Kind", "LeaderUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventFeedback_LeaderUserId",
                schema: "engagement",
                table: "EventFeedback",
                column: "LeaderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EventFeedback_RaterUserId",
                schema: "engagement",
                table: "EventFeedback",
                column: "RaterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentReports_EventId_ReportedAt",
                schema: "engagement",
                table: "IncidentReports",
                columns: new[] { "EventId", "ReportedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentReports_ReporterUserId",
                schema: "engagement",
                table: "IncidentReports",
                column: "ReporterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentReports_ReviewedAt",
                schema: "engagement",
                table: "IncidentReports",
                column: "ReviewedAt");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentReports_ReviewedByUserId",
                schema: "engagement",
                table: "IncidentReports",
                column: "ReviewedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventFeedback",
                schema: "engagement");

            migrationBuilder.DropTable(
                name: "IncidentReports",
                schema: "engagement");
        }
    }
}
