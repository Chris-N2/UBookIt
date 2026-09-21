using System.Diagnostics;
using System.Text.RegularExpressions;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// Every retired claim's evidence text is the text the named commit actually holds.
/// </summary>
/// <remarks>
/// <para>
/// <b>This exists because the claim it checks was false when it was written.</b>
/// <see cref="RetiredClaims"/> asserted that every <c>AsWritten</c> had been recovered with
/// <c>git show</c> rather than retyped. Fifteen of sixteen had been. The sixteenth said
/// <i>"presence check over every key the dialog renders"</i> where <c>e545deeb</c> says
/// <i>"…every key the cancel flow can emit"</i> — close enough to read as right, and wrong.
/// QA found it by hand, with <c>git show</c>, because nothing in the suite could.
/// </para>
/// <para>
/// <b>Why the two existing controls could not see it.</b>
/// <c>Every_needle_matches_the_text_it_was_written_against</c> compares the needle to
/// <c>AsWritten</c> — both authored in the same sitting, so a retyped fixture agrees with its
/// needle perfectly. <c>Every_needle_says_where_its_evidence_came_from</c> only checks that the
/// evidence string contains an <c>@</c>. Between them they prove the fixture is
/// self-consistent, which is exactly what an invented fixture also is. Self-consistency is not
/// provenance.
/// </para>
/// <para>
/// <b>This reverses design decision D2, on evidence rather than preference.</b> D2 rejected
/// checking against the git object store as "correct in principle, but makes the suite depend
/// on the git object store and on history never being rewritten". That trade-off was argued
/// before there was a counter-example; there is one now, and it was in the fixture the
/// argument was written to justify. The dependency is real and accepted: this repository is
/// always a git checkout, the commits named are old and on <c>main</c>, and a rewrite that
/// orphaned them would be a far larger event than a red test.
/// </para>
/// <para>
/// <b>It fails rather than skips when git cannot answer.</b> A provenance check that quietly
/// skips when the thing it verifies is unreachable is the same green-for-no-reason this whole
/// change is about.
/// </para>
/// </remarks>
public sealed class RetiredClaimEvidenceTests
{
    [Fact]
    public void Every_fixture_is_the_text_the_named_commit_holds()
    {
        var failures = new List<string>();
        var verified = 0;

        foreach (var claim in RetiredClaims.All)
        {
            // Evidence is "<path> @ <sha>", optionally followed by commentary.
            var match = Regex.Match(
                claim.Evidence,
                @"^(?<path>\S+)\s+@\s+(?<sha>[0-9a-f]{7,40})");

            if (!match.Success)
            {
                failures.Add(
                    $"\"{claim.Needle}\": evidence \"{claim.Evidence}\" is not in the form "
                    + "\"<path> @ <sha>\", so its provenance cannot be re-derived.");

                continue;
            }

            var path = match.Groups["path"].Value;
            var sha = match.Groups["sha"].Value;

            var (ok, content) = GitShow($"{sha}:{path}");

            if (!ok)
            {
                failures.Add(
                    $"\"{claim.Needle}\": `git show {sha}:{path}` failed. {content}");

                continue;
            }

            var expected = Collapse(claim.AsWritten);

            if (expected.Length == 0)
            {
                // `Contains("")` is always true, so an empty fixture would verify against any
                // file at all. Caught here rather than left to the sibling control, so this
                // guard stands on its own.
                failures.Add(
                    $"\"{claim.Needle}\": the AsWritten text is empty, which every document "
                    + "trivially contains. Recover the sentence from git.");
            }
            else if (Collapse(content).Contains(expected, StringComparison.Ordinal))
            {
                verified++;
            }
            else
            {
                failures.Add(
                    $"\"{claim.Needle}\": the AsWritten text is NOT in {path} at {sha}. It was "
                    + "retyped or mis-transcribed, so it is not evidence of anything. Recover "
                    + $"it with `git show {sha}:{path}`.");
            }
        }

        Assert.True(
            failures.Count == 0,
            $"{failures.Count} of {RetiredClaims.All.Count} retired claims carry evidence the "
            + $"named commit does not hold:{Environment.NewLine}  "
            + string.Join(Environment.NewLine + "  ", failures));

        // Anti-vacuity: an emptied fixture must not read as a clean run.
        //
        // Only the `> 0` half can fire, and saying so is better than implying otherwise: every
        // claim either increments `verified` or adds a failure, and the assert above already
        // requires no failures — so `verified == All.Count` is entailed by the time we reach
        // here. It is kept because it states the invariant, not because it can catch a
        // different fault. What `> 0` does catch is `RetiredClaims.All` being emptied, which
        // would otherwise make this whole class pass over nothing.
        Assert.True(
            verified > 0,
            "No fixture was compared against git at all, so this guard proved nothing. Either "
            + $"{nameof(RetiredClaims)}.{nameof(RetiredClaims.All)} is empty or none of its "
            + "entries reached the comparison.");

        Assert.Equal(RetiredClaims.All.Count, verified);
    }

