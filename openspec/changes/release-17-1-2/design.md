## Context

See `proposal.md` — *Why*. The measurement it rests on was taken deliberately before the version
number was chosen, because the number depended on the answer.

**How it was measured.** A scratch site from `dotnet new umbraco` (Umbraco 18.1.0), a
`nuget.config` clearing and re-declaring the machine-level `packageSourceMapping` so nuget.org is
reachable, then `dotnet add package UBookIt --version 17.1.1` exactly as a member of the public
would. Install succeeded silently, build succeeded with 0 errors, and `dotnet run` terminated with
an unhandled `ReflectionTypeLoadException` inside `AddUmbraco()`.

**Why it fails is worth stating precisely, because the obvious explanation is wrong — and because
the first version of this paragraph was itself imprecise.** Installing the `UBookIt`
meta-package, it is *not* a missing Swashbuckle assembly: `UBookIt.Backoffice 17.1.1` derives
from two types Umbraco itself deleted in 18 — `BackOfficeSecurityRequirementsOperationFilterBase`
and `OperationIdHandler` — so the failure is a `TypeLoadException` naming
`Umbraco.Cms.Api.Management` and `Umbraco.Cms.Api.Common`. **But QA measured a second path that
does fail on Swashbuckle**: `UBookIt.Web` + `UBookIt.Persistence` without the backoffice dies with
`FileNotFoundException: Swashbuckle.AspNetCore.SwaggerGen` inside
`UBookItDeliveryApiComposer.Compose`. Two independent failures, two different causes, and the
generator swap is behind both.

**And it is earlier in startup than this change first said.** QA read the stack properly: it dies
at `Program.cs` line 4 inside `CreateUmbracoBuilder()` → `UmbracoBuilder`'s **constructor** →
`AddAllCoreCollectionBuilders` — *core collection-builder* discovery, **not** `AddComposers()`.
Two things follow, and both were open questions when the measurement was taken: **no database is
involved** (it dies before `BootUmbracoAsync()` and before any connection string is read), and
**suppressing composers cannot avoid it**, because every Umbraco site calls
`CreateUmbracoBuilder()`.

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

**The claim was broader than the measurement, and QA caught it.** "The site cannot start" was
asserted of the package line and measured only of the `UBookIt` meta-package — the install
essentially everyone performs, and the one the readme documents. QA tested the narrower
configurations and found:

| installed on Umbraco 18.1.0 | result |
|---|---|
| `UBookIt` (meta) | `TypeLoadException` in `CreateUmbracoBuilder()` — **does not start** |
| `UBookIt.Web` + `UBookIt.Persistence` | `FileNotFoundException` on Swashbuckle in the delivery composer — **does not start** |
| **`UBookIt.Persistence` alone** | **starts, listens, serves HTTP 200** |

**So the universal is false, and the case it is false for is dismissed deliberately rather than
by silence.** `UBookIt.Persistence` alone ships entities, migrations and repositories: no
endpoint, no UI, no booking flow, nothing a site or a visitor can reach. A site in that
configuration has installed a schema and nothing that uses it, and the readme says plainly that
installing the sub-packages without `UBookIt.Web` **fails silently**. It loses no working
*behaviour*, because it had none to lose — and QA's test had no database, so it is not even
established the migration would apply on 18.

That is a judgement, not a measurement, and it is written down as one. **If it is wrong, the
consequence is that a site running Persistence-alone on Umbraco 18 loses the ability to upgrade
within `17.x`** — and it should move to `18.x` anyway, which is one line in the changelog rather
than a reason to spend a minor.

**Rejected: `17.2.0`.** A minor is where a *break* goes, and calling this one would say the
package used to support something it never supported. It would also hold the fix behind a feature
release while sites can still brick themselves.

**The measurement, not the reasoning, is load-bearing.** Had the site booted — even with a broken
backoffice — this would belong in `17.2.0`, and the change would have said so.

### D2 — `[17.6.2,18.0.0)`, and the lower bound is unchanged

Only the ceiling is added; `17.6.2` is already what `Directory.Packages.props` declares. Every
Umbraco 17 patch and minor continues to resolve.

### D3 — The guard is cherry-picked from `dev/v18`, not rewritten

`Every_umbraco_dependency_names_an_upper_bound` exists on the 18 line and reads the **packed
nuspec** rather than `Directory.Packages.props`.

**It was re-proved here rather than trusted, and one of the three mutations turned out to prove
less than claimed.** A removed bound fails; a prefix matching nothing fails on the anti-vacuity
assertion. But `[17.6.2, )` — the "open at the top" case whose story is that a naive
`EndsWith(')')` would accept it — **never reaches the guard in that form**: NuGet normalises it
to the bare string `17.6.2` in the packed nuspec, so the guard sees an unbounded version and
fails for the ordinary reason. The `EndsWith(", )")` clauses are unreachable through a
props-file mutation. They are harmless and correct; the claim about what they were proved
against was not. **The same sentence sits on `dev/v18` and is wrong there too** — recorded in
§9 as an obligation rather than silently fixed on one line only.

Rewriting it here would produce a second implementation of one requirement, and the two would
drift. It comes across as-is; only the expected major differs, and the guard does not hard-code
one — it asserts that a bound *exists*, which is the requirement's wording.

### D6 — The requirement's third scenario gets a guard, and it is a NEW test rather than a widened one

`Every_umbraco_dependency_names_an_upper_bound` asserts only that *a* ceiling exists, so
`[17.6.2,17.7.0)` passes it — a bound that is present, wrong, and refuses every Umbraco 17 minor
this line claims to support. The requirement states both halves; only one was guarded. QA found
the gap the moment the requirement moved onto `main`, which is exactly when it became ours.

`The_bound_admits_this_major_and_excludes_the_next` covers it, **derived from the declared
version** rather than hard-coding `17`, so a future major cannot leave it behind — the same
mechanism the readme's image pin uses.

**Written as a sibling rather than folded into the cherry-picked guard**, deliberately: D3's
whole point is that the cherry-picked method stays byte-identical across the two lines, and
widening it here would break that on the first edit.

**It is proved in one direction and unreachable in the other, which is worth stating rather than
implying two.** A ceiling inside the major (`[17.6.2,17.7.0)`) fails it, naming the dependency
and the expected ceiling. A *floor* in the wrong major (`[16.0.0,18.0.0)`) never reaches the
test at all — NuGet rejects it at restore with `NU1107`, because Umbraco 16's own transitive
constraints conflict with 17.6.2. The floor check is therefore defensive rather than
demonstrated, which is the same shape as the `[17.6.2, )` finding in D3: **a clause can be
correct and still have no reachable path through the layer the guard reads.**

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

## §9 — obligations this change creates on the OTHER line

**Two things now exist on `main` and not on `dev/v18`**, which is the mirror image of the problem
QA's first finding was about. Recorded here rather than left to be rediscovered:

1. **`The_bound_admits_this_major_and_excludes_the_next`** (D6). `dev/v18` holds the same
   requirement with the same uncovered scenario, and its bound could drift to `[18.2.0,18.3.0)`
   with every guard green.
2. **D3's mutation claim is wrong on `dev/v18` too.** Its `run-on-umbraco-18` record says the
   `[18.2.0, )` mutation proves the `EndsWith(", )")` clause; NuGet normalises that away before
   the guard sees it. The clause is fine, the sentence about it is not, and it is archived — so
   the correction belongs wherever that line's next release records its inherited claims.

**Neither blocks this release.** Both are the same species as the README/`CLAUDE.md` cherry-picks
that `release-18-0-0` carried: a statement or a guard true of both lines, living on one.
