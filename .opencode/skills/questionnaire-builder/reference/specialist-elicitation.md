# Working with a medical specialist — elicitation, reconciliation, mediation

How to get usable content out of a specialist, and how to turn it into a coded,
reusable inventory without re-deriving the same questions every time.

Learned with Dr. Jundt. The dated evidence behind every claim here lives in
`import/fhir/Questionnaires/doc/jundt/LESSONS.md`.

**Audience:** whoever maintains the questionnaire library. The specialist supplies
content. **Structure, classification and coding are always ours.**

---

## 1. The loop

```
request  →  receive  →  transcribe  →  reconcile  →  questions  →  review  →  inventory
 (ask)      (format)    (faithful)     (library+SCT)  (closed)      (in app)    (items + blocks)
```

One round yields one region, or one form type. **Never skip reconcile** — it is the
step that stops a re-lookup producing a second version of a question we already have.

## 2. How to ask

Give the specialist a shape, and keep it small.

- **One region or one form type per document.** He thinks in regions, and we can process
  incrementally.
- **Four columns he already uses:** Question · Answers · Conditional · Detail.
  Add three:
  - **Scope** — the ICD-O range
  - **Form type** — intake / lab / imaging / history / assessment. Only he knows whether
    a patient can self-report a given item.
  - **Uncertain?** — gives his `?` marks a home instead of letting them become prose noise.
- **State removals explicitly.** *"This replaces the previous list; the following are
  intentionally removed: …"* Without this, a removal is indistinguishable from an omission.
- **Ask what changed, not what he thinks.** Diffs between versions are cheap;
  re-litigating structure is expensive.

Format rules:

| Accept | Why |
|---|---|
| `.docx`, `.odt` | real table geometry, clean UTF-8, compact |
| flat Word `.xml` | parses fine, but ~17× the size for identical content |
| Word `.htm` | works, but windows-1252 + NBSP indentation; prefer the `.docx` |
| **avoid PDF / scans / screenshots** | image-only, no text layer, nothing extractable |

Do **not** ask him for FHIR, SNOMED codes, nesting or `enableWhen`. Asking him for
structure invites the layout-driven classification problem (§4.2).

## 3. How to receive

- **He supersedes, he does not amend.** Always work from the latest and diff against the
  previous version. One revision took a document from 7 sections to 19.
- **He recycles his own older documents.** His newest region content was lifted from an
  earlier form suggestion. Cross-check against earlier artefacts, not just the last one.
- **Verify encoding before believing a defect.** A windows-1252 file read as UTF-8 turned
  123 non-breaking spaces into `?` and looked like the document was full of placeholders.
  It contained zero question marks.
- **He annotates visually.** His review PDF was an image plus one orange ink highlight with
  an empty popup — no text at all. Ask for words, or read the PDF by eye yourself.

## 4. How to interpret — the traps

1. **He mixes form types in one table.** Symptoms, lab, imaging and history appear
   together. Classification is our job, never his.
2. **He groups by document layout, not by meaning.** "Previous radiation therapy" sat under
   *Soft tissue Tumours*, between Size and Depth, and inherited that section's filing as
   *diagnostic assessment* — wrongly, for months. **A row's position in his document is
   not a semantic classification.**
3. **His `yes/no/unknown` is not a boolean.** A boolean renders a third state
   ("Not Answered") he never asked for — and *unknown* (he says it is unknown) is not
   *not answered* (he did not answer). Model the three states explicitly.
4. **His table template leaks structure.** `Other | Yes/no | If yes: (specify)` appears in
   every section and means **free text**; the yes/no is a template artefact.
5. **Dates vs durations.** `DD MM YY` where a duration belongs. Ask; do not guess.
6. **Duplicated rows signal intent.** `Erythrocytes` listed twice in one table is a signal
   (probably a missing Hb), not a copy-paste artefact to silently dedupe.
7. **Topography ranges are approximate and sometimes wrong.** `C60–C63` labelled *female*;
   `C30–C34` for the `C30–C39` chapter; `C64` for the whole urinary tract. Check every
   range against the ICD-O reference.
