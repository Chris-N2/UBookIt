using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UBookIt.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookerErasure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "BookerName",
                table: "uBookItBooking",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "BookerEmail",
                table: "uBookItBooking",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(320)",
                oldMaxLength: 320);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "BookerErasedUtc",
                table: "uBookItBooking",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        /// <remarks>
        /// <b>Not a supported path, and destructive if it were run.</b> Narrowing the columns
        /// back requires a value for every row, so EF's generated <c>defaultValue: ""</c> turns
        /// every erased booking into a booker with an empty name and email — the blanked
        /// representation the booker-erasure capability forbids by name, and one the domain
        /// then refuses to rehydrate. The package applies migrations forward only, at startup,
        /// and its persistence capability requires them to be additive; this exists because the
        /// tooling generates it, not because anything calls it.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BookerErasedUtc",
                table: "uBookItBooking");

            migrationBuilder.AlterColumn<string>(
                name: "BookerName",
                table: "uBookItBooking",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BookerEmail",
                table: "uBookItBooking",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(320)",
                oldMaxLength: 320,
                oldNullable: true);
        }
    }
}
