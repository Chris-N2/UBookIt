using System.Text.RegularExpressions;
using UBookIt.Core.Bookings;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// That <c>UBookIt.Core</c> depends on nothing.
/// </summary>
/// <remarks>
/// <para>
/// This is the constraint the observation design is shaped around, not a tidiness
/// preference. <see cref="IBookingObserver"/> lives in Core — rather than the package using
/// an Umbraco notification type directly, or a decorator in the Umbraco layer — <b>because</b>
/// Core carries no framework, so a host that is not Umbraco can still observe. The adapter is
/// exiled to <c>UBookIt.Persistence</c> at an admitted cost for the same reason.
/// </para>
/// <para>
/// Take the dependency away and every one of those decisions becomes arbitrary, with a fully
/// green suite. The pressure is real and already present: Core's catch around an observer is
/// <i>silent</i> precisely because it cannot log, and the obvious fix is one
/// <c>PackageReference</c> away.
/// </para>
/// <para>
/// The change that introduced the port wrote "assert it rather than assume it" in its own
/// task list, then asserted it in a spec scenario and assumed it in code. QA found the
/// scenario with nothing behind it.
/// </para>
/// </remarks>
public class CoreIndependenceTests
{
    [Fact]
    public void Core_declares_no_package_reference()
    {
        // The literal form of the guarantee: the project file names none.
        var project = RepoFiles.Read("src/UBookIt.Core/UBookIt.Core.csproj");

        var references = Regex.Matches(project, "<PackageReference\\b")
            .Select(match => match.Value)
            .ToList();

        Assert.True(
            references.Count == 0,
            $"UBookIt.Core now declares {references.Count} package reference(s). The observation "
            + "port lives in Core because Core depends on nothing; adding one makes that "
            + "reasoning false and the adapter's exile to Persistence pointless.");
    }

    [Fact]
    public void Core_declares_no_project_reference_either()
    {
        // A ProjectReference would be a package dependency wearing a different hat: Core
        // could acquire Umbraco transitively without ever naming it.
        var project = RepoFiles.Read("src/UBookIt.Core/UBookIt.Core.csproj");

        Assert.DoesNotContain("<ProjectReference", project, StringComparison.Ordinal);
    }

    [Fact]
    public void Nothing_but_the_framework_reaches_Core_at_runtime()
    {
        // The csproj states the intent; this checks what the built assembly actually binds
        // to, which is what would break a non-Umbraco host.
        //
        // WHAT IT DOES NOT CATCH, because an earlier version of this comment claimed it did:
        // `GetReferencedAssemblies` reports only what the compiled IL binds to. A reference
        // injected project-wide and never used is invisible here, and a `FrameworkReference`
        // or an analyzer never appears at all. Those are the csproj test's job — this one's
        // job is the reference that is declared somewhere else and actually used, which the
        // csproj test cannot see.
        //
        // The two are complements, and neither is a superset of the other.
        var referenced = typeof(IBookingObserver).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name ?? string.Empty)
            .Where(name => !name.StartsWith("System.", StringComparison.Ordinal))
            .Where(name => name is not ("System" or "netstandard" or "mscorlib"))
            .ToList();

        Assert.True(
            referenced.Count == 0,
            "UBookIt.Core binds to something outside the framework: " + string.Join(", ", referenced));
    }
}
