using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UBookIt.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteClosures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "uBookItSiteClosure",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uBookItSiteClosure", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "uBookItResourceClosureOptOut",
                columns: table => new
                {
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClosureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uBookItResourceClosureOptOut", x => new { x.ResourceId, x.ClosureId });
                    table.ForeignKey(
                        name: "FK_uBookItResourceClosureOptOut_uBookItResource_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "uBookItResource",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_uBookItResourceClosureOptOut_uBookItSiteClosure_ClosureId",
                        column: x => x.ClosureId,
                        principalTable: "uBookItSiteClosure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_uBookItResourceClosureOptOut_ClosureId",
                table: "uBookItResourceClosureOptOut",
                column: "ClosureId");

            migrationBuilder.CreateIndex(
                name: "IX_uBookItResourceClosureOptOut_ResourceId",
                table: "uBookItResourceClosureOptOut",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_uBookItSiteClosure_Date",
                table: "uBookItSiteClosure",
                column: "Date",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "uBookItResourceClosureOptOut");

            migrationBuilder.DropTable(
                name: "uBookItSiteClosure");
        }
    }
}
