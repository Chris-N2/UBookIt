## ADDED Requirements

### Requirement: A theme replaces the package's rendering with its own controls
The package SHALL support a **theme**: a set of Razor views, supplied by an assembly other
than the package's own, that renders the booking flows in place of the views the package
ships. A theme SHALL be able to render entirely different controls — not merely restyle the
shipped ones — because that is the tier of customisation neither the site's own template
nor the token and class contract can reach.

A theme SHALL be supplied as an ordinary Razor class library whose views are **precompiled
into its own assembly** and discovered through the standard application-part mechanism, so
that a theme is versionable, testable and distributable as a package like any other.

The theme's views SHALL live under a theme-rooted path incorporating the theme's own name,
so that two themes present in one application cannot collide.

**A theme SHALL NOT be required to reproduce the package's markup, and the package SHALL
NOT define a theme in terms of widgets.** What a theme receives is the same strongly-typed
view models the shipped views receive. A theme is an alternative *rendering* of the package's
data, never an implementation of a package-defined control abstraction.

#### Scenario: A theme's view renders instead of the package's
- **WHEN** a theme supplying a view for one of the package's ViewComponent views is registered, and that view component renders
- **THEN** the theme's view produces the markup, and the package's view for it does not render

#### Scenario: A theme is an assembly, not a folder of loose files
- **WHEN** a theme supplies its views precompiled in its own assembly
- **THEN** they are found and rendered, without the theme needing to place any file into the consuming site

#### Scenario: A theme receives the package's view models unchanged
- **WHEN** a theme's view renders
- **THEN** it is passed the same view model the package's own view for that state receives, and the package exposes no widget-level abstraction for it to implement

#### Scenario: A site that registers no theme is unaffected
- **WHEN** no theme is registered
- **THEN** every flow renders exactly the markup it rendered before this change

### Requirement: A registered theme actually wins, and a theme that does not win is not silent
Registering a theme SHALL cause the theme's view to be **resolved ahead of** the package's
own view for the same component and view name. This SHALL be guaranteed by the package
rather than left to the consuming site's registration order.

**This requirement exists because the failure mode is invisible.** View-location expanders
run in the order they were added, and Umbraco adds expanders of its own that *prepend*
locations — among them the location at which the package's own views are found. A theme
registered too early is therefore ordered *behind* the package's views, and the resulting
site renders correctly, completely, and entirely unthemed. There is no error, no warning
and no missing view: the only symptom is that the theme appears not to have been written.

**The guard for this SHALL be stated over the guarantee, not the mechanism.** It SHALL
assert that the theme's view is what resolves — by exercising the whole resolution chain as
the framework exercises it — and SHALL NOT be satisfied by asserting that a registration
took place, that an expander is present, or that it was registered from a particular place.
A guard of the latter kind passes on precisely the broken configuration this requirement
exists to catch. This is the same fault, in the same shape, as the styling guard that was
rewritten twice before it named what it was protecting.

The resolved view SHALL be the theme's for **every** view the theme supplies, not only for
the first one resolved, so that a chain correct for one component and wrong for another
cannot pass.

**Registering a theme SHALL work wherever the site calls it.** The guarantee SHALL NOT
depend on the registration being made before or after any other call the host makes, and
this SHALL be asserted for every supported call order rather than documented as a rule the
site has to follow. A rule a site can break silently is not a guarantee, and the first
implementation of this requirement broke exactly that way: it depended on being called
before the host's composers ran, and a site that called it afterwards got no theme, no
diagnostic, and the package's stylesheet suppressed as well.

**The package SHALL also verify the outcome at boot, in the running application.** Because
the ordering mechanism has been wrong once, its replacement SHALL NOT be trusted either: at
startup the package SHALL check that the theme's view location is the first the view engine
will search, and SHALL log an error naming what resolves instead when it is not. This is a
check on the guarantee in the site actually running, and it holds even if a future host
version changes how it configures the view engine.

#### Scenario: The call order a site writes does not change what resolves
- **WHEN** a site registers a theme before, or after, the host's own view-engine registration
- **THEN** the theme's view resolves in both cases, identically

#### Scenario: A theme that is registered but does not resolve first is reported at boot
- **WHEN** an application starts with a registered theme whose view location is not the first the view engine will search
- **THEN** an error is logged naming the theme, the location that resolves first, and the theme's own location — rather than the site rendering correctly and entirely unthemed

