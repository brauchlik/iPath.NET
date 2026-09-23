# LESSONS — what working with Dr. Jundt taught us

Dated evidence behind `../../../../../.opencode/skills/questionnaire-builder/reference/specialist-elicitation.md`.
That file is the method; this file is the record it was derived from. Both grow as he gives
more feedback.

---

## The one that got away

**2026-09-10** — `Form Suggestions — Questions for Dr. Jundt.md` §3 recorded a decision:

> *"Your form uses 'unknown / no / yes' for symptoms. Our earlier meeting agreed on yes/no
> only (D1). **We adopted your 3-state format** — it's clinically more accurate since
> 'unknown' is genuinely different from 'no'. This change affects all existing symptom
> blocks. Once you approve, we'll update them."*

**It was never applied.** Every symptom block still uses `boolean`, and a boolean renders
**Yes / No / Not Answered** — a third state he never asked for, conflating "he says it is
unknown" with "he did not answer". It surfaced again on 2026-09-21 as a visual oddity in his
own screenshot.

**Lesson:** an agreed decision that is not written into the artefacts is not a decision.
This is why the method keeps the library as the single authority and reconciles every input
against it.

**Resolved 2026-09-21** — confirmed with Jundt by phone, applied to the registry (47 items
converted to a 3-state `choice`) and both samples rebuilt. The lesson stands: it took eleven
days and a phone call to close a decision that was already written down.

---

## Defects found in our own artefacts

| Finding | Evidence | Consequence |
|---|---|---|
| `radiation.history` coded `416237000` | resolves to **"Procedure not done (situation)"**, not "History of radiation therapy" | wrong concept exported; correct is `429479009` |
| `breast.lump` coded `274647000` | **404 — does not exist** on the terminology server | invalid code in two blocks; valid concept is `89164003` |
| `pregnancy.previous.lastdelivery` typed `date` | the extractor silently drops `date`/`time` | answer never reaches the export |
| `pregnancy.week` typed `integer` | case-description preview handles Boolean/Choice/Quantity/Group only | invisible in the narrative |
| `menopause → regularcycle → irregularities` | grandchild under a question | never renders in the case-description preview |
| `lab.hematology` differential nested under `granulocytes` | grandchild | the differential is silently dropped |

**Lesson:** none of these were caught by the app — upload performs no FHIR validation, and
the conformity checker has no item-type whitelist. Two wrong codes out of a handful checked
means **inherited codes must be verified, not trusted**.

---

## Structure churn — the normal case, not an exception

| Date | Input | What changed |
|---|---|---|
| 2026-08-14 | `originals/Clinical data_Details.docx` | "Previous radiation therapy" written **inside** the *Soft tissue Tumours* block, with the note *"(Gilt auch für Knochen!)"* |
| 2026-08-18 | agent design approved | pipeline: intake → intent → SNOMED → FHIR → review |
| 2026-09-02 | `meeting-2026-09-02.md` | block-by-block review agenda |
| 2026-09-10 | `Form Suggestions — Questions for Dr. Jundt.md` | 3-state symptoms adopted (not applied) |
| 2026-09-17 | v1 (`odt`/`htm`) | 7 regions |
| 2026-09-21 | v2 (`docx`) | **19 regions** — respiratory, urinary, bone, skin, CNS, thyroid, male genital added; `C60–C63` relabelled correctly; a general lab gate disappeared |
| 2026-09-21 | `feedback.txt` + annotated PDF | C49 approved; radiation needs *"(time period and protocol)"*; the general symptom list **rejected** |

**Lessons:**
- He **supersedes** rather than amends, and **recycles his own older documents** (v2's
  lymph-node section is his earlier form suggestion).
- He **adds and forgets**: the general list, the respiratory block and seven general symptoms
  vanished between versions with no note. Removals must be asked about explicitly.
- **A row's position in his document is not a semantic classification** — that is the entire
  cause of the radiation mis-filing.
- He delivers **incrementally** and says so ("some C-forms I'll send later"), so `pending`
  is a real, long-lived state.

---

## His rejections, and what they settled

| He rejected | Where | What it settles |
|---|---|---|
| the **general "Other Symptoms" list** (25 items) — *"Diese Liste ist nicht notwendig"* | annotated PDF, 2026-09-21 | no general symptom list; **region-specific only**; keep "others" as **free text** |
| the same, for the old staged form | same | the general+tag model is superseded; the `symptoms` master block dies, its items survive |

**Lesson:** he is consistent about wanting *region-relevant* content. Questions we thought
were "missing" (heartburn, constipation, chronic chest pain, smoker) were **deliberate
omissions**, not gaps. Do not chase them back in.

---

## Format and medium

| Observed | Action |
|---|---|
| `.odt` and `.docx` parse perfectly | ask for those |
| flat Word `.xml` = 432 KB for 5.7 KB of text | accept, but prefer the docx twin |
| `.htm` (windows-1252, NBSP indentation) | readable; the `?` noise was **our** decoding bug, not his document |
| **PDF is image-only** — one ink highlight, empty popup, no text layer, no `AcroForm` | never rely on a PDF for content; ask for words or read it by eye |
| he reviewed in the **app**, not our viewer | the test server is the review surface; the viewer is out of the loop |

**Lesson:** verify before blaming the input. The encoding "defect" and the "missing feedback"
were both our misreading of the medium.

---

## Open, and worth remembering

- **`tier` (`core` / `standard` / `extended`)** is not decided. Until it is, we cannot express
  "minimal form for a generalist group" vs "extensive form for a specialist group" from a
  prompt.
- **Community-level questionnaire assignment** exists in the model but is **not** merged into
  case creation — the assignment unit today is the **group**. A community such as the
  Arbeitsgemeinschaft Knochentumor would want one form across many groups.
- **AGKT** (`Follow Up/AGKT/`) is the natural next pilot: a specialist group, a different form
  type, and a three-level nested chain that the case-description preview cannot render.
