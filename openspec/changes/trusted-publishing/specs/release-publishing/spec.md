## Purpose

How a uBookIt release reaches nuget.org. A pushed release tag is checked, packed and verified
without anyone's involvement. It is then published with a short-lived credential, only after a
maintainer approves, so no long-lived key is needed and no release depends on steps a maintainer
has to remember.

## ADDED Requirements

### Requirement: A release is published by pushing its tag
Pushing a tag of the form `<major>.<minor>.<patch>` to the public repository SHALL start the
publishing workflow for that tag, and that workflow SHALL be the route by which a release reaches
nuget.org. A push of any other tag or of a branch SHALL NOT publish anything. The packages pushed SHALL be
built from the tagged commit, packed from a clean clone within the same workflow run. They SHALL NOT
be packages built anywhere else or carried over from an earlier run.

#### Scenario: A release tag starts publishing
- **WHEN** a maintainer pushes the tag `17.2.2` to the public repository
- **THEN** the publishing workflow starts for the commit that tag names, without anyone triggering
  it by hand

#### Scenario: Other refs do not publish
- **WHEN** a branch is pushed, or a tag that is not three dot-separated numbers (such as
  `17.2.2-rc1` or `v17.2.2`)
- **THEN** the publishing workflow does not start, or ends without publishing and names the tag
  as the reason

#### Scenario: What is pushed is what this run built
- **WHEN** the workflow publishes
- **THEN** every package pushed was packed from the tagged commit earlier in the same run

### Requirement: A release tag is checked before anything is built
Before packing, the workflow SHALL fail, naming the reason, when any of these holds:

- the tag is not the `<Version>` declared by the tagged commit;
- the tagged commit is not contained in the published line whose current declared major equals
  the tag's major (the published lines being `main` and `dev/v18`), or no published line has that
  major;
- the tagged commit has no successful verification run for a push to a published line.

When a verification run for the tagged commit is still queued or in progress, the workflow SHALL
wait for it, for a bounded time, rather than fail at once. It SHALL fail when that run ends in
anything but success, or when the bound is reached. A failed check SHALL leave nuget.org
untouched.

#### Scenario: A tag that disagrees with the declared version is refused
- **WHEN** the tag `17.2.2` is pushed on a commit whose `<Version>` is `17.2.1`
- **THEN** the workflow fails before packing, naming both versions, and nothing is published

#### Scenario: A tag on the wrong line is refused
- **WHEN** a tag with major `18` names a commit that is contained in `main` but not in `dev/v18`
- **THEN** the workflow fails before packing, naming the line the tag's major requires

#### Scenario: A tag on an unpushed or feature-branch commit is refused
- **WHEN** a release tag names a commit that no published line contains
- **THEN** the workflow fails before packing, and nothing is published

#### Scenario: A commit whose verification failed is refused
- **WHEN** the tagged commit's verification run on its published line failed or was cancelled
- **THEN** the workflow fails before packing, naming that run

#### Scenario: Tagging straight after a merge waits for verification
- **WHEN** the tag is pushed while the tagged commit's verification run is still in progress
- **THEN** the workflow waits for that run, and proceeds only once it has succeeded

### Requirement: The packed release is verified before it can be published
After packing, and before the publish step can start, the workflow SHALL verify what it produced.
It SHALL fail, naming the offending file, unless:

- exactly one package exists for each packable project in the tagged commit's solution, and each
  carries the tag as its version;
- each package that carries assemblies has its symbol package, holding a symbol file for every
  one of those assemblies, and a package that carries none has no symbol package. Whether a
  package carries assemblies SHALL be read from the package itself. A project setting that
  disagrees with the package's contents is itself a failure; it SHALL NOT be where the
  expectation comes from;
- every package's manifest names the public repository and the tagged commit;
- every assembly a package carries has a SourceLink map, and every SourceLink map fetches source
  from the public repository at the tagged commit.

#### Scenario: Assemblies that would ship without symbols are caught
- **WHEN** a library's project is set not to produce symbols, while its package still carries an
  assembly
