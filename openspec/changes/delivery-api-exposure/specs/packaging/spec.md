# Delta for packaging

Found by the sync-time outward sweep (task 6.1): a sentence stated the delivery API as
unconditionally available, which the off-by-default flip falsified. The requirement is
replaced with its body verbatim except the amendment described in tasks 6.1.

## MODIFIED Requirements

### Requirement: The package can be installed
A site SHALL be able to obtain uBookIt as a package and use it, by installing **one** thing.

**The package SHALL declare no dependency it does not ship.** Every uBookIt package a
consumer resolves SHALL be one this repository produces, so that adding the package succeeds
rather than failing to restore. A dependency on an assembly that exists only inside this
repository is not a dependency — it is a build that has never been asked to be consumed.

**Every assembly a working installation needs SHALL be reachable from that one package.**
Specifically: the domain, the persistence and migrations, the backoffice management API and
its client, and **the front-end rendering** — the Razor views, the theming mechanism and the
shipped stylesheet. The last of those is named explicitly because it is the one nothing else
references, so its absence is silent: the backoffice works, the booking page renders nothing,
and no error is raised anywhere.

**Package versions SHALL be produced together and SHALL match.** A uBookIt package depending
on a different version of a uBookIt package is a resolution nobody chose.

**Everything the packages carry SHALL be produced by the build that packs them.** A build
artifact that is not under version control and is not built by the build is absent from a
clean checkout, and packing then succeeds while shipping less than it claims. This is not a
convenience requirement about developer setup: it is the same silent absence as a missing
assembly, and it is *harder* to notice, because the working copy where anyone would check has
usually been made complete by hand already.

#### Scenario: One install is enough
- **WHEN** a site adds the single uBookIt package to a newly created Umbraco project
- **THEN** restore succeeds, and the site has the backoffice section, the management API and the front-end rendering available, and the delivery API installed — present and off, until the site enables its directions as the `delivery-api` capability defines

#### Scenario: Nothing is declared that is not published
- **WHEN** the produced packages' dependencies are inspected
- **THEN** every uBookIt dependency names a package this repository also produces, at the same version

#### Scenario: The front-end rendering is in the box
- **WHEN** a site installs the package and publishes a booking page
- **THEN** the flow renders, using views and a stylesheet that arrived with the package rather than being copied into the site

#### Scenario: The backoffice client is in the box
- **WHEN** the packages are built from a checkout containing only what version control carries
- **THEN** the backoffice package contains the compiled client bundle and its manifest, rather than whatever an earlier manual build happened to leave in the working copy

#### Scenario: The aggregate does not drift
- **WHEN** a new packable assembly is added to the solution
- **THEN** the single package a site installs is required to reach it, rather than silently omitting it
