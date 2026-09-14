# Design — github-move-urls

## D1 — The guard is replaced, not deleted

`The_publication_blocker_stands_while_the_private_urls_do` asserts a biconditional: while the
URLs are the Azure ones, the warning must stand; when they are fixed, the warning must go. It
is *designed* to fail here, and the temptation it creates is the interesting part — **the
cheapest way to make it pass is to delete it**, which would satisfy the test suite while
throwing away the only thing watching the package's public identity.

So it is replaced by a guard for the property the release now depends on: the two URLs are
**public, `https`, and name the repository the source actually lives in**. That is a positive
property rather than the absence of a bad one, so it keeps working after the move instead of
becoming vacuous the moment it passes.

**What it deliberately does not do:** reach the network. A test that fetched the URL would be
slow, flaky offline, and would start failing for reasons that have nothing to do with this
repository — a private repo, a rate limit, a DNS hiccup. Reachability is a human check at
publish time and the runbook says so.

## D2 — The order is the deliverable, because SourceLink follows the remote

Two mechanisms, opposite sources, and only one of them is edited here:

| Surface | Derived from | Corrected by |
|---|---|---|
| `PackageProjectUrl` / `RepositoryUrl` in the `.nupkg` | `Directory.Build.props` | this change |
| `AssemblyMetadataAttribute("RepositoryUrl", …)` | the same properties | this change, **on rebuild** |
| SourceLink URLs in the `.snupkg` | **the git remote** | switching the remote, **on rebuild** |

So a package built *before* the remote switch carries Azure SourceLink URLs even with this
change applied, and a package built *after* it but without a clean rebuild may carry stale
`obj/` artifacts. **The runbook's order is therefore load-bearing, not tidiness:** correct the
properties, switch the remote, then rebuild and repack from clean — in that order, and verify
the emitted `sourcelink.json` rather than assuming.

This change cannot verify its own SourceLink outcome, because the remote is not ours to switch.
That is recorded as an explicit ordering dependency with a verification step Chris can run,
rather than quietly assumed to work.

## D3 — The runbook records what cannot be undone

`docs/publishing.md` is maintainer-facing and exists for one reason: **a first publish has
one-way doors, and one-way doors deserve a checklist.** nuget.org will not let a pushed
version's metadata be edited and will not let a version number be reused, so the mistakes it
covers are the ones that cost a version rather than a commit.

It also states, up front, that **the publication-claim guard is EXPECTED to fail in the change
that publishes** — `AcceptedPublicationMentions` is built to absorb claims that become true one
at a time, each with a reason and a count. Whoever runs that change should read the failure as
the checklist it is. Without this sentence the natural reading of a red test is "delete it",
which is precisely the failure mode D1 describes.

## D4 — What this change must not do

No behaviour, no schema, no API surface. If correcting a URL seemed to require one, the honest
response is that something else is wrong.
