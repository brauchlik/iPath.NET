# iPath Form Types — Content Classification Guide

## Overview

iPath.NET uses `eQuestionnaireUsage` to classify questionnaires. Each type is
used at a different point in the clinical workflow, filled by different people.
When processing pathologist instructions, classify each piece of content by its
proper form type.

## Form types

### CaseDescription (value: 2)

**When:** Case creation (Wizard Step 1)
**Who fills it:** Referring physician / intake staff
**Purpose:** Capture the initial clinical picture

**Content belongs here if it's:**
- Material for examination (cytology, histology, bone marrow, blood smear)
- Symptoms (pain, swelling, weight loss, fatigue, bleeding, etc.)
- Imaging available at intake (X-ray, CT, MRI, PET — if the patient arrives with results)
- Lab values (blood count, basic chemistry — if available at intake)
- Clinical history relevant to the presenting complaint (steroid treatment, previous radiation, comorbidities)
- Disease type / clinical diagnosis (CML, CLL, MDS, etc.) — this is clinical info, not pathological assessment

**Content does NOT belong here if it's:**
- Margin status (R0/R1/R2) — that's Diagnostic Assessment
- Tumor staging (pTNM) — that's Diagnostic Assessment
- Histological grading — that's Diagnostic Assessment
- Age, gender, ICD-O topography, submitting institute — those are wizard-managed fields
- Treatment response — that's Follow-Up

**Composition pattern:**
```
Material (always)
+ Symptoms (always, organ-specific block)
+ Imaging (optional)
+ Lab (optional)
```

### Annotation (value: 3)

**When:** Any time after case creation
**Who fills it:** Anyone (pathologist, clinician, consultant)
**Purpose:** Comments, questions, notes, quick observations

**Content belongs here if it's:**
- Free-text comments
- Questions for colleagues
- Quick observations
- Second opinions
- Structured annotations (using Annotation questionnaires)

### FollowUp (value: 4)

**When:** After initial assessment, during treatment
**Who fills it:** Clinician / treating physician
**Purpose:** Track treatment response, recurrence, interval changes

**Content belongs here if it's:**
- Treatment response assessment
- Recurrence monitoring
- Interval changes (size, symptoms)
- Treatment modifications
- Follow-up imaging results

### FinalAssessment (value: 5)

**When:** After microscopic examination
**Who fills it:** Pathologist
**Purpose:** Summarize all diagnostic input + follow-up

This is a **specific form of Diagnostic Assessment** — it aggregates the
pathologist's findings (margin status, tumor staging, grading, etc.) and
optionally includes follow-up recommendations.

**Content belongs here if it's:**
- Margin status (R0/R1/R2)
- Tumor size (pathological measurement)
- Tumor depth (superficial/deep)
- Histological subtype and grading
- Lymphovascular invasion (LVI)
- Perineural invasion (PNI)
- Resection margins (distance, involvement)
- Pathological staging (pTNM)
- Immunohistochemistry results
- Molecular markers
- Follow-up recommendations (next steps, imaging schedule, clinical monitoring)

## Wizard-managed fields

These are handled by iPath's Case Description Wizard, NOT by our forms:

- Patient age / date of birth
- Patient gender / sex
- ICD-O topography code (body site)
- Submitting institute / department
- Journal number / case ID
- Submitting physician

When the pathologist includes these in their form suggestions, note them as
"wizard-managed" and do not include them in the FHIR Questionnaire.

## Decomposition examples

### Example 1: Dr. Jundt's Hematology Form

Pathologist's original (mixed):
```
Material (histology, blood smear, bone marrow)
Disease type (CML, CLL, MDS, AML, etc.)
Steroid treatment (yes/no)
Accidental finding (yes/no)
Symptoms (fatigue, bleeding, infections, night sweats, weight loss)
Lab values (CBC, differential, LDH, uric acid)
Imaging (CT, PET-CT, ultrasound)
Diagnostic assessment (margin status, tumor size, staging)
```

Decomposed:
| Content | Form Type | Reason |
|---|---|---|
| Material | CaseDescription | Intake info |
| Disease type | CaseDescription | Clinical info at intake |
| Steroid treatment | CaseDescription | Clinical history |
| Accidental finding | CaseDescription | Clinical context |
| Symptoms | CaseDescription | Presenting complaint |
| Lab values | CaseDescription | Available at intake |
| Imaging | CaseDescription | If available at intake |
| Diagnostic assessment | FinalAssessment | Summarizes all diagnostic input + follow-up |

### Example 2: Dr. Jundt's Digestive Form

Pathologist's original (mixed):
```
Material (biopsy, resection specimen)
Clinical info (dysphagia, weight loss, pain, obstruction)
Imaging (barium swallow, CT, endoscopy)
Lab values (CEA, CA 19-9)
Diagnostic assessment (tumor size, depth, margins, grading, pTNM)
```

Decomposed:
| Content | Form Type | Reason |
|---|---|---|
| Material | CaseDescription | Intake info |
| Clinical symptoms | CaseDescription | Presenting complaint |
| Imaging | CaseDescription | If available at intake |
| Lab values | CaseDescription | Tumor markers at intake |
| Tumor size | FinalAssessment | Pathological measurement |
| Tumor depth | FinalAssessment | Pathological assessment |
| Margins | FinalAssessment | Pathological assessment |
| Grading | FinalAssessment | Histological assessment |
| pTNM | FinalAssessment | Pathological staging |

## Key principle

**The pathologist sees everything as one "general picture." Our job is to
recognize that different pieces of information belong to different forms,
filled at different times by different people.**

When in doubt, ask:
1. "Is this needed to CREATE the case?" → CaseDescription
2. "Is this the result of EXAMINING the specimen?" → FinalAssessment (summarizes all diagnostic input + follow-up)
3. "Is this tracking the PATIENT over time?" → FollowUp
4. "Is this a COMMENT or QUESTION?" → Annotation
5. "Is this handled by the WIZARD?" → Skip (wizard-managed)
