using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UBookIt.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClaimResourceIndexIncludesBookingId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_uBookItResourceClaim_ResourceId",
                table: "uBookItResourceClaim");

            migrationBuilder.CreateIndex(
                name: "IX_uBookItResourceClaim_ResourceId",
                table: "uBookItResourceClaim",
                column: "ResourceId")
                .Annotation("SqlServer:Include", new[] { "BookingId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_uBookItResourceClaim_ResourceId",
                table: "uBookItResourceClaim");

            migrationBuilder.CreateIndex(
                name: "IX_uBookItResourceClaim_ResourceId",
                table: "uBookItResourceClaim",
                column: "ResourceId");
        }
    }
}
