using System.Reflection;
using System.Text.RegularExpressions;
using UBookIt.Core.Bookings;
using UBookIt.Core.Stores;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The boundary that keeps the plaintext secret out of storage (`persistence`, "Cancellation
/// secrets are held in their own additive table" — <i>a row SHALL NOT be a credential</i>).
/// </summary>
/// <remarks>
/// <b>Structural, not behavioural, and deliberately so.</b> "The package does not store the
/// secret" asserted by inspecting rows proves it for the paths a test happens to drive. Asserted by
/// showing the persistence layer <i>cannot see the type</i>, it holds for paths nobody has written
/// yet — which is the difference between a rule somebody must remember and one they cannot break.
/// </remarks>
public class CancellationSecretBoundaryTests
{
    [Fact]
    public void The_store_port_deals_only_in_hashes()
    {
        // No member accepts or returns CancellationSecret. An implementation cannot persist a
        // plaintext it is never handed, which is what makes the guarantee structural rather than a
        // convention.
        // TYPE IDENTITY, NOT A NAME SUBSTRING. The first version of this guard matched on the
        // name and flagged FindAsync, because `CancellationSecretRecord` contains
        // `CancellationSecret` as a substring — an instrument loose enough to report a boundary
        // violation that was not one. Compare the types.
        static IEnumerable<Type> Unwrap(Type type)
        {
            yield return type;
            foreach (var argument in type.GetGenericArguments().SelectMany(Unwrap))
            {
                yield return argument;
            }
        }

        var offenders = typeof(ICancellationSecretStore)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method =>
                Unwrap(method.ReturnType).Any(type => type == typeof(CancellationSecret))
                || method.GetParameters().Any(parameter =>
                    Unwrap(parameter.ParameterType).Any(type => type == typeof(CancellationSecret))))
            .Select(method => method.Name)
            .ToArray();

        Assert.Empty(offenders);

        // POSITIVE CONTROL, first in spirit: the scan must be looking at a port that has members,
        // or an empty offender list would mean nothing.
        Assert.NotEmpty(typeof(ICancellationSecretStore).GetMethods(BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void What_a_read_returns_carries_no_string_at_all()
    {
        // CancellationSecretRecord names a booking, two facts about time, and nothing else. A
        // string member would be the place a hash — or worse, a secret — came to live, and
        // everything that reads this type is one interpolation away from a log.
        var strings = typeof(CancellationSecretRecord)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(string))
            .Select(property => property.Name)
            .ToArray();

        Assert.Empty(strings);
        Assert.NotEmpty(typeof(CancellationSecretRecord).GetProperties(BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void The_persistence_assembly_cannot_see_the_secret_type()
    {
        // THE SCAN THAT CAN FAIL. Word-bounded, so `CancellationSecretRow`, `...Store` and
        // `...Record` do not match — only the Core value type itself, whose arrival in this
        // project would mean the plaintext had been handed to something whose job is to write
        // things down.
        var offenders = RepoFiles.Paths("src/UBookIt.Persistence", "*.cs")
            .Where(path => Regex.IsMatch(
                string.Join('\n', File.ReadAllLines(path).Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal))),
                @"\bCancellationSecret\b"))
            .Select(Path.GetFileName)
            .ToArray();

        Assert.Empty(offenders);

        // POSITIVE CONTROL: the scan must be reading files at all. An empty offender list from a
        // scan that matched nothing anywhere is the failure this project has shipped before.
        Assert.Contains(
            RepoFiles.Paths("src/UBookIt.Persistence", "*.cs").Select(Path.GetFileName),
            name => name == "SqlCancellationSecretStore.cs");
    }
}
