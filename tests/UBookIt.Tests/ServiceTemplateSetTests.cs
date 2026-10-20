using UBookIt.Core.Common;
using UBookIt.Core.Services;

namespace UBookIt.Tests;

/// <summary>
/// A service's email template set (service-email-templates, design 2): one normalising rule,
/// shared by <see cref="Service.WithTemplateSet"/>, the static helper and the predicate the
/// renderer re-checks against, and a factory whose published shape gives every existing caller a
/// service with none.
/// </summary>
public class ServiceTemplateSetTests
{
    private static readonly string SixtyFour = new('a', Service.TemplateSetMaxLength);
    private static readonly string SixtyFive = new('a', Service.TemplateSetMaxLength + 1);

    private static Service Massage()
        => Service.Create(
            "Massage",
            ServiceDuration.Fixed(TimeSpan.FromMinutes(60)).Value,
            [new ServiceRole("person", 2)
            {
                RequiredCapabilities = CapabilitySet.Create(["deep-tissue"], CapabilitySet.RequiredField).Value,
                VisitorSelectable = true,
            }]).Value;

    public static TheoryData<string> Accepted => new()
    {
        "remote", "in-person", "a", "a1-b2", "0", "9-z", SixtyFour,
    };

    public static TheoryData<string> Rejected => new()
    {
        SixtyFive, "../partials", "a/b", "a\\b", ".", "in person", "-remote", "remote-", "re--mote", "café",
        "-", "a.b", "re\0mote", "re\nmote",
    };

    [Fact]
    public void A_service_from_the_factory_has_no_template_set()
        => Assert.Null(Massage().TemplateSet);

    [Fact]
    public void Surrounding_whitespace_is_trimmed()
        => Assert.Equal("remote", Massage().WithTemplateSet("  remote \t\n").Value.TemplateSet);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\t \n")]
    public void Empty_or_whitespace_alone_means_none(string? templateSet)
    {
        var result = Massage().WithTemplateSet(templateSet);

        Assert.True(result.Succeeded);
        Assert.Null(result.Value.TemplateSet);
    }

    [Fact]
    public void Upper_case_is_lowered_before_validation()
        => Assert.Equal("remote-consult", Massage().WithTemplateSet("Remote-Consult").Value.TemplateSet);

    [Theory]
    [MemberData(nameof(Accepted))]
    public void A_valid_name_is_accepted_as_written(string templateSet)
    {
        var result = Massage().WithTemplateSet(templateSet);

        Assert.True(result.Succeeded);
        Assert.Equal(templateSet, result.Value.TemplateSet);
    }

    [Theory]
    [MemberData(nameof(Rejected))]
    public void An_invalid_name_is_rejected_with_its_code_and_field(string templateSet)
    {
        var result = Massage().WithTemplateSet(templateSet);

        Assert.False(result.Succeeded);
        var failure = Assert.Single(result.Failures);
        Assert.Equal("service-template-set-invalid", failure.Code);
        Assert.Equal("templateSet", failure.Field);
    }

    /// <summary>
    /// The renderer relies on the two agreeing: every value the normaliser can produce must pass the
    /// predicate, so a stored set is never refused as a path; and every input the normaliser refuses
    /// must, once trimmed and lowered, fail it too, so the predicate admits nothing the rule rejects.
    /// A name that already satisfies the predicate is its own normal form.
    /// </summary>
    [Theory]
    [MemberData(nameof(Accepted))]
    [MemberData(nameof(Rejected))]
    [InlineData("Remote-Consult")]
    [InlineData("  remote  ")]
    [InlineData("CAFÉ")]
    public void The_predicate_agrees_with_the_normaliser(string input)
    {
        var normalized = Service.NormalizeTemplateSet(input);
        var candidate = input.Trim().ToLowerInvariant();

        Assert.Equal(normalized.Succeeded, Service.IsValidTemplateSetName(candidate));

        if (normalized.Succeeded)
        {
            Assert.True(Service.IsValidTemplateSetName(normalized.Value));
        }

        if (Service.IsValidTemplateSetName(input))
        {
            Assert.Equal(input, normalized.Value);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Remote")]
    [InlineData(" remote")]
    [InlineData("remote\n")]
    public void The_predicate_applies_no_normalisation(string? name)
        => Assert.False(Service.IsValidTemplateSetName(name));

    [Fact]
    public void The_set_can_be_removed_again()
        => Assert.Null(Massage().WithTemplateSet("remote").Value.WithTemplateSet(" ").Value.TemplateSet);

    [Fact]
    public void Every_other_member_is_kept_when_the_set_is_given_after_the_instructions()
    {
        var original = Massage().WithBookerInstructions("Directions").Value;

        var changed = original.WithTemplateSet("remote").Value;

        AssertSameExceptSetAndInstructions(original, changed);
        Assert.Equal("Directions", changed.BookerInstructions);
        Assert.Equal("remote", changed.TemplateSet);
    }

    [Fact]
    public void Every_other_member_is_kept_when_the_instructions_are_given_after_the_set()
    {
        var original = Massage().WithTemplateSet("remote").Value;

        var changed = original.WithBookerInstructions("Directions").Value;

        AssertSameExceptSetAndInstructions(original, changed);
        Assert.Equal("remote", changed.TemplateSet);
        Assert.Equal("Directions", changed.BookerInstructions);
    }

    [Fact]
    public void A_failed_set_leaves_the_original_untouched()
    {
        var original = Massage().WithTemplateSet("remote").Value;

        Assert.False(original.WithTemplateSet("../x").Succeeded);
        Assert.Equal("remote", original.TemplateSet);
    }

    [Fact]
    public void The_static_rule_and_the_instance_method_agree()
    {
        Assert.Equal(
            Service.NormalizeTemplateSet("../x").Failures,
            Massage().WithTemplateSet("../x").Failures);
        Assert.Equal("remote", Service.NormalizeTemplateSet(" Remote ").Value);
    }

    private static void AssertSameExceptSetAndInstructions(Service original, Service changed)
    {
        Assert.NotSame(original, changed);
        Assert.Equal(original.Id, changed.Id);
        Assert.Equal(original.Name, changed.Name);
        Assert.Equal(original.Duration, changed.Duration);
        Assert.Equal(original.Roles, changed.Roles);
    }
}