- **THEN** the workflow fails, naming the package and the assembly, and nothing is published

#### Scenario: A symbol package missing an assembly's symbols is caught
- **WHEN** a symbol package lacks the symbol file for an assembly its package carries
- **THEN** the workflow fails, naming the symbol package and the missing file

#### Scenario: A package at the wrong version is caught before publishing
- **WHEN** a produced package's manifest carries a version other than the tag
- **THEN** the workflow fails, naming the package, and the publish step does not run

#### Scenario: A missing package is caught before publishing
- **WHEN** a packable project in the solution produced no package
- **THEN** the workflow fails, naming the project, and nothing is published

#### Scenario: Source links that would not resolve are caught
- **WHEN** a SourceLink map names a repository or commit other than the public repository at the
  tagged commit
- **THEN** the workflow fails and nothing is published

### Requirement: Publishing waits for a maintainer's approval
The step that obtains a nuget.org credential and pushes SHALL run only after a maintainer has
approved that specific run. It SHALL NOT start until the checks and the verified pack have
succeeded in the same run. The approval SHALL be enforced by the repository hosting's deployment
protection, not by anything the workflow itself could skip. A run that is not approved, or is
rejected, SHALL publish nothing.

#### Scenario: A pushed tag waits for approval
- **WHEN** a release tag's checks and pack have succeeded
- **THEN** the run pauses before publishing until a maintainer approves it, and publishes nothing
  in the meantime

#### Scenario: A rejected run publishes nothing
- **WHEN** a maintainer rejects the pending publish
- **THEN** no package is pushed and the run ends without publishing

#### Scenario: A failed pack is never offered for approval
- **WHEN** the pack verification fails
- **THEN** the publish step does not start and no approval is requested

### Requirement: Publishing holds no long-lived credential and confines the short-lived one
Publishing SHALL obtain its nuget.org credential through Trusted Publishing: an identity token
issued to this repository's publishing workflow and exchanged for a temporary API key. No nuget.org
API key SHALL be stored in the repository or in its secrets. Only the publishing step SHALL be
permitted to request an identity token. That step SHALL NOT check out or execute the repository's
code; it SHALL push only the artifacts the verified pack produced. Every other job, in this
workflow and in every other, SHALL hold read-only repository permissions. Every third-party action
the workflow uses SHALL be referenced by a full commit identifier.

#### Scenario: No key is stored
- **WHEN** the repository and its configured secrets are searched for a nuget.org API key
- **THEN** none is found

#### Scenario: Only the publishing step can obtain a credential
- **WHEN** any job other than the publishing step, in any workflow, requests an identity token
- **THEN** the request is refused for lack of permission

#### Scenario: The credential never meets repository code
- **WHEN** the publishing step runs
- **THEN** it has not checked out the repository and runs nothing but the workflow's own steps and
  pinned actions. So no script, build logic or dependency in the repository can act while the
  credential exists.

#### Scenario: A workflow that would widen the credential is caught before it runs
- **WHEN** any workflow, including one added later, does any of the following:
  - grants an identity token outside the publishing step, at any position in the file, in block
    or flow style;
  - grants all permissions;
  - omits its workflow-level permissions;
  - references an action by anything but a full commit identifier;
  - or, outside its shell script bodies, uses a quoted key, an escape sequence, an anchor, an alias
    or a merge key — the forms a text reading of the file cannot see through, and which are
    therefore refused rather than interpreted;

  or the publishing step uses any action beyond those that fetch the verified packages, set up
  the SDK and exchange the token, invokes git or gh, or references the repository's own addresses
- **THEN** the repository's test suite fails, naming the workflow and the offending line. A step
  that fetches and runs code from a host other than this repository's is not detected, and rests
  on review.

