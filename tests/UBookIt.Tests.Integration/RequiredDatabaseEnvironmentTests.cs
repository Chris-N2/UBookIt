using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

/// <summary>
/// The required mode through the fixture's PUBLIC entry point — the one xunit calls — so that the
/// environment variables themselves are what is being tested.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists alongside <see cref="RequiredDatabaseTests"/>.</b> That class drives the
/// internal overload with explicit inputs, which proves what the fixture does with the values and
/// nothing about whether it reads them. QA replaced the public overload's read of
/// <c>UBOOKIT_TEST_DB_REQUIRED</c> with <c>null</c> and every test stayed green — and in CI, with a
/// reachable server, so would every other test. The guarantee is keyed on the environment
/// variable, so the variable has to be in the test.
/// </para>
/// <para>
/// <b>It writes process-wide state, so it runs alone.</b> The collection disables parallelisation,
/// which in xunit v3 runs it by itself after the parallel collections have finished, so no other
/// fixture can be reading the variables while they are changed. Both are restored afterwards,
/// because CI sets them for the whole run.
/// </para>
/// </remarks>
[Collection(Name)]
public class RequiredDatabaseEnvironmentTests
{
    public const string Name = "Required database, read from the environment";

    private const string Unreachable = "Server=tcp:127.0.0.1,1;User ID=nobody;TrustServerCertificate=true";

    [Fact]
    public async Task The_public_entry_point_fails_when_the_environment_requires_a_database()
    {
        await WithEnvironmentAsync(Unreachable, required: "true", async () =>
        {
            await using var fixture = new SqlServerFixture();

            var failure = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await fixture.InitializeAsync());

            Assert.Contains(SqlServerFixture.RequiredEnvVar, failure.Message);
        });
    }

    [Fact]
    public async Task The_public_entry_point_skips_when_the_environment_does_not_require_one()
    {
        await WithEnvironmentAsync(Unreachable, required: null, async () =>
        {
            await using var fixture = new SqlServerFixture();

            await fixture.InitializeAsync();

            Assert.NotNull(fixture.SkipReason);
        });
    }

    private static async Task WithEnvironmentAsync(string connection, string? required, Func<Task> body)
    {
        var savedConnection = Environment.GetEnvironmentVariable(SqlServerFixture.ConnectionEnvVar);
        var savedRequired = Environment.GetEnvironmentVariable(SqlServerFixture.RequiredEnvVar);

        try
        {
            Environment.SetEnvironmentVariable(SqlServerFixture.ConnectionEnvVar, connection);
            Environment.SetEnvironmentVariable(SqlServerFixture.RequiredEnvVar, required);

            await body();
        }
        finally
        {
            Environment.SetEnvironmentVariable(SqlServerFixture.ConnectionEnvVar, savedConnection);
            Environment.SetEnvironmentVariable(SqlServerFixture.RequiredEnvVar, savedRequired);
        }
    }
}

[CollectionDefinition(RequiredDatabaseEnvironmentTests.Name, DisableParallelization = true)]
public sealed class RequiredDatabaseEnvironmentCollection;
