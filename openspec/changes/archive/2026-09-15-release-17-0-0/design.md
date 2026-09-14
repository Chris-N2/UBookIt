# Design — release-17-0-0

## D1 — One version, and the prose is checked against it rather than restating it

`Directory.Build.props`'s `<Version>` is already the single source: every package takes it,
and `UBookIt.Backoffice.csproj` stamps it into `umbraco-package.json` at build time with a
build-time failure if the `"version": "0.0.0"` placeholder is missing. Nothing else declares
a version, so the bump is one edit.

What is missing is the other direction: **the prose that names the version is read by
nothing.** So the guard derives the expected version by parsing `<Version>` out of
`Directory.Build.props` and asserts the documents state it — never by hard-coding
`17.0.0` in the test, which would merely move the drift one file along. A future bump to
`17.1.0` then fails loudly in the documents that must move with it.

The guard's honest limit, stated because this project has been bitten by guards claiming
more than they check: it proves the *number* in the prose matches, not that the sentences
around it are true. The sentences are covered separately by `DocumentationAssert`, the
house idiom for a claim a site owner acts on.

## D2 — The policy lives in the README, and nowhere else

The README is where a consumer meets the version, so it is where the version's meaning
belongs. A `docs/versioning.md` was considered and declined: the policy's practical
consequence for a reader is three sentences ("the major tracks Umbraco; a minor may
break with an upgrade path; a patch never breaks"), and a page created to hold three
sentences becomes the second place the truth lives — the drift this repository has paid
for repeatedly.

**The SemVer deviation is stated, not implied.** Under this scheme the major is spent on
the Umbraco major, so "major means breaking" is unavailable; a reader who assumes SemVer
and is not told would plan an upgrade wrongly. The wording therefore names SemVer and
says where uBookIt departs from it, rather than describing the rule and leaving the
reader to notice the difference.

`CLAUDE.md`'s Conventions section is updated alongside, because it currently states the
compatibility promise without the minor-version rule — the repository's own instructions
should not be the stalest account of its own policy.

## D3 — `docs/mvp.md` becomes a historical record, deliberately rather than by neglect

The document instructs its own future: *"a living record until v1 ships, and then it
becomes the README's account of what the package is for."* Read literally that is a
fold-into-README-and-delete. **Declined, with reasons:** it is cited by several archived
changes and swept by a live test (`NotificationDocumentationTests`), so deleting it
breaks real references; and its content — what was deliberately *not* in v1, and why — is
scope history the README should not carry, because a README describes what the package
does rather than what it once chose not to do.

So it is marked as the historical record it now is, and its version sentence corrected.
The alternative — leaving it saying "the first version is `0.1.0`" — is the exact shape of
defect this change exists to fix, one file over.

## D4 — The repository URLs are knowingly deferred, and the deferral is recorded twice

`PackageProjectUrl` / `RepositoryUrl` name Azure DevOps; the repository is moving to
GitHub. The URL is not guessable, so it is **not guessed**. It is recorded in the tasks as
a blocker on publication (not on merge) and reported to Chris, because a package published
with them uncorrected sends every consumer to a repository they cannot open — a defect that
only exists after the act this change is preparing for.

This is the one place the change knowingly leaves something wrong, and it is wrong only
for a future that has not happened yet.

## D5 — What this change must not do

No behaviour, no schema, no API surface. If shipping 17.0.0 seemed to require one, the
honest response is that the release is not ready — not that the release change grew. The
verification is therefore the existing suites staying green at the same counts, plus the
new guards.
