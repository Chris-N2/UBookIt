# Design — company-name

## D1 — One declaration, everything else checked against it

`Directory.Build.props`' `<Company>` becomes the single source, exactly as `<Version>` already
is. The guard parses it and asserts `LICENSE` and `README.md` state the same string. Restating
the literal name in the test would reproduce the defect being fixed — five copies of a name
with nothing comparing them — one file further along.

**`Authors` and `Company` are both set and both must match.** They are distinct NuGet fields
(`Authors` is displayed as the publisher on the listing; `Company` lands in assembly metadata),
and nothing stops them drifting apart, which is a way for the listing and the assemblies to
disagree about who published the package.

## D2 — The ampersand is the trap

XML requires `&amp;`. A bare `&` makes `Directory.Build.props` unparseable and every build
fails immediately — loud, and therefore not the dangerous case. **The dangerous case is the
opposite:** writing `&amp;` where the value is *not* XML — `LICENSE` and `README.md` are plain
text and must carry a literal `&`. So the same name is spelled two ways across three files,
and the guard must compare the *decoded* value rather than the raw text, or it will report a
mismatch that is not one.

## D3 — What the guard does not do

It does not check the name is the *correct* legal name — no test can know that. It checks the
three places agree with the one declaration. Correctness of the name itself was established by
the person who owns it, and the guard's remarks say so rather than implying more.
