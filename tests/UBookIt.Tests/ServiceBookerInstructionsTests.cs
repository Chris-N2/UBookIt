using UBookIt.Core.Common;
using UBookIt.Core.Services;

namespace UBookIt.Tests;

/// <summary>
/// A service's instructions for the booker (service-booker-instructions, design 1): one
/// normalising rule, shared by <see cref="Service.WithBookerInstructions"/> and the static helper,
/// and a factory whose published shape gives every existing caller a service with none.
/// </summary>
public class ServiceBookerInstructionsTests
{
    private static Service Massage()
        => Service.Create(
            "Massage",
            ServiceDuration.Fixed(TimeSpan.FromMinutes(60)).Value,
            [new ServiceRole("person", 2)
            {
                RequiredCapabilities = CapabilitySet.Create(["deep-tissue"], CapabilitySet.RequiredField).Value,
                VisitorSelectable = true,
            }]).Value;

    [Fact]
    public void A_service_from_the_factory_has_no_instructions()
        => Assert.Null(Massage().BookerInstructions);

    [Fact]
    public void Surrounding_whitespace_and_blank_lines_are_trimmed()
    {
        var result = Massage().WithBookerInstructions("  \r\n\n Join on Teams \n\t ");

        Assert.Equal("Join on Teams", result.Value.BookerInstructions);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\t \n")]
    public void Empty_or_whitespace_alone_means_none(string? instructions)
        => Assert.Null(Massage().WithBookerInstructions(instructions).Value.BookerInstructions);

    [Fact]
    public void Line_breaks_within_the_text_are_kept_as_written()
    {
        const string text = "Join on Teams:\r\nhttps://example.test/meet\n\nParking at the rear.";

        Assert.Equal(text, Massage().WithBookerInstructions(text).Value.BookerInstructions);
    }

    [Fact]
    public void Exactly_the_limit_after_trimming_is_accepted()
    {
        var text = new string('a', Service.BookerInstructionsMaxLength);

        var result = Massage().WithBookerInstructions($"  {text}\n");

        Assert.True(result.Succeeded);
        Assert.Equal(text, result.Value.BookerInstructions);
    }

    [Fact]
    public void One_over_the_limit_after_trimming_is_rejected_with_its_code_and_field()
    {
        var result = Massage().WithBookerInstructions(new string('a', Service.BookerInstructionsMaxLength + 1));

        Assert.False(result.Succeeded);
        var failure = Assert.Single(result.Failures);
        Assert.Equal("service-booker-instructions-too-long", failure.Code);
        Assert.Equal("bookerInstructions", failure.Field);
    }

    [Fact]
    public void Every_other_member_of_the_returned_service_is_the_original()
    {
        var original = Massage();

        var changed = original.WithBookerInstructions("Directions").Value;

        Assert.NotSame(original, changed);
        Assert.Equal(original.Id, changed.Id);
        Assert.Equal(original.Name, changed.Name);
        Assert.Equal(original.Duration, changed.Duration);
        Assert.Equal(original.Roles, changed.Roles);
        Assert.Null(original.BookerInstructions);
    }

    [Fact]
    public void Instructions_can_be_removed_again()
    {
        var withInstructions = Massage().WithBookerInstructions("Directions").Value;

        Assert.Null(withInstructions.WithBookerInstructions("  ").Value.BookerInstructions);
    }

    [Fact]
    public void The_static_rule_and_the_instance_method_agree()
    {
        var overLong = new string('a', Service.BookerInstructionsMaxLength + 1);

        Assert.Equal(
            Service.NormalizeBookerInstructions(overLong).Failures,
            Massage().WithBookerInstructions(overLong).Failures);
        Assert.Equal(
            " a\nb ".Trim(),
            Service.NormalizeBookerInstructions(" a\nb ").Value);
    }
}
