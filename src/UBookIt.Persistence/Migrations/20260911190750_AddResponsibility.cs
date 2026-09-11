using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UBookIt.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResponsibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "uBookItResponsibility",
                columns: table => new
                {
                    SubjectType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartyType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PartyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uBookItResponsibility", x => new { x.SubjectType, x.SubjectId, x.PartyType, x.PartyKey });
                });

            migrationBuilder.CreateIndex(
                name: "IX_uBookItResponsibility_SubjectType_SubjectId",
                table: "uBookItResponsibility",
                columns: new[] { "SubjectType", "SubjectId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "uBookItResponsibility");
        }
    }
}
