using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UBookIt.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "uBookItBooking",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    MemberKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BookerName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    BookerEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    BookerPhone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uBookItBooking", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "uBookItResource",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GranularityMinutes = table.Column<int>(type: "int", nullable: false),
                    MinDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    MaxDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    LeadTimeMinutes = table.Column<int>(type: "int", nullable: false),
                    HorizonDays = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uBookItResource", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "uBookItResourceClaim",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uBookItResourceClaim", x => x.Id);
                    table.ForeignKey(
                        name: "FK_uBookItResourceClaim_uBookItBooking_BookingId",
                        column: x => x.BookingId,
                        principalTable: "uBookItBooking",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_uBookItResourceClaim_uBookItResource_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "uBookItResource",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "uBookItResourceException",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uBookItResourceException", x => x.Id);
                    table.ForeignKey(
                        name: "FK_uBookItResourceException_uBookItResource_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "uBookItResource",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "uBookItResourceOpenHours",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uBookItResourceOpenHours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_uBookItResourceOpenHours_uBookItResource_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "uBookItResource",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_uBookItBooking_StartUtc_EndUtc",
                table: "uBookItBooking",
                columns: new[] { "StartUtc", "EndUtc" })
                .Annotation("SqlServer:Include", new[] { "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_uBookItResourceClaim_BookingId_ResourceId",
                table: "uBookItResourceClaim",
                columns: new[] { "BookingId", "ResourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_uBookItResourceClaim_ResourceId",
                table: "uBookItResourceClaim",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_uBookItResourceException_ResourceId_Date",
                table: "uBookItResourceException",
                columns: new[] { "ResourceId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_uBookItResourceOpenHours_ResourceId",
                table: "uBookItResourceOpenHours",
                column: "ResourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "uBookItResourceClaim");

            migrationBuilder.DropTable(
                name: "uBookItResourceException");

            migrationBuilder.DropTable(
                name: "uBookItResourceOpenHours");

            migrationBuilder.DropTable(
                name: "uBookItBooking");

            migrationBuilder.DropTable(
                name: "uBookItResource");
        }
    }
}
