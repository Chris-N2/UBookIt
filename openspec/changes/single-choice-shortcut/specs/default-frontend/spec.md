## MODIFIED Requirements

### Requirement: A site may enter the flow at any point
The front-end SHALL let a site author start a visitor at the catalogue, at a
particular service, or at a particular resource, and SHALL NOT require the catalogue
to be traversed first.

A site offering one service should be able to render its booking flow directly. Making
a visitor choose that service from a list of one is a step that exists only because
the software has more than one shape, which is not the visitor's problem.

Each entry point SHALL be independently renderable and SHALL produce the same flow
from that point on as it would have if reached from the catalogue.

**A catalogue of one SHALL NOT be shown while the site's `UBookIt:SkipSingleChoiceCatalogue`
setting is on.** When the flow is entered with no subject — the site author named none and the
request URL carries none — and the catalogue would offer **exactly one** choice, the front-end
SHALL render that choice's flow instead, **as if the visitor had chosen it from the catalogue**:
the steps that follow SHALL carry what is being booked in their URLs exactly as they do after a
real choice, so that a later step stays linkable if the site comes to offer a second thing.

"Exactly one choice" SHALL be counted over the catalogue as the requirement *A catalogue of
bookable things* composes it — every service plus every resource that permits direct booking —
and over nothing else. In particular it SHALL NOT depend on whether that choice currently has
availability: a choice with none leads to its flow's own explanation of that, as it would if the
visitor had chosen it. A catalogue offering no choices, or two or more, SHALL be rendered as it
is today, whatever the setting says.

The setting defaults to **on**. With it off, the catalogue SHALL be rendered whenever there is no
subject, however many choices it offers, which is the behaviour before this requirement existed.
A subject the site author names, or one carried in the URL, SHALL take precedence over the
catalogue whatever the setting says, exactly as before.

The visitor SHALL still be told what they are booking: the flow rendered in place of the
catalogue is the same flow, with the same heading naming the thing booked, as the one the
visitor would have reached by choosing it.

#### Scenario: A site starts at one service
- **WHEN** a site author renders the flow for a particular service
- **THEN** the visitor sees that service's date and length step, with no catalogue

#### Scenario: A site starts at the catalogue
- **WHEN** a site author renders the catalogue, and it offers two or more choices or the skip setting is off
- **THEN** a visitor may choose any bookable thing and continue into its flow

#### Scenario: The entry point does not change what follows
- **WHEN** a service flow is reached directly and by way of the catalogue
- **THEN** the steps, controls and outcomes from that point are the same

#### Scenario: A catalogue of one service is skipped
- **WHEN** the flow is entered with no subject, the skip setting is on, and the catalogue's only choice is a service
- **THEN** the visitor sees that service's date and length step, headed with the service's name, and no catalogue

#### Scenario: A catalogue of one resource is skipped
- **WHEN** the flow is entered with no subject, the skip setting is on, and the catalogue's only choice is a resource that permits direct booking
- **THEN** the visitor sees that resource's booking step, headed with the resource's name, and no catalogue

#### Scenario: A skipped catalogue behaves as a choice made
- **WHEN** a visitor reaches a flow because the catalogue was skipped, and moves on to its next step
- **THEN** that step's URL names what is being booked, exactly as it would had the visitor chosen it from the catalogue

#### Scenario: A skipped catalogue is the same flow as a chosen one
- **WHEN** a flow is reached because the catalogue was skipped, and the same flow is reached by choosing it from the catalogue
- **THEN** the steps, controls and outcomes from that point are the same, the confirmation included

#### Scenario: Two choices are not skipped
- **WHEN** the flow is entered with no subject, the skip setting is on, and the catalogue offers one service and one resource
- **THEN** the catalogue is rendered offering both

#### Scenario: An empty catalogue is not skipped
- **WHEN** the flow is entered with no subject, the skip setting is on, and the catalogue offers no choices
- **THEN** the catalogue is rendered exactly as it is when empty today

#### Scenario: A resource not offered on its own does not count
- **WHEN** a site has one service and one resource that withholds permission to be booked on its own, and the flow is entered with no subject with the skip setting on
- **THEN** the service's flow is rendered, because the catalogue offers only the service

#### Scenario: A choice without availability is still skipped to
- **WHEN** the catalogue's only choice currently has no availability, and the flow is entered with no subject with the skip setting on
- **THEN** that choice's flow is rendered with its own explanation of the absence, and no catalogue

#### Scenario: The setting off restores the catalogue
- **WHEN** the skip setting is off and the flow is entered with no subject on a site whose catalogue offers exactly one choice
- **THEN** the catalogue is rendered offering that one choice

#### Scenario: A named subject wins whatever the setting says
- **WHEN** the flow is entered with a subject named by the site author or carried in the URL
- **THEN** that subject's flow is rendered, whether the setting is on or off and however many choices the catalogue offers
