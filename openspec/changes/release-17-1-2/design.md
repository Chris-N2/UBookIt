## Context

See `proposal.md` — *Why*. The measurement it rests on was taken deliberately before the version
number was chosen, because the number depended on the answer.

**How it was measured.** A scratch site from `dotnet new umbraco` (Umbraco 18.1.0), a
`nuget.config` clearing and re-declaring the machine-level `packageSourceMapping` so nuget.org is
reachable, then `dotnet add package UBookIt --version 17.1.1` exactly as a member of the public
would. Install succeeded silently, build succeeded with 0 errors, and `dotnet run` terminated with
an unhandled `ReflectionTypeLoadException` inside `AddUmbraco()`.

**Why it fails is worth stating precisely, because the obvious explanation is wrong.** It is *not*
a missing Swashbuckle assembly. `UBookIt.Backoffice 17.1.1` derives from two types that Umbraco
itself deleted in 18 — `BackOfficeSecurityRequirementsOperationFilterBase` and
`OperationIdHandler` — which is why the failure is a `TypeLoadException` naming
`Umbraco.Cms.Api.Management` and `Umbraco.Cms.Api.Common` rather than Swashbuckle. The generator
swap is the reason those types went; it is not the proximate cause.

**`UBookIt.Backoffice 17.1.1` never declared Swashbuckle at all** — its published nuspec lists
only the two uBookIt packages and four `Umbraco.Cms.*` ones. It relied on Umbraco 17 bringing the
generator transitively.

## Goals / Non-Goals

**Goals**

- A site on Umbraco 18 cannot install `17.1.2`, and is told why by its package manager.
- The 17 line complies with a `packaging` requirement that already binds it.
- Nothing changes for a site on Umbraco 17.

**Non-Goals**

- Not repairing the past. `17.0.0`–`17.1.1` are immutable.
- Not making `17.x` run on Umbraco 18. That is what `18.x` is for, and it took a whole change.

## Decisions

### D1 — This is a patch, and the evidence is what makes it one

The project's rule is that a patch never breaks, and CLAUDE.md's test is that **an existing site
keeps working**. The bound stops something that restores today, so the rule turns entirely on
whether that something ever worked.

**It does not work. The site does not start.** So no site loses a working configuration; a site
loses the ability to *reach* a configuration that has never functioned. What actually changes for
a reader is the failure's shape:

| before | after |
|---|---|
| installs, builds, then an unhandled `TypeLoadException` at boot, naming Umbraco's internals | NuGet refuses the package and names the constraint |

That is strictly better and strictly not a break.

**Rejected: `17.2.0`.** A minor is where a *break* goes, and calling this one would say the
package used to support something it never supported. It would also hold the fix behind a feature
release while sites can still brick themselves.

**The measurement, not the reasoning, is load-bearing.** Had the site booted — even with a broken
backoffice — this would belong in `17.2.0`, and the change would have said so.

### D2 — `[17.6.2,18.0.0)`, and the lower bound is unchanged

Only the ceiling is added; `17.6.2` is already what `Directory.Packages.props` declares. Every
Umbraco 17 patch and minor continues to resolve.

### D3 — The guard is cherry-picked from `dev/v18`, not rewritten

`Every_umbraco_dependency_names_an_upper_bound` exists on the 18 line, reads the **packed
nuspec** rather than `Directory.Packages.props`, and was proved by three mutations — a removed
bound, a range left open at the top (`[17.6.2, )`, which a naive `EndsWith(')')` accepts), and a
prefix matching nothing.

Rewriting it here would produce a second implementation of one requirement, and the two would
drift. It comes across as-is; only the expected major differs, and the guard does not hard-code
one — it asserts that a bound *exists*, which is the requirement's wording.

### D4 — The changelog entry leads with the restore failure

`ChangelogTests` requires an entry that says what upgrading asks of the reader. Here the honest
answer is unusual: for almost everybody, nothing. For the one reader it does affect — a site on
Umbraco 18 pinning `17.x` — a restore that used to succeed now fails, and they must be told that
this is the package refusing a combination that cannot run, with `18.x` named as where they
should be.

**Saying "nothing to do" and stopping would be false for exactly the people this release is for.**

### D5 — Release order is unchanged

`docs/publishing.md` holds it and it is load-bearing: push → tag → pack → publish → confirm per
package on the flat-container → stamp the changelog date → commit → sync → archive.

## Risks / Trade-offs

- **A site on Umbraco 18 pinning `17.x` sees a restore break with no obvious cause** → the
  changelog entry is written for that reader specifically (D4), and the NuGet error names the
  constraint, which the boot-time crash never did.
- **The bound is wrong if some future Umbraco 17.x is incompatible** → not a risk this introduces:
  the lower bound already claimed 17.6.2+, and the ceiling only excludes 18.
- **Two guards for one requirement if D3 is ignored** → cherry-pick, do not rewrite.
- **`17.0.0`–`17.1.1` stay installable into Umbraco 18 forever** → unavoidable and recorded.
  Anyone landing on those pages sees a version with no bound; only the changelog and readme of
  later versions can say anything.
- **The retaken `bookings-screen.png` belongs to the 18 line only** → the 17 line keeps its own
  image, and the version bump will repin it to `17.1.2` through the existing guard. The two lines
  holding different screenshots is correct, not drift.

## Migration Plan

For a site on Umbraco 17: upgrade to `17.1.2` normally. No schema change, no API change, no
behaviour change.

For a site on Umbraco 18 running `17.x`: it is not running — it cannot start. Move to
`UBookIt 18.x`, which is what that line exists for.

## Open Questions

- **Whether the two `packaging` requirements `release-18-0-0` added about readme refs should also
  be satisfied on this line now.** The 17 readme still names `blob/main`, which is correct *today*
  on `main` and becomes wrong the moment the lines' documentation diverges. Deliberately out of
  scope here — this change exists to stop sites bricking — but it is a real obligation and the
  next 17-line release should take it.
