## Context

Five approved changes have accumulated on `main` since `17.0.1`: `admin-settings-screen`,
`move-booking`, `booking-on-behalf`, `find-booking` and `self-service-cancellation`. Each was
QA-approved and archived; none has shipped. The release turns that pile into a version number.

Two constraints shape everything below.

**A version number is a one-way door.** `17.1.0` is spent the moment it is pushed, successfully or
not, and the packed README and every piece of package metadata are frozen at push. This repository
has already paid one version number for learning that, which is why `docs/publishing.md` exists and
why verification happens against the produced `.nuspec` rather than the source.

**The interesting content is not the bump.** The bump is three files and the suite enforces it.
What is missing is any place where a consumer learns that five published interfaces gained members
with no default implementation, so an upgrade that should be routine is a compile error for anyone
who implements a port — and that three of the five features become available the moment a site
upgrades, without anyone enabling anything.

## Goals / Non-Goals

**Goals.** Declare `17.1.0`. Give a consumer a per-release account of what upgrading asks of them,
starting with the contract changes. Guard that account so it cannot silently stop being
written. Leave the tree in a state where the runbook can be followed by a human.

**Non-goals.** Publishing (Chris runs the runbook). CI. Retrofitting tags for `17.0.0`/`17.0.1`.
`<PackageReleaseNotes>`. Rewriting the Status section of `docs/publishing.md` — see D5.

## Decisions

### D1 — A `CHANGELOG.md`, not release notes in the package metadata

`<PackageReleaseNotes>` is the obvious home and the wrong one. It is frozen per version like the
rest of the metadata, so a typo in it costs a version number; and keeping it in step with a
changelog means maintaining two copies of a document that changes, which is precisely the "two
versions of the truth" the version declaration's own comment warns about.

A single `CHANGELOG.md` in the repository, reachable from the package page through the README's
existing absolute links, has neither problem. The cost is one indirection for the reader. Deferred
rather than rejected: once the changelog has survived a release, a *link* to it in the metadata is
safe, because a link does not go stale when its target is edited.

### D2 — The entry leads with the obligation, not the feature list

The natural way to write a release note is to list what was added. For `17.1.0` that buries the
only sentence that matters — that a site implementing any of the five affected ports **will not
compile** until it adds the new members.

So each entry opens with what upgrading asks of the reader, and only then says what they gain. A
reader who stops after the first block has the part they cannot afford to miss. This is why the
spec requires the *obligation* to be stated rather than only the change: "gained a member" is true
and does not warn anybody.

**The obligation is not only compilation.** QA round 1 found the first draft asserting that "every
feature is off until you turn it on", which was true of one feature out of five: move, book-on-behalf
and booking lookup are gated by verbs the permission seed had already granted, so they arrive live.
A sentence promising a quiet upgrade is worse than no sentence, because a site that reads it does
not go and look.

### D3 — The changelog is guarded, and the guard is about presence, not content

A release note nobody checks is stale at the next release. The guard therefore asserts that the
version `Directory.Build.props` declares has an entry, and that the entry is not empty — failing
with the version it could not find, exactly as `VersionTruthTests` fails with the document that
still states the old version.

**Deliberately not guarded: whether the entry is true or complete.** No test can know that
`17.1.0` contains ten contract changes rather than nine — and QA proved that exactly, by
deriving the set independently and finding the first draft short by an entire interface. Claiming otherwise would repeat this
project's most expensive mistake — a guard whose name promises more than it reads. The guard keeps
the document *present*; a human keeps it honest, and the spec says so rather than implying
coverage it does not have.

### D4 — Earlier releases get short honest entries, and never move again

`17.0.0` and `17.0.1` predate the changelog. Reconstructing detailed notes for them from the
archive would be inventing a document that never existed; omitting them leaves a changelog whose
first entry is `17.1.0`, which reads as though nothing came before.

Two short entries, stating what each release actually was — a first release, and a fix for nine
relative README links that resolved against nuget.org. Once written they are history, on the same
terms as the version anchors in `README.md` and `docs/publishing.md`: **an entry for a release that
has happened is never edited forward.** A changelog that can be rewritten backwards records
nothing, and this repository already has a guard family built on exactly that principle.

### D5 — The Status section of `docs/publishing.md` is not touched

Worth stating explicitly, because `release-17-0-1` spent six tasks repairing documentation that
publication falsified, and the obvious assumption is that this release owes the same debt.

