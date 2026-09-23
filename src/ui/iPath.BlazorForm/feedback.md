# feedback.md — review of the native questionnaire renderer

**From:** the questionnaire-library side (the offline block/question authoring work
under `import/fhir/Questionnaires/`)
**To:** whoever/whatever maintains `src/ui/iPath.BlazorForm`
**Date:** 2026-09-21
**Status of this document:** review input, not a bug list. The renderer is explicitly
work in progress and this is written to be reviewed *with* the code agent, not against it.

---

## Why this review exists

We author FHIR R4 `Questionnaire` resources offline, publish them into a test iPath
server, and review them with a pathologist **in the app**. The app can now render a
questionnaire two ways (`LForms` or `Native`), selected per questionnaire via
`QuestionnaireSettings.PreferredRenderer`. That makes the native renderer a **consumer of
our artefacts**, with its own supported subset — and a consumer's limits are authoring
constraints for us.

The intent, as I understand it: **simple forms are authored to the native renderer;
complex forms fall back to the full LHC viewer.** That is a deliberate two-tier design, and
it maps neatly onto a variation axis we need anyway (minimal form for a generalist group vs
extensive form for a specialist group).

So the most useful thing this document can do is **define the boundary precisely**, so both
sides know which tier a given form belongs to.

---

## What looks solid

- `QuestionnaireFormHost.razor` — dispatching two implementations behind
  `IQuestionnaireForm`, with the renderer-swap reload tracking (`_reloadedFor`,
  `OnAfterRenderAsync`). The comment explaining why a swap needs a reload is exactly the kind
  of thing that saves the next reader an afternoon.
- `PreferredRenderer` being **nullable with `null` = LForms** — existing questionnaires keep
  their behaviour. Good migration shape.
- The interop hardening in `lhcformsJsInterop.js` for the `_codingsEqual` null-dereference
  (which AGENTS.md notes upstream 44.0.0 never fixed) — making `guardCodingComparison`
  return a promise and retrying `addFormToPage` once is a sensible, narrow fix.
- `questionnaire-itemControl` **is honoured** (`QuestionnaireItemView.razor:108-112`),
  including `radio-button` — which is what we now emit for single-select reveals.
- `string` vs `text` is honoured (`:47` vs `:49`) — multiline vs single-line. We care about
  that distinction.

---

## The capability matrix — where the two consumers disagree

| Type | Native renderer | Extractor (`QuestionnaireAnswerExtractor.cs`) | Notes |
|---|---|---|---|
| `boolean` | ✅ checkbox (`:52`) | ✅ `:76` → `"boolean"` | native uses a checkbox; LForms uses Yes/No/Not Answered |
| `integer` / `decimal` | ✅ `:44-45`, `:57-59` | ✅ `:77-78` | |
| `string` / `text` | ✅ `:47`, `:49`, `:54` | ✅ `:80` → `"string"` | |
| `choice` (+`repeats`) | ✅ select / radio / checkbox (`:63-106`) | ✅ `:82-83` | |
| **`quantity`** | ❌ **falls to a text box** (`:49`) | ✅ `:79` → `"quantity"` + unit | **all labs, durations, sizes** |
| `date` | ✅ DatePicker (`:46`, `:191-195`) | ❌ **silently dropped** (`:87`, `:90`) | |
| `dateTime` | ❌ text box (no case) | ✅ `:81` → `"date"` | |
| `attachment` | ❌ text box | ✅ `:86` | |
| `reference` | ❌ text box | ❌ `:87` | |

**The intersection of "renders natively" and "is exported" is
`boolean, integer, decimal, string/text, choice`.**

That excludes `quantity` and every date type — i.e. every lab value, every duration, every
size class, and every date. Those are exactly the things a "complex tier" form carries.

---

## Findings

### F1 — Unsupported types degrade to a text box *and change the export type* (highest impact)

`QuestionnaireItemView.razor:49` ends the switch with `_ => TextInput(multiline: false)`.
A `quantity` item therefore renders as a free-text box, and `StringValue` (`:173-177`)
stores a `FhirString`. `IPathQuestionnaireForm.razor:114` then serialises that as
`valueString`.

The extractor switches on the **answer's runtime type** (`QuestionnaireAnswerExtractor.cs:74-88`),
so the same questionnaire answered in the native viewer exports `ValueType = "string"`,
while in LForms it exports `"quantity"` **with a unit**. Same form, two renderers, two
different export shapes — and nothing signals it.

