# Case Description — Bones, joints and articular cartilage (C40–C41) — sample for review

**Status: draft sample for discussion, not final content.**
Companion file: `CLINICAL INFORMATIONS - v2 - sample Bones C40-C41.json` (FHIR R4 — loadable
in the NLM Form Builder, or imported into the iPath test server and reviewed there).

Third of the sample set, after Lymph nodes (C77) and Soft tissue (C49). You asked for the
radiation detail to be added here as well, and **Bones is the only section of your v2 that
asks about previous chemotherapy** — so this is where the two treatment-history questions
sit together.

---

## Structure

| # | Section | Questions | Where it comes from |
|---|---|---|---|
| 0 | **Material for examination** | 5 | our generic Material block |
| 1 | **Clinical history and symptoms** | 9 | **your v2, section "Bones, Joints and Articular Cartilage C40-C41"** |
| 2 | **Laboratory** | — | **not shown** — your Bones section asks for no lab |
| 3 | **Imaging (optional)** | 6 | our generic Imaging block |

Total: 3 sections, 20 questions.

## Section 1 in detail — and how each of your rows was modelled

| Your row | How we modelled it | Note |
|---|---|---|
| Pain; *if yes* duration (D, W, M) | the **shared** Pain question (yes / no / unknown) + duration | same question as 10 other organs |
| Night pain | Night pain (yes / no / unknown) | already existed as a shared question |
| Fever | Fever (yes / no / unknown) | shared with Respiratory, C42 and Lymph nodes |
| Previous radiation therapy to the area now affected; *if yes* **(specify time period and protocol)** | the **shared** "Previous radiation therapy" question (yes / no / unknown) + free-text **"Time period and protocol"** | the *same question* as in your C49 section — one shared item, one code |
| Previous chemotherapy; *if yes* **(specify time period and protocol)** | Previous chemotherapy (yes / no / unknown) + free-text **"Time period and protocol"** | new question, defined here for the first time |
| Other; *if yes* specify | free text | |

**Previous radiation therapy** and **Previous chemotherapy** are rendered as **radio buttons**
(one selection each).

## A note on the radiation question

You wrote *(specify time period)* in your C49 section and *(specify time period)* here; your
note asked for **(time period and protocol)** in both. We applied the same wording to both, so
there is one question with one code, used in two sections.

## Imaging — why six modalities

X-ray, CT, MRI and PET-CT cover bone generally. We also included **Panoramic view** and
**CBCT**, because the jaw bones (C41.0 maxilla, C41.1 mandible) fall *inside* your C40–C41
range and those are the modalities used there. If you would rather keep them separate, say so.

---

## Open questions

1. **The symptom list is thin.** Pain, night pain and fever only — for bone cases, should we
   also ask about swelling, weight loss, or a preceding trauma / pathological fracture?
2. **Size and depth are asked in C49 but not here** — even though "tumour size" and "tumour
   depth" are generic questions. Is that intentional, or should the bone form ask them too?
3. **Chemotherapy is asked only here.** It is now defined, so your C49 section could ask it
   too — should it?
4. **Duration units** — you write (D, W, M); we currently offer days / weeks / months / years.
   Confirm which set you want.
5. **Previous treatment answered "unknown"** — we now offer yes / no / unknown for both
   radiation and chemotherapy. Is "unknown" the right third state here, or should the clinician
   always know?
6. **Material for bone** — we offer Cytology (incl. fine-needle aspiration) and Histology
   (core / open / excisional biopsy / resection). Right list for this organ?
7. **Imaging set** — see the note above; is Panoramic / CBCT wanted, or is that a separate
   jaw form?

## What is shared (the reuse story)

| Question | Status | Also used by |
|---|---|---|
| Pain (+ duration) | shared | 10 organs |
| Night pain | shared | (was tagged for C40 in the old general list) |
| Fever | shared | Respiratory, C42, Lymph nodes |
| Previous radiation therapy | **shared** | Soft tissue C49 |
| Previous chemotherapy | new | (defined here) |
| X-ray / CT / MRI / PET-CT | shared | every organ's imaging section |
