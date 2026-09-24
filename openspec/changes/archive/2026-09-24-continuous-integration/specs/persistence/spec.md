## MODIFIED Requirements

### Requirement: Integration test coverage on real SQL Server
Integration tests SHALL run against a real SQL Server instance (connection from the `UBOOKIT_TEST_DB` environment variable, defaulting to LocalDB), creating and dropping a uniquely named database per run. They SHALL cover: migration application and idempotency, resource and booking round-trip fidelity, half-open overlap at the SQL layer, status-change persistence, and the concurrency proof. When no SQL Server is reachable the tests SHALL skip with an explicit diagnostic, never silently pass — unless the run declares a database required by setting the `UBOOKIT_TEST_DB_REQUIRED` environment variable to `true`, in which case an unreachable server SHALL fail the tests with the same diagnostic rather than skip them. Any other value, or the variable's absence, SHALL leave the skip behaviour unchanged.

#### Scenario: Test database lifecycle
- **WHEN** the integration test run completes
- **THEN** the per-run database has been dropped

#### Scenario: Unreachable server is visible
- **WHEN** no SQL Server is reachable at the configured connection and a database is not declared required
- **THEN** integration tests report skipped with a reason, not passed

#### Scenario: Unreachable server fails when a database is required
- **WHEN** no SQL Server is reachable at the configured connection and `UBOOKIT_TEST_DB_REQUIRED` is `true`
- **THEN** integration tests report failed, with the diagnostic naming the connection variable, and none report skipped or passed

#### Scenario: A reachable server is unaffected by the required flag
- **WHEN** a SQL Server is reachable and `UBOOKIT_TEST_DB_REQUIRED` is `true`
- **THEN** the integration tests run exactly as they would without the flag
