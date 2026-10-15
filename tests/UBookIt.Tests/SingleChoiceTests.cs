using UBookIt.Web.Rendering;

namespace UBookIt.Tests;

/// <summary>
/// The single-choice skip's rule, as a pure function: given the catalogue as composed and the
/// site's setting, which subject (if any) is entered as if the visitor had chosen it.
/// </summary>
/// <remarks>
/// The whole matrix lives here because it needs no host. The rendering suite proves the
/// dispatcher actually consults it, through the real component.
/// </remarks>
public class SingleChoiceTests
{
    private static readonly BookingSubject Massage = BookingSubject.Service(Guid.NewGuid());
    private static readonly BookingSubject RoomA = BookingSubject.Resource(Guid.NewGuid());

    private static CatalogueModel Catalogue(params BookingSubject[] subjects) => new()
    {
        Entries = subjects.Select((s, i) => new CatalogueEntry(s, $"Entry {i}")).ToList(),
    };

    [Fact]
    public void One_service_with_the_setting_on_is_entered()
        => Assert.Equal(Massage, SingleChoice.Sole(Catalogue(Massage), skipSingleChoice: true));

    [Fact]
    public void One_resource_with_the_setting_on_is_entered()
        => Assert.Equal(RoomA, SingleChoice.Sole(Catalogue(RoomA), skipSingleChoice: true));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void An_empty_catalogue_is_never_skipped(bool skip)
        => Assert.Null(SingleChoice.Sole(Catalogue(), skip));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Two_choices_are_never_skipped(bool skip)
        => Assert.Null(SingleChoice.Sole(Catalogue(Massage, RoomA), skip));

    [Fact]
    public void Three_choices_are_never_skipped()
        => Assert.Null(SingleChoice.Sole(
            Catalogue(Massage, RoomA, BookingSubject.Service(Guid.NewGuid())), skipSingleChoice: true));

    [Fact]
    public void One_choice_with_the_setting_off_is_not_skipped()
    {
        Assert.Null(SingleChoice.Sole(Catalogue(Massage), skipSingleChoice: false));
        Assert.Null(SingleChoice.Sole(Catalogue(RoomA), skipSingleChoice: false));
    }
}