8. **Depth may exceed what the target form type can render.** A follow-up chain three levels
   deep is fine for the default list service and silently truncated by the case-description
   preview. Check the *target consumer*, not just FHIR validity.

## 5. How to reconcile — the library, then SNOMED

Classify every incoming question:

| Outcome | Action |
|---|---|
| **reuse** | already canonical → reference it, add nothing |
| **variant** | same concept, richer shape → new item, declare its base |
| **new** | resolve SNOMED once, add to the registry |
| **conflict** | same concept in a second shape → decide once, record it |
| **dropped** | present before, absent now, **confirmed** by the specialist |
| **pending** | absent but expected — the work-in-progress state |

Then the SNOMED checks:

- **Placement.** Is the incoming concept a *descendant* of one we already have? Then it is a
  variant, not a new question. This is how two near-identical abdominal-pain items in the
  same section get caught.
- **Semantic tag.** If he describes a finding and the concept is a procedure (or vice
  versa), that is a signal, not a match.
- **The concept is often in an unexpected hierarchy.** "History of chemotherapy" sits under
  *situation*, not `clinical_finding`. Searching only findings returns nothing and produces
  a wrong "no concept" conclusion. Search more than one hierarchy.
- **Verify inherited codes; never assume.** Two of a handful of existing codes were wrong —
  one resolved to *"Procedure not done"*, one did not exist at all. Unverified inheritance
  is the default failure mode.
- **Record `no-concept-found`.** "We looked and there is nothing clean" is a legitimate,
  recorded outcome — not a gap to be quietly filled with a plausible-looking code.
- **Record our simplifications.** Where we deliberately collapse SNOMED's finer structure
  into one item, write it down — otherwise someone later rediscovers the finer concepts and
  forks the library.
- **Never fabricate a concept id.**

## 6. How to mediate — SNOMED is usually right, and too complex

His list is a flat, practical projection. SNOMED is a deep, precise model. Neither is wrong;
our job is the translation.

- Do not lecture him with SNOMED's structure. Present the simplified projection and let him
  confirm it.
- Where SNOMED's finer concepts would genuinely help, offer them as **options**, not as a
  correction.
- Where he insists on a flatter shape, record it as a **deliberate simplification** rather
  than a defect.
- **Ask closed questions with options.** *"Is this one field or two?"* works.
  *"How would you model this?"* does not.

## 7. How to handle churn

- **Version; never edit a published definition.** Old responses are version-pinned, so old
  exports keep working.
- Produce an **explicit delta** per version: reuse / variant / new / conflict / dropped /
  pending.
- **Removals are never silent.** They become `dropped` only with confirmation; otherwise
  they stay `pending`.
- Keep a **coverage tracker** per region so "not yet delivered" and "deliberately removed"
  are distinguishable at a glance.
- He delivers incrementally and says so. Treat `pending` as a long-lived, first-class state.

## 8. How to validate before showing him

Validate against the **target consumer**, not just FHIR validity. The application's own
consumers define a supported subset — top-level items must be groups; `date`/`time` are
silently dropped on export; details must be leaves for the case-description preview; radio
buttons need the control extension; free text reaches the export but not the summary.

Where possible, validate with the **real implementation**. A reimplementation of these rules
will drift from the shipping behaviour.

## 9. The communication format that works

A short document per round, in this order:

1. **What we included** from your input — a table, with where each part came from
2. **What we excluded, and why** — e.g. wizard-managed fields
3. **Changes we adopted** and their consequences
4. **Questions**, each with concrete options and a recommendation
5. **Next steps**

It works because it maps his own content back to him, states our reasoning, and never asks
an open-ended question. Precedent: `doc/jundt/Form Suggestions — Questions for Dr. Jundt.md`.

## 10. Anti-patterns — all of these happened

- Treating a document section as a semantic classification → the radiation item filed as
  *assessment*.
- Inheriting codes without verification → two wrong codes.
- Assuming a general list was wanted → a 25-item block he rejected outright.
- Asking open questions instead of offering options.
- Building forms before the inventory had settled.
- Reading a PDF export as if it had a text layer.
- Leaving an agreed decision unapplied → the 3-state symptom format was agreed 2026-09-10 and
  went unapplied for eleven days until a phone call settled it. Written down is not applied.
