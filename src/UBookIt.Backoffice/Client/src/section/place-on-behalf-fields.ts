import type { ApiError } from "./api-errors.js";

/**
 * The placement dialog's decisions, as pure functions so they are tested as claims — the
 * client's established pattern, and the only one available: the suite has no DOM environment,
 * so the element itself renders these and asserts nothing.
 *
 * Four things live here. What the dialog opens showing. What it sends (a wall-clock start with
 * no offset, which the server reads in the site's zone — the same convention the move dialog and
 * the list's window dates follow). What it says when the domain refuses. And whether the booking
 * it just placed is one the list being shown would contain, which decides whether the operator
 * is told where it went or merely that it happened.
 */

/** What the dialog's fields hold. */
export interface PlaceOnBehalfFields {
  /** `"service"` or `"resource"`, and the id of whichever was chosen. */
  subject: { kind: "service" | "resource"; id: string } | undefined;
  /** `YYYY-MM-DD` */
  date: string;
  /** `HH:mm`, 24-hour */
  time: string;
  lengthMinutes: number;
  bookerName: string;
  bookerEmail: string;
  bookerPhone: string;
}

/**
 * What the dialog opens showing: nothing chosen, and the date the operator is already looking
 * at.
 *
 * **The window's first date rather than today**, because an operator taking a booking while
 * looking at next week almost certainly means next week — and because a default of today on a
 * screen showing another week produces a booking that vanishes from the table the moment it is
 * made, which is the confusion `placedOutsideWindow` exists to handle rather than to cause.
 */
export function emptyFields(windowStart: string): PlaceOnBehalfFields {
  return {
    subject: undefined,
    date: windowStart,
    time: "",
    lengthMinutes: 60,
    bookerName: "",
    bookerEmail: "",
    bookerPhone: "",
  };
}

/** What the endpoint is sent. A wall-clock start with NO offset, for the move dialog's reason. */
export function toRequest(fields: PlaceOnBehalfFields): {
  serviceId?: string;
  resourceId?: string;
  start: string;
  lengthMinutes: number;
  bookerName: string;
  bookerEmail: string;
  bookerPhone?: string;
} {
  const phone = fields.bookerPhone.trim();

  return {
    // EXACTLY ONE, by construction rather than by two nullable fields the caller fills in: the
    // server refuses a request naming both, and building the body from one chosen subject means
    // the client cannot produce that request by forgetting to clear the other one.
    ...(fields.subject?.kind === "service"
      ? { serviceId: fields.subject.id }
      : { resourceId: fields.subject?.id }),
    start: `${fields.date}T${fields.time}:00`,
    lengthMinutes: fields.lengthMinutes,
    bookerName: fields.bookerName.trim(),
    bookerEmail: fields.bookerEmail.trim(),
    ...(phone === "" ? {} : { bookerPhone: phone }),
  };
}

/**
 * Whether the fields can be sent at all.
 *
 * A convenience, not the rule: the endpoint refuses each of these with the domain's own code,
 * and the dialog shows that too. This exists so the obvious case is caught before a round trip.
 * The address is checked for shape only — whether it is deliverable is not a thing any client
 * knows, and the server's own validation is the one that decides.
 */
export function isComplete(fields: PlaceOnBehalfFields): boolean {
  return (
    fields.subject !== undefined &&
    fields.subject.id !== "" &&
    /^\d{4}-\d{2}-\d{2}$/.test(fields.date) &&
    /^\d{2}:\d{2}$/.test(fields.time) &&
    Number.isInteger(fields.lengthMinutes) &&
    fields.lengthMinutes > 0 &&
    fields.bookerName.trim() !== "" &&
    fields.bookerEmail.trim() !== ""
  );
}

/**
 * The localisation key for a refusal, by the domain's stable code — or the generic one for a
 * code this client does not know.
 *
 * A closed map, on `refusalTerm`'s terms: a code added to the domain later arrives here as the
 * generic sentence and a deliberate decision, not as a raw code in a dialog. Every key named
 * here has a string in `en-us.ts`, and the localisation test holds that.
 */
