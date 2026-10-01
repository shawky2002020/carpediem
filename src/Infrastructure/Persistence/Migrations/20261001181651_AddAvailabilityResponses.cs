using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Carpediem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAvailabilityResponses : Migration
    {
        private static readonly string[] EventResponseColumns = ["EventId", "Response"];
        private static readonly string[] StaffingCycleUserColumns = ["StaffingCycleId", "UserId"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "staffing");

            migrationBuilder.CreateTable(
                name: "AvailabilityResponses",
                schema: "staffing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffingCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Response = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DeclaredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staffing_AvailabilityResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AvailabilityResponses_Events",
                        column: x => x.EventId,
                        principalSchema: "events",
                        principalTable: "Events",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AvailabilityResponses_StaffingCycles",
                        column: x => x.StaffingCycleId,
                        principalSchema: "events",
                        principalTable: "StaffingCycles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AvailabilityResponses_Users",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilityResponses_Event_Response",
                schema: "staffing",
                table: "AvailabilityResponses",
                columns: EventResponseColumns);

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilityResponses_UserId",
                schema: "staffing",
                table: "AvailabilityResponses",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_AvailabilityResponses_StaffingCycle_User",
                schema: "staffing",
                table: "AvailabilityResponses",
                columns: StaffingCycleUserColumns,
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AvailabilityResponses",
                schema: "staffing");
        }
    }
}
