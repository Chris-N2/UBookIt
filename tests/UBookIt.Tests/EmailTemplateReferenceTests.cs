using System.Reflection;
using System.Text.RegularExpressions;
using UBookIt.Core.Bookings;
using UBookIt.Core.Notifications;
using UBookIt.Tests.Support;
using UBookIt.Web.Emails;

namespace UBookIt.Tests;

/// <summary>
/// The Email templates page is complete against the code it documents: the files, the folder,
/// the <c>@inherits</c> types, and every member of both models.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> A site owner upgrading to use template sets could not find the view
/// names or the model and read the source instead. The members were described in prose, which
/// nothing could check, and two of them had been added by later changes that each remembered to
/// add a sentence. A member added to a model now fails here until the page's table lists it.
/// </para>
/// <para>
/// <b>The guarantee spans two languages, so the guard sits where both are visible:</b> this
/// project references the assemblies and can read the page.
/// </para>
/// </remarks>
public class EmailTemplateReferenceTests
{
    private static string Page() => RepoFiles.Read("docs/email-templates.md");

    private static readonly Regex Row = new(@"^\|\s*`([^`]+)`\s*\|\s*([^|]*?)\s*\|\s*([^|]*?)\s*\|\s*$", RegexOptions.CultureInvariant);

    /// <summary>
    /// The rows of the first table beneath the heading that names <paramref name="headingNeedle"/>,
    /// as (first cell, second cell, third cell), with the first cell's backticks removed.
    /// </summary>
    private static IReadOnlyList<(string Name, string Second, string Third)> TableUnder(string page, string headingNeedle)
    {
        var heading = DocsMarkdown.Headings(page).SingleOrDefault(h => h.Text.Contains(headingNeedle, StringComparison.Ordinal));
        Assert.True(heading is not null, $"The Email templates page has no heading naming {headingNeedle}.");

        var lines = page.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var rows = new List<(string, string, string)>();
        var inTable = false;

        for (var i = heading.Line; i < lines.Length; i++)
        {
            var line = lines[i];

            if (line.StartsWith('#'))
            {
                break;
            }

            if (line.StartsWith('|'))
            {
                inTable = true;
                var match = Row.Match(line);

                if (match.Success)
                {
                    rows.Add((match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value));
                }
            }
            else if (inTable)
            {
                break;
            }
        }

        Assert.True(rows.Count > 0, $"The section headed '{heading.Text}' has no table rows.");

        return rows;
    }

    private static IReadOnlyList<PropertyInfo> PublicMembers(Type model)
        => model.GetProperties(BindingFlags.Public | BindingFlags.Instance);

    /// <summary>How the page writes a member's type: the C# keyword or simple name, with <c>?</c> where it can be null.</summary>
    private static string Written(PropertyInfo property)
    {
        var nullable = new NullabilityInfoContext().Create(property).ReadState == NullabilityState.Nullable;
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        return Name(type) + (nullable ? "?" : string.Empty);

        static string Name(Type t)
        {
            if (t == typeof(string))
            {
                return "string";
            }

            if (t == typeof(bool))
            {
                return "bool";
            }

            if (!t.IsGenericType)
            {
                return t.Name;
            }

            var name = t.Name[..t.Name.IndexOf('`', StringComparison.Ordinal)];

            return $"{name}<{string.Join(", ", t.GetGenericArguments().Select(Name))}>";
        }
    }

    public static TheoryData<string, Type, string?> Tables => new()
    {
        // (heading needle, model, heading needle of the table it extends)
        { "`BookingMessageModel`", typeof(BookingMessageModel), null },
        { "`BookerMessageModel`", typeof(BookerMessageModel), "`BookingMessageModel`" },
        { "`InternalMessageModel`", typeof(InternalMessageModel), "`BookingMessageModel`" },
    };

