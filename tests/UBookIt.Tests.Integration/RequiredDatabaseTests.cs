using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

/// <summary>
/// The fixture's two answers to an unreachable server: skip, or — when a database is declared
/// required — fail.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this matters more than it looks.</b> CI has no LocalDB. If the required mode stopped
/// working, every integration test would skip there, and a skip does not fail a run: the suite
/// would report green having tested nothing against SQL Server. CI's results check would still
/// catch the skips, but this is the guard on the mechanism that names the cause.
/// </para>
/// <para>
/// It deliberately needs no server, and so runs — and can fail — everywhere, including on a
/// machine where every other test in this project skips. The address is a closed local port;
/// whether the attempt is refused or times out depends on the platform (on the Windows dev machine
/// it reports "The wait operation timed out"), and either way it is bounded by the fixture's own
/// connect timeout, which is why these six cost seconds rather than minutes. It drives the fixture's real initialisation
/// with explicit inputs rather than setting environment variables, which every fixture
/// initialising in parallel would also read.
/// </para>
/// </remarks>
public class RequiredDatabaseTests
{
    private const string Unreachable = "Server=tcp:127.0.0.1,1;User ID=nobody;TrustServerCertificate=true";

    [Fact]
    public async Task An_unreachable_server_fails_when_a_database_is_required()
    {
        await using var fixture = new SqlServerFixture();

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await fixture.InitializeAsync(Unreachable, required: "true"));

        Assert.Contains(SqlServerFixture.RequiredEnvVar, failure.Message);
        Assert.Contains(SqlServerFixture.ConnectionEnvVar, failure.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("false")]
    [InlineData("TRUE")]
    [InlineData("1")]
    public async Task An_unreachable_server_skips_unless_the_flag_is_exactly_true(string? required)
    {
        await using var fixture = new SqlServerFixture();

        await fixture.InitializeAsync(Unreachable, required);

        Assert.NotNull(fixture.SkipReason);
        Assert.Contains(SqlServerFixture.ConnectionEnvVar, fixture.SkipReason);
    }
}
