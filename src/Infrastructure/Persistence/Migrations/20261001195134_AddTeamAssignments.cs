using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Carpediem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeamAssignments",
                schema: "staffing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffingCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AssignedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staffing_TeamAssignments", x => x.Id);
                    table.CheckConstraint("CK_TeamAssignments_Status", "[Status] IN ('Active','Cancelled')");
                    table.ForeignKey(
                        name: "FK_TeamAssignments_AssignedByUsers",
                        column: x => x.AssignedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TeamAssignments_Events",
                        column: x => x.EventId,
                        principalSchema: "events",
                        principalTable: "Events",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TeamAssignments_StaffingCycles",
                        column: x => x.StaffingCycleId,
                        principalSchema: "events",
                        principalTable: "StaffingCycles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TeamAssignments_Users",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamAssignments_AssignedByUserId",
                schema: "staffing",
                table: "TeamAssignments",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamAssignments_Event_Status",
                schema: "staffing",
                table: "TeamAssignments",
                columns: new[] { "EventId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamAssignments_UserId",
                schema: "staffing",
                table: "TeamAssignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_TeamAssignments_ActiveLeaderPerCycle",
                schema: "staffing",
                table: "TeamAssignments",
                columns: new[] { "StaffingCycleId", "UserId" },
                unique: true,
                filter: "[Status] = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamAssignments",
                schema: "staffing");
        }
    }
}
