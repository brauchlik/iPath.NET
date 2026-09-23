# Case Description — Lymph nodes (C77) — sample for review

**Status: draft sample for discussion, not final content.**
Companion file: `CLINICAL INFORMATIONS - v2 - sample Lymph nodes C77.json` (FHIR R4 —
loadable in the NLM Form Builder, or imported into the iPath test server and reviewed
there, which is how it will actually be used).

This is the *shape* of a Case Description form for lymph-node cases, built to discuss
the approach — not to fix the wording. Once the shape is agreed, every organ gets the
same treatment.

---

## The idea in three lines

1. The reusable unit is a **question**, not a form. Each question has one canonical id
   and **one** SNOMED/LOINC code.
2. Forms are built by **picking questions**. The same question used in another organ's
   form is literally the same question — so the same concept can never get two codes.
3. Our block library is only a **selection aid**. It can be re-cut and reorganised at any
   time without changing any published form.

## The four sections (families)

| # | Section | Questions | Where it comes from |
|---|---|---|---|
| 0 | **Material for examination** | 5 | our generic Material block — *your v2 does not mention material* |
| 1 | **Clinical history and symptoms** | 13 | **your v2, section "Lymph nodes C77"** |
| 2 | **Laboratory (if available)** | 9 | our generic blood-count block — *your C77 says "Blood cell count available … (see C42)"* |
| 3 | **Imaging (optional)** | 4 | our generic Imaging block — *your C77 has no imaging* |

Total: 4 sections, 31 questions.

Sections 0, 2 and 3 are **generic** — the same sections attach to every organ.
Section 1 is **organ-specific**. That is the whole composition rule:

> every case gets the generic sections + the section for its topography

## Section 1 in detail (your C77 content)

| Question | Answers | Detail |
|---|---|---|
| Accidental finding | yes / no | — |
| Swelling | yes / no / unknown | *if yes:* duration (days / weeks / months / years) |
| Pain | yes / no / unknown | *if yes:* duration (days / weeks / months / years) |
| Night sweat | yes / no / unknown | |
| Weight loss | yes / no / unknown | |
| Fever | yes / no / unknown | |
| Pallor | yes / no / unknown | |
| Bleeding tendency | yes / no / unknown | |
| Susceptibility to infection | yes / no / unknown | |
| Previous steroid treatment | yes / no / unknown | |
| Other clinical information | free text | |

Every symptom and history question offers **yes / no / unknown** — three explicit answers, as
you wrote them. "Unknown" is a deliberate answer; we do not ask *why* it is unknown. The only
two-state questions left are the structural gates (Material, and here the "Accidental finding"
switch), because those are binary facts rather than clinical states.

The eight symptoms are shown **only when the case is *not* an accidental finding** —
i.e. we implemented your rule literally ("Accidental finding? Yes/No; if No: …").

## Deliberately not included

- **Staging.** Distribution (above / below the diaphragm), Localization (the 7 node
  regions) and Organ involved (spleen / liver / bone marrow) are **held back** because you
  said "no staging yet". They are listed under open questions below.
- **The duplicated `Erythrocytes` row** from your C42 table — included once only.
- **No imaging for lymph nodes** in your v2, so the Imaging section is shown as our
  generic block, purely to demonstrate how an optional section attaches.

## What is shared with other organs (the reuse story)

These questions in this form are the **same questions** used in other organs' forms —
one code each, never duplicated:

| Question | Also used by |
|---|---|
| Pain | 10 organs |
| Swelling | 6 organs |
| Fever | Respiratory, Bones, C42 |
| Weight loss | Intestine, Gallbladder/Pancreas, Respiratory |
| Night sweat, Pallor, Bleeding tendency, Susceptibility to infection | C42 / C77 |
| Blood cell count | C42 (your "see C42") |

---

## Open questions

**A. The structure**

1. **The four sections** — does this split match how you think about a case? Especially:
   Material and Imaging are *generic* sections we reuse for every organ, while symptoms
   are organ-specific.
2. **Accidental finding gate** — we show the eight symptoms only when it is *not* an
   accidental finding. Correct, or the other way round?
3. **Staging items** — Distribution / Localization / Organ involved: are these what the
   **requesting** pathologist reports when submitting, or are they the **specialist's**
   assessment after review? This decides which section they belong to.

**B. The content**

4. **Previous steroid treatment** — there is no suitable SNOMED concept, so we used a
   local (iPath-internal) code. Acceptable?
5. **"Other"** — you wrote "Yes/no + specify". We modelled it as a plain free-text field.
   Note: free text is stored in the data export but does **not** appear in the
   case-description summary text. Is that acceptable, or should "Other" offer coded options?
6. **Laboratory** — is the blood count really the only lab you want for lymph-node cases?
   Your C42 table lists `Erythrocytes` twice; if the second one was meant to be
   **haemoglobin**, that is currently missing.
7. **Imaging** — which modalities should be offered for lymph-node cases? We offered
   Sonography, CT, MRI, PET-CT. Your v2 asks for none.
8. **Material** — your v2 never mentions material. For nodes we offer Cytology
   (incl. fine-needle aspiration) and Histology (core / open / excisional biopsy /
   resection). Is that the right list for this organ?

---

## Note on a fix we made

Your C42 blood-count table groups the differential (Neutrophils, Eosinophils, Basophils)
under "Granulocytes". In the current form the sub-counts would be **silently dropped** by
the case-description summary text. In this sample they sit alongside the other counts so
all of them display. Worth applying to the C42 form as well.
