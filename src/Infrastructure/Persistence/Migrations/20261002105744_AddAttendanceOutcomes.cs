using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Carpediem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceOutcomes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "attendance");

            migrationBuilder.CreateTable(
                name: "AttendanceOutcomes",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FinalizedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinalizedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CorrectedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorrectedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_AttendanceOutcomes", x => x.Id);
                    table.CheckConstraint("CK_AttendanceOutcomes_Outcome", "[Outcome] IN ('Attended','NoShow')");
                    table.ForeignKey(
                        name: "FK_AttendanceOutcomes_CorrectedByUsers",
                        column: x => x.CorrectedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AttendanceOutcomes_FinalizedByUsers",
                        column: x => x.FinalizedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AttendanceOutcomes_TeamAssignments",
                        column: x => x.TeamAssignmentId,
                        principalSchema: "staffing",
                        principalTable: "TeamAssignments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceOutcomes_CorrectedByUserId",
                schema: "attendance",
                table: "AttendanceOutcomes",
                column: "CorrectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceOutcomes_FinalizedByUserId",
                schema: "attendance",
                table: "AttendanceOutcomes",
                column: "FinalizedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_AttendanceOutcomes_Assignment",
                schema: "attendance",
                table: "AttendanceOutcomes",
                column: "TeamAssignmentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceOutcomes",
                schema: "attendance");
        }
    }
}
