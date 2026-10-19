## Context

- The packages' `PackageProjectUrl` is `https://github.com/Chris-N2/UBookIt` and
  `<Authors>` is `Norwood Design &amp; Development Ltd.` (`Directory.Build.props:58`, `:99`).
  `origin/HEAD` is `main`.
- The Marketplace's listing docs say the file is read from the folder of the project URL, which for
  a GitHub repository means the root of the default branch. `AuthorDetails.Name` "takes precedence
  over the NuGet Authors field".
- **The listing as observed on 2026-10-19** (screenshot from Chris):
  - The author name already reads "Norwood Design & Development Ltd.", from `<Authors>`.
  - The avatar is the Marketplace's generic placeholder. The organisation's Gravatar had only just
    been set up, so it is unknown whether the Marketplace would ever use it. An explicit `ImageUrl`
    removes the dependence.
  - There is no description and no website.
  - "Explore more packages by CNorwood69" is derived from the NuGet **owners**. The docs give
    `PackagesByAuthor` as the only related field, and it lists package IDs, not a label. So this
    file is not expected to change that link.
- `docs/publishing.md:504-523` already documents the Marketplace. Only the meta-package is tagged,
  the listing showed the 18.x line with support for 17 and 18, and the listing's description is
  the meta-package's `<Description>`.
- The packages have just been given to the nuget.org organisation, outside the repo. The listing's
  owner-derived name may change for that reason alone, independently of this change.
- `PackedSolution` already packs the real solution for `PackageCompositionTests`, which reads the
  packed nuspecs.

## Goals / Non-Goals

**Goals:**
- The listing names Norwood Design & Development Ltd., with a short description, a website and a
  logo.
- The file cannot drift from the package metadata, and cannot override anything that differs
  between lines.

**Non-Goals:**
- Any other listing field.
- Any change to the packed packages.
- The ID rename.
- The Trusted Publishing owner documentation. All four are covered in `proposal.md`.

## Decisions

### 1. The name is compared with the PACKED authors, not the props text

`<Authors>` lives in `Directory.Build.props` as XML with `&amp;`, and the Marketplace reads it from
the packed nuspec. Comparing against the props **text** would compare the wrong layer, and would
need its own entity decoding. So the test reads `<authors>` from the packed meta-package's nuspec
through the existing `PackedSolution`, exactly as `PackageCompositionTests` reads the tag. That is
the artifact the Marketplace consumes. *Alternative rejected:* parsing `Directory.Build.props` with
`XDocument`. It is cheaper but one step removed from what is published, and the props-versus-packed
seam has cost this project before.

### 2. An allow-list of top-level fields, not a deny-list

A deny-list (`Title`, `Description`) protects only against the overrides someone thought of. The
schema has others, such as `VersionSpecificPackageIds` and fields added later. An allow-list of
`$schema` and `AuthorDetails` fails on **any** addition and names the field, so each new field
becomes a deliberate spec change. The cost is a test edit whenever the listing is enriched, and
that is the point.

### 3. The file is not copied to `dev/v18`. The documentation is.

The Marketplace reads only the default branch. A copy on `dev/v18` would do nothing, and an inert
copy drifts unnoticed. The test therefore runs on `main` only. Usually a test cherry-picked to one
line and not the other is the "fix reaching one line" defect. Here it is the intended shape, and
this paragraph is where that is recorded. If the default branch ever changes, the file follows it.

**The `docs/publishing.md` paragraph goes to both lines** (QA round 1). It is true on `dev/v18`
too: it describes a listing both lines share, and it already says the file and its test are on
`main`. Whoever releases either line reads `publishing.md`, and that document otherwise differs
between the lines only by version-specific text. Leaving the paragraph off one line would make it
the one-line fix this project keeps paying for, and would make the next nearby cherry-pick
conflict. It lands on `dev/v18` as a documentation-only commit on its own branch from `dev/v18`.
The JSON file and the test do not go with it.

### 4. The logo is committed and referenced by its raw `main` URL

The image is committed at `docs/images/ndd-logo.png` and referenced as
`https://raw.githubusercontent.com/Chris-N2/UBookIt/main/docs/images/ndd-logo.png`.
- The image is versioned and reviewed with the file that points to it.
- The test can prove it exists (the spec's "an image the repository does not contain" scenario).
- It stays independent of the company website and Gravatar.

*Alternatives:* the NDD website, which is outside the repo, so the test cannot see it. Or the
organisation's Gravatar. That is the image NuGet shows, but its URL is a hash with no `.png`/`.jpg`
suffix, which the Marketplace docs ask for. Either can replace this later. The spec's existence
check applies only when the URL points into this repository.

**The image file and the copy itself are content Chris supplies.** The proposal does not invent a
description of the company. Apply stops at that task until the values are given:
- the logo (square PNG; around 500 px works for the Marketplace's avatar slot);
- the one- or two-sentence description;
- the website URL.

### 4a. One accepted form for a URL into this repository, checked against tracked files (QA round 1)

Round 1 found the existence check scoped to one exact string prefix. Any other URL that reaches
this repository skipped the check and passed on scheme and extension alone. That included the
`refs/heads/main` form GitHub's own Raw button produces, `github.com/…/raw/…`, and a different
letter case. The fix does not try to recognise and normalise every form, which is a list that is
never finished. Instead:
- Any URL on a GitHub host whose first two path segments are this owner and repository,
  compared case-insensitively as GitHub does, **must be the canonical form exactly**. Anything
  else is refused, whether the file exists or not.
- The canonical form's path is looked up in `git ls-files`, compared ordinally. GitHub serves only
  committed files and is case-sensitive. A file-system check on Windows is neither, so a local
  green run would have been weaker than CI's.
- The canonical-form check rejects dot segments and query strings, because `Uri.AbsoluteUri` must
  equal the text as written.

**Free text naming an Umbraco major** (QA round 1, MINOR). The top-level allow-list checks the
mechanism. The guarantee is that the file states nothing that differs between lines, and a
description saying "for Umbraco 17" would break it inside an allowed field. So every string value
in `AuthorDetails` is checked against the package-description guard's vocabulary (`Umbraco`,
whitespace, digits). The spec now names that vocabulary and its limit, and does not claim more.

### 5. The guard can fire

Each scenario is checked by running its assertion against a mutated copy of the file, held in
memory or in a temp directory, never by editing the real file:
- the name changed;
- `Url` set to `http://`;
- `ImageUrl` set to `.gif`;
- an image path that does not exist;
- `Title` added;
- the file absent.

Each mutation must turn the assertion red, and the real file must pass. The assertions take the
document as a parameter so that this is possible.

## Risks / Trade-offs

- **The "more packages by CNorwood69" link is out of this file's reach.** It follows NuGet
  ownership. It is expected to change only once the personal account is no longer an owner, and
  that waits until the next release has proven the organisation policy. If it persists after
  that, record it in the publishing docs. It is not a defect in this file. The listing is checked
  after the sync and not claimed before it.
- **The sync schedule is the Marketplace's.** The docs mention a single-package sync endpoint.
  If the change has not shown up after a reasonable wait, use that endpoint. Do not keep editing
  the file.
- **A raw GitHub URL depends on the repository name.** A repo rename would break it silently. A
  rename is not planned, and it would break the Trusted Publishing policy anyway, which is a much
  louder failure.
- **The allow-list makes enrichment a spec change.** That is accepted on purpose (Decision 2).
