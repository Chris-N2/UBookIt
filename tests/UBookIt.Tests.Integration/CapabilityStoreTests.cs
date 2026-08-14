using Microsoft.EntityFrameworkCore;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;
using UBookIt.Persistence.Stores;
using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

/// <summary>
/// Capability persistence against a real SQL Server: round-trips, schema-level
/// uniqueness, cascade cleanup, and the two management projections
/// (persistence spec, "Capability hydration on the read path" and "Capability
/// projections on the management store").
/// </summary>
[Collection(SqlServerCollection.Name)]
public class CapabilityStoreTests(SqlServerFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Resource Person(string name, params string[] capabilities)
        => Resource.Create("cap-therapist", name, capabilities: capabilities).Value;

    private static Service Svc(string name, params string[] required)
        => Service.Create(
            name,
            null,
            [
                new ServiceRole("cap-therapist", 1)
                {
                    RequiredCapabilities = CapabilitySet.Create(required).Value,
                }
            ]).Value;

    [Fact]
    public async Task Resource_capabilities_round_trip()
    {
        fixture.EnsureAvailable();

        var resource = Person($"Mary {Guid.NewGuid():N}", "cert-x", "massage");

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await new SqlResourceManagementStore(context).CreateAsync(resource, Ct)).Succeeded);
        }

        await using (var context = fixture.CreateContext())
        {
            var reloaded = await new SqlResourceStore(context).GetAsync(resource.Id, Ct);

            Assert.NotNull(reloaded);
            Assert.Equal(resource.Capabilities, reloaded.Capabilities);
        }
    }

    [Fact]
    public async Task An_empty_capability_set_round_trips_as_empty_not_null()
    {
        fixture.EnsureAvailable();

        var resource = Person($"Joan {Guid.NewGuid():N}");

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await new SqlResourceManagementStore(context).CreateAsync(resource, Ct)).Succeeded);
        }

        await using (var context = fixture.CreateContext())
        {
            var reloaded = await new SqlResourceStore(context).GetAsync(resource.Id, Ct);

            Assert.NotNull(reloaded);
            Assert.True(reloaded.Capabilities.IsEmpty);
        }
    }

    [Fact]
    public async Task Capabilities_are_hydrated_by_the_list_by_type_read_the_candidate_loop_uses()
    {
        // The eligibility subset test runs in Core over whatever this read
        // returns, so a missing include would silently empty every pool rather
        // than fail (design D5).
        fixture.EnsureAvailable();

        var type = $"cap-type-{Guid.NewGuid():N}"[..24];
        var resource = Resource.Create(type, "Hydrated", capabilities: ["cert-x"]).Value;

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await new SqlResourceManagementStore(context).CreateAsync(resource, Ct)).Succeeded);
        }

        await using (var context = fixture.CreateContext())
        {
            var listed = await new SqlResourceStore(context).ListByTypeAsync(type, Ct);

            Assert.Equal(["cert-x"], Assert.Single(listed).Capabilities.Keys);
        }
    }

    /// <summary>
    /// Every read that publishes capabilities must hydrate them, not only the
    /// two the candidate loop uses.
    /// <para>
    /// These exist because deleting any one `Include`/`ThenInclude` on a paged
    /// list path left the whole suite green (QA finding). The consequence is
    /// not cosmetic: the anonymous `GET /resources` and `GET /services` run
    /// through these list reads, and silently publishing an empty capability
    /// collection would make the derivable pool a strict superset of the real
    /// one — at which point `resource-not-eligible` starts disclosing something
    /// a caller could not compute, and ⑦-2's D9 is reopened invisibly.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Resource_list_reads_hydrate_capabilities_on_both_ports()
    {
        fixture.EnsureAvailable();

        var name = $"Listed {Guid.NewGuid():N}";
        var resource = Person(name, "cert-x", "massage");

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await new SqlResourceManagementStore(context).CreateAsync(resource, Ct)).Succeeded);
        }

        await using (var context = fixture.CreateContext())
        {
            // The read port's paged list — behind the anonymous GET /resources.
            var readPage = await new SqlResourceStore(context).ListAsync(0, 500, Ct);
            var fromRead = readPage.Items.Single(r => r.Id == resource.Id);
            Assert.Equal(resource.Capabilities, fromRead.Capabilities);

            // The management port's paged list — behind the backoffice grid.
            var managementPage = await new SqlResourceManagementStore(context).ListAsync(0, 500, Ct);
            var fromManagement = managementPage.Items.Single(r => r.Id == resource.Id);
            Assert.Equal(resource.Capabilities, fromManagement.Capabilities);
        }
    }

    [Fact]
    public async Task Service_list_reads_hydrate_required_capabilities_on_both_ports()
    {
        fixture.EnsureAvailable();

        var service = Svc($"Listed {Guid.NewGuid():N}", "cert-x", "welsh");

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await new SqlServiceManagementStore(context).CreateAsync(service, Ct)).Succeeded);
        }

        await using (var context = fixture.CreateContext())
        {
            var expected = service.Roles[0].RequiredCapabilities;

            // The read port's paged list — behind the anonymous GET /services.
            var readPage = await new SqlServiceStore(context).ListAsync(0, 500, Ct);
            var fromRead = readPage.Items.Single(s => s.Id == service.Id);
            Assert.Equal(expected, fromRead.Roles[0].RequiredCapabilities);

            // The management port's paged list — behind the backoffice grid.
            var managementPage = await new SqlServiceManagementStore(context).ListAsync(0, 500, Ct);
            var fromManagement = managementPage.Items.Single(s => s.Id == service.Id);
            Assert.Equal(expected, fromManagement.Roles[0].RequiredCapabilities);
        }
    }

    [Fact]
    public async Task Update_replaces_the_capability_set_rather_than_merging()
    {
        fixture.EnsureAvailable();

        var name = $"Replaced {Guid.NewGuid():N}";
        var original = Person(name, "cert-x", "massage");

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await new SqlResourceManagementStore(context).CreateAsync(original, Ct)).Succeeded);
        }

        var replacement = Resource.Create(
            "cap-therapist", name, capabilities: ["massage"], id: original.Id).Value;

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await new SqlResourceManagementStore(context).UpdateAsync(replacement, Ct)).Succeeded);
        }

        await using (var context = fixture.CreateContext())
        {
            var reloaded = await new SqlResourceStore(context).GetAsync(original.Id, Ct);

            Assert.NotNull(reloaded);
            Assert.Equal(["massage"], reloaded.Capabilities.Keys);
        }
    }

    [Fact]
    public async Task Deleting_a_resource_removes_its_capability_rows()
    {
        fixture.EnsureAvailable();

        var resource = Person($"Doomed {Guid.NewGuid():N}", "cert-x");

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await new SqlResourceManagementStore(context).CreateAsync(resource, Ct)).Succeeded);
        }

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await new SqlResourceManagementStore(context).DeleteAsync(resource.Id, Ct)).Succeeded);
        }

        await using (var context = fixture.CreateContext())
        {
            var orphans = await context.ResourceCapabilities
                .Where(c => c.ResourceId == resource.Id)
                .CountAsync(Ct);

            Assert.Equal(0, orphans);
        }
    }

    [Fact]
    public async Task A_duplicate_capability_row_is_refused_by_the_schema()
    {
        // CapabilitySet deduplicates on the way in, so this can only be reached
        // by writing rows directly — which is exactly why the composite key
        // exists rather than trusting the domain to be the only writer.
        fixture.EnsureAvailable();

        var resource = Person($"Duped {Guid.NewGuid():N}", "cert-x");

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await new SqlResourceManagementStore(context).CreateAsync(resource, Ct)).Succeeded);
        }

        await using (var context = fixture.CreateContext())
        {
            await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO uBookItResourceCapability (ResourceId, [Key]) VALUES ({0}, {1})",
                    [resource.Id, "cert-x"],
                    Ct);
            });
        }
    }

    [Fact]
    public async Task Role_required_capabilities_round_trip()
    {
        fixture.EnsureAvailable();

        var service = Svc($"Massage {Guid.NewGuid():N}", "cert-x", "welsh");

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await new SqlServiceManagementStore(context).CreateAsync(service, Ct)).Succeeded);
        }

        await using (var context = fixture.CreateContext())
        {
            var reloaded = await new SqlServiceStore(context).GetAsync(service.Id, Ct);

            Assert.NotNull(reloaded);
            Assert.Equal(service.Roles[0].RequiredCapabilities, reloaded.Roles[0].RequiredCapabilities);
        }
    }

    [Fact]
    public async Task Deleting_a_service_removes_its_role_capability_rows()
    {
        fixture.EnsureAvailable();

        var service = Svc($"Doomed {Guid.NewGuid():N}", "cert-x");

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await new SqlServiceManagementStore(context).CreateAsync(service, Ct)).Succeeded);
        }

        long[] roleIds;
        await using (var context = fixture.CreateContext())
        {
            roleIds = await context.ServiceRoles
                .Where(r => r.ServiceId == service.Id)
                .Select(r => r.Id)
                .ToArrayAsync(Ct);

            Assert.NotEmpty(roleIds);
            Assert.True((await new SqlServiceManagementStore(context).DeleteAsync(service.Id, Ct)).Succeeded);
        }

        await using (var context = fixture.CreateContext())
        {
            var orphans = await context.ServiceRoleCapabilities
                .Where(c => roleIds.Contains(c.ServiceRoleId))
                .CountAsync(Ct);

            Assert.Equal(0, orphans);
        }
    }

    [Fact]
    public async Task The_usage_projection_reports_keys_with_counts_in_key_order()
    {
        fixture.EnsureAvailable();

        var suffix = $"{Guid.NewGuid():N}"[..8];
        var alpha = $"zz-a-{suffix}";
        var beta = $"zz-b-{suffix}";

        await using (var context = fixture.CreateContext())
        {
            var store = new SqlResourceManagementStore(context);
            Assert.True((await store.CreateAsync(Person($"One {suffix}", alpha, beta), Ct)).Succeeded);
            Assert.True((await store.CreateAsync(Person($"Two {suffix}", alpha), Ct)).Succeeded);
        }

        await using (var context = fixture.CreateContext())
        {
            var usage = await new SqlResourceManagementStore(context).ListCapabilitiesAsync(Ct);
            var mine = usage.Where(u => u.Key.EndsWith(suffix, StringComparison.Ordinal)).ToList();

            Assert.Collection(
                mine,
                first => { Assert.Equal(alpha, first.Key); Assert.Equal(2, first.Count); },
                second => { Assert.Equal(beta, second.Key); Assert.Equal(1, second.Count); });
        }
    }

    [Fact]
    public async Task The_match_projection_returns_resources_carrying_every_required_capability()
    {
        fixture.EnsureAvailable();

        var type = $"cap-m-{Guid.NewGuid():N}"[..20];
        var mary = Resource.Create(type, "Mary", capabilities: ["cert-x", "massage"]).Value;
        var frank = Resource.Create(type, "Frank", capabilities: ["massage"]).Value;
        var joan = Resource.Create(type, "Joan").Value;

        await using (var context = fixture.CreateContext())
        {
            var store = new SqlResourceManagementStore(context);
            foreach (var resource in new[] { mary, frank, joan })
            {
                Assert.True((await store.CreateAsync(resource, Ct)).Succeeded);
            }
        }

        await using (var context = fixture.CreateContext())
        {
            var store = new SqlResourceManagementStore(context);

            var certX = await store.ListMatchingAsync(type, CapabilitySet.Create(["cert-x"]).Value, Ct);
            Assert.Equal(["Mary"], certX.Select(m => m.DisplayName));

            var massage = await store.ListMatchingAsync(type, CapabilitySet.Create(["massage"]).Value, Ct);
            Assert.Equal(["Frank", "Mary"], massage.Select(m => m.DisplayName));

            var unconstrained = await store.ListMatchingAsync(type, CapabilitySet.Empty, Ct);
            Assert.Equal(3, unconstrained.Count);

            var absent = await store.ListMatchingAsync(type, CapabilitySet.Create(["never-tagged"]).Value, Ct);
            Assert.Empty(absent);
        }
    }

    [Fact]
    public async Task The_match_projection_agrees_with_core_candidate_resolution()
    {
        // The backoffice readout and the booking path must answer the capability
        // question identically — a readout that disagrees with the booker is
        // worse than none (design D6). The match projection is a superset: it
        // omits the duration narrowing that resolution also applies, which is
        // why the assertion is containment plus equality on the capability term.
        fixture.EnsureAvailable();

        var type = $"cap-a-{Guid.NewGuid():N}"[..20];
        var mary = Resource.Create(type, "Mary", capabilities: ["cert-x", "massage"]).Value;
        var frank = Resource.Create(type, "Frank", capabilities: ["massage"]).Value;
        var joan = Resource.Create(type, "Joan").Value;

        await using (var context = fixture.CreateContext())
        {
            var store = new SqlResourceManagementStore(context);
            foreach (var resource in new[] { mary, frank, joan })
            {
                Assert.True((await store.CreateAsync(resource, Ct)).Succeeded);
            }
        }

        await using (var context = fixture.CreateContext())
        {
            var required = CapabilitySet.Create(["massage"]).Value;

            var projected = (await new SqlResourceManagementStore(context)
                .ListMatchingAsync(type, required, Ct))
                .Select(m => m.Id)
                .OrderBy(id => id)
                .ToList();

            // The same question, asked through the read port the candidate loop
            // uses and the same Core predicate it applies.
            var resolved = (await new SqlResourceStore(context).ListByTypeAsync(type, Ct))
                .Where(r => required.IsSatisfiedBy(r.Capabilities))
                .Select(r => r.Id)
                .OrderBy(id => id)
                .ToList();

            Assert.Equal(resolved, projected);
            Assert.Equal(2, projected.Count);
        }
    }
}
