## ADDED Requirements

### Requirement: Front-end assets ship as static web assets, never through the manifest
Any front-end asset the package ships — a stylesheet, and any script or file that may
follow — SHALL be delivered as a **static web asset** carried in the package and served
from the package's own virtual path. It SHALL NOT be delivered through the package
migration manifest, and the manifest SHALL declare no `Stylesheets`, `Scripts`, `Files`
or `PartialViews` section.

This states the property that keeps the existing fence true now that the package has an
asset to ship, and does not modify it. The fence forbids installing on paths the site
owns; a static web asset is not installed anywhere and creates no file in the site, so
the fence is satisfied rather than widened. Stating it separately guards the reason
rather than the mechanism: a later change could move the stylesheet into
`<Stylesheets>` and still pass a manifest-section check written only against content,
because a stylesheet genuinely is a front-end asset the package is entitled to ship.
What it is not entitled to do is hand the file to the site.

**Which delivery mechanism is used decides who can never be fixed again.** A manifest
import writes a real file into the consuming site, and because the package's migration
plan is run-once by design, that file is then never touched again. Some of the
package's CSS carries accessibility weight — focus visibility, contrast of derived
colours, minimum target size — so a stylesheet the site owns is a stylesheet in which
an accessibility fix shipped in a later release never reaches any existing install. A
static web asset is replaced by the upgrade like any other part of the assembly, which
is the behaviour an accessibility fix requires.

The consequence for ownership SHALL be documented as it is for schema: the site does
not own the asset and does not edit it. What the site owns is its **appearance**, and
the route to changing that is the published token and class contract rather than a
copy of the file.

#### Scenario: The manifest declares no asset sections
- **WHEN** the shipped package manifest is inspected
- **THEN** it declares no `Stylesheets`, `Scripts`, `Files` or `PartialViews` section

#### Scenario: Installing the package creates no stylesheet in the site
- **WHEN** the package is installed into a site and its migration runs
- **THEN** no stylesheet file appears anywhere the site owns, and the asset is served from the package's own path

#### Scenario: An upgrade replaces the shipped asset
- **WHEN** a release changes the shipped stylesheet and an existing install is upgraded
- **THEN** the site serves the new stylesheet, without the site editing anything and without an editor's work being overwritten

#### Scenario: Appearance is documented as the site's, the file as the package's
- **WHEN** a site author reads the packaging documentation
- **THEN** it states that the package owns the asset and the site owns its appearance, and names the token and class contract as the route
