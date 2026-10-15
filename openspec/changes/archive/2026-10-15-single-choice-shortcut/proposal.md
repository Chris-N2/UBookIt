## Why

The packaged Booking Page renders `BookingFlow` with no subject, so a visitor always starts at
the catalogue. On a site offering exactly one bookable thing, that means choosing it from a list of
one before the date step appears. The spec already calls this step something that "exists only
because the software has more than one shape, which is not the visitor's problem", but today the
only way to avoid it is for an author to name the subject in Razor or link with
`?ubBook=…`, and the packaged page gives an editor no way to do either. Chris's own production
site works around it with query-string links.

## What Changes

- When the `BookingFlow` component is entered **without a subject** (no author-supplied id, no
  `ubBook` query) and the catalogue it would render has **exactly one entry**, it renders that
  entry's flow instead, **as if the visitor had chosen it from the catalogue**. The flow token is
  carried, so every later step's URL names the subject, exactly as after a real choice.
- "One entry" means one entry **in the catalogue as already composed**: every service, plus every
  resource that permits direct booking. No second notion of what is bookable is introduced. Zero
  entries keeps today's empty catalogue, and two or more show the catalogue, including one service
  plus one resource.
- A new **editable** site setting, `UBookIt:SkipSingleChoiceCatalogue` (bool), **on by default**,
  turns this off for a site that wants its catalogue shown regardless (for example, because it has
  written page copy around it). It is editable in the backoffice because the documented tier rule
  puts what a visitor is shown under "policy and communication", which belongs to whoever runs the
  bookings. It is stored first and configured second, like every editable setting. An absent value
  is on. A written value that cannot be read is on and reported, on `AutoConfirm`'s precedent.
- **Behaviour change for existing sites:** an upgraded site with exactly one catalogue entry stops
  showing its catalogue. This is not a public-API break, but it changes what renders, so it ships
  in a **minor** (`17.4.0` / `18.3.0`) with a changelog note that names the setting and how to turn
  it off.
- Docs: `booking-page.md` (what the bare page shows), `configuration.md` (the new setting in the
  editable table and the fallback table), and a sentence that the delivery API is unchanged, so a
  headless consumer decides for itself.
- Ported to `dev/v18` in the same sitting.

## Non-goals

- **No change to the delivery API.** It has no catalogue; a headless client builds the list from
  `/services` and `/resources` and makes its own choice.
- **No availability-based counting.** An entry with no free dates still counts. Counting "things
  with availability" would be a second notion of bookable, and it would make the page's shape
  depend on the calendar.
- **No new view, view model, theme contract entry, endpoint or migration.** The setting is stored
  in the existing settings table.
- **No change to the legacy `Booking` component**, to an author-named subject, or to a `ubBook`
  query. Those already bypass the catalogue.
- **No "back to the catalogue" link.** The flows have none today, and none is added.
- **No public signature changes.** `FlowEntry.Resolve`, `BookingCatalogue.BuildAsync`,
  `BookingFlowViewComponent.InvokeAsync` and every published constructor keep their shapes. New
  behaviour gets new members.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `default-frontend`: *A site may enter the flow at any point* gains the single-entry rule and
  its setting, and carries every existing scenario forward. **One is deliberately narrowed:**
  *A site starts at the catalogue* ("a visitor may choose any bookable thing") now applies when
  the catalogue offers two or more choices or the setting is off. With one choice and the setting
  on, the visitor is taken to that choice, which is the point of the change. *A catalogue of
  bookable things* is not modified: each of its scenarios stays true as written.
- `site-settings`: *Settings are tiered, and the tier is enforced by the server* lists the new key
  in the **Editable** tier, and carries every existing scenario forward.
- `persistence`: a new requirement (ADDED, so no existing requirement is replaced) for how the
  setting resolves: absent means on and is silent; unreadable means on and is reported; a readable
  false is honoured.

## Impact

- **Core:** `SiteBookingSettings` gains a defaulted property (`= true`). This is additive.
- **Persistence:** a key constant, resolution in `ResolveSettings`, and a startup error for an
  unreadable written value, alongside `ErrorIfAutoConfirmUnreadable`.
- **Backoffice:** a `SettingCatalogue` entry (Editable, Boolean), a `SettingText` mapping, and the
  en-us label and description terms. The settings screen renders booleans generically, so no new
  element is needed.
- **Web:** `BookingFlowViewComponent` reads the setting and, on the no-subject path, enters the
  sole entry's flow. Getting the setting into the component must not change its published
  constructor.
- **Tests:** unit tests for resolution and catalogue presence; dispatcher tests for 0/1/2 entries
  with the setting on and off, a service entry and a resource entry, and the token carried into
  the next step; a rendering test through the real component.
- **Docs and release:** `docs/booking-page.md`, `docs/configuration.md`, `CHANGELOG.md`,
  README feature line if affected; the minor-release procedure afterwards, as its own change.