It does not. Those tasks were the one-time transition from "nothing is pushed" to "something is
pushed" — re-premising a guard that asserted the package had never reached a feed, repairing a
runbook sentence written in the future tense about an event that had now happened. That transition
has already occurred. The Status section says uBookIt *reached* nuget.org on 2026-09-15 with
`17.0.0` as the first release, which is history and stays true when `17.1.0` publishes.

Checked rather than assumed: the section's two pinned properties are that it names **nuget.org**
(so the accounting guard counts it) and states the version in the *first release* phrasing (so it
is pinned to the archive, not to the declared version). Publishing `17.1.0` changes neither.

### D6 — The bump moves two prose claims and leaves two alone

Measured rather than reasoned: setting `<Version>` to `17.1.0` and running the suite names exactly
`README.md` and `docs/publishing.md`. The two sentences that stay are `"the first release is
17.0.0"` and `"17.0.1 exists because of it"` — history, true because they do not move, and pinned
against `openspec/changes/archive/*-release-<version>`.

A repo-wide find-and-replace moves all four together and leaves them agreeing, which is why the
pin is to the archive rather than to the other anchors. If one of those two goes red, the fix is to
put it back, never to edit it forward.

### D7 — The breaking set is derived from the compiled surface, never from the proposals

**Added after QA round 1 rejected this change on exactly this point.** The first draft named four
interfaces. There are five. `IBookingManagementStore.FindByReferenceAsync` was missing entirely,
and two of `IServiceBookingService`'s three additions and three of `IBookingService`'s four were
collapsed into single mentions.

The cause is worth recording, because it was not carelessness — it was a *reasonable-sounding
method*. The set was transcribed from the archived proposals, which is where a break is supposed to
be declared. But `find-booking`'s proposal contains no `BREAKING` line at all, so no amount of care
reading proposals could have found that port; and `booking-on-behalf` described its port additions
under a heading about a **defaulted** observer member, which is how three no-default members read as
none. A source that is complete *by convention* is not complete.

So the rule: **the compiled interface surface is the authority, and the diff is the instrument.**

```
for each `public interface` in src/**/*.cs:
    members at HEAD minus members at the previous release commit,
    keeping only those whose declaration ends in `;` (no default body),
    excluding interfaces that did not exist at that commit (a new
    interface breaks nobody).
```

Run against `f831986` this yields five interfaces and ten members, and additionally shows that
`IBookingObserver.BookingPlacedOnBehalfAsync` **is** defaulted — a fact the entry states, because an
implementer needs to know which additions they can ignore. `ICancellationSecretStore` is excluded as
new in this release.

This is the same lesson as [[a-statement-that-describes-what-is-not-there]] one level up: not a
comment describing code it did not have, but a *document* describing a surface it had never read.

## Risks / Trade-offs

**The changelog can be complete and wrong.** D3 accepts this: the guard proves presence only, and
D7 records how the first draft was both — confidently transcribed, and missing an interface. The
mitigation is now mechanical rather than editorial: the set is derived by diff, and the command is
in D7 so the next release derives it the same way.

**A reader may never find it.** The changelog lives in the repository, one link from the package
page. Accepted, and the reason the README's API-promise callout gains the pointer — the sentence
promising breaks are called out should sit next to where they are.

**Publishing may still fail for reasons this change cannot prevent.** Key ownership, feed
validation, a stale artifact. All are runbook territory and all have cost something before; none is
made worse by this change.

## Migration Plan

None for the repository.

For a consuming site, `17.0.1` → `17.1.0` has two distinct costs, and the second was missed in the
first draft of this document.

**Compilation.** A site implementing `IBookingObserver`, `IBookingStore`, `IBookingManagementStore`,
`IServiceBookingService` or `IBookingService` must add the new members before it builds.

**Behaviour, without anyone turning anything on.** Move, book-on-behalf and booking lookup are
gated by `BookingsManage`/`BookingsRead`, which the permission seed already granted before this
release — so on upgrade every group holding those verbs gains the new operations and the bookings
screen shows them. On-behalf additionally requires Umbraco's *Sensitive data* group. A moved booking
sends the booker a `BookerMoved` message that did not previously exist. Only the settings screen
(its `Settings` verb is deliberately outside the seeded set) and self-service cancellation (flag,
default off) are inert on upgrade. **"Behind a flag that defaults off" was true of one of the five
features, not of all of them**, and stating it of all of them would have told a site the upgrade was
quieter than it is.

## Open Questions

**Tags.** `17.0.0` and `17.0.1` were never tagged, and dating tags after the fact invents history.
Tagging `17.1.0` at release would start the practice honestly while leaving the gap visible.
Deferred to Chris rather than decided here, because it is a repository-history decision rather than
a packaging one.