    /// <summary>
    /// No positive documentation pin anywhere in the suite requires a sentence that has been
    /// retired.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The machine-checkable form of "A guard does not require a claim that has become
    /// false".</b> Until now that requirement rested on a manual sweep (task 4.2) whose stated
    /// blind spot was that it filtered call sites by absence-shaped wording — so a pin phrased
    /// without any of those words would not have been in the set it looked at.
    /// </para>
    /// <para>
    /// <b>It is the exact defect this change was written to repair, in its general form.</b>
    /// <c>BackofficeDocumentationTests</c> asserted <c>Says(docs, "It does not place bookings")</c>
    /// for two releases after an operator could place one: the suite REQUIRED a false sentence,
    /// so correcting the document turned the build red and told the person doing the right thing
    /// they had broken something. This makes the next occurrence fail on the day the sentence is
    /// retired, rather than on the day somebody happens to grep for it.
    /// </para>
    /// <para>
    /// Deliberately scoped to the two halves of the same mechanism — a needle held out by the
    /// sweep must not be a needle pinned in place by a <c>Says</c>. It cannot judge whether a
    /// sentence nobody has retired is true; nothing can.
    /// </para>
    /// </remarks>
    [Fact]
    public void No_guard_pins_a_sentence_the_sweep_holds_out()
    {
        var testSources = Directory
            .EnumerateFiles(
                Path.Combine(RepoFiles.Root, "tests"),
                "*.cs",
                new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true })
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.EndsWith("RetiredClaims.cs", StringComparison.Ordinal))

            // And this file, which QUOTES call sites in VariableSentencePins. Without this the
            // scanner reads its own registry as five more unreadable pins — the document
            // written to explain the guard defeating the guard, which is the precise shape
            // that broke `SaysOnce`'s pin on docs/publishing.md. It holds no pins of its own;
            // if it ever does, this exclusion has to be narrowed rather than kept.
            .Where(path => !path.EndsWith("RetiredClaimEvidenceTests.cs", StringComparison.Ordinal))
            .ToList();

        Assert.True(
            testSources.Count > 10,
            $"Only {testSources.Count} test sources found; this guard is not reading the suite.");

        var conflicts = new List<string>();
        var callsFound = 0;
        var callsRead = 0;
        var unreadable = new List<string>();

        foreach (var file in testSources)
        {
            var text = File.ReadAllText(file);
            var relative = Path.GetRelativePath(RepoFiles.Root, file).Replace('\\', '/');

            foreach (Match call in Regex.Matches(text, @"DocumentationAssert\.Says(?:Once)?\("))
            {
                callsFound++;

                var pinned = SentenceOf(text, call.Index + call.Length);

                if (pinned is null)
                {
                    // The sentence is an expression, not a literal. Nothing to compare.
                    //
                    // Identified by its CALL TEXT rather than its line number: a line number
                    // moves whenever anything above it is edited, so the registry would go red
                    // on unrelated work and get weakened rather than fixed.
                    unreadable.Add($"{relative}  {CallTextAt(text, call.Index)}");

                    continue;
                }

                callsRead++;

                foreach (var claim in RetiredClaims.All)
                {
                    if (!pinned.Contains(claim.Needle, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    conflicts.Add(
                        $"{relative}:{LineOf(text, call.Index)} pins \"{pinned}\", which "
                        + $"contains the retired claim \"{claim.Needle}\" (falsified by "
                        + $"{claim.RetiredBy}). A guard that requires a false sentence reports "
                        + "green while the documentation is wrong, and turns red when somebody "
                        + "corrects it.");
                }
            }
        }

        Assert.True(
            conflicts.Count == 0,
            $"{conflicts.Count} guard(s) require a sentence the sweep holds out:"
            + $"{Environment.NewLine}  "
            + string.Join(Environment.NewLine + "  ", conflicts));

        // Anti-vacuity, in the only form that can notice this guard going blind.
        //
        // The first version counted matched call sites against a floor of 50, and matched 176
        // of 185 — so the nine it could not read were invisible, and an ENTIRE CLASS of pin
        // could have been lost without the number moving. QA reinstated the retired pin split
        // across two concatenated literals, a shape three pins in this repository already use,
        // and this guard passed.
        //
        // So the floor is no longer a number: every call site must be READ, and the only ones
        // allowed to go unread are those whose sentence is genuinely not a literal. That makes
        // the coverage gap the failure rather than the silence.
        var unreadableThatAreNotVariables = unreadable
            .Where(site => !VariableSentencePins.Contains(site, StringComparer.Ordinal))
            .ToList();

        Assert.True(
            unreadableThatAreNotVariables.Count == 0,
            $"{unreadableThatAreNotVariables.Count} documentation pin(s) could not be read, so "
            + "this guard cannot see what they require. Either the sentence is built in a way "
            + "the reader does not understand — line-wrapping and literal concatenation are the "
            + "shapes that have defeated guards here before — or it is a variable and belongs "
            + $"in {nameof(VariableSentencePins)} with a reason:{Environment.NewLine}  "
            + string.Join(Environment.NewLine + "  ", unreadableThatAreNotVariables));

        // And the other direction, because a registry is a normalisation and both directions
        // of one need guarding: an entry that no longer matches any call site is a stale
        // exemption nobody will notice, quietly widening what this guard is allowed to skip.
        var registeredButNotSeen = VariableSentencePins
            .Where(entry => !unreadable.Contains(entry, StringComparer.Ordinal))
            .ToList();

        Assert.True(
            registeredButNotSeen.Count == 0,
            $"{registeredButNotSeen.Count} entr(y/ies) in {nameof(VariableSentencePins)} match "
            + "no call site any more. The pin was deleted or rewritten, so the exemption is "
            + $"stale and should go with it:{Environment.NewLine}  "
            + string.Join(Environment.NewLine + "  ", registeredButNotSeen));

        Assert.True(
            callsFound > 100 && callsRead > 0,
            $"Only {callsFound} documentation pins were found ({callsRead} readable). This "
            + "repository has far more, so the call-site pattern itself has stopped matching "
            + "and the guard is passing over almost nothing.");
    }

    /// <summary>
    /// Call sites whose pinned sentence is a variable rather than a literal, so there is
    /// nothing for the crossing check to read.
    /// </summary>
    /// <remarks>
    /// Registered individually, and the test fails when an unreadable call site is NOT on this
    /// list. That is the difference between "we could not read nine of them" and "we could not
    /// read these nine, for this reason" — the first hides a blind spot, the second is a
    /// measurement. A line that moves is a deliberate re-check, which is the point.
    /// </remarks>
    private static readonly string[] VariableSentencePins =
    [
        // A loop over a list of phrases, each already a literal in that list.
        "tests/UBookIt.Tests/BackofficeDocumentationTests.cs  DocumentationAssert.Says(purpose, phrase)",

        // DocumentationAssert's own tests, which call it with the sentence under test.
        "tests/UBookIt.Tests/DocumentationAssertTests.cs  DocumentationAssert.Says(document, sentence)",
        "tests/UBookIt.Tests/DocumentationAssertTests.cs  DocumentationAssert.SaysOnce(document, sentence)",

        // A path derived from production code, so the document is pinned to the real value.
        "tests/UBookIt.Tests/NotificationDocumentationTests.cs  DocumentationAssert.Says(docs, RazorBookingTemplateRenderer.TemplateFolder.TrimStart('~', '/'))",

        // The needle control itself: both arguments come from the fixture.
        "tests/UBookIt.Tests/NotificationDocumentationTests.cs  DocumentationAssert.Says(claim.AsWritten, claim.Needle)",
    ];

    /// <summary>
    /// The call as written, whitespace collapsed, for identifying a call site stably.
    /// </summary>
    private static string CallTextAt(string source, int start)
    {
        var depth = 0;
        var i = source.IndexOf('(', start);

        for (; i < source.Length; i++)
        {
            if (source[i] == '(')
            {
                depth++;
            }
            else if (source[i] == ')')
            {
                depth--;

                if (depth == 0)
                {
                    break;
                }
            }
        }

        return Regex.Replace(source[start..Math.Min(i + 1, source.Length)], @"\s+", " ").Trim();
    }

    /// <summary>
    /// Reads the sentence a <c>Says</c>/<c>SaysOnce</c> call pins, joining concatenated string
    /// literals, or <c>null</c> when the argument is not made of literals at all.
    /// </summary>
    /// <remarks>
    /// <b>A regex cannot do this, and trying was the defect.</b> A pattern ending in
    /// <c>"…"\s*\)</c> reads only a sentence written as one literal on one line; this repository
    /// wraps prose at a column, so a long pinned sentence is routinely split with <c>+</c>.
    /// Scanning to the matching close paren and joining every literal inside reads both shapes,
    /// and reports honestly when it can read neither.
    /// </remarks>
    private static string? SentenceOf(string source, int afterOpenParen)
    {
        var depth = 1;
        var literals = new List<string>();
        var i = afterOpenParen;

        while (i < source.Length && depth > 0)
        {
            var c = source[i];

            if (c == '"')
            {
                // <b>The guarantee: a pin written as a verbatim (@"…") or raw ("""…""") literal
                // cannot silently pass.</b> Stated as the guarantee rather than as a mechanism,
                // because the mechanism this comment used to describe was wrong — it claimed
                // such a literal "would read as unreadable", and QA measured both forms being
                // read CORRECTLY and caught as conflicts. Safer than the comment said, which is
                // still a comment describing behaviour the code does not have.
                var literal = new System.Text.StringBuilder();
                i++;

                while (i < source.Length && source[i] != '"')
                {
                    if (source[i] == '\\' && i + 1 < source.Length)
                    {
                        literal.Append(source[i]).Append(source[i + 1]);
                        i += 2;

                        continue;
                    }

                    literal.Append(source[i]);
                    i++;
                }

                literals.Add(Unescape(literal.ToString()));
                i++;

                continue;
            }

            if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
            }

            i++;
        }

        return literals.Count == 0 ? null : string.Concat(literals);
    }

    private static int LineOf(string source, int index)
        => source.Take(index).Count(c => c == '\n') + 1;

    /// <summary>
    /// Resolves C# escapes, and returns the text unchanged when it holds a sequence
    /// <see cref="Regex.Unescape(string)"/> does not recognise.
    /// </summary>
    /// <remarks>
    /// A verbatim literal may legitimately contain <c>\p</c> or <c>\d</c> as ordinary
    /// characters, and <c>Regex.Unescape</c> throws on those. Unguarded, the one input that
    /// reaches it turns this guard into an unhandled exception — a guard that crashes reports
    /// nothing about the thing it guards, which is worse than one that reports a miss.
    /// </remarks>
    private static string Unescape(string literal)
    {
        try
        {
            return Regex.Unescape(literal);
        }
        catch (ArgumentException)
        {
            return literal;
        }
    }

    /// <summary>Whitespace-insensitive, because AsWritten is re-indented by the raw literal.</summary>
    private static string Collapse(string text)
        => Regex.Replace(text, @"\s+", " ").Trim();

    private static (bool Ok, string Content) GitShow(string spec)
    {
        var start = new ProcessStartInfo("git", $"show {spec}")
        {
            WorkingDirectory = RepoFiles.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,

            // This repository's prose is full of em dashes, and every document here is UTF-8.
            // Without these, the redirected stream is decoded with the console's code page —
            // Windows-1252 on this machine — and every sentence containing an em dash reports
            // as "retyped". The first run of this guard accused five correct fixtures for
            // exactly that reason: a text-comparing instrument must be normalised before it is
            // believed, or it manufactures findings.
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };

        using var process = Process.Start(start);

        if (process is null)
        {
            return (false, "git could not be started.");
        }

        // Both streams read concurrently, then wait. Reading one to the end before starting
        // the other deadlocks if the unread pipe fills — `git show` on a large blob is exactly
        // that shape, and the fault would look like a hang rather than a failure.
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(milliseconds: 30_000))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Already gone between the timeout and the kill.
            }

            return (false, "git did not exit within 30s; the comparison was not made.");
        }

        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();

        return process.ExitCode == 0 ? (true, stdout) : (false, stderr.Trim());
    }
}
