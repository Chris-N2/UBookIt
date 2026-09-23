using Microsoft.EntityFrameworkCore;
using UBookIt.Core.Availability;
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

    internal DbSet<ResponsibilityRow> Responsibilities => Set<ResponsibilityRow>();

    internal DbSet<FlagRow> Flags => Set<FlagRow>();

    internal DbSet<SettingRow> Settings => Set<SettingRow>();

    internal DbSet<CancellationSecretRow> CancellationSecrets => Set<CancellationSecretRow>();

    internal DbSet<SiteClosureRow> SiteClosures => Set<SiteClosureRow>();

    internal DbSet<ResourceClosureOptOutRow> ResourceClosureOptOuts => Set<ResourceClosureOptOutRow>();

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
            resource.HasMany(r => r.ClosureOptOuts).WithOne()
                .HasForeignKey(o => o.ResourceId).OnDelete(DeleteBehavior.Cascade);
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

            // Serves the by-address search a data-subject request needs. That read is
            // UNWINDOWED by necessity — a request carries an address and no dates — so without
            // this it is a scan of a table that grows without limit, reintroducing the very
            // cost the management list's window guard exists to bound. The index is what makes
            // the search affordable rather than merely permitted.
            //
            // NOT unique: one person books many times, and uniqueness here would refuse their
            // second booking. It is not a natural key either — erasure sets the column NULL, so
            // the value is not stable for the life of the row.
            //
            // An index on personal data, added to build the tool that REMOVES personal data,
            // which reads oddly enough to be worth stating: it is sound because an UPDATE
            // maintains the index with the row, so an erased booking leaves the index at the
            // moment its address does, and the index holds nothing the table does not.
            booking.HasIndex(b => b.BookerEmail);
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

            // Serves the retention sweep, which asks for bookings that ENDED before a cutoff and
            // have not been erased. The index above leads on StartUtc, so it cannot seek on the
            // end — a sweep relying on it would scan a table that grows without limit.
            //
            // FILTERED, and that is what keeps it cheap in the steady state: it holds only
            // un-erased bookings, so on a site running retention it settles at roughly one
            // retention period's worth and stops growing. An unfiltered index would keep an entry
            // for every booking the site has ever taken, including all the erased ones the sweep
            // must never select again — the rows it would be largest for are exactly the rows it
            // exists to exclude.
            //
            // The filter's predicate mirrors the store query's, in the same order, so that a
            // change to either is visibly a change to both.
            booking.HasIndex(b => b.EndUtc)
                .HasFilter("[BookerErasedUtc] IS NULL")
                .HasDatabaseName("IX_uBookItBooking_EndUtc_Unerased");
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

        modelBuilder.Entity<ResponsibilityRow>(responsibility =>
        {
            responsibility.ToTable("uBookItResponsibility");

            // The compound key IS the semantics: an assignment exists or it
            // does not, so a duplicate is impossible in storage and an
            // idempotent write needs no application-side uniqueness check.
            responsibility.HasKey(r => new { r.SubjectType, r.SubjectId, r.PartyType, r.PartyKey });
            responsibility.Property(r => r.SubjectType).HasMaxLength(16);
            responsibility.Property(r => r.PartyType).HasMaxLength(16);

            // Lookup by subject is the only query shape the package runs:
            // the editors read one subject, the resolver reads a handful.
            responsibility.HasIndex(r => new { r.SubjectType, r.SubjectId });

            // No foreign keys, deliberately. The party side CANNOT have one —
            // users and groups live in Umbraco's own tables — and giving only
            // the subject side one would make the two halves behave
            // differently for no query we run; the stores delete assignment
            // rows with their owner instead.
        });

        modelBuilder.Entity<FlagRow>(flag =>
        {
            flag.ToTable("uBookItFlag");
            flag.HasKey(f => f.Key);
            flag.Property(f => f.Key).HasMaxLength(128);
        });

        modelBuilder.Entity<SettingRow>(setting =>
        {
            setting.ToTable("uBookItSetting");

            // The configuration key IS the primary key, which is what enforces "at most one row
            // per key" in the schema rather than in the store's code.
            setting.HasKey(s => s.Key);
            setting.Property(s => s.Key).HasMaxLength(256);
            setting.Property(s => s.Value).HasMaxLength(2048);
        });

        modelBuilder.Entity<CancellationSecretRow>(secret =>
        {
            secret.ToTable("uBookItCancellationSecret");

            // The HASH is the primary key. One row per secret is then a property of the schema
            // rather than of the store's code, and the lookup a redemption performs is a seek on
            // the key — which matters, because that lookup happens on an anonymous request.
            secret.HasKey(s => s.Hash);
            secret.Property(s => s.Hash).HasMaxLength(64).IsFixedLength();

            // Finding the outstanding secrets for a booking, which is what issuing a replacement
            // and tidying after a cancellation both need.
            secret.HasIndex(s => s.BookingId);
        });

        modelBuilder.Entity<SiteClosureRow>(closure =>
        {
            closure.ToTable("uBookItSiteClosure");
            closure.HasKey(c => c.Id);
            closure.Property(c => c.Id).ValueGeneratedNever();

            // Held on the domain type so the column and the validation cannot drift apart —
            // the arrangement BookingReference.Length already has with the reference column.
            closure.Property(c => c.Label).HasMaxLength(SiteClosure.MaxLabelLength);

            // UNIQUE, so "at most one closure per date" is enforced by the schema. A check
            // before writing is a race, and two closures on one date can only repeat or
            // contradict each other.
            closure.HasIndex(c => c.Date).IsUnique();

            // Deleting a closure takes its exemptions with it: an exemption cannot outlive the
            // thing it exempts from.
            closure.HasMany(c => c.OptOuts).WithOne()
                .HasForeignKey(o => o.ClosureId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ResourceClosureOptOutRow>(optOut =>
        {
            optOut.ToTable("uBookItResourceClosureOptOut");

            // The pair IS the primary key, so a duplicate exemption is impossible in storage
            // and not only in the domain — the same shape the capability tables use.
            optOut.HasKey(o => new { o.ResourceId, o.ClosureId });

            // Hydration reads every opt-out for a batch of resources in one query; this is the
            // index that read seeks on.
            optOut.HasIndex(o => o.ResourceId);
        });
    }
}
