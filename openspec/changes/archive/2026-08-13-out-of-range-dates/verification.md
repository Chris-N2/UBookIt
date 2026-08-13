# Live boundary verification

Every case below was issued against the running TestSite over HTTPS. The probe
script is deterministic and fixed-order, so BEFORE and AFTER are directly
comparable. Cases `A*`/`P*` are the defects; cases `O*` are controls that were
already correct and must not change.

## BEFORE — pre-fix build, 2026-08-13

| Case | Request | Status | Codes |
| --- | --- | --- | --- |
| A1 | `GET /resources/{id}/free-time?from=9999-12-31&to=9999-12-31` | **500** | — |
| A2 | `GET /resources/{id}/slots?…&durationMinutes=60` | **500** | — |
| A3 | `GET /resources/{id}/bookable-starts?…` | **500** | — |
| A4 | `GET /services/{id}/bookable-starts?…` | **500** | — |
| P1 | `POST /bookings` start `9999-12-31T23:00:00Z` | **500** | — |
| P2 | `POST /services/{id}/bookings` same start | **500** | — |
| P3 | `POST /bookings` `durationMinutes=-2147483648` | **500** | — |
| P4 | `POST /bookings` start `0001-01-01T00:00:00Z` | **500** | — |
| P5 | `POST /services/{id}/bookings` same start | **500** | — |
| O1 | `GET /resources/{id}/free-time` at `0001-01-01` | 200 | — |
| O2 | `GET /resources/{id}/bookable-starts` at `0001-01-01` | 200 | — |
| O3 | `GET /services/{id}/bookable-starts` at `0001-01-01` | 200 | — |
| O4 | `GET /resources/{id}/free-time` at `9999-12-30` | 200 | — |
| O5 | `POST /bookings` `durationMinutes=2147483647` | 400 | `granularity`, `duration-too-long`, `outside-open-hours` |
| O6 | `POST /bookings` `durationMinutes=-60` | 400 | `interval-invalid` |
| O7 | `POST /services/{id}/bookings` `durationMinutes=-60` | 400 | `duration-too-short` |

**500s: 9.**

Note O5: a large *positive* duration is not a trigger — `int.MaxValue` minutes is
about 4,084 years, which lands short of the representable ceiling from a
present-day start. Note O7 vs O6: the service route answers `duration-too-short`
where the direct route answers `interval-invalid`, because the service pool-wide
duration pre-check runs ahead of the placement pipeline. Both are correct; they
differ by design and must keep differing.

## AFTER — fixed build, same probe, same order

| Case | Status | Codes |
| --- | --- | --- |
| A1 | 400 | `date-range-invalid` |
| A2 | 400 | `date-range-invalid` |
| A3 | 400 | `date-range-invalid` |
| A4 | 400 | `date-range-invalid` |
| P1 | 400 | `interval-invalid` |
| P2 | 400 | `interval-invalid` |
| P3 | 400 | `interval-invalid` |
| P4 | 400 | `interval-invalid` |
| P5 | 400 | `interval-invalid` |
| O1 | **400** | `date-range-invalid` — changed, deliberately (see below) |
| O2 | **400** | `date-range-invalid` — changed, deliberately |
| O3 | **400** | `date-range-invalid` — changed, deliberately |
| O4 | 200 | unchanged |
| O5 | 400 | `granularity`, `duration-too-long`, `outside-open-hours` — unchanged |
| O6 | 400 | `interval-invalid` — unchanged |
| O7 | 400 | `duration-too-short` — unchanged |

**500s: 0.**

### The three controls that did change

O1–O3 queried the *first* representable date and returned 200 before. They now
return 400. This is the deliberate narrowing recorded in the proposal: the same
query throws for any site zone east of UTC, because mapping a wall-clock time on
year one into UTC lands before the first representable instant. The old
behaviour was not "works" but "works in London", so the lower edge is now
rejected on the same terms as the upper one.

### One behaviour corrected after the first AFTER run

The first pass had P2 and P5 answering `service-unavailable` where the direct
route answered `interval-invalid` — the candidate loop was translating a
request-level fault into an all-candidates-failed outcome. An unrepresentable
interval is a property of the request, identical for every candidate, so it is
now echoed rather than translated, exactly as a broken site time zone already
was. Both routes now agree.

### Four more cases added during QA remediation

QA found that the interval guard was wrong for a start carrying a non-zero UTC
offset: it measured headroom in UTC, while the addition moves the clock
component. Three anonymous requests were still 500 after the first fix, and no
test caught them because every case used a zero offset. The probe now covers the
offset dimension, and all four are 400 `interval-invalid`:

| Case | Request | After remediation |
| --- | --- | --- |
| P6 | `POST /bookings` start `9999-12-31T23:00:00+14:00` | 400 `interval-invalid` |
| P7 | `POST /bookings` start `0001-01-01T00:00:00-14:00`, duration −60 | 400 `interval-invalid` |
| P8 | `POST /services/{id}/bookings` start `9999-12-31T23:00:00+14:00` | 400 `interval-invalid` |
| P9 | `POST /bookings` start `9999-12-31T23:00:00+01:00` | 400 `interval-invalid` |

P9 matters: a one-hour offset is enough. This was not an extreme-value problem.

**20 cases, 0 × 500.**

### Normal behaviour, same session

Free-time returns its single interval; the resource offers 16 bookable starts;
the service offers 31 starts with at most 2 runs at any start (its pool has two
distinct grids); a service booking and a direct booking both place and confirm.

