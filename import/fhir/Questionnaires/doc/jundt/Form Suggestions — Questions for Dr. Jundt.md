# Form Suggestions — Questions for Dr. Jundt

**Date:** 2026-09-10
**Purpose:** Compare your form suggestion with our building block architecture and flag open questions for discussion.

---

## 1. What we included from your form

Your Hematology/Lymph form suggestion (`.txt`) was used as the primary source. We included:

| Section | Your form | Our draft |
|---|---|---|
| Material (Cytology/Histology) | Lines 28-30 | ✅ Included (material block) |
| Imaging (general + special) | Lines 31-54 | ✅ Included (imaging block) |
| Symptoms C42/C77 | Lines 59-88 | ✅ Included (symptoms block, 3-state) |
| Blood cell count | Lines 91-99 | ✅ Included (lab.hematology block) |
| Type of disease | Lines 12-20 | ✅ Included (new inline section) |
| Steroid treatment | Lines 55, 61 | ✅ Included (new inline section) |
| Accidental finding | Line 63 | ✅ Included (new inline section) |
| Hematology compartment | Lines 103-110 | ✅ Included (new inline section) |
| Extended lab (Hb, VitB12, etc.) | Lines 111-112 | ✅ Included (new inline section) |
| Lymph node mapping | Lines 113-141 | ✅ Included (new inline section) |

## 2. What we excluded (wizard handles these)

These fields are already captured by the iPath Case Description Wizard and would be redundant in the Questionnaire:

| Your form field | Wizard location |
|---|---|
| Submitter | Wizard metadata |
| Journal number | Wizard Step 1 |
| Age | Wizard Step 1 |
| Gender | Wizard Step 1 |
| Localization Groups (ICDO) | Wizard Step 0 (topography tree) |
| Suspected diagnosis | Wizard Step 1 |

**Question:** Should any of these still be included in the Questionnaire for completeness?

## 3. Change we adopted

**3-state symptoms (unknown / no / yes)**

Your form uses "unknown / no / yes" for symptoms. Our earlier meeting agreed on yes/no only (D1). We adopted your 3-state format — it's clinically more accurate since "unknown" is genuinely different from "no."

This change affects all existing symptom blocks. Once you approve, we'll update them.

## 4. Questions about your form

### 4a. Duplicate "Previous steroid treatment"
Your form lists "Previous steroid treatment" at **line 55** (in the general Case Details section) and again at **line 61** (in the Symptoms section). Is this:
- **Intentional** — once as a general case attribute, once as a symptom-context field?
- **A copy artifact** — should it appear only once?

### 4b. Imaging for Hematology
Our meeting plan said hematology cases **don't need imaging**. Your form clearly includes **all imaging modalities** (general + special). We followed your form. Can you confirm this is correct?

### 4c. Diagnostic assessment
Your form doesn't include **margin status, tumor size, tumor depth, or radiation history**. Our meeting plan had these as a standard section. Should we:
- **Keep** diagnostic assessment in the hematology form (useful for surgical cases)?
- **Remove** it from hematology (it's more relevant for solid tumors)?

### 4d. Extended laboratory findings
Your form says "please specify (Hb, VitB12, Fe, MCV, MCH etc.)" as free text. Should these be:
- **Structured fields** (quantity with units, like the blood count)?
- **Free text** (as you wrote it)?
- **Both** — structured for common values, free text for "other"?

### 4e. Lymph node specific locations
When "Above and below the diaphragm" is selected, should the specific locations (Cervical, Axillary, etc.) be:
- **Required** (at least one must be selected)?
- **Optional** (can leave blank)?

## 5. Digestive form

We created a **best-effort draft** for the Digestive Organs form based on:
- The meeting plan (material + digestive symptoms + imaging + diagnostic)
- The `symptoms.digestive` block (17 digestive-specific symptoms)
- Your 3-state convention

**No `.txt` file exists yet for digestive organs.** Could you provide a similar form suggestion, or should we present our draft for your review first?

## 6. New sections — reusable blocks

The new sections in your hematology form (type of disease, steroid treatment, hematology compartments, extended lab, lymph node mapping) are currently inlined directly in the composed form.

After you review and approve the content, we'll extract these as **reusable building blocks** so they can be included in other forms too:

| Section | Suggested block ID | Reuse potential |
|---|---|---|
| Type of disease | `clinical.info` | All forms |
| Steroid treatment + Accidental finding | `clinical.info` | All forms |
| Hematology compartment | `hematology.compartment` | Hematology only |
| Extended lab | `lab.extended` | Hematology, possibly others |
| Lymph node mapping | `lymph.node` | Hematology/Lymph forms |

---

**Next steps:** Review the two draft forms in the iPath viewer, then discuss the questions above. Once we align, we'll finalize the blocks and register the forms for import.
