# Delta for responsibility

Found by the sync-time outward sweep (task 7.1): the authorization requirement's
colon-enumeration ("access to the package's section, and nothing weaker") described the
whole of the authorization, which the `permissions` capability falsified — these
endpoints now also require the configuration verb. The requirement is replaced verbatim
except that sentence; the "same as every other endpoint" framing and every scenario
carry unchanged.

## MODIFIED Requirements

### Requirement: Assignments are managed through section-authorized endpoints

The management API SHALL expose reading and writing of a subject's assignments under the same
backoffice authorization every other management endpoint uses — the section grant as the outer
gate, refined since the `permissions` capability by its verbs, and for these endpoints the
configuration verb, responsibility being configuration — and nothing weaker. The read SHALL annotate each assignment with what its party currently
resolves to — a display name, whether it still exists, and its state where the party is a user —
so an editor can show a stale or disabled assignment rather than hide it.

#### Scenario: Anonymous request is rejected
- **WHEN** an unauthenticated caller reads or writes assignments
- **THEN** the request is refused on the same terms as every other management endpoint

#### Scenario: Assignments round-trip through the API
- **WHEN** an authorized caller writes assignments for a resource and reads them back
- **THEN** the same set returns, each annotated with the party's current display name

#### Scenario: A dangling assignment is reported, not hidden
- **WHEN** assignments are read for a subject whose assigned user has since been deleted
- **THEN** the assignment is returned and marked as no longer resolving