    [Theory]
    [MemberData(nameof(Tables))]
    public void Each_models_table_lists_exactly_its_public_members(string heading, Type model, string? extends)
    {
        var page = Page();
        var documented = TableUnder(page, heading).Select(r => r.Name)
            .Concat(extends is null ? [] : TableUnder(page, extends).Select(r => r.Name))
            .ToList();

        var actual = PublicMembers(model).Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        var duplicated = documented.GroupBy(n => n, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        var undocumented = actual.Except(documented, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var stale = documented.Except(actual, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        Assert.True(duplicated.Count == 0, $"{model.Name}: documented more than once: {string.Join(", ", duplicated)}");
        Assert.True(undocumented.Count == 0, $"{model.Name}: members the page does not document: {string.Join(", ", undocumented)}");
        Assert.True(stale.Count == 0, $"{model.Name}: the page documents members the model does not have: {string.Join(", ", stale)}");
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public void Each_row_states_the_members_type_and_what_it_carries(string heading, Type model, string? extends)
    {
        _ = extends;
        var members = PublicMembers(model).ToDictionary(p => p.Name, StringComparer.Ordinal);

        foreach (var (name, type, carries) in TableUnder(Page(), heading))
        {
            Assert.True(members.TryGetValue(name, out var property), $"{model.Name} has no member {name}.");
            Assert.Equal($"`{Written(property!)}`", type);
            Assert.False(string.IsNullOrWhiteSpace(carries), $"{model.Name}.{name} has no description.");
        }
    }

    [Fact]
    public void The_status_row_names_every_status()
    {
        var status = TableUnder(Page(), "`BookingMessageModel`").Single(r => r.Name == nameof(BookingMessageModel.Status));

        foreach (var value in Enum.GetNames<BookingStatus>())
        {
            Assert.Contains($"`{value}`", status.Third, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_file_table_lists_exactly_one_view_per_message_kind()
    {
        var documented = TableUnder(Page(), "Writing your own message content").Select(r => r.Name).ToList();
        var expected = Enum.GetValues<BookingMessageKind>()
            .Select(kind => RazorBookingTemplateRenderer.PathFor(kind)[RazorBookingTemplateRenderer.TemplateFolder.Length..])
            .ToList();

        Assert.Equal(expected.Order(StringComparer.Ordinal), documented.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// A link made to the notifications page's heading for writing message content, before the
    /// content moved, still lands on that heading and finds the way to it.
    /// </summary>
    /// <remarks>
    /// The link guard cannot see this: no published page links that fragment any more, so a
    /// renamed or deleted heading broke no link it checks. QA round 1 renamed the heading and the
    /// whole suite stayed green. Links from outside — a bookmark, a forum post, a page on
    /// <c>main</c> — are what this keeps working.
    /// </remarks>
    [Fact]
    public void The_notifications_page_keeps_its_pointer_to_this_page()
    {
        var notifications = RepoFiles.Read("docs/notifications.md");
        var headings = DocsMarkdown.Headings(notifications);
        var index = headings.ToList().FindIndex(h => h.Text == "Writing your own message content");

        Assert.True(index >= 0, "docs/notifications.md no longer has the heading 'Writing your own message content'.");
        Assert.Contains("writing-your-own-message-content", DocsMarkdown.HeadingIds(notifications));

        var start = headings[index].Line;
        var end = index + 1 < headings.Count ? headings[index + 1].Line : int.MaxValue;
        var pointers = DocsMarkdown.Links(notifications)
            .Where(l => l.Line > start && l.Line < end && l.Target.StartsWith("email-templates.md", StringComparison.Ordinal))
            .ToList();

        Assert.True(pointers.Count > 0, "The heading 'Writing your own message content' in docs/notifications.md no longer links to email-templates.md beneath it.");
    }

    [Fact]
    public void The_folder_stated_is_the_folder_the_package_reads()
        => Assert.Contains($"`{RazorBookingTemplateRenderer.TemplateFolder}`", Page(), StringComparison.Ordinal);

    [Fact]
    public void Each_inherits_line_names_types_that_exist_and_both_models_have_one()
    {
        var lines = Regex.Matches(Page(), @"@inherits\s+([\w.]+)<([\w.]+)>")
            .Select(m => (Page: m.Groups[1].Value, Model: m.Groups[2].Value))
            .ToList();

        foreach (var (page, model) in lines)
        {
            Assert.NotNull(typeof(UBookItEmailPage<>).Assembly.GetType($"{page}`1"));
            Assert.NotNull(typeof(BookingMessageModel).Assembly.GetType(model));
        }

        Assert.Contains(lines, l => l.Model == typeof(BookerMessageModel).FullName);
        Assert.Contains(lines, l => l.Model == typeof(InternalMessageModel).FullName);
    }
}
