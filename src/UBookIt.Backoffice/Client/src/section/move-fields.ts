import type { BookingModel } from "../api/index.js";
import type { ApiError } from "./api-errors.js";

/**
 * The move modal's decisions, as pure functions so they are tested as claims — the client's
 * established pattern, and the only one available: the suite has no DOM environment, so the
 * element itself renders these and asserts nothing.
 *
 * Three things live here. What the modal opens showing (the booking's current interval, in the
 * zone the booking records). What it sends (a wall-clock start with no offset, which the server
 * reads in the site's zone — the same convention the list's window dates follow). And what it
 * says when the domain refuses: one sentence per stable code, so an operator whose time was
 * outside open hours, taken, in the past or unchanged is told which and can change the time
 * without starting over.
 */

/** What the modal's three inputs hold. */
export interface MoveFields {
  /** `YYYY-MM-DD` */
  date: string;
  /** `HH:mm`, 24-hour */
  time: string;
  lengthMinutes: number;
}

type BookingLike = Pick<BookingModel, "startUtc" | "endUtc" | "timeZoneId">;

/**
 * The booking's current interval, as the three fields — in the zone the booking records.
 *
 * The booking's own zone rather than the reader's, for the reason the row is formatted in it: a
 * booking is a record of a local time somebody agreed to. It is also, on any site that has not
 * changed its zone since the booking was placed, the site's zone — which is what the server will
 * read the submitted start in. A site that HAS changed its zone sees the pre-filled time
 * reinterpreted in the new zone on submit, which is the honest consequence of that setting
 * having been changed, and is why the time is shown rather than hidden.
 *
 * An unknown zone id falls back to UTC, as the row does.
 */
export function prefill(
  booking: BookingLike,
  formatter: (options: Intl.DateTimeFormatOptions) => Intl.DateTimeFormat = (options) =>
    new Intl.DateTimeFormat("en-GB", options),
): MoveFields {
  const zone = resolveZone(booking.timeZoneId);
  const start = new Date(booking.startUtc);
  const end = new Date(booking.endUtc);

  // Parts rather than a formatted string, so the result is machine-shaped regardless of locale.
  const parts = formatter({
    timeZone: zone,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
  }).formatToParts(start);

  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find((p) => p.type === type)?.value ?? "";

  return {
    date: `${part("year")}-${part("month")}-${part("day")}`,
    time: `${part("hour")}:${part("minute")}`,
    lengthMinutes: Math.round((end.getTime() - start.getTime()) / 60000),
  };
}

/**
 * What the endpoint is sent: the start as a wall-clock time with NO offset, and the length.
 *
 * No `Z`, no `+01:00`. The server resolves the start against the site's zone, once, because that
 * zone is a server setting and a headless client is not one client. Appending an offset here
 * would make the client the second implementation of a daylight-saving rule.
 */
export function toRequest(fields: MoveFields): { start: string; lengthMinutes: number } {
  return {
    start: `${fields.date}T${fields.time}:00`,
    lengthMinutes: fields.lengthMinutes,
  };
}

/**
 * Whether the three fields can be sent at all — every part present and a positive length.
 *
 * A convenience, not the rule: the endpoint refuses a missing start or a non-positive length
 * with the domain's own code, and the modal shows that too. This exists so the obvious case is
 * caught before a round trip.
 */
export function isComplete(fields: MoveFields): boolean {
  return (
    /^\d{4}-\d{2}-\d{2}$/.test(fields.date) &&
    /^\d{2}:\d{2}$/.test(fields.time) &&
    Number.isInteger(fields.lengthMinutes) &&
    fields.lengthMinutes > 0
  );
}

/**
 * The localisation key for a refusal, by the domain's stable code — or the generic one for a
 * code this client does not know.
 *
 * A closed map, deliberately: a code added to the domain later arrives here as the generic
 * sentence and a deliberate decision, not as a raw code in a dialog. Every key named here has a
 * string in `en-us.ts`, and the localisation test holds that.
 */
export function refusalTerm(code: string | undefined): string {
  switch (code) {
    case "outside-open-hours":
      return "moveRefusedOutsideOpenHours";
    case "conflict":
      return "moveRefusedConflict";
    case "lead-time":
      return "moveRefusedInThePast";
    case "interval-unchanged":
      return "moveRefusedUnchanged";
    case "invalid-status-transition":
      return "moveRefusedStatus";
    case "granularity":
      return "moveRefusedGranularity";
    case "duration-too-short":
    case "duration-too-long":
      return "moveRefusedDuration";
    case "interval-invalid":
      return "moveRefusedInterval";
    case "booking-not-found":
      return "moveRefusedNotFound";
    default:
      return "moveFailed";
  }
}

/**
 * The sentence the modal shows for a refusal: the FIRST error's own term, because the domain
 * orders failures by its pipeline and the first is the one to fix first. The endpoint's
 * message is not shown in its place — it is written for a developer reading a response, and
 * the terms here are written for an operator holding a telephone.
 */
export function refusalTermFor(errors: ApiError[]): string {
  return refusalTerm(errors[0]?.code);
}

/** The booking's zone if the runtime knows it, UTC if it does not — the row's own rule. */
function resolveZone(timeZoneId: string): string {
  try {
    new Intl.DateTimeFormat("en", { timeZone: timeZoneId });
    return timeZoneId;
  } catch {
    return "UTC";
  }
}
