## ADDED Requirements

### Requirement: Front-end assets ship with the assembly, not into the site
Any front-end asset the package ships — a stylesheet, and any script or file that may
follow — SHALL be delivered as a **static web asset** carried in the package and served
from the package's own virtual path, and SHALL NOT be delivered by the package
migration manifest.

**This does not restate the manifest fence, and deliberately does not name forbidden
manifest sections.** The requirement *The package installs nothing on paths the site
owns* already prohibits installing stylesheets, scripts, partial views and arbitrary
files, and it does so with an **allowlist** of the sections the package declares —
which is strictly stronger than any list of forbidden ones and which that requirement
argues for explicitly. Enumerating sections here would reintroduce the denylist that
fences only what someone thought of. What is added here is the **positive** obligation:
that a front-end asset has a delivery mechanism at all, and which one.

**Which mechanism is used decides who can never be fixed again, and that is why this is
a requirement rather than an implementation detail.** A manifest import writes a real
file into the consuming site, and because the package's migration plan is run-once by
design, that file is then never touched again. Some of this package's CSS carries
accessibility weight — focus visibility, the derivation of muted and border colours
from the host's text colour, minimum target size — so a stylesheet the site owns is a
stylesheet in which an accessibility fix shipped in a later release reaches **no
existing install, ever**, with nothing reporting that it did not. A static web asset is
replaced by the upgrade like any other part of the assembly, which is the behaviour an
accessibility fix requires.

The consequence for ownership SHALL be documented as it is for schema: the site does
not own the asset and does not edit it. What the site owns is its **appearance**, and
the supported route to changing that is the published token and class contract rather
than a copy of the file.

#### Scenario: Installing the package creates no stylesheet in the site
- **WHEN** the package is installed into a site and its migration runs
- **THEN** no stylesheet file appears anywhere the site owns, and the asset is served from the package's own path

#### Scenario: An upgrade replaces the shipped asset
- **WHEN** a release changes the shipped stylesheet and an existing install is upgraded
- **THEN** the site serves the new stylesheet, without the site editing anything and without an editor's work being overwritten

#### Scenario: A front-end asset with no declared delivery mechanism fails
- **WHEN** the package ships a front-end asset that is neither a static web asset nor covered by an allowed manifest section
- **THEN** the check fails, naming the asset — so an asset cannot arrive by an undecided route

#### Scenario: Appearance is documented as the site's, the file as the package's
- **WHEN** a site author reads the packaging documentation
- **THEN** it states that the package owns the asset and the site owns its appearance, and names the token and class contract as the route