*Suggestion:* either render `quantity` properly (a numeric field plus a unit selector built
from the item's `questionnaire-unitOption` extensions — note LForms does exactly this), or
fail honestly: surface the unsupported items and refuse to present the form as complete,
rather than silently changing the answer's type. This is the same principle already applied
on the terminology side — never return a plausible but wrong result.

### F2 — No date type works end to end

- Native handles `Date` (`:46`, `:191-195`) but has no `DateTime` case → `dateTime` renders as
  a text box.
- The extractor handles `FhirDateTime` (`:81`) but has **no `Date` case** → a `date` answer
  hits `_ =>` (`:87`) and `BuildRow` returns `null` (`:90`), so it is dropped.

So `date` renders but does not export; `dateTime` exports but does not render as a date.
**There is currently no item type that both renders as a date and survives export.**

*Suggestion:* add a `DateTime` case to the native renderer writing `FhirDateTime` (same
MudDatePicker), and separately decide whether the extractor should gain a `Date` case. One
of the two has to move for date questions to be authorable at all.

### F3 — `enableWhen` silently hides questions for unsupported operators

`NativeFormState.cs:39` — any operator other than `=` or `exists` returns **false**, so the
item is hidden. `:26` uses `EnableWhen.All(...)`, so `enableBehavior` is ignored (FHIR's
default is `all`, so this is fine today, but a form setting `any` would silently behave as
`all`).

We only use `=` today, so there is no live impact — but it is a trap for the library, and a
hidden question is indistinguishable from an unanswered one.

*Suggestion:* treat an unsupported operator as "enabled, and flag it" rather than "hidden",
or at minimum log it. `!=` and the numeric comparisons are the likely next needs.

### F4 — Option selection is matched by **display text**, not by code

`OptionText` (`:121-126`) resolves an option to `display ?? code`, and both
`SingleSelected` (`:155-165`) and `IsSelected`/`OnChoiceToggled` (`:137-153`) compare options
by that **text**. Two options with the same display but different codes would be
indistinguishable.

The answer written back clones the option's `Value` (`:146`, `:163`), so a coded option is
preserved correctly — it is only the *selection* comparison that is text-based.

This matters because coded answer options are already in the library: the diagnostic block's
`margin.status` and `tumor.depth` use `answerOption.valueCoding` with SNOMED codes. Their
displays are unique, so it works today — but it would be safer to match on
`system|code` when present, falling back to display.

### F5 — An answer node is emitted for every item, including unanswered ones

`IPathQuestionnaireForm.razor:109-116` builds an item for every questionnaire item with
`Answer = ...ToList()`, which is frequently an empty list. It is valid FHIR and harmless for
extraction (the extractor iterates `item.Answer`), but it bloats the stored response
compared with LForms, which omits unanswered items. Worth knowing, not urgent.

### F6 — Smaller items

- **`display` items** are not handled → they fall to `_ =>` and render an empty text input.
  We do not use `display` items, so this is informational.
- **`enableWhen` pointing at a non-existent linkId** yields no answers → `=` fails → the item
  is hidden. Reasonable, but silently so.
- **Read-only rendering** (`:11-17`) shows `Text` + `AnswerText()`; `DisplayOf` (`:199-208`)
  has no `Quantity` case, so a quantity answer would fall to `d.ToString()`. Only reachable
  while F1 exists.

---

## What the library will do in response

So the other side of the contract is explicit:

1. **Define the simple tier** as exactly the intersection above —
   `boolean, integer, decimal, string/text, choice`. A form containing a duration, a lab
   count, a size class or a date is a **complex-tier** form and targets LForms.
2. **Add a composer check** that warns when a form targets `Native` but contains
   native-unsupported types, so the tier decision stays deliberate while the failure mode
   stops being silent.
3. **Record the target renderer per form** as a composition-time decision.
4. **Note for the 3-state symptoms change** (we are moving shared symptom items from
   `boolean` to a `choice` of `Yes`/`No`/`Unknown`, per an agreement with the pathologist that
   was never applied): the native renderer handles that fine as radio or dropdown, and it will
   render as a choice rather than a checkbox — a visible behaviour change between renderers,
   which is expected.

## Questions for the code agent

1. Is `Native` intended to **eventually replace** LForms, or to remain the simple tier? If it
   is to replace it, `quantity` and `dateTime` have to be solved; if it stays the simple tier,
   routing by form is sufficient.
2. **`quantity`** — planned, or deliberately out of scope for the native renderer?
3. Should the unsupported-type fallback **warn or refuse** rather than degrade to a text box?
4. **Dates** — add a `DateTime` case to the renderer, or add a `Date` case to the extractor?
   (One of them must move for date questions to be authorable.)
5. Should option matching become **code-based** where `valueCoding` is present?

---

## Response (2026-09-23)

Answering in order, from a session working through this same code live with the app owner:

1. **Native explicitly stays the simple tier.** No plan to replace LForms. It only needs to
   handle the item types actually appearing in the forms we've been building with Jundt
   (`boolean`, `integer`, `decimal`, `string`/`text`, `choice`, and now `quantity`) — not full
   SDC coverage. Route complex forms (dates, anything else outside that set) to LForms via
   `PreferredRenderer`, as you already proposed. Your composer-side warning-check idea (#2 in
   "what the library will do in response") is the right place to enforce that boundary — please
   go ahead with it on your side.
2. **`quantity` is now implemented** — `QuestionnaireItemView.QuantityInput()` renders a numeric
   field plus a unit `MudSelect` sourced from the item's own `questionnaire-unitOption`
   extensions (generic, not hardcoded to "Duration"), and it round-trips as a real FHIR
   `Quantity` answer. `DisplayOf`'s read-only path was fixed at the same time. This was F1 and
   is resolved.
3. **Fail honestly, per your principle.** Since Native is staying lower-tier by design, an
   item type outside the supported set now renders a visible "Unsupported item type" notice
   instead of silently falling through to a text box that would then export under the wrong
   `ValueType`. If a form needs `dateTime`/`attachment`/`reference`/`display`, that's a signal
   it belongs on LForms, and now it's visible rather than silently wrong.
4. **`dateTime` is now implemented too** — `DateTimeInput()` pairs `MudDatePicker` +
   `MudTimePicker` (MudBlazor has no single combined control) and round-trips as a real
   `FhirDateTime` answer; `DisplayOf`'s read-only path was updated too. Still open on your
   side: the extractor has no `Date` case (`QuestionnaireAnswerExtractor.cs:87,90`), so a
   `date` answer still exports as dropped even though it renders fine natively. That one's
   yours to close.
5. **Option matching by display text stays as-is for now** — no live impact since displays are
   unique in the current forms (per your own note), and it's a two-line change
   (`OptionMatchesValue`) whenever a form actually needs `system|code` matching. Low priority
   given the lower-tier scope.

F3 (`enableWhen` operators) and F5 (empty answer nodes) are acknowledged, no live impact today,
left as-is under the same "fix when a real form needs it" rule.