### Requirement: A release that is already on nuget.org is not reported as newly published
The workflow SHALL push libraries before the meta-package that depends on them. It SHALL push
each package and each symbol package separately, so that a symbol package is attempted even when
its package is already present. It SHALL decide whether each package was newly published or
already present from the feed's answer to that package's push. It SHALL NOT decide from a lookup
made beforehand, which can lag a recent push. A symbol package SHALL be reported as submitted, or
as pending, and SHALL NEVER be reported or counted as published. The feed accepts the same symbol
package again and again with the same answer, so its answer cannot distinguish new from
resubmitted. A push whose answer is none of these SHALL fail the run. When some of a release's
packages are already on nuget.org — for example on a re-run after a partial push — it SHALL push
the remainder and report, file by file, what happened to each. When no package was newly
published, the run SHALL fail, stating that nothing was published and how many symbol packages
were submitted, even if every symbol push was accepted.

#### Scenario: A re-run completes a partial publish
- **WHEN** a publish run is re-run after an earlier attempt pushed some but not all packages
- **THEN** the remaining packages are pushed, and the run reports which were already present

#### Scenario: A re-run repairs symbols that failed to publish
- **WHEN** an earlier attempt published a package but not its symbol package, and the run is
  re-run
- **THEN** the symbol package is submitted and reported as submitted, not as published, and the
  package is reported as already present. If no package in the run was newly published, the run
  fails, stating how many symbol packages were submitted, because it cannot tell whether that
  repaired anything.

#### Scenario: Symbol packages accepted again do not make a re-run look like a publish
- **WHEN** a run is re-run after every package and symbol package was published and became
  available, so that the feed accepts every symbol package again
- **THEN** every package is reported as already present, every symbol package as submitted, and
  the run fails, stating that nothing was published

#### Scenario: A re-run straight after a complete publish is not reported as a publish
- **WHEN** a run is re-run immediately after every file was published, before the feed has
  finished indexing them
- **THEN** no file is reported as newly published, and the run fails

#### Scenario: Re-publishing a complete release is reported, not silently green
- **WHEN** the publish step runs for a version whose packages are all already on nuget.org
- **THEN** the run fails, stating that every package was already present and nothing was
  published

#### Scenario: The meta-package is never briefly uninstallable
- **WHEN** a release is pushed
- **THEN** every library package is pushed before the `UBookIt` meta-package

### Requirement: The pipeline can be proved without publishing
A maintainer SHALL be able to run the workflow by hand against an existing release tag as a dry
run. A dry run SHALL perform the release checks and the verified pack exactly as a tag push does. It
SHALL NOT request a credential or publish. It SHALL work for tags that predate the workflow file.

#### Scenario: A dry run against an existing release
- **WHEN** a maintainer starts a dry run against the tag `17.2.1`
- **THEN** the checks and the verified pack run against that tag's commit and report their result,
  and no credential is requested and nothing is published

#### Scenario: A dry run cannot publish
- **WHEN** a dry run's checks and pack succeed
- **THEN** the run ends without offering the publish step for approval

### Requirement: The runbook describes the workflow as the route and the key as the fallback
`docs/publishing.md` SHALL describe publishing by tag push through the workflow as the route. It
SHALL state the one-time setup the workflow depends on outside the repository: the approval
environment and the nuget.org Trusted Publishing policy, with the exact values each needs. It SHALL
state that the policy belongs to the account that owns the packages, so a change of ownership
requires a new policy. The manual API-key procedure SHALL remain documented, labelled as the
fallback for when the workflow cannot be used. The status section SHALL no longer list Trusted
Publishing as outstanding.

#### Scenario: A maintainer releasing follows the workflow
- **WHEN** a maintainer reads the runbook to publish a release
- **THEN** it tells them to push the tag and approve the run, and names what the run checks so
  they do not repeat it by hand

#### Scenario: The setup outside the repository can be reproduced
- **WHEN** the approval environment or the nuget.org policy has to be recreated
- **THEN** the runbook gives every value each one needs, including the workflow file name and the
  environment name the policy must match

#### Scenario: The manual route remains available
- **WHEN** the workflow cannot publish (for example, nuget.org's token exchange is unavailable)
- **THEN** the runbook's fallback section still gives a working manual procedure, and says it is
  the fallback
