using Microsoft.EntityFrameworkCore;
using UBookIt.Persistence.Entities;

namespace UBookIt.Persistence;

/// <summary>
/// uBookIt's EF Core context. All tables carry the uBookIt prefix and
/// migrations record into a package-private history table so uBookIt never
/// collides with Umbraco's schema or other packages' EF Core usage.
/// </summary>
public sealed class UBookItDbContext(DbContextOptions<UBookItDbContext> options) : DbContext(options)
{
    public const string MigrationsHistoryTableName = "__uBookItEFMigrationsHistory";

    internal DbSet<ResourceRow> Resources => Set<ResourceRow>();

    internal DbSet<OpenHoursRow> OpenHours => Set<OpenHoursRow>();

    internal DbSet<ExceptionRow> Exceptions => Set<ExceptionRow>();

    internal DbSet<BookingRow> Bookings => Set<BookingRow>();

    internal DbSet<ClaimRow> Claims => Set<ClaimRow>();

    /// <summary>
    /// Single place that configures the SQL Server provider (uBookIt requires
    /// SQL Server 2019+) with the package-private migrations history table.
    /// Used by runtime registration, the design-time factory, and tests.
    /// </summary>
    public static void ConfigureSqlServer(DbContextOptionsBuilder builder, string connectionString)
        => builder.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable(MigrationsHistoryTableName));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ResourceRow>(resource =>
        {
            resource.ToTable("uBookItResource");
            resource.HasKey(r => r.Id);
            resource.Property(r => r.Id).ValueGeneratedNever();
            resource.Property(r => r.Type).HasMaxLength(64);
            resource.Property(r => r.DisplayName).HasMaxLength(512);
            resource.HasMany(r => r.OpenHours).WithOne().HasForeignKey(w => w.ResourceId).OnDelete(DeleteBehavior.Cascade);
            resource.HasMany(r => r.Exceptions).WithOne().HasForeignKey(e => e.ResourceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OpenHoursRow>(window =>
        {
            window.ToTable("uBookItResourceOpenHours");
            window.HasKey(w => w.Id);
            window.HasIndex(w => w.ResourceId);
        });

        modelBuilder.Entity<ExceptionRow>(exception =>
        {
            exception.ToTable("uBookItResourceException");
            exception.HasKey(e => e.Id);
            exception.HasIndex(e => new { e.ResourceId, e.Date });
        });

        modelBuilder.Entity<BookingRow>(booking =>
        {
            booking.ToTable("uBookItBooking");
            booking.HasKey(b => b.Id);
            booking.Property(b => b.Id).ValueGeneratedNever();
            booking.Property(b => b.TimeZoneId).HasMaxLength(64);
            booking.Property(b => b.BookerName).HasMaxLength(256);
            booking.Property(b => b.BookerEmail).HasMaxLength(320);
            booking.Property(b => b.BookerPhone).HasMaxLength(64);
            // The availability date-range lookup must hit this index (QA gate).
            booking.HasIndex(b => new { b.StartUtc, b.EndUtc }).IncludeProperties(b => b.Status);
            booking.HasMany(b => b.Claims).WithOne().HasForeignKey(c => c.BookingId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ClaimRow>(claim =>
        {
            claim.ToTable("uBookItResourceClaim");
            claim.HasKey(c => c.Id);
            claim.HasIndex(c => new { c.BookingId, c.ResourceId }).IsUnique();
            claim.HasIndex(c => c.ResourceId).IncludeProperties(c => c.BookingId);
            claim.HasOne<ResourceRow>().WithMany().HasForeignKey(c => c.ResourceId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
