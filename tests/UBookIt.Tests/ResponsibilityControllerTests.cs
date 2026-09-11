using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Backoffice.Controllers;
using UBookIt.Backoffice.Models;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Persistence.Responsibility;

namespace UBookIt.Tests;

/// <summary>
/// Responsibility endpoints over an in-memory store: round-trip with annotations,
/// replace-not-merge, the missing-subject refusal, dangling keys accepted and marked, the
/// unknown-kind validation failure, and the structural authorization assertion every
/// management controller carries. (Anonymous rejection is enforced by the shared base
/// controller's policy; the store semantics themselves are integration-tested against
/// real SQL Server in <c>ResponsibilityStoreTests</c>.)
/// </summary>
public class ResponsibilityControllerTests
{
    private static readonly Guid ResourceId = Guid.NewGuid();
    private static readonly Guid ServiceId = Guid.NewGuid();

    private static (ResponsibilityController Controller, InMemoryResponsibilityStore Store, FakeDescriber Describer) Wire()
    {
        var store = new InMemoryResponsibilityStore(
            resources: [ResourceId], services: [ServiceId]);
        var describer = new FakeDescriber();

        return (new ResponsibilityController(store, describer), store, describer);
    }

    private static ResponsibilityRequestModel Request(params (string Kind, Guid Key)[] assignments)
        => new()
        {
            Assignments = assignments
                .Select(a => new ResponsibilityAssignmentModel { Kind = a.Kind, Key = a.Key })
                .ToList(),
        };

