using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UBookIt.Persistence.Migrations
{
    /// <summary>
    /// Gives every booking a quotable reference.
    /// <para>
    /// <b>Existing bookings are backfilled, not deleted</b>, which is deliberately the opposite
    /// of the call made for the service attribution. There, the missing value was genuinely
    /// unknowable and any backfill would have been a fabrication presented as history. A
    /// reference carries no information about the booking — it is an arbitrary label whose only
    /// requirements are uniqueness and stability — so one generated here is exactly as valid as
    /// one generated at placement. And in a booking system, deleting rows to avoid generating
    /// labels for them means somebody turning up to a room that is not theirs.
    /// </para>
    /// <para>
    /// The column is therefore added <b>nullable and without a default</b>, filled, and only
    /// then constrained. Adding it NOT NULL with a default — which is what the scaffolder
    /// generates — would give every existing row the same blank value and then fail creating
    /// the unique index. A lingering default would also be a quiet trap: the second insert that
    /// forgot to supply a reference would be the one that broke.
    /// </para>
    /// </summary>
    public partial class AddBookingReference : Migration
    {
        /// <summary>
        /// Eight symbols drawn from the same alphabet the domain uses. `NEWID()` is
        /// non-deterministic and is evaluated per row and per occurrence, so the eight
        /// expressions give eight independent symbols rather than one repeated.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The obvious spelling, `ABS(CHECKSUM(NEWID())) % 28`, throws: `CHECKSUM` can return
        /// `-2147483648`, whose absolute value overflows a signed 32-bit integer.
        /// </para>
        /// <para>
        /// The obvious fix, `ABS(CHECKSUM(NEWID()) % 27)`, does not throw but is <b>biased</b>.
        /// The modulo yields -26..26; taking the absolute value folds ±n onto n, so every
        /// symbol but the first is reachable two ways and the first only one — `B` would be
        /// drawn at half the rate of the other 26. Harmless in isolation, but it would mean
        /// backfilled references and newly-placed ones came from measurably different
        /// distributions, where the design describes exactly one alphabet.
        /// </para>
        /// <para>
        /// Masking the sign bit avoids both. `&amp; 0x7FFFFFFF` yields 0..2147483647 with no
        /// overflow, and the residual modulo bias over 2^31 values is on the order of one part
        /// in 79 million — uniform to any standard this needs to meet.
        /// </para>
        /// </remarks>
        private const string ReferenceExpression = """
            SUBSTRING(@alphabet, (CHECKSUM(NEWID()) & 0x7FFFFFFF) % 27 + 1, 1) +
            SUBSTRING(@alphabet, (CHECKSUM(NEWID()) & 0x7FFFFFFF) % 27 + 1, 1) +
            SUBSTRING(@alphabet, (CHECKSUM(NEWID()) & 0x7FFFFFFF) % 27 + 1, 1) +
            SUBSTRING(@alphabet, (CHECKSUM(NEWID()) & 0x7FFFFFFF) % 27 + 1, 1) +
            SUBSTRING(@alphabet, (CHECKSUM(NEWID()) & 0x7FFFFFFF) % 27 + 1, 1) +
            SUBSTRING(@alphabet, (CHECKSUM(NEWID()) & 0x7FFFFFFF) % 27 + 1, 1) +
            SUBSTRING(@alphabet, (CHECKSUM(NEWID()) & 0x7FFFFFFF) % 27 + 1, 1) +
            SUBSTRING(@alphabet, (CHECKSUM(NEWID()) & 0x7FFFFFFF) % 27 + 1, 1)
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Reference",
                table: "uBookItBooking",
                type: "nchar(8)",
                fixedLength: true,
                maxLength: 8,
                nullable: true);

            // Fill, then resolve any collision, then constrain. The loop is not optimism about
            // randomness: a duplicate here would fail the index creation below and take the
            // whole migration — and therefore the site's boot — down with it. Collisions are
            // vanishingly unlikely at 28^8, which is exactly why the fix must be automatic
            // rather than something an operator is expected to diagnose at three in the
            // morning.
            migrationBuilder.Sql($"""
                DECLARE @alphabet nchar(27) = N'BCDFGHJKMNPQRSTVWXZ23456789';

                UPDATE uBookItBooking
                SET Reference = {ReferenceExpression}
                WHERE Reference IS NULL;

                WHILE EXISTS (
                    SELECT 1 FROM uBookItBooking
                    GROUP BY Reference HAVING COUNT(*) > 1)
                BEGIN
                    WITH duplicates AS (
                        SELECT Id,
                               ROW_NUMBER() OVER (PARTITION BY Reference ORDER BY Id) AS rn
                        FROM uBookItBooking)
                    UPDATE b
                    SET Reference = {ReferenceExpression}
                    FROM uBookItBooking b
                    JOIN duplicates d ON d.Id = b.Id
                    WHERE d.rn > 1;
                END
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Reference",
                table: "uBookItBooking",
                type: "nchar(8)",
                fixedLength: true,
                maxLength: 8,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nchar(8)",
                oldFixedLength: true,
                oldMaxLength: 8,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_uBookItBooking_Reference",
                table: "uBookItBooking",
                column: "Reference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_uBookItBooking_Reference",
                table: "uBookItBooking");

            migrationBuilder.DropColumn(
                name: "Reference",
                table: "uBookItBooking");
        }
    }
}
