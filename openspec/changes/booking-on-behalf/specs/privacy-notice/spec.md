## MODIFIED Requirements

### Requirement: The booking form states what happens to the details it collects

Every booking flow the package ships **for a person to complete about themselves** SHALL present
a privacy notice **at the point where contact details are collected**, and SHALL state all four
of:

- **what** personal data is collected — the booker's name, email address and, where given, phone
  number;
- **why** it is collected — to hold and identify the booking, and so that the site is able to contact the booker about it;
- **how long** it is kept, per the requirement below;
- **who can see it** — that a booker's contact details are shown only to backoffice users
  Umbraco permits to see sensitive data, per the `sensitive-data` capability.

**At the point of collection, not on some other page.** A notice a visitor must go looking for is
not a notice. It SHALL appear in the same form that asks for the details, ahead of the control
that submits them, so that it is read before the data is given rather than after.

**A screen on which an OPERATOR records somebody else's details is not such a flow, and SHALL
NOT present this notice.** The requirement is addressed to the person whose data it is, at the
moment they hand it over. On an operator's screen that person is not present — they spoke on the
telephone or stood at a desk — so the notice would be rendered to a member of staff, read by
somebody it was not written for, and would inform nobody who needed informing. Showing it there
would make this capability's guarantee *look* kept while the data subject learned nothing, which
is worse than the gap it papers over.

**This narrowing does NOT discharge the obligation to tell that person; it locates it.** What a
site must tell somebody whose details were taken by telephone is a real question, and the package
does not answer it today: the booker's own message is the first thing that actually reaches them,
and it carries no such statement. Recorded here as an open question rather than left as an
implication of the wording, because a requirement narrowed in silence reads afterwards as a
requirement that never applied.

**The notice SHALL NOT gate submission.** It is a statement, not a consent mechanism: there is
nothing to tick, nothing to agree to, and a booking SHALL complete exactly as it did before. The
lawful basis for holding a booker's details is performance of the booking, and an unrefusable
tickbox would misrepresent that as consent — which would also imply a right to withdraw it and
make the site's own records revocable.

#### Scenario: The notice appears where the details are asked for
- **WHEN** a visitor reaches a step that asks for their own contact details, in any flow the package ships for a person to complete about themselves
- **THEN** the privacy notice is present in that same form, ahead of the control that submits it

#### Scenario: All four statements are made
- **WHEN** the notice renders
- **THEN** it states what is collected, why, how long it is kept, and who can see it

#### Scenario: Booking is unaffected by the notice
- **WHEN** a visitor completes a booking
- **THEN** it succeeds without the visitor having agreed to, ticked, or dismissed anything, and the notice offers no such control

#### Scenario: An operator's screen presents no visitor notice
- **WHEN** an operator records a booking on somebody's behalf and enters that person's name, email address and telephone number
- **THEN** the package presents no privacy notice on that screen, because the person it addresses is not the one reading it