#### Scenario: A theme that does resolve first is not reported
- **WHEN** an application starts with a registered theme that resolves first
- **THEN** no ordering error is logged, for any supported call order

**The theme is fixed at startup, and that limit SHALL be stated rather than discovered.**
There is one theme per application, so nothing about the theme contributes to the
view-location cache key. Changing the registered theme while the application is running
would therefore leave a populated cache keyed without it, and views resolved before the
change would keep resolving to the previous theme. This is a consequence of the
one-theme-package-wide decision, not a defect in it, and it is recorded here so that a
later change proposing runtime theme switching meets it rather than rediscovering it.

#### Scenario: The theme's view is what resolves
- **WHEN** a theme is registered and a themed view name is resolved through the full view-location chain
- **THEN** the view that resolves is the theme's, not the package's

#### Scenario: A misordered registration is detected rather than rendering unthemed
- **WHEN** the theme's location is ordered behind the location at which the package's own views are found
- **THEN** the guard fails, naming the ordering — rather than a correct-looking, unthemed rendering passing

#### Scenario: The guard is not satisfied by registration alone
- **WHEN** a theme is registered but its view does not resolve first
- **THEN** the guard fails, because it asserts what resolves and not that a registration occurred

#### Scenario: Every supplied view wins, not just one
- **WHEN** a theme supplies views for more than one view component
- **THEN** each of them resolves to the theme's view

### Requirement: What a complete theme supplies is published, and an incomplete theme is reported
The package SHALL publish the set of views a **complete** theme supplies — the views of both
public view components, by name — together with the view model each receives. A theme author
SHALL be able to discover what a theme must provide from the package rather than by reading
the package's source or by observing which pages come out wrong.

The package SHALL provide a **completeness check** over a theme's own assembly that reports
every required view the theme does not supply. The check SHALL be **public API**, so a theme
author runs it from their own test suite and an incomplete theme fails the theme's build
rather than reaching a site.

The check SHALL verify **the view model each view declares as well as the view's presence**.
A view at the correct path declaring the wrong model satisfies a name-only check and then
throws when a visitor renders it, which is a worse failure than the one being prevented.

At boot, a registered theme that is incomplete SHALL be **logged as an error naming each
missing view**, and the package's own view SHALL render in place of each one. The site SHALL
remain operable.

**The residue of that choice SHALL be stated rather than argued away.** Falling back per view
is what the framework does by default and it does it silently — a theme supplying one view of
a pair and not the other renders a mixture of two designs with no diagnostic whatsoever. What
this requirement changes is the silence, not the fallback: a theme author who never runs the
published check still ships a mixed rendering, and only a log entry reports it. The check is
where that mistake is meant to be caught, and it is placed in the theme author's build for
that reason.

#### Scenario: The required view set is published
- **WHEN** a theme author asks what a complete theme must supply
- **THEN** the package names every required view and the view model each receives

#### Scenario: An incomplete theme fails the theme author's own build
- **WHEN** a theme author runs the published completeness check against a theme missing a required view
- **THEN** the check fails, naming each missing view

#### Scenario: A view with the wrong model is incomplete, not complete
- **WHEN** a theme supplies a view at a required path whose declared model is not the one that view receives
- **THEN** the completeness check fails, naming the view and the expected model, rather than passing and failing later at render time

#### Scenario: An incomplete theme is logged at boot and the site stays up
- **WHEN** an application starts with a registered theme that does not supply every required view
- **THEN** an error is logged naming each missing view, and the package's own view renders in place of each missing one

#### Scenario: A complete theme logs nothing
- **WHEN** an application starts with a registered theme that supplies every required view
- **THEN** no missing-view error is logged

### Requirement: A themed rendering's markup is the theme author's, and the package says so
The package's guarantees about **rendered markup** — every WCAG 2.2 AA criterion determined
by markup, semantic HTML, programmatically associated labels, grouped choices, keyboard
operability, reading and focus order, and usability with no author stylesheet — are
guarantees about **the views the package ships**. Where a theme replaces a view, the markup
is the theme's and those guarantees are the theme author's.

**The package SHALL NOT claim conformance for a rendering it did not produce, and SHALL NOT
let its published accessibility statement read as though the shipped markup always renders.**
Both directions matter: the package neither asserts that a theme is accessible nor allows a
statement written about its own views to be silently inherited by markup it has never seen.