export function refusalTerm(code: string | undefined): string {
  switch (code) {
    case "outside-open-hours":
      return "placeRefusedOutsideOpenHours";
    case "conflict":
      return "placeRefusedConflict";
    case "lead-time":
      return "placeRefusedInThePast";
    case "granularity":
      return "placeRefusedGranularity";
    case "duration-too-short":
    case "duration-too-long":
      return "placeRefusedDuration";
    case "interval-invalid":
      return "placeRefusedInterval";
    case "resource-not-found":
    case "service-not-found":
      return "placeRefusedSubjectNotFound";
    case "service-unavailable":
      return "placeRefusedServiceUnavailable";
    case "resource-not-eligible":
      return "placeRefusedResourceNotEligible";
    case "booking-subject-invalid":
      return "placeRefusedSubject";

    // THE BOOKER'S OWN DETAILS, and these were missing until the live probe found them: the
    // dialog marked the right field invalid and moved focus to it, and then said only "The
    // booking could not be recorded" — so an operator saw an outlined box and no reason. The
    // discrimination worked; the sentence did not.
    case "email-invalid":
      return "placeRefusedEmail";
    case "name-required":
      return "placeRefusedName";
    default:
      return "placeFailed";
  }
}

/** The sentence for a refusal: the FIRST error's term, because the domain orders by its pipeline. */
export function refusalTermFor(errors: ApiError[]): string {
  return refusalTerm(errors[0]?.code);
}

/**
 * Whether a refusal is about the booker's details rather than the time.
 *
 * The two call for different corrections, and marking the time inputs invalid because an
 * address was malformed would send an operator to change a time that was fine — the same
 * reasoning `refusalConcernsFields` records for the move dialog.
 */
export function refusalConcernsBooker(errors: ApiError[]): boolean {
  return errors.some(
    (error) =>
      error.field === "BookerEmail" || error.field === "BookerName" || error.field === "BookerPhone",
  );
}

/**
 * Whether a booking that has just been placed falls inside the window the list is showing.
 *
 * **This exists because an operator who sees nothing happen concludes nothing happened.** Every
 * other action on this screen operates on a row already in front of them; this one can produce a
 * booking for next month on a screen showing this week, and leaving the table unchanged is
 * indistinguishable from a failure.
 *
 * Compared on the site-local DATES the window is expressed in, not on instants: the window is a
 * pair of dates and the list matches any booking overlapping them, so an instant comparison
 * would disagree with the server at both edges.
 */
export function placedInsideWindow(
  placedStartLocalDate: string,
  from: string,
  to: string,
): boolean {
  return placedStartLocalDate >= from && placedStartLocalDate <= to;
}

/**
 * The site-local date a placed booking starts on, from the response's UTC instant and the zone
 * it reports.
 *
 * Derived from the RESPONSE rather than from what the operator typed, deliberately: the two
 * agree on any ordinary site, and where they do not — a site whose zone was changed between the
 * dialog opening and the request landing — the response is what actually happened.
 */
export function placedLocalDate(
  startUtc: string,
  timeZoneId: string,
  formatter: (options: Intl.DateTimeFormatOptions) => Intl.DateTimeFormat = (options) =>
    new Intl.DateTimeFormat("en-GB", options),
): string {
  const parts = formatter({
    timeZone: resolveZone(timeZoneId),
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).formatToParts(new Date(startUtc));

  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find((p) => p.type === type)?.value ?? "";

  return `${part("year")}-${part("month")}-${part("day")}`;
}

/** The zone if the runtime knows it, UTC if it does not — the row's own rule. */
function resolveZone(timeZoneId: string): string {
  try {
    new Intl.DateTimeFormat("en", { timeZone: timeZoneId });
    return timeZoneId;
  } catch {
    return "UTC";
  }
}
