## MODIFIED Requirements

### Requirement: What the package owns and what the site owns is documented
A site author SHALL be able to find out, from the package's own documentation, which
of their changes survive a uBookIt release and which do not. Ownership SHALL be stated
rather than left to be inferred from behaviour, because the cost of inferring it wrongly
is discovering that work has been destroyed.

The documentation SHALL state at least: that the schema is imported once and an ordinary
release does not re-import it; that a release carrying a migration step does re-import,
replacing the template's contents and the document type's name, icon, description and
allow-at-root; that nothing is ever removed, so an editor's own properties and pages are
safe; how a site changes the page's appearance; and **how a site changes the markup inside
the flow**.

**The package SHALL NOT claim a customisation route it does not have.** Placing a file in
the consuming site at the same path as one of the package's views does **not** work, and
the documentation SHALL continue to say so: those views are compiled into the package
assembly without source checksums, so ASP.NET Core uses the compiled copy and never
consults the site's file. This remains the trap a site author falls into first, and it is
not made less likely by a supported route existing elsewhere — so it SHALL be documented as
a route that does not work, not merely omitted in favour of the one that does.

**The route that does exist SHALL be documented as what it is: a theme.** The flow's markup
is customisable by supplying views from a **separate assembly** at a theme path, which the
package resolves ahead of its own. The documentation SHALL name this route and SHALL
distinguish it from the site-file override that does not work, because the two look alike
to a reader and only one of them has any effect.

**The documentation SHALL NOT assert more than has been measured.** Expectation and
measurement SHALL be distinguishable to a reader. This clause exists because a requirement
this one replaces asserted a mechanism nobody had run, and a correction that repeats the
habit is not a correction. Two specific limits follow from it:

- The site-file override failure was measured on a development site. It has **not** been
  measured without runtime compilation, where the site's own override is itself compiled
  and precedence falls to application-part ordering.
- The theme route SHALL NOT be documented as working in a configuration in which it has not
  been measured. It has been measured in **two**: under Umbraco page rendering on a
  development site, and in a host with runtime compilation absent from the dependency
  closure — which is what a fully precompiled production site runs. Both SHALL be stated,
  and nothing beyond them SHALL be claimed. The second measurement was taken because the
  first alone would have repeated the habit this clause exists to correct.

The documentation SHALL also state what a theme's author owns: a theme's markup is the
theme's, and the package's markup and accessibility guarantees describe the views the
package ships.

#### Scenario: A site author can find out whether their change survives
- **WHEN** a site author asks whether an edit of theirs will survive a uBookIt release
- **THEN** the package's documentation answers it, distinguishing an ordinary release from one carrying a migration step

#### Scenario: The documented customisation route works
- **WHEN** the documentation names a way to change how the booking page looks
- **THEN** following it changes what the site renders

#### Scenario: The route for changing the flow's markup is documented and works
- **WHEN** a site author wants to restyle or replace the markup inside the booking flow
- **THEN** the documentation names the theme route, and following it changes what the site renders

#### Scenario: The route that does not work is still documented as not working
- **WHEN** a site author places their own file at the path of one of the package's views
- **THEN** the documentation has already told them this has no effect, and names the theme route instead — rather than leaving the failed attempt to be discovered

#### Scenario: A claim beyond what was measured is not made
- **WHEN** the documentation describes either the override failure or the theme route
- **THEN** it states the configuration in which the behaviour was measured, and does not assert it for a configuration in which it was not

#### Scenario: A theme's ownership is stated
- **WHEN** a site author or theme author reads the ownership documentation
- **THEN** it states that a theme's markup is the theme author's, and that the package's markup and accessibility guarantees describe the views the package ships
