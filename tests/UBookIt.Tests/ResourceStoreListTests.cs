using UBookIt.Core.Resources;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The read port's <c>ListAsync</c> (added for public discovery): paging plus
/// the unpaged total, deterministically ordered by display name then id.
/// </summary>
public class ResourceStoreListTests
{
    private static Resource Room(string name) => Resource.Create(ResourceTypes.Room, name).Value;

    [Fact]
    public async Task Lists_all_with_total_ordered_by_name()
    {
        var store = new InMemoryResourceStore()
            .Add(Room("Charlie")).Add(Room("Alpha")).Add(Room("Bravo"));

        var page = await store.ListAsync(0, 50);

        Assert.Equal(3, page.Total);
        Assert.Equal(["Alpha", "Bravo", "Charlie"], page.Items.Select(r => r.DisplayName));
    }

    [Fact]
    public async Task Pages_report_the_unpaged_total()
    {
        var store = new InMemoryResourceStore()
            .Add(Room("Alpha")).Add(Room("Bravo")).Add(Room("Charlie"));

        var page = await store.ListAsync(skip: 1, take: 1);

        Assert.Equal(3, page.Total);                                  // unpaged total
        Assert.Equal("Bravo", Assert.Single(page.Items).DisplayName); // second by name
    }
}
