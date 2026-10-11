using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UBookIt.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClosureDateRanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "EndDate",
                table: "uBookItSiteClosure",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            // Every closure before this version was one date, and must keep covering exactly
            // that date: an end left at the column default would make each one end before it
            // starts. Filled here, in the same migration, so no row is ever observed without it.
            migrationBuilder.Sql("UPDATE [uBookItSiteClosure] SET [EndDate] = [Date];");

            migrationBuilder.CreateTable(
                name: "uBookItResourceClosedPeriod",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uBookItResourceClosedPeriod", x => x.Id);
                    table.ForeignKey(
                        name: "FK_uBookItResourceClosedPeriod_uBookItResource_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "uBookItResource",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_uBookItResourceClosedPeriod_ResourceId",
                table: "uBookItResourceClosedPeriod",
                column: "ResourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "uBookItResourceClosedPeriod");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "uBookItSiteClosure");
        }
    }
}
