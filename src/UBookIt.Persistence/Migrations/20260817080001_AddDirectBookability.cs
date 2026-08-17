using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UBookIt.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectBookability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DirectlyBookable",
                table: "uBookItResource",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DirectlyBookable",
                table: "uBookItResource");
        }
    }
}
