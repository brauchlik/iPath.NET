# Pathologist Discussion Brief: Inconsistencies & Recommendations

**Date**: 2026-08-31  
**Project**: iPath.NET — FHIR R4 Questionnaire Definition Pipeline  
**Audience**: Dr. Jundt & Project Team  

---

## Executive Summary

This document summarizes all detected inconsistencies, redundancies, and proposed technical/clinical recommendations across the source documents provided for the iPath.NET FHIR Questionnaire building blocks.

---

## 🫁 1. Respiratory Symptoms (`Respiratory tract C30-C39.md`)

| Issue / Inconsistency | What the Source Notes Say | Clinical / Technical Conflict | Antigravity Recommendation |
|---|---|---|---|
| **1.1 Shortness of breath vs. Dyspnea** | Listed as two separate questions (lines 6 & 7). | In medical terminology and SNOMED CT (`267036007`), *Shortness of breath* and *Dyspnea* are 100% synonymous. | **Merge into one question**: *"Shortness of breath / Dyspnea"* to avoid confusing referring doctors. |
| **1.2 Acute vs. Chronic Cough** | `General` lists *Cough + duration*; `Respiratory` lists *Chronic cough (>3 months)*. | Having both could result in two cough questions on a lung form. | In respiratory-specific forms, use **"Chronic cough (>3 months)"** or general **"Cough"** with duration selector (which automatically covers >3 months). |
| **1.3 Smoking History** | Asks: *Smoker (yes/no) ──► how long (years)*. | Does not capture daily intensity (packs per day). | Ask Dr. Jundt if **years smoked** is sufficient or if **Pack-Years** is required for lung cancer submissions. |

---

## 🌸 2. Breast & Gynecologic Symptoms (`Breast and female genital organs C50-C58.md`)

| Issue / Inconsistency | What the Source Notes Say | Clinical / Technical Conflict | Antigravity Recommendation |
|---|---|---|---|
| **2.1 Uterine vs. Vaginal Bleeding & Discharge** | `General` uses *Uterine bleeding/discharge*; `Female Organs` uses *Vaginal bleeding/discharge*. | Two different anatomical terms for the same patient-reported presentation. | **Standardize on "Vaginal bleeding" and "Vaginal discharge"** (and *Postmenopausal bleeding*), since referring clinicians and patients observe vaginal symptoms directly. |
| **2.2 Menstrual Irregularities Selection** | Lists 4 types under irregular cycle: *Menorrhagia, Spotting, Amenorrhea, Dysmenorrhea*. | Patients often experience multiple irregularities (e.g. heavy bleeding AND pain). | Model as **multi-select checkboxes** so doctors can select all that apply. |
| **2.3 Date of Last Delivery** | Asks: *date of last delivery (dd,mm,yyyy)*. | Free text risks inconsistent formats (`12.05.2020` vs `May 2020`). | Use FHIR **`type: date`** (renders an interactive calendar/date picker). |

---

## 🍎 3. Digestive Symptoms (`Digestive organs C15-C26.md` vs `General`)

| Issue / Inconsistency | What the Source Notes Say | Clinical / Technical Conflict | Antigravity Recommendation |
|---|---|---|---|
| **3.1 Overlapping General Symptoms** | 6 symptoms appear in both documents: *Weight loss, Fever, Jaundice, Constipation, Diarrhoea, Melaena*. | Duplication if both blocks are loaded naively. | Keep single canonical definitions in the registry. In the master block, provide the richer `Weight loss` (with optional `kg` field). |
| **3.2 Abdominal Pain Locations** | GI uses a 6-quadrant grid (*Upper/Middle/Lower × L/R*); Gyn uses *Lower abdominal pain + L/Mid/R*. | Slightly different location pickers. | Use the 6-quadrant picker for GI cases and the focused Lower Abdomen picker for Gynecologic cases. |

---

## 🔬 4. Laboratory Blood Counts (`Hematology [C42-C42] and Lymph nodes [C77].md`)

| Issue / Inconsistency | What the Source Notes Say | Clinical / Technical Conflict | Antigravity Recommendation |
|---|---|---|---|
| **4.1 Total Granulocytes vs. Sub-counts** | Original grouped Neutrophils, Eosinophils, Basophils under Granulocytes without a total field. | Screening analyzers often report only a **Total Granulocyte Count** without sub-breakdowns. | **Allow entering a Total Granulocyte Count** AND optional sub-counts (Neutrophils, Eosinophils, Basophils). |
| **4.2 Flexible Units (`%` vs `10^3/µL`)** | Lab sheets vary between percentages and absolute microLiter concentrations. | Hardcoding one unit breaks when doctors receive the other. | Use **`questionnaire-unitOption`** with **`%`** and **`10^3/µL`** in a side-by-side dropdown. |

---

## 📑 5. Diagnostic Assessment (`General diagnostic assement fields.md`)

| Issue / Inconsistency | What the Source Notes Say | Clinical / Technical Conflict | Antigravity Recommendation |
|---|---|---|---|
| **5.1 Tumor Size Gap** | Lists `Size < 5cm` and `Size > 10cm`. | Missing the intermediate range (`5 – 10cm`). | Add an explicit choice for **`Size 5 – 10cm`**. |
| **5.2 Scope of Tumor Depth** | Handwritten note: *"Gilt auch für Knochen!"* (Applies also to bone!). | Anatomically, bone tumors are intrinsically sub-fascial/deep. | Clarify with Dr. Jundt whether *Superficial vs. Deep* applies to extra-skeletal bone extension or soft tissue only. |

---

## 💡 Summary of Decisions for Dr. Jundt

1. [ ] **Shortness of breath / Dyspnea**: Confirm merging into a single question.
2. [ ] **Smoking History**: Confirm if duration (years) is sufficient vs pack-years.
3. [ ] **Bleeding/Discharge Terminology**: Confirm using "Vaginal bleeding" & "Vaginal discharge" across forms.
4. [ ] **Menstrual Irregularities**: Confirm multi-select checkboxes for irregular periods.
5. [ ] **Granulocytes**: Confirm total Granulocyte count + differential percentages (`%`) / microLiter (`10^3/µL`) unit options.
6. [ ] **Tumor Size**: Confirm adding `5 – 10cm` choice option.
7. [ ] **Tumor Depth for Bone**: Clarify intention behind *"Gilt auch für Knochen!"*.
