# Case Description — Soft tissue (C49) — sample for review

**Status: draft sample for discussion, not final content.**
Companion file: `CLINICAL INFORMATIONS - v2 - sample Soft tissue C49.json` (FHIR R4 —
loadable in the NLM Form Builder, or imported into the iPath test server and reviewed there).

Companion to the Lymph nodes (C77) sample. C77 shows what a "full" case looks like;
**C49 is the interesting one**, because it is the only section in your v2 that asks for
**tumour size and depth** — so it is where the question *"who reports size and depth?"*
becomes concrete.

---

## Structure

| # | Section | Questions | Where it comes from |
|---|---|---|---|
| 0 | **Material for examination** | 5 | our generic Material block |
| 1 | **Clinical history, symptoms and findings** | 8 | **your v2, section "Connective, subcutaneous and other soft tissues C49"** |
| 2 | **Laboratory** | — | **not shown** — your C49 asks for no lab (unlike C77) |
| 3 | **Imaging (optional)** | 4 | our generic Imaging block |

Total: 3 sections, 17 questions. Note that the Laboratory section simply **is not
included** here — sections are optional, and this is what "optional" looks like.

## Section 1 in detail — and how each of your rows was modelled

| Your row | How we modelled it | Note |
|---|---|---|
| Pain; *if yes* duration (D, M, Y) | the **shared** Pain question (yes / no / unknown) + duration (days / weeks / months / years) | same question as 10 other organs |
| Swelling/size; *if yes*: `< 5 cm` / `> 10 cm` / `other` / `No data` | the **shared** Swelling question (yes / no / unknown) + a "Size" detail | the Swelling question is used by 6 organs; here its detail is the size class |
| Depth: `Superficial (above fascia)` / `Deep (below fascia/intramuscular)` / `unknown` | Depth — **no yes/no gate**, exactly as you wrote it | overlaps our existing "Tumour depth" |
| Previous radiation therapy to the area now affected; *if yes* **(specify time period and protocol)** | the **shared** "Previous radiation therapy" question (yes / no / unknown) + a free-text **"Time period and protocol"** | also asked in Bones C40–C41 — one shared question, one code |
| Other; *if yes* specify | free text | |

**Depth** and **Previous radiation therapy** are rendered as **radio buttons** (one
selection); Depth's options are stacked one per line because the labels are long. The other
single-select in this form, the "Size" detail, currently uses the default presentation — say
if you want it switched to radio buttons as well.

---

## The question this sample exists to ask

**Size and depth are currently filed under Diagnostic Assessment** — that is, as part of
the specialist's report *after* review, not the submission. Your C49 section puts them
into the request:

- If the **requesting (primary) pathologist** reports size and depth at submission →
  they are case information and belong where this sample puts them.
- If they are the **remote specialist's** finding → they belong in the assessment, and
  should not appear in the request form at all.

**Confirm which, because the same question applies to C77's** Distribution, Localization
and Organ involved — i.e. whether the request form captures the primary pathologist's own
findings, or only what they were told.

## Further points on this section

1. **Size classes have a gap** — nothing between 5 cm and 10 cm. The same gap exists in our
   current version. Should it be `< 5 cm` / `5–10 cm` / `> 10 cm`?
2. **Depth is ungated** while Size sits behind a yes/no. Intentional, or should both be
   gated (or neither)?
3. **Radiation therapy is asked in two of your sections** (Bones and C49). That is fine —
   it becomes *one* shared question with one code — but **chemotherapy** appears only under
   Bones. Should C49 also ask previous chemotherapy? *(The chemotherapy question is now
   defined alongside radiation, so Bones can use both.)*
4. **Vocabulary** — settled: `yes / no / unknown` for every symptom and history question,
   including "Previous radiation therapy". *(This is the one change since the version you
   reviewed, which read "Yes / No / No data".)*
5. **Duration units** — you write (D, M, Y); we currently offer days / weeks / months / years.
   Confirm which set you want for pain duration.
6. **No laboratory for C49** — confirm. If soft-tissue cases typically come with imaging
   only, that is exactly what the optional section is for.
7. **Material for soft tissue** — we offer Cytology (incl. fine-needle aspiration) and
   Histology (core / open / excisional biopsy / resection). Right list for this organ?
8. **Imaging** — we offer Sonography, CT, MRI, PET-CT. For soft tissue we would expect MRI
   to matter most; confirm the set.

## What is shared (the reuse story)

| Question | Status | Also used by |
|---|---|---|
| Pain (+ duration) | shared | 10 organs |
| Swelling | shared | 6 organs |
| Previous radiation therapy | shared | Bones C40–C41 |
| Size class | **new** — overlaps "Tumour size" | (Diagnostic assessment) |
| Depth | **new** — overlaps "Tumour depth" | (Diagnostic assessment) |

Size class and Depth deliberately carry **no terminology code yet**. If they turn out to be
the *same* question as the existing "Tumour size" / "Tumour depth", they inherit those
codes; if they are a different question, they get their own. Assigning a code now would
prejudge the decision above.
