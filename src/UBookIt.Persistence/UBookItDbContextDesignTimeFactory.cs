using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UBookIt.Persistence;

/// <summary>
/// Design-time factory for `dotnet ef` migration authoring only. The
/// connection string is never opened during `migrations add`.
/// </summary>
internal sealed class UBookItDbContextDesignTimeFactory : IDesignTimeDbContextFactory<UBookItDbContext>
{
    public UBookItDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<UBookItDbContext>();
        UBookItDbContext.ConfigureSqlServer(
            builder,
            "Server=(localdb)\\MSSQLLocalDB;Database=uBookItDesign;Integrated Security=true;TrustServerCertificate=true");
        return new UBookItDbContext(builder.Options);
    }
}
