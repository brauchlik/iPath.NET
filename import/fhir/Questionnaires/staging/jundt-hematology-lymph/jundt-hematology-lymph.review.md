# Review — jundt/Hematology [C42-C42] and Lymph nodes [C77].md

**Proposed id:** `jundt-hematology-lymph`
**Proposed title:** `Hematology and Lymph nodes`
**Source:** `import/fhir/Questionnaires/doc/jundt/Hematology [C42-C42] and Lymph nodes [C77].md`
**Phase:** 0 (intake & validation) — awaiting clarification

## Structure as understood

```
Hematology [C42-C42] and Lymph nodes [C77]      ICD-O scope: C42, C77 (advisory)
└── Labor
    ├── Blood cell count available              boolean (Yes/No)
    ├── Erythrocytes                            integer
    ├── Granulocytes                            integer (?) — see Q1
    │   ├── Neutrophils                         integer
    │   ├── Eosinophils                         integer
    │   └── Basophils                           integer
    ├── Monocytes                               integer
    ├── Lymphocytes                             integer
    └── Thrombocytes                            integer
```

"Type a Number" maps to `integer`. The indented rows under `Granulocytes` are
its breakdown (Neutrophils / Eosinophils / Basophils).

## Block candidate

`lab.blood-count` — the whole `Labor` group is reusable for other hematologic /
lymph-node workups (ICD-O C42 / C77).

## Reuse check

Registry is empty (first questionnaire). No overlap with existing content.

## Ambiguities & clarifying questions

1. **`Granulocytes` row.** It has its own "Type a Number", *and* three indented
   sub-rows (Neutrophils / Eosinophils / Basophils). Which is intended?
   - (a) `Granulocytes` is a **group header** only, and the three subtypes are
     the actual inputs (recommended — avoids double entry of a total + parts),
   - (b) `Granulocytes` is a separate total *and* the three subtypes are entered,
   - (c) only the total, ignore the subtypes.

2. **Conditional logic.** Are the blood-count fields only shown when
   "Blood cell count available" = **Yes**? (Recommendation: yes, via
   `enableWhen`.) Or is "Blood cell count available" a pure informational flag
   with the fields always visible?

3. **Units.** The counts have no units. Should they be plain numbers (no unit),
   or fixed units (e.g. Erythrocytes in 10⁶/µL; Granulocytes/lymphocytes/
   monocytes/thrombocytes in 10³/µL)? If units matter, which ones?

4. **Title.** The source title embeds the ICD-O scope. For the questionnaire
   `title` I propose `Hematology and Lymph nodes` (drop the brackets) and keep
   `C42, C77` as advisory `icdOScope` in the intent. OK?
