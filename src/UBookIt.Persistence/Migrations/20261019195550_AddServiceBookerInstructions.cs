using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UBookIt.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceBookerInstructions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BookerInstructions",
                table: "uBookItService",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BookerInstructions",
                table: "uBookItService");
        }
    }
}
