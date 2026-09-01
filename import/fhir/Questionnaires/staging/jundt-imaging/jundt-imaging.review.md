# Review — jundt/imaging.md

**Proposed id:** `jundt-imaging`
**Proposed title:** `Imaging`
**Source:** `import/fhir/Questionnaires/doc/jundt/imaging.md`
**Phase:** 0 (intake & validation) — awaiting clarification

## Structure as understood

```
Imaging (generally used techniques, irrespective of speciality)
├── Conventional X-rays (2 planes)     boolean + upload on yes
├── Sonography                         boolean + upload on yes
├── CT scan                            boolean + upload on yes
├── MRI                                boolean + upload on yes
├── PET CT                             boolean + upload on yes
├── PET MRI                            boolean + upload on yes
├── PET/CT MRI                         boolean + upload on yes
├── Other                              <open — see Q3>
└── Special procedures Jaws (Mandible C41.1 / Maxilla C41.0)
    ├── Panoramic view                 boolean + upload on yes
    └── Cone-beam CT (CBCT)            boolean + upload on yes
└── Special procedures Breast
    └── Mammography                    boolean + upload on yes
```

Every "if yes, please upload images and report" pattern maps to a `boolean` item
with a conditional `attachment` child (`enableWhen`), per `fhir-conventions.md`.

## Block candidates

This document is explicitly *"irrespective of speciality"* — it is ideal
building-block material:

| Block | Content |
|---|---|
| `imaging.general` | the 7 general techniques + `Other` |
| `imaging.jaw` | panoramic view + CBCT (site-specific: Mandible C41.1 / Maxilla C41.0) |
| `imaging.breast` | mammography |

## Reuse check

Registry is empty (first questionnaire). No overlap with existing content.

## Ambiguities & clarifying questions

1. **Is `imaging.md` a standalone questionnaire, or only building blocks?**
   The title says "irrespective of speciality". Do you want a standalone
   `jundt-imaging` questionnaire to exist, or should this content live only as
   reusable blocks that organ-specific questionnaires include? (Recommendation:
   blocks only — compose it into site-specific forms; the jaw and breast
   sections then ride along only where relevant.)

2. **`Other` line.** It has no "if yes, please upload images and report"
   suffix. Should `Other` be:
   - a free-text line naming the other technique, plus an optional upload, or
   - just free text?

3. **Typo:** line 2 "generelly" → intended "generally". I'll normalize to
   "generally" in the title/description (English output rule).

4. **Grouping:** should the imaging techniques render as one flat group, or as
   "General" / "Jaws" / "Breast" subgroups? (Recommendation: keep the three
   subgroups exactly as the pathologist structured them.)
