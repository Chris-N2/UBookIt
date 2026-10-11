## REMOVED Requirements

### Requirement: The runbook describes the workflow as the route and the key as the fallback
**Reason**: Both lines have released through the workflow (`17.2.2` and `18.1.2`, 2026-09-26), so
the manual API-key procedure this requirement kept documented is no longer wanted as a route. A hand
push bypasses every check the workflow makes, and the maintainer's last key expires in mid-October
2026. The one guarantee deliberately dropped is the SHALL keeping that procedure documented,
together with its scenario *The manual route remains available*. Every other guarantee of this
requirement is restated in *The runbook describes the workflow as the only route*: the workflow
as the route, the one-time setup and its values, the policy belonging to the owning account, and
the status section not listing Trusted Publishing as outstanding.
**Migration**: When the workflow cannot publish, the release waits, or the workflow is repaired
first and the release then goes through it. See *The runbook describes the workflow as the only
route*.

## ADDED Requirements

### Requirement: The runbook describes the workflow as the only route
`docs/publishing.md` SHALL describe publishing by tag push through the workflow as the route, and
as the only one. It SHALL state the one-time setup the workflow depends on outside the
repository: the approval environment and the nuget.org Trusted Publishing policy, with the exact
values each needs. It SHALL state that the policy belongs to the account that owns the packages,
so a change of ownership requires a new policy. The status section SHALL NOT list Trusted
Publishing as outstanding.

The runbook SHALL NOT give a procedure for pushing a release to the feed by hand. It SHALL say
that no such route exists, and what to do instead when the workflow cannot publish: the release
waits, or the workflow is repaired in a change of its own and the release then goes through it.
It SHALL state why: a release pushed by hand bypasses every check the workflow makes.

Where the runbook explains a hazard the workflow guards against, such as stale artifacts, source
links following the remote, or a commit absent from the public repository, it SHALL describe it
as the reason for a check the workflow makes. It SHALL NOT present it as steps a maintainer
performs to pack a release.

The runbook SHALL record that a `403` from the workflow's push names only the API key, as it did
for a key created by hand, because the workflow pushes with a key obtained by the token exchange.
It SHALL direct the maintainer to the Trusted Publishing policy first: whether the policy's owner
can publish, then the policy's scope, then its package selection.

#### Scenario: A maintainer releasing follows the workflow
- **WHEN** a maintainer reads the runbook to publish a release
- **THEN** it tells them to push the tag and approve the run, and names what the run checks so
  they do not repeat it by hand

#### Scenario: The setup outside the repository can be reproduced
- **WHEN** the approval environment or the nuget.org policy has to be recreated
- **THEN** the runbook gives every value each one needs, including the workflow file name and the
  environment name the policy must match

#### Scenario: A change of package ownership is recognised as needing a new policy
- **WHEN** a maintainer reads the runbook before transferring the packages to another account
- **THEN** it states that the policy belongs to the owning account, so the new owner needs a new
  policy

#### Scenario: The workflow cannot publish
- **WHEN** the workflow cannot publish a release (for example, nuget.org's token exchange is
  unavailable)
- **THEN** the runbook states that there is no hand route, gives no procedure for pushing by hand,
  and says the release waits or the workflow is repaired first, because a hand push skips every
  check the workflow makes

#### Scenario: A hazard the workflow guards against is explained, not prescribed
- **WHEN** a maintainer reads why source links depend on the remote and the commit, or why stale
  artifacts are dangerous
- **THEN** the runbook presents each as a reason for something the workflow checks, and gives no
  sequence of local pack commands to follow for a release

#### Scenario: A 403 on the workflow's push is traced to the policy
- **WHEN** the workflow's push fails with a `403` whose message names only the API key
- **THEN** the runbook directs the maintainer to check the policy's owner can publish, then its
  scope, then its package selection, rather than the key the message names
