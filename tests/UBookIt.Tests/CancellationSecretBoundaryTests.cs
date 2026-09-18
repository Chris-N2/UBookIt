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
    public void No_store_can_see_the_secret_type()
    {
        // THE SCAN THAT CAN FAIL. Word-bounded, so `CancellationSecretRow`, `...Store` and
        // `...Record` do not match — only the Core value type itself, whose arrival among the
        // stores would mean the plaintext had been handed to something whose job is to write
        // things down.
        //
        // NARROWED FROM "the persistence assembly", and the narrowing is a finding rather than a
        // convenience. The first version claimed no file in UBookIt.Persistence could see the
        // type, and it failed the moment the email handler was wired up — because minting has to
        // happen where the message is composed, and the handler is in this assembly. The claim was
        // wrong, not the code: what the package guarantees is that the plaintext never reaches
        // STORAGE, and the stores are where that is decided. Broadening a guard until it fails and
        // then quietly deleting it is how a guarantee turns into a comment.
        var offenders = RepoFiles.Paths("src/UBookIt.Persistence/Stores", "*.cs")
            .Where(path => Regex.IsMatch(
                string.Join('\n', File.ReadAllLines(path).Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal))),
                @"\bCancellationSecret\b"))
            .Select(Path.GetFileName)
            .ToArray();

        Assert.Empty(offenders);

        // POSITIVE CONTROL: the scan must be reading files at all. An empty offender list from a
        // scan that matched nothing anywhere is the failure this project has shipped before.
        Assert.Contains(
            RepoFiles.Paths("src/UBookIt.Persistence/Stores", "*.cs").Select(Path.GetFileName),
            name => name == "SqlCancellationSecretStore.cs");

        // AND THE COUNTERPART, so the narrowing above cannot be read as "nobody may see it". One
        // place mints the secret, and it is the one that puts it in a message — if that ever stops
        // being true, this fails and the next reader finds out where minting moved to.
        Assert.Contains(
            RepoFiles.Paths("src/UBookIt.Persistence/Notifications", "*.cs"),
            path => Regex.IsMatch(File.ReadAllText(path), @"\bCancellationSecret\b"));
    }
}
