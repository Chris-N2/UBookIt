using Microsoft.EntityFrameworkCore;
using UBookIt.Core.Bookings;
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

    internal DbSet<ServiceRow> Services => Set<ServiceRow>();

    internal DbSet<ServiceRoleRow> ServiceRoles => Set<ServiceRoleRow>();

    internal DbSet<ResourceCapabilityRow> ResourceCapabilities => Set<ResourceCapabilityRow>();

    internal DbSet<ServiceRoleCapabilityRow> ServiceRoleCapabilities => Set<ServiceRoleCapabilityRow>();

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
            resource.HasMany(r => r.Capabilities).WithOne().HasForeignKey(c => c.ResourceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ResourceCapabilityRow>(capability =>
        {
            capability.ToTable("uBookItResourceCapability");

            // The composite key is the uniqueness rule: a resource cannot carry
            // the same capability twice at the schema level, not merely because
            // CapabilitySet deduplicates on the way in.
            capability.HasKey(c => new { c.ResourceId, c.Key });
            capability.Property(c => c.Key).HasMaxLength(64);

            // Backs the capability usage projection, which groups by key.
            capability.HasIndex(c => c.Key);
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
            // Fixed length: every reference is exactly BookingReference.Length symbols in
            // canonical form, so a wider column would only admit values the domain refuses.
            booking.Property(b => b.Reference).HasMaxLength(BookingReference.Length).IsFixedLength();
            // THE uniqueness guarantee. It is here rather than in a read-then-write because a
            // check followed by an insert is a race, and two bookings sharing a reference
            // makes both of them unquotable — the one thing a reference exists to prevent.
            booking.HasIndex(b => b.Reference).IsUnique();
            // Nullable, and their nullability means ERASED — never "a placement omitted
            // them". The domain requires a name and a well-formed email of every booker it
            // places, so no row is written with these NULL; they become NULL only when a
            // booking's personal data is erased, and BookerErasedUtc records when. The
            // lengths are unchanged: what a stored name may be is not affected by whether it
            // can later be removed.
            booking.Property(b => b.BookerName).HasMaxLength(256);
            booking.Property(b => b.BookerEmail).HasMaxLength(320);
            booking.Property(b => b.BookerPhone).HasMaxLength(64);
            // Matches the service's own name column, because it stores the same value —
            // a snapshot of it. A shorter bound here would silently truncate a name the
            // service itself accepts; an unbounded one would be the only unbounded string
            // in the schema.
            //
            // No foreign key to the service, deliberately: a booking outlives its service,
            // and ON DELETE SET NULL would turn "placed for a service that no longer
            // exists" into "placed directly".
            booking.Property(b => b.ServiceName).HasMaxLength(512);
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

        modelBuilder.Entity<ServiceRow>(service =>
        {
            service.ToTable("uBookItService");
            service.HasKey(s => s.Id);
            service.Property(s => s.Id).ValueGeneratedNever();
            service.Property(s => s.Name).HasMaxLength(512);

            // Stored as its name so the column is readable in the database and
            // does not silently shift meaning if the enum is ever reordered.
            service.Property(s => s.DurationKind).HasConversion<string>().HasMaxLength(16);
            service.HasMany(s => s.Roles).WithOne().HasForeignKey(r => r.ServiceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ServiceRoleRow>(role =>
        {
            role.ToTable("uBookItServiceRole");
            role.HasKey(r => r.Id);
            role.Property(r => r.ResourceType).HasMaxLength(64);

            // Additive and non-nullable with a false default, so existing rows
            // load as not selectable and no service changes behaviour.
            role.Property(r => r.VisitorSelectable).HasDefaultValue(false);
            role.HasIndex(r => r.ServiceId);
            role.HasMany(r => r.Capabilities).WithOne().HasForeignKey(c => c.ServiceRoleId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ServiceRoleCapabilityRow>(capability =>
        {
            capability.ToTable("uBookItServiceRoleCapability");
            capability.HasKey(c => new { c.ServiceRoleId, c.Key });
            capability.Property(c => c.Key).HasMaxLength(64);
        });
    }
}
