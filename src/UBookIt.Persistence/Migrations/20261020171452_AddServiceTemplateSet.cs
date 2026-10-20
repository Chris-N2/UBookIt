using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UBookIt.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceTemplateSet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TemplateSet",
                table: "uBookItService",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TemplateSet",
                table: "uBookItService");
        }
    }
}
