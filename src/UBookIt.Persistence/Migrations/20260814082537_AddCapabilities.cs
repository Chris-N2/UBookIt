using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UBookIt.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCapabilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "uBookItResourceCapability",
                columns: table => new
                {
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uBookItResourceCapability", x => new { x.ResourceId, x.Key });
                    table.ForeignKey(
                        name: "FK_uBookItResourceCapability_uBookItResource_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "uBookItResource",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "uBookItServiceRoleCapability",
                columns: table => new
                {
                    ServiceRoleId = table.Column<long>(type: "bigint", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uBookItServiceRoleCapability", x => new { x.ServiceRoleId, x.Key });
                    table.ForeignKey(
                        name: "FK_uBookItServiceRoleCapability_uBookItServiceRole_ServiceRoleId",
                        column: x => x.ServiceRoleId,
                        principalTable: "uBookItServiceRole",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_uBookItResourceCapability_Key",
                table: "uBookItResourceCapability",
                column: "Key");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "uBookItResourceCapability");

            migrationBuilder.DropTable(
                name: "uBookItServiceRoleCapability");
        }
    }
}
