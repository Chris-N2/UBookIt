using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UBookIt.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "uBookItFlag",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AppliedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uBookItFlag", x => x.Key);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "uBookItFlag");
        }
    }
}