    private static T Ok<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);

    private static (int Status, string[] Codes) Problem(IActionResult result)
    {
        var obj = Assert.IsType<ObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(obj.Value);
        var errors = Assert.IsType<ApiErrorModel[]>(problem.Extensions["errors"]);
        return (obj.StatusCode!.Value, errors.Select(e => e.Code).ToArray());
    }

    [Fact]
    public async Task Assignments_round_trip_with_annotations()
    {
        var (controller, _, describer) = Wire();
        var userKey = Guid.NewGuid();
        var groupKey = Guid.NewGuid();
        describer.Users[userKey] = ("Ada Lovelace", "Active");
        describer.Groups[groupKey] = "Studio Team";

        var put = Ok<ResponsibilityResponseModel>(await controller.PutResourceResponsibility(
            ResourceId, Request(("user", userKey), ("group", groupKey))));
        var got = Ok<ResponsibilityResponseModel>(await controller.GetResourceResponsibility(ResourceId));

        foreach (var response in new[] { put, got })
        {
            Assert.Equal(2, response.Assignments.Count);

            var user = Assert.Single(response.Assignments, a => a.Kind == "user");
            Assert.Equal(userKey, user.Key);
            Assert.True(user.Exists);
            Assert.Equal("Ada Lovelace", user.DisplayName);
            Assert.Equal("Active", user.UserState);

            var group = Assert.Single(response.Assignments, a => a.Kind == "group");
            Assert.Equal("Studio Team", group.DisplayName);
            Assert.Null(group.UserState);
        }
    }

    [Fact]
    public async Task A_service_subject_takes_the_same_shape()
    {
        var (controller, _, describer) = Wire();
        var userKey = Guid.NewGuid();
        describer.Users[userKey] = ("Grace Hopper", "Active");

        Ok<ResponsibilityResponseModel>(await controller.PutServiceResponsibility(
            ServiceId, Request(("user", userKey))));

        var got = Ok<ResponsibilityResponseModel>(await controller.GetServiceResponsibility(ServiceId));

        Assert.Equal(userKey, Assert.Single(got.Assignments).Key);
    }

    [Fact]
    public async Task Writing_replaces_rather_than_merges()
    {
        var (controller, _, _) = Wire();
        var survivor = Guid.NewGuid();

        Ok<ResponsibilityResponseModel>(await controller.PutResourceResponsibility(
            ResourceId, Request(("user", Guid.NewGuid()), ("user", Guid.NewGuid()), ("group", Guid.NewGuid()))));
        Ok<ResponsibilityResponseModel>(await controller.PutResourceResponsibility(
            ResourceId, Request(("user", survivor))));

        var got = Ok<ResponsibilityResponseModel>(await controller.GetResourceResponsibility(ResourceId));

        Assert.Equal(survivor, Assert.Single(got.Assignments).Key);
    }

    [Theory]
    [InlineData("resource")]
    [InlineData("service")]
    public async Task Writing_to_a_missing_subject_is_404_and_stores_nothing(string subject)
    {
        var (controller, store, _) = Wire();
        var missing = Guid.NewGuid();

        var result = subject == "resource"
            ? await controller.PutResourceResponsibility(missing, Request(("user", Guid.NewGuid())))
            : await controller.PutServiceResponsibility(missing, Request(("user", Guid.NewGuid())));

        var (status, codes) = Problem(result);

        Assert.Equal(404, status);
        Assert.Equal(
            subject == "resource" ? FailureCodes.ResourceNotFound : FailureCodes.ServiceNotFound,
            Assert.Single(codes));
        Assert.Empty(store.All);
    }

    [Fact]
    public async Task A_dangling_party_key_is_accepted_and_marked()
    {
        var (controller, _, _) = Wire();

        // The describer knows nothing about this key: the party was deleted, or never
        // existed. The save succeeds — a save must not fail for racing a deletion — and
        // the response marks the assignment as not resolving rather than dropping it.
        var put = Ok<ResponsibilityResponseModel>(await controller.PutResourceResponsibility(
            ResourceId, Request(("user", Guid.NewGuid()))));

        var assignment = Assert.Single(put.Assignments);

        Assert.False(assignment.Exists);
        Assert.Null(assignment.DisplayName);
    }

    [Fact]
    public async Task An_unknown_kind_is_400_and_stores_nothing()
    {
        var (controller, store, _) = Wire();

        var (status, codes) = Problem(await controller.PutResourceResponsibility(
            ResourceId, Request(("User", Guid.NewGuid()))));

        // "User" exactly — the casing mistake a caller actually makes — refused rather
        // than silently normalized, matching the package's other normalized keys.
        Assert.Equal(400, status);
        Assert.Equal(ResponsibilityController.UnknownPartyKind, Assert.Single(codes));
        Assert.Empty(store.All);
    }

    [Fact]
    public void Controller_inherits_the_authorized_backoffice_base()
    {
        Assert.True(typeof(UBookItBackofficeApiControllerBase).IsAssignableFrom(typeof(ResponsibilityController)));
        Assert.NotNull(
            typeof(UBookItBackofficeApiControllerBase)
                .GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>(inherit: true));
    }

    // ---- doubles ----

    /// <summary>
    /// Mirrors the SQL store's contract: wholesale replace, distinct, missing-subject
    /// refusal with the same failure codes. The real semantics are integration-tested;
    /// this exists so the controller's mapping can be exercised without a database.
    /// </summary>
    private sealed class InMemoryResponsibilityStore(Guid[] resources, Guid[] services) : IResponsibilityStore
    {
        private readonly Dictionary<(ResponsibilitySubject, Guid), List<ResponsibilityAssignment>> _assignments = [];

        public IReadOnlyList<ResponsibilityAssignment> All
            => _assignments.Values.SelectMany(a => a).ToList();

        public Task<IReadOnlyList<ResponsibilityAssignment>> GetAsync(
            ResponsibilitySubject subject, Guid subjectId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ResponsibilityAssignment>>(
                _assignments.TryGetValue((subject, subjectId), out var stored) ? stored : []);

        public Task<DomainResult> ReplaceAsync(
            ResponsibilitySubject subject,
            Guid subjectId,
            IReadOnlyCollection<ResponsibilityAssignment> assignments,
            CancellationToken cancellationToken = default)
        {
            var exists = subject == ResponsibilitySubject.Resource
                ? resources.Contains(subjectId)
                : services.Contains(subjectId);

            if (!exists)
            {
                return Task.FromResult(DomainResult.Failure(
                    subject == ResponsibilitySubject.Resource
                        ? FailureCodes.ResourceNotFound
                        : FailureCodes.ServiceNotFound,
                    "No such subject."));
            }

            _assignments[(subject, subjectId)] = assignments.Distinct().ToList();
            return Task.FromResult(DomainResult.Success());
        }

        public Task<bool> HasAnyForBookingAsync(Booking booking, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The controller never asks about bookings.");

        public Task<IReadOnlyList<ResponsibilityAssignment>> GetForBookingAsync(
            Booking booking, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The controller never asks about bookings.");
    }

    private sealed class FakeDescriber : IResponsibleRecipientResolver
    {
        public Dictionary<Guid, (string Name, string State)> Users { get; } = [];

        public Dictionary<Guid, string> Groups { get; } = [];

        public Task<IReadOnlyList<ResponsibilityPartyStatus>> DescribeAsync(
            IReadOnlyCollection<ResponsibilityAssignment> assignments,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ResponsibilityPartyStatus>>(assignments
                .Select(a => a.Kind == ResponsibilityPartyKind.User
                    ? Users.TryGetValue(a.Key, out var user)
                        ? new ResponsibilityPartyStatus(a, true, user.Name, user.State)
                        : new ResponsibilityPartyStatus(a, false, null, null)
                    : Groups.TryGetValue(a.Key, out var name)
                        ? new ResponsibilityPartyStatus(a, true, name, null)
                        : new ResponsibilityPartyStatus(a, false, null, null))
                .ToList());

        public Task<bool> HasAssignmentsAsync(Booking booking, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The controller never asks about bookings.");

        public Task<IReadOnlyList<string>> ResolveAddressesAsync(
            Booking booking, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The controller never resolves addresses.");
    }
}