This is the same narrowing already agreed for the CSS-determined criteria, applied to the one
category previously held unconditionally, on the same reasoning: the package does not take
responsibility for code it did not write.

**It reaches every requirement whose subject is rendered markup, and it is stated here once
rather than repeated on each of them.** That includes accessible failure handling and input
preservation, and the resolution of a rendering's own `aria` and in-page-link references —
both of which are properties of the markup that renders, and neither of which the package
can hold for markup it has never seen. It equally includes behaviour a view's markup
determines: a theme that renders no form renders no booking. Restating the split on each
affected requirement is precisely what the "one bar, stated once" clause forbids, because a
per-surface restatement is what drifts.

**Requirements whose subject is the package's own views are not narrowed at all**, and the
distinction is the subject rather than the topic. That a view renders every state its model
can express, that every branch it carries can be taken, that every view the package ships is
exercised, and that a composed document is the composition a flow view renders, are all
statements about the package's own views: a theme adds views elsewhere and takes none away.

**The narrowing SHALL NOT reach the shipped views.** For a site with no theme registered —
and for every view a theme does not supply — every markup guarantee holds exactly as it did
before this change. In particular the clause that each flow remains usable, operable and
correctly ordered **with no author stylesheet applied at all** is untouched, and remains the
anchor on which the CSS narrowing rests.

The published accessibility statement SHALL state which rendering it describes, so that a
reader can tell whether it applies to the site in front of them.

#### Scenario: The statement names the rendering it describes
- **WHEN** the published accessibility statement is read
- **THEN** it states that its markup claims describe the views the package ships, and that a theme's markup is the theme author's

#### Scenario: No claim is made about a theme
- **WHEN** a theme replaces a view
- **THEN** the package makes no accessibility claim about the resulting markup, and requires none of the theme

#### Scenario: The shipped rendering keeps every markup guarantee
- **WHEN** no theme is registered, or a view is one the theme does not supply
- **THEN** every markup guarantee holds for it exactly as before, including that it is usable with no author stylesheet applied

### Requirement: The package emits no styling of its own when a theme is active
When a theme is active, the package's styling partial SHALL emit nothing, unless the theme
declares that it wants the package's stylesheet.

A theme owns the markup, so the package's classes largely do not exist in the rendered
document: the stylesheet would then style nothing while still being able to collide with the
theme's own. A theme that reuses the package's partials as building blocks asks for the
stylesheet and receives it.

This SHALL remain the **only** route by which the package emits its own styling. A second
link or style element emitted from anywhere else would survive the theme and fight it, which
is why the single-emission-route requirement was written before a theme existed.

#### Scenario: A theme suppresses the package's stylesheet
- **WHEN** a theme is active, the theme does not ask for the package's stylesheet, and the site renders the package's styling partial
- **THEN** nothing is emitted, and no request is made for the package's stylesheet

#### Scenario: A theme may opt back in
- **WHEN** a theme declares that it wants the package's stylesheet and the site renders the styling partial
- **THEN** the stylesheet is linked exactly as it is for an unthemed site

#### Scenario: Suppression does not create a second emission route
- **WHEN** the package's views and stylesheet are inspected with a theme active
- **THEN** no view emits a link or style element for package styling other than through that one partial

### Requirement: The building blocks a theme may call are a promised contract
The package's shared partials, and the view model types its views receive, SHALL be a
**published compatibility contract**, because a theme calls them by path and by type.

A theme SHALL be able to call the package's shared partials as optional building blocks — a
theme wanting the package's accessible time-picker calls it; a theme replacing it does not.
This is what makes a theme cheap to write and is why the partials are not themselves
replaceable: a design that protected them would let a theme change headings and nothing
else, which is the opposite of a theme's purpose.

The partials and view models are **already public**, and this requirement changes their
status rather than their visibility: public-by-accident becomes public-by-promise, and a
change to either is thereafter a breaking change to be called out as one.

#### Scenario: A theme calls a package partial by path
- **WHEN** a theme's view renders one of the package's shared partials by its documented path with the model that partial declares
- **THEN** it renders, and the theme is not required to reproduce it

#### Scenario: The contract is published
- **WHEN** a theme author asks which partials and models they may depend on
- **THEN** the package names them, and states that changing them is a breaking change

#### Scenario: A theme need not call any of them
- **WHEN** a theme supplies a required view that calls none of the package's partials
- **THEN** it renders, and no completeness or resolution rule requires the partials to be used
