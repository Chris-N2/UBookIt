# persistence

## ADDED Requirements

### Requirement: Resource management write store
`UBookIt.Core` SHALL expose an `IResourceManagementStore` port (create, full update, delete, paged list with total) accepting only domain `Resource` aggregates — which are constructible solely via the validating Core factories — and `UBookIt.Persistence` SHALL implement it against SQL Server. `IResourceStore` SHALL remain unchanged.

#### Scenario: Created resource is readable through the existing store
- **WHEN** a resource is created through the management store
- **THEN** `IResourceStore.GetAsync` returns a value-equal resource

#### Scenario: Paged list reports totals
- **WHEN** 25 resources exist and the management store lists with skip 20, take 10
- **THEN** 5 items and a total of 25 are returned

### Requirement: Write-path exception uniqueness
Availability writes SHALL replace a resource's open-hours and exception rows wholesale within a single transaction, so that duplicate exception dates for a resource cannot exist after any write, including under concurrent updates (each transaction writes a complete, internally consistent set; concurrent full updates are last-writer-wins). (Discharges the write-surface uniqueness obligation recorded at the persistence archive.)

#### Scenario: Racing updates never produce duplicates
- **WHEN** two updates with different exception sets for the same resource run concurrently
- **THEN** the final state equals exactly one writer's complete set and contains at most one exception row group per date

#### Scenario: Update replaces, never merges
- **WHEN** a resource with a Monday window and one exception is updated to have only a Tuesday window and no exceptions
- **THEN** reloading shows exactly the Tuesday window and zero exceptions

### Requirement: Delete semantics at the store
Deleting an unclaimed resource SHALL remove it and its availability child rows. Deleting a resource with any booking claims SHALL fail with stable code `resource-in-use` (a new additive `FailureCodes` entry) and change nothing — including when a claim is placed concurrently with the delete (the restrictive foreign key is the backstop and its violation maps to the same structured failure).

#### Scenario: Delete refused for claimed resource
- **WHEN** the management store deletes a resource that has a booking claim
- **THEN** the result is failure with code `resource-in-use` and the resource and its configuration remain

#### Scenario: Delete removes configuration rows
- **WHEN** an unclaimed resource with open-hours and exception rows is deleted
- **THEN** no rows for that resource remain in any uBookIt table
