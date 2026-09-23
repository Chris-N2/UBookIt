using Microsoft.Extensions.DependencyInjection;
using UBookIt.Core.Stores;
using UBookIt.Persistence.Composing;
using UBookIt.Tests.Support;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using UBookIt.Persistence.Migrations;

namespace UBookIt.Tests;

/// <summary>
/// The site-closure migration is additive — asserted from the operations it actually
/// emits, not from reading it.
/// </summary>
/// <remarks>
/// <b>"Additive" is a claim about a file somebody can edit.</b> The persistence spec
/// requires that a migration alter and drop nothing, and a comment saying so cannot fail;
/// this enumerates the operation types and the tables they touch, so adding an
/// <c>AlterColumn</c> or a <c>DropIndex</c> here turns a sentence into a red test.
/// </remarks>
public class SiteClosureMigrationShapeTests
{
    private static IReadOnlyList<MigrationOperation> UpOperations()
        => new AddSiteClosures().UpOperations;

    [Fact]
    public void The_migration_only_creates()
    {
        var kinds = UpOperations().Select(o => o.GetType().Name).Distinct().Order(StringComparer.Ordinal);

        Assert.Equal(new[] { "CreateIndexOperation", "CreateTableOperation" }, kinds);
    }

    [Fact]
    public void It_creates_exactly_the_two_new_tables()
    {
        var created = UpOperations()
            .OfType<CreateTableOperation>()
            .Select(o => o.Name)
            .Order(StringComparer.Ordinal);

        Assert.Equal(new[] { "uBookItResourceClosureOptOut", "uBookItSiteClosure" }, created);
    }

    [Fact]
    public void Every_operation_targets_one_of_those_two_tables()
    {
        // The indexes included: an index added to an EXISTING table would be an alteration
        // of that table, which is the thing this migration must not be doing.
        var tables = UpOperations()
            .Select(o => o switch
            {
                CreateTableOperation create => create.Name,
                CreateIndexOperation index => index.Table,
                _ => "unexpected",
            })
            .Distinct()
            .Order(StringComparer.Ordinal);

        Assert.Equal(new[] { "uBookItResourceClosureOptOut", "uBookItSiteClosure" }, tables);
    }

    [Fact]
    public void The_closure_date_is_uniquely_indexed()
    {
        // The schema is what guarantees one closure per date; a check before writing is a
        // race. If this index stops being unique, the store's duplicate reporting becomes a
        // suggestion rather than a rule.
        var index = Assert.Single(
            UpOperations().OfType<CreateIndexOperation>(),
            o => o.Table == "uBookItSiteClosure");

        Assert.True(index.IsUnique);
        Assert.Equal(new[] { "Date" }, index.Columns);
    }
}

/// <summary>
/// The closure stores are registered, and the resource store can therefore be built.
/// </summary>
/// <remarks>
/// <b>A missing registration here has no compile-time symptom.</b> <c>SqlResourceStore</c>
/// takes <c>ISiteClosureStore</c>, so an unregistered closure store would leave every
/// resource read failing at resolution on a running site while the whole suite stayed
/// green — the tests construct their stores directly.
/// </remarks>
public class SiteClosureCompositionTests
{
    [Fact]
    public void The_closure_stores_are_registered_by_the_persistence_composer()
    {
        var registrations = new ServiceCollection();
        new UBookItPersistenceComposer().Compose(new ServicesOnlyUmbracoBuilder(registrations));

        var read = Assert.Single(registrations, d => d.ServiceType == typeof(ISiteClosureStore));
        var management = Assert.Single(registrations, d => d.ServiceType == typeof(ISiteClosureManagementStore));

        Assert.Equal("SqlSiteClosureStore", read.ImplementationType?.Name);
        Assert.Equal("SqlSiteClosureManagementStore", management.ImplementationType?.Name);

        // Scoped, like every other store: they share the request's DbContext.
        Assert.Equal(ServiceLifetime.Scoped, read.Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, management.Lifetime);
    }

    [Fact]
    public void The_resource_store_can_be_constructed_from_what_the_composer_registered()
    {
        // The registration list alone does not prove the graph resolves: the resource store's
        // dependency on the closure store is the new edge, and this is the assertion that it
        // can be satisfied.
        var registrations = new ServiceCollection();
        new UBookItPersistenceComposer().Compose(new ServicesOnlyUmbracoBuilder(registrations));

        var resourceStore = Assert.Single(registrations, d => d.ServiceType == typeof(IResourceStore));
        var dependencies = resourceStore.ImplementationType!
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(p => p.ParameterType);

        Assert.All(
            dependencies.Where(t => t.IsInterface),
            t => Assert.Contains(registrations, d => d.ServiceType == t));
    }
}
