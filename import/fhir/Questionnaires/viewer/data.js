// Auto-generated questionnaire data bundle for iPath.NET Questionnaire Review Studio
window.QUESTIONNAIRE_REGISTRY_DATA = {
  "version": "1.1",
  "updated": "2026-09-01",
  "buildingBlocks": [
    {
      "id": "material",
      "title": "Master Material List",
      "category": "01 Material",
      "topography": [],
      "status": "ready_for_review",
      "deprecated": false,
      "sourceOriginal": "doc/jundt/Case Descriptions/01 Material/Material for examination.md",
      "sourceNotes": "doc/jundt/Case Descriptions/01 Material/Material for examination.notes.md",
      "sourceFhir": "blocks/01 Material/material.json",
      "summary": "Master block for Cytology and Histology examination material.",
      "openQuestions": [
        "Confirm if exfoliative cytology sub-list should also be multi-select (currently repeats: true).",
        "Confirm uncoded status of top-level Cytology/Histology gates."
      ],
      "originalContent": "# Material for examination\n\n| | | |\n|---|---|---|\n|**Cytology**|yes/no| |\n| |Pap smear| |\n| |Smear cytology (eg Bronchus)| |\n| |Sputum| |\n| |Exfoliative cytology|Liquor|\n| | |Pleural effusion|\n| | |Ascites|\n| | |Urine|\n| | |Lavage|\n| |Fine needle aspiration| |\n|**Histology**|yes/no| |\n| |Core biopsy| |\n| |Open biopsy| |\n| |Excisional biopsy| |\n| |Resection| |",
      "notesContent": "# Notes — Material for examination\n\nCompanion notes for `Material for examination.md` (pathologist original stays untouched).\n\n## Structure\n\n- Cytology (gate, boolean)\n  - Pap smear, Smear cytology, Sputum, Exfoliative cytology (Liquor, Pleural effusion, Ascites, Urine, Lavage), Fine needle aspiration\n- Histology (gate, boolean)\n  - Core biopsy, Open biopsy, Excisional biopsy, Resection\n\n## Decisions\n- Multi-select sub-types when gate is checked.\n- Cytology and Histology are independent gates (both can be submitted).",
      "fhirJson": {
        "resourceType": "Questionnaire",
        "id": "material",
        "status": "draft",
        "title": "Master Material List",
        "subjectType": ["Patient"],
        "item": [
          { "linkId": "cytology", "type": "boolean", "text": "Cytology" },
          {
            "linkId": "cytology.types",
            "type": "choice",
            "repeats": true,
            "text": "Cytology material",
            "enableWhen": [{ "question": "cytology", "operator": "=", "answerBoolean": true }],
            "answerOption": [
              { "valueString": "Pap smear" },
              { "valueString": "Smear cytology (eg Bronchus)" },
              { "valueString": "Sputum" },
              { "valueString": "Exfoliative cytology" },
              { "valueString": "Fine needle aspiration" }
            ]
          },
          {
            "linkId": "cytology.exfoliative",
            "type": "choice",
            "repeats": true,
            "text": "Exfoliative cytology — site",
            "enableWhen": [{ "question": "cytology.types", "operator": "=", "answerString": "Exfoliative cytology" }],
            "answerOption": [
              { "valueString": "Liquor" },
              { "valueString": "Pleural effusion" },
              { "valueString": "Ascites" },
              { "valueString": "Urine" },
              { "valueString": "Lavage" }
            ]
          },
          { "linkId": "histology", "type": "boolean", "text": "Histology" },
          {
            "linkId": "histology.types",
            "type": "choice",
            "repeats": true,
            "text": "Histology material",
            "enableWhen": [{ "question": "histology", "operator": "=", "answerBoolean": true }],
            "answerOption": [
              { "valueString": "Core biopsy" },
              { "valueString": "Open biopsy" },
              { "valueString": "Excisional biopsy" },
              { "valueString": "Resection" }
            ]
          }
        ]
      }
    },
    {
      "id": "symptoms",
      "title": "Master Symptoms List",
      "category": "02 Symptoms",
      "topography": [],
      "status": "ready_for_review",
      "deprecated": false,
      "sourceOriginal": "doc/jundt/Case Descriptions/02 Symptoms/Symptoms general.md",
      "sourceNotes": "doc/jundt/Case Descriptions/02 Symptoms/Symptoms general.notes.md",
      "sourceFhir": "blocks/02 Symptoms/symptoms.json",
      "summary": "Master block containing all clinical symptoms across all organ systems with duration selectors and conditional gating.",
      "openQuestions": [
        "Confirm duration units (d, wk, mo, a) for Pain, Swelling, Cough, Uterine bleeding.",
        "Confirm gating of region-specific symptoms behind 'Other symptoms present'."
      ],
      "originalContent": "# Symptoms General\n\nSymptoms list including Pain (duration: d, wk, mo, a), Night pain (esp. C40), Swelling (duration), Night sweat (esp. C77), Weight loss, Fever, Pallor (esp. C42), Bleeding tendency, Susceptibility to infection, Jaundice (esp. C22), Cough (duration), Breathlessness, Constipation, Diarrhoea, Melaena, Nipple discharge (esp. C50), Uterine bleeding (duration), Uterine discharge, Dysuria, Haematuria.",
      "notesContent": "# Notes — Symptoms General\n\n- Primary symptoms modelled as boolean choices with UCUM-coded duration quantities where specified.\n- Region-specific scope annotations preserved for reference.",
      "fhirJson": {
        "resourceType": "Questionnaire",
        "id": "symptoms",
        "status": "draft",
        "title": "Master Symptoms List",
        "subjectType": ["Patient"],
        "item": [
          {
            "linkId": "pain",
            "type": "boolean",
            "text": "Pain",
            "code": [{ "system": "http://snomed.info/sct", "code": "22253000", "display": "Pain" }],
            "item": [
              {
                "linkId": "pain.duration",
                "type": "quantity",
                "text": "Duration",
                "code": [{ "system": "http://snomed.info/sct", "code": "162442009", "display": "Time symptom lasts" }],
                "extension": [
                  { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "d", "display": "days" } },
                  { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "wk", "display": "weeks" } },
                  { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "mo", "display": "months" } },
                  { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "a", "display": "years" } }
                ],
                "enableWhen": [{ "question": "pain", "operator": "=", "answerBoolean": true }]
              }
            ]
          },
          {
            "linkId": "nightpain",
            "type": "boolean",
            "text": "Night pain (esp. C40)",
            "code": [{ "system": "http://snomed.info/sct", "code": "36163009", "display": "Night pain" }]
          },
          {
            "linkId": "swelling",
            "type": "boolean",
            "text": "Swelling",
            "code": [{ "system": "http://snomed.info/sct", "code": "65124004", "display": "Swelling" }],
            "item": [
              {
                "linkId": "swelling.duration",
                "type": "quantity",
                "text": "Duration",
                "code": [{ "system": "http://snomed.info/sct", "code": "162442009", "display": "Time symptom lasts" }],
                "extension": [
                  { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "d", "display": "days" } },
                  { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "wk", "display": "weeks" } },
                  { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "mo", "display": "months" } },
                  { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "a", "display": "years" } }
                ],
                "enableWhen": [{ "question": "swelling", "operator": "=", "answerBoolean": true }]
              }
            ]
          },
          {
            "linkId": "nightsweat",
            "type": "boolean",
            "text": "Night sweat (esp. C77)",
            "code": [{ "system": "http://snomed.info/sct", "code": "42984000", "display": "Night sweats" }]
          },
          {
            "linkId": "weightloss",
            "type": "boolean",
            "text": "Weight loss",
            "code": [{ "system": "http://snomed.info/sct", "code": "89362005", "display": "Weight loss" }],
            "item": [
              {
                "linkId": "weightloss.kg",
                "type": "quantity",
                "text": "Amount lost (kg)",
                "extension": [
                  { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "kg", "display": "kg" } }
                ],
                "enableWhen": [{ "question": "weightloss", "operator": "=", "answerBoolean": true }]
              }
            ]
          },
          {
            "linkId": "fever",
            "type": "boolean",
            "text": "Fever",
            "code": [{ "system": "http://snomed.info/sct", "code": "386661006", "display": "Fever" }]
          },
          {
            "linkId": "pallor",
            "type": "boolean",
            "text": "Pallor (esp. C42)",
            "code": [{ "system": "http://snomed.info/sct", "code": "274643008", "display": "Body pale" }]
          },
          {
            "linkId": "bleedingtendency",
            "type": "boolean",
            "text": "Bleeding tendency (esp. C42)",
            "code": [{ "system": "http://snomed.info/sct", "code": "78596001", "display": "Bleeding diathesis" }]
          },
          {
            "linkId": "susceptibility",
            "type": "boolean",
            "text": "Susceptibility to infection (esp. C42)",
            "code": [{ "system": "http://snomed.info/sct", "code": "102463001", "display": "Susceptibility to infections" }]
          },
          {
            "linkId": "jaundice",
            "type": "boolean",
            "text": "Jaundice",
            "code": [{ "system": "http://snomed.info/sct", "code": "18165001", "display": "Jaundice" }]
          },
          {
            "linkId": "cough",
            "type": "boolean",
            "text": "Cough (esp. C34)",
            "code": [{ "system": "http://snomed.info/sct", "code": "49727002", "display": "Cough" }],
            "item": [
              {
                "linkId": "cough.duration",
                "type": "quantity",
                "text": "Duration",
                "code": [{ "system": "http://snomed.info/sct", "code": "162442009", "display": "Time symptom lasts" }],
                "extension": [
                  { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "d", "display": "days" } },
                  { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "wk", "display": "weeks" } },
                  { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "mo", "display": "months" } },
                  { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "a", "display": "years" } }
                ],
                "enableWhen": [{ "question": "cough", "operator": "=", "answerBoolean": true }]
              }
            ]
          },
          {
            "linkId": "breathlessness",
            "type": "boolean",
            "text": "Breathlessness / Shortness of breath",
            "code": [{ "system": "http://snomed.info/sct", "code": "267036007", "display": "Dyspnea" }]
          },
          {
            "linkId": "constipation",
            "type": "boolean",
            "text": "Constipation",
            "code": [{ "system": "http://snomed.info/sct", "code": "14760008", "display": "Constipation" }]
          },
          {
            "linkId": "diarrhoea",
            "type": "boolean",
            "text": "Diarrhoea",
            "code": [{ "system": "http://snomed.info/sct", "code": "62315008", "display": "Diarrhea" }]
          },
          {
            "linkId": "melaena",
            "type": "boolean",
            "text": "Melaena",
            "code": [{ "system": "http://snomed.info/sct", "code": "2901004", "display": "Melena" }]
          },
          {
            "linkId": "symptoms.other",
            "type": "string",
            "text": "Other symptoms (please specify)"
          }
        ]
      }
    },
    {
      "id": "symptoms.respiratory",
      "title": "Symptoms — Respiratory tract (C30-C39)",
      "category": "02 Symptoms",
      "topography": ["C30-C39"],
      "status": "ready_for_review",
      "deprecated": false,
      "sourceOriginal": "doc/jundt/Case Descriptions/02 Symptoms/Respiratory tract C30-C39.md",
      "sourceNotes": "doc/jundt/Case Descriptions/02 Symptoms/Respiratory tract C30-C39.notes.md",
      "sourceFhir": "blocks/02 Symptoms/symptoms.respiratory.json",
      "summary": "Specific respiratory symptoms including chronic cough, hemoptysis, expectoration, and smoking history.",
      "openQuestions": [
        "Merge Shortness of breath and Dyspnea into one question (synonymous in SNOMED 267036007)?",
        "Confirm if smoking history duration in years is sufficient or if Pack-Years is needed."
      ],
      "originalContent": "## Atemwege\n\n- Chronic cough (>3 months) yes/no\n- Shortness of breath yes/no\n- Dyspnea yes/no\n- Coughing up blood yes/no\n- Chronic chest pain yes/no\n- Smoker yes/no; If yes, how long (years)\n- Exspectoration (sputum) yes/no",
      "notesContent": "# Notes — Respiratory tract C30-C39\n\n- Shortness of breath / Dyspnea merged into single SNOMED concept 267036007.\n- Smoker duration in years coded as UCUM quantity.",
      "fhirJson": {
        "resourceType": "Questionnaire",
        "id": "symptoms.respiratory",
        "status": "draft",
        "title": "Symptoms — Respiratory tract (C30-C39)",
        "subjectType": ["Patient"],
        "item": [
          {
            "linkId": "chroniccough",
            "type": "boolean",
            "text": "Chronic cough (>3 months)",
            "code": [{ "system": "http://snomed.info/sct", "code": "68154008", "display": "Chronic cough" }]
          },
          {
            "linkId": "breathlessness",
            "type": "boolean",
            "text": "Shortness of breath / Dyspnea",
            "code": [{ "system": "http://snomed.info/sct", "code": "267036007", "display": "Dyspnea" }]
          },
          {
            "linkId": "hemoptysis",
            "type": "boolean",
            "text": "Coughing up blood (Hemoptysis)",
            "code": [{ "system": "http://snomed.info/sct", "code": "66857006", "display": "Hemoptysis" }]
          },
          {
            "linkId": "chestpain.chronic",
            "type": "boolean",
            "text": "Chronic chest pain",
            "code": [{ "system": "http://snomed.info/sct", "code": "82886004", "display": "Chronic chest pain" }]
          },
          {
            "linkId": "smoker",
            "type": "boolean",
            "text": "Smoker",
            "code": [{ "system": "http://snomed.info/sct", "code": "77176002", "display": "Smoker" }],
            "item": [
              {
                "linkId": "smoker.years",
                "type": "quantity",
                "text": "If yes, how long (years)",
                "code": [{ "system": "http://snomed.info/sct", "code": "160617001", "display": "Number of years smoked" }],
                "extension": [
                  { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "a", "display": "years" } }
                ],
                "enableWhen": [{ "question": "smoker", "operator": "=", "answerBoolean": true }]
              }
            ]
          },
          {
            "linkId": "expectoration",
            "type": "boolean",
            "text": "Expectoration (sputum)",
            "code": [{ "system": "http://snomed.info/sct", "code": "284523002", "display": "Sputum production" }]
          }
        ]
      }
    },
    {
      "id": "symptoms.digestive",
      "title": "Symptoms — Digestive organs (C15-C26)",
      "category": "02 Symptoms",
      "topography": ["C15-C26"],
      "status": "ready_for_review",
      "deprecated": false,
      "sourceOriginal": "doc/jundt/Case Descriptions/02 Symptoms/Digestive organs C15-C26.md",
      "sourceNotes": "doc/jundt/Case Descriptions/02 Symptoms/Digestive organs C15-C26.notes.md",
      "sourceFhir": "blocks/02 Symptoms/symptoms.digestive.json",
      "summary": "Digestive symptoms including 6-quadrant abdominal pain grid, nausea, vomiting, bowel habit changes, and jaundice.",
      "openQuestions": [
        "Confirm 6-quadrant abdominal pain grid (Upper/Middle/Lower x Left/Right).",
        "Deduplication of general symptoms (Weight loss, Fever, Jaundice) when used in composite forms."
      ],
      "originalContent": "# Digestive organs C15-C26\n\n- Dysphagia\n- Heartburn\n- Painful swallowing\n- Regurgitation\n- Dyspepsia\n- Abdominal pain (Location: Upper/Middle/Lower x R/L; Duration)\n- Flatulence\n- Weight loss (kg)\n- Vomiting (Vomiting blood)\n- Constipation, Diarrhoea, Stool changes, Melaena, Bloody stool\n- Fever, Jaundice, Easy bruising\n- Other",
      "notesContent": "# Notes — Digestive organs C15-C26\n\n- 6-quadrant location picker modelled as choice options.\n- Shared symptoms aligned with coding-registry.",
      "fhirJson": {
        "resourceType": "Questionnaire",
        "id": "symptoms.digestive",
        "status": "draft",
        "title": "Symptoms — Digestive organs (C15-C26)",
        "subjectType": ["Patient"],
        "item": [
          { "linkId": "dysphagia", "type": "boolean", "text": "Dysphagia", "code": [{ "system": "http://snomed.info/sct", "code": "40739000", "display": "Dysphagia" }] },
          { "linkId": "heartburn", "type": "boolean", "text": "Heartburn", "code": [{ "system": "http://snomed.info/sct", "code": "16331000", "display": "Heartburn" }] },
          { "linkId": "painfulswallowing", "type": "boolean", "text": "Painful swallowing", "code": [{ "system": "http://snomed.info/sct", "code": "30233002", "display": "Swallowing painful" }] },
          { "linkId": "regurgitation", "type": "boolean", "text": "Regurgitation", "code": [{ "system": "http://snomed.info/sct", "code": "78104003", "display": "Regurgitation of gastric content" }] },
          { "linkId": "dyspepsia", "type": "boolean", "text": "Dyspepsia", "code": [{ "system": "http://snomed.info/sct", "code": "162031009", "display": "Indigestion" }] },
          {
            "linkId": "pain.abdominal",
            "type": "boolean",
            "text": "Abdominal pain",
            "code": [{ "system": "http://snomed.info/sct", "code": "21522001", "display": "Abdominal pain" }],
            "item": [
              {
                "linkId": "pain.abdominal.location",
                "type": "choice",
                "text": "Location",
                "answerOption": [
                  { "valueString": "Upper abdomen (left)" },
                  { "valueString": "Upper abdomen (right)" },
                  { "valueString": "Middle abdomen (left)" },
                  { "valueString": "Middle abdomen (right)" },
                  { "valueString": "Lower abdomen (left)" },
                  { "valueString": "Lower abdomen (right)" }
                ],
                "enableWhen": [{ "question": "pain.abdominal", "operator": "=", "answerBoolean": true }]
              },
              {
                "linkId": "pain.abdominal.duration",
                "type": "quantity",
                "text": "Duration",
                "code": [{ "system": "http://snomed.info/sct", "code": "162442009", "display": "Time symptom lasts" }],
                "extension": [
                  { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "d", "display": "days" } },
                  { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "wk", "display": "weeks" } },
                  { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "mo", "display": "months" } },
                  { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "a", "display": "years" } }
                ],
                "enableWhen": [{ "question": "pain.abdominal", "operator": "=", "answerBoolean": true }]
              }
            ]
          },
          { "linkId": "flatulence", "type": "boolean", "text": "Flatulence", "code": [{ "system": "http://snomed.info/sct", "code": "249504006", "display": "Passing flatus" }] },
          {
            "linkId": "weightloss",
            "type": "boolean",
            "text": "Weight loss",
            "code": [{ "system": "http://snomed.info/sct", "code": "89362005", "display": "Weight loss" }],
            "item": [
              {
                "linkId": "weightloss.kg",
                "type": "quantity",
                "text": "Amount lost (kg)",
                "extension": [
                  { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "kg", "display": "kg" } }
                ],
                "enableWhen": [{ "question": "weightloss", "operator": "=", "answerBoolean": true }]
              }
            ]
          },
          {
            "linkId": "vomiting",
            "type": "boolean",
            "text": "Vomiting",
            "code": [{ "system": "http://snomed.info/sct", "code": "422400008", "display": "Vomiting" }],
            "item": [
              {
                "linkId": "vomiting.blood",
                "type": "boolean",
                "text": "Vomiting blood",
                "code": [{ "system": "http://snomed.info/sct", "code": "8765009", "display": "Hematemesis" }],
                "enableWhen": [{ "question": "vomiting", "operator": "=", "answerBoolean": true }]
              }
            ]
          },
          { "linkId": "constipation", "type": "boolean", "text": "Constipation", "code": [{ "system": "http://snomed.info/sct", "code": "14760008", "display": "Constipation" }] },
          { "linkId": "diarrhoea", "type": "boolean", "text": "Diarrhoea", "code": [{ "system": "http://snomed.info/sct", "code": "62315008", "display": "Diarrhea" }] },
          { "linkId": "stoolchanges", "type": "boolean", "text": "Stool changes", "code": [{ "system": "http://snomed.info/sct", "code": "88111009", "display": "Altered bowel function" }] },
          { "linkId": "melaena", "type": "boolean", "text": "Melaena", "code": [{ "system": "http://snomed.info/sct", "code": "2901004", "display": "Melena" }] },
          { "linkId": "bloodystool", "type": "boolean", "text": "Bloody stool", "code": [{ "system": "http://snomed.info/sct", "code": "405729008", "display": "Hematochezia" }] },
          { "linkId": "fever", "type": "boolean", "text": "Fever", "code": [{ "system": "http://snomed.info/sct", "code": "386661006", "display": "Fever" }] },
          { "linkId": "jaundice", "type": "boolean", "text": "Jaundice", "code": [{ "system": "http://snomed.info/sct", "code": "18165001", "display": "Jaundice" }] },
          { "linkId": "easybruising", "type": "boolean", "text": "Easy bruising", "code": [{ "system": "http://snomed.info/sct", "code": "424131007", "display": "Easy bruising" }] }
        ]
      }
    },
    {
      "id": "symptoms.breast-female",
      "title": "Symptoms — Breast and Female Genital Organs (C50-C58)",
      "category": "02 Symptoms",
      "topography": ["C50", "C51-C58"],
      "status": "ready_for_review",
      "deprecated": false,
      "sourceOriginal": "doc/jundt/Case Descriptions/02 Symptoms/Breast and female genital organs C50-C58.md",
      "sourceNotes": "doc/jundt/Case Descriptions/02 Symptoms/Breast and female genital organs C50-C58.notes.md",
      "sourceFhir": "blocks/02 Symptoms/symptoms.breast-female.json",
      "summary": "Breast lumps, nipple discharge, skin changes, vaginal bleeding/discharge, pelvic pain, menstrual cycle, pregnancy history.",
      "openQuestions": [
        "Standardize terminology to 'Vaginal bleeding' and 'Vaginal discharge' across forms?",
        "Confirm multi-select for menstrual irregularities (Menorrhagia, Spotting, Amenorrhea, Dysmenorrhea).",
        "Confirm interactive date picker for date of last delivery."
      ],
      "originalContent": "# Breast & Female Genital Organs C50-C58\n\n- Breast: Lump (duration, growth, consistency, border), Nipple discharge, Nipple retracted, Skin discolored\n- Female Genital: Pregnancy (week), Previous pregnancies (last delivery date), Menopause (postmenopausal bleeding vs regular cycle vs irregular periods), Lower abdominal pain (duration, side), Lower abdominal swelling (duration, side), Vaginal bleeding, Vaginal discharge.",
      "notesContent": "# Notes — Breast and Female Genital Organs\n\n- Conditional branching on Menopause (Yes -> Postmenopausal bleeding; No -> Regular cycle -> Irregularities multi-select).\n- Date picker for delivery date, integer week for gestational age.",
      "fhirJson": {
        "resourceType": "Questionnaire",
        "id": "symptoms.breast-female",
        "status": "draft",
        "title": "Symptoms — Breast and Female Genital Organs (C50-C58)",
        "subjectType": ["Patient"],
        "item": [
          {
            "linkId": "sec.breast",
            "type": "group",
            "text": "Breast Symptoms (C50)",
            "item": [
              {
                "linkId": "breast.lump",
                "type": "boolean",
                "text": "Lump",
                "code": [{ "system": "http://snomed.info/sct", "code": "274647000", "display": "Breast lump" }],
                "item": [
                  {
                    "linkId": "breast.lump.duration",
                    "type": "quantity",
                    "text": "If yes, since when",
                    "code": [{ "system": "http://snomed.info/sct", "code": "162442009", "display": "Time symptom lasts" }],
                    "extension": [
                      { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "d", "display": "days" } },
                      { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "wk", "display": "weeks" } },
                      { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "mo", "display": "months" } }
                    ],
                    "enableWhen": [{ "question": "breast.lump", "operator": "=", "answerBoolean": true }]
                  },
                  {
                    "linkId": "breast.lump.consistency",
                    "type": "choice",
                    "text": "Consistency",
                    "answerOption": [
                      { "valueCoding": { "system": "http://snomed.info/sct", "code": "255395000", "display": "Hard" } },
                      { "valueCoding": { "system": "http://snomed.info/sct", "code": "255397008", "display": "Smooth" } },
                      { "valueString": "Unknown" }
                    ],
                    "enableWhen": [{ "question": "breast.lump", "operator": "=", "answerBoolean": true }]
                  },
                  {
                    "linkId": "breast.lump.border",
                    "type": "choice",
                    "text": "Border",
                    "answerOption": [
                      { "valueCoding": { "system": "http://snomed.info/sct", "code": "255535002", "display": "Irregular" } },
                      { "valueCoding": { "system": "http://snomed.info/sct", "code": "255534003", "display": "Regular" } },
                      { "valueString": "Unknown" }
                    ],
                    "enableWhen": [{ "question": "breast.lump", "operator": "=", "answerBoolean": true }]
                  }
                ]
              },
              {
                "linkId": "nippledischarge",
                "type": "boolean",
                "text": "Nipple discharge",
                "code": [{ "system": "http://snomed.info/sct", "code": "54302000", "display": "Discharge from nipple" }]
              },
              {
                "linkId": "breast.retraction",
                "type": "boolean",
                "text": "Nipple retracted",
                "code": [{ "system": "http://snomed.info/sct", "code": "69085004", "display": "Nipple retraction" }]
              },
              {
                "linkId": "breast.skindiscoloration",
                "type": "boolean",
                "text": "Breast-Skin discolored",
                "code": [{ "system": "http://snomed.info/sct", "code": "402773007", "display": "Discoloration of skin of breast" }]
              }
            ]
          },
          {
            "linkId": "sec.female.genital",
            "type": "group",
            "text": "Female Genital Organs (C51-C58)",
            "item": [
              {
                "linkId": "pregnancy",
                "type": "boolean",
                "text": "Pregnancy",
                "code": [{ "system": "http://snomed.info/sct", "code": "77386006", "display": "Pregnant" }],
                "item": [
                  {
                    "linkId": "pregnancy.week",
                    "type": "integer",
                    "text": "If yes, which week (please specify)",
                    "code": [{ "system": "http://snomed.info/sct", "code": "1156895004", "display": "Gestational age" }],
                    "enableWhen": [{ "question": "pregnancy", "operator": "=", "answerBoolean": true }]
                  }
                ]
              },
              {
                "linkId": "pregnancy.previous",
                "type": "boolean",
                "text": "Previous pregnancies",
                "code": [{ "system": "http://snomed.info/sct", "code": "161732006", "display": "Gravida" }],
                "item": [
                  {
                    "linkId": "pregnancy.previous.lastdelivery",
                    "type": "date",
                    "text": "If yes, date of last delivery (dd,mm,yyyy)",
                    "code": [{ "system": "http://snomed.info/sct", "code": "161714006", "display": "Date of last delivery" }],
                    "enableWhen": [{ "question": "pregnancy.previous", "operator": "=", "answerBoolean": true }]
                  }
                ]
              },
              {
                "linkId": "menopause",
                "type": "boolean",
                "text": "Menopause",
                "code": [{ "system": "http://snomed.info/sct", "code": "426979002", "display": "Menopause" }],
                "item": [
                  {
                    "linkId": "menopause.postbleeding",
                    "type": "boolean",
                    "text": "If yes: Postmenopausal bleeding",
                    "code": [{ "system": "http://snomed.info/sct", "code": "289530006", "display": "Postmenopausal bleeding" }],
                    "enableWhen": [{ "question": "menopause", "operator": "=", "answerBoolean": true }]
                  },
                  {
                    "linkId": "menopause.regularcycle",
                    "type": "boolean",
                    "text": "If no: Regular menstrual cycle",
                    "code": [{ "system": "http://snomed.info/sct", "code": "289947009", "display": "Regular menstrual cycle" }],
                    "enableWhen": [{ "question": "menopause", "operator": "=", "answerBoolean": false }],
                    "item": [
                      {
                        "linkId": "menopause.irregularities",
                        "type": "choice",
                        "repeats": true,
                        "text": "If no: Irregular periods",
                        "answerOption": [
                          { "valueCoding": { "system": "http://snomed.info/sct", "code": "386692008", "display": "Heavy menstrual bleeding / Menorrhagia" } },
                          { "valueCoding": { "system": "http://snomed.info/sct", "code": "289535001", "display": "Abnormal bleeding / Spotting" } },
                          { "valueCoding": { "system": "http://snomed.info/sct", "code": "89217008", "display": "Missed periods / Amenorrhea" } },
                          { "valueCoding": { "system": "http://snomed.info/sct", "code": "266599000", "display": "Painful periods / Dysmenorrhea" } }
                        ],
                        "enableWhen": [{ "question": "menopause.regularcycle", "operator": "=", "answerBoolean": false }]
                      }
                    ]
                  }
                ]
              },
              {
                "linkId": "vaginalbleeding",
                "type": "boolean",
                "text": "Vaginal bleeding",
                "code": [{ "system": "http://snomed.info/sct", "code": "249021008", "display": "Vaginal bleeding" }],
                "item": [
                  {
                    "linkId": "vaginalbleeding.duration",
                    "type": "quantity",
                    "text": "If yes, since when",
                    "code": [{ "system": "http://snomed.info/sct", "code": "162442009", "display": "Time symptom lasts" }],
                    "extension": [
                      { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "d", "display": "days" } },
                      { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "wk", "display": "weeks" } },
                      { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "mo", "display": "months" } }
                    ],
                    "enableWhen": [{ "question": "vaginalbleeding", "operator": "=", "answerBoolean": true }]
                  }
                ]
              },
              {
                "linkId": "vaginaldischarge",
                "type": "boolean",
                "text": "Vaginal discharge",
                "code": [{ "system": "http://snomed.info/sct", "code": "271939006", "display": "Vaginal discharge" }],
                "item": [
                  {
                    "linkId": "vaginaldischarge.duration",
                    "type": "quantity",
                    "text": "If yes, since when",
                    "code": [{ "system": "http://snomed.info/sct", "code": "162442009", "display": "Time symptom lasts" }],
                    "extension": [
                      { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "d", "display": "days" } },
                      { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "wk", "display": "weeks" } },
                      { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "mo", "display": "months" } }
                    ],
                    "enableWhen": [{ "question": "vaginaldischarge", "operator": "=", "answerBoolean": true }]
                  }
                ]
              }
            ]
          }
        ]
      }
    },
    {
      "id": "imaging",
      "title": "Master Imaging List",
      "category": "03 Imaging",
      "topography": [],
      "status": "ready_for_review",
      "deprecated": false,
      "sourceOriginal": "doc/jundt/Case Descriptions/03 Imaging/Imaging (generelly used techniques irrespective of speciality).md",
      "sourceNotes": "doc/jundt/Case Descriptions/03 Imaging/Imaging (generelly used techniques irrespective of speciality).notes.md",
      "sourceFhir": "blocks/03 Imaging/imaging.json",
      "summary": "Master imaging block covering X-ray, Ultrasound, CT, MRI, PET, Bone scan, Mammography, Dental panoramic, CBCT.",
      "openQuestions": [
        "Confirm conditional report and DICOM image upload prompts on 'Yes' selection.",
        "Confirm organ-specific modalities (Mammography for C50, Panoramic/CBCT for C41)."
      ],
      "originalContent": "# Imaging\n\n- Conventional X-rays (2 planes)\n- Sonography\n- CT\n- MRI\n- PET CT\n- PET MRI\n- PET/CT MRI\n- Panoramic view (Orthopantogram)\n- Cone-beam CT (CBCT)\n- Mammography\n- Other",
      "notesContent": "# Notes — Imaging\n\n- Imaging questions are clean boolean indicators.\n- File uploads are managed natively in iPath.",
      "fhirJson": {
        "resourceType": "Questionnaire",
        "id": "imaging",
        "status": "draft",
        "title": "Master Imaging List",
        "subjectType": ["Patient"],
        "item": [
          { "linkId": "xray", "type": "boolean", "text": "Conventional X-rays (2 planes)", "code": [{ "system": "http://snomed.info/sct", "code": "168537006", "display": "Plain X-ray" }] },
          { "linkId": "sonography", "type": "boolean", "text": "Sonography", "code": [{ "system": "http://snomed.info/sct", "code": "16310003", "display": "Ultrasonography" }] },
          { "linkId": "ct", "type": "boolean", "text": "CT scan", "code": [{ "system": "http://snomed.info/sct", "code": "77477000", "display": "Computed tomography" }] },
          { "linkId": "mri", "type": "boolean", "text": "MRI", "code": [{ "system": "http://snomed.info/sct", "code": "113091000", "display": "Magnetic resonance imaging" }] },
          { "linkId": "petct", "type": "boolean", "text": "PET CT", "code": [{ "system": "http://snomed.info/sct", "code": "450436003", "display": "Positron emission tomography with computed tomography" }] },
          { "linkId": "panoramic", "type": "boolean", "text": "Panoramic view / Orthopantogram [esp. Jaws C41.0, C41.1]", "code": [{ "system": "http://snomed.info/sct", "code": "89846007", "display": "Orthopantogram" }] },
          { "linkId": "cbct", "type": "boolean", "text": "Cone-beam computed tomography / CBCT [esp. Jaws C41.0, C41.1]", "code": [{ "system": "http://snomed.info/sct", "code": "717193008", "display": "Cone beam CT" }] },
          { "linkId": "mammography", "type": "boolean", "text": "Mammography [esp. Breast C50]", "code": [{ "system": "http://snomed.info/sct", "code": "71651007", "display": "Mammography" }] },
          { "linkId": "imaging.other", "type": "string", "text": "Other imaging (please specify)" }
        ]
      }
    },
    {
      "id": "lab.blood-count",
      "title": "Labor — Blood Cell Count",
      "category": "04 Lab",
      "topography": ["C42", "C77"],
      "status": "ready_for_review",
      "deprecated": false,
      "sourceOriginal": "doc/jundt/Case Descriptions/04 Lab/Hematology [C42-C42] and Lymph nodes [C77].md",
      "sourceNotes": "doc/jundt/Case Descriptions/04 Lab/Hematology [C42-C42] and Lymph nodes [C77].notes.md",
      "sourceFhir": "blocks/04 Lab/lab.blood-count.json",
      "summary": "Standard blood cell count with SI default units (10*12/L, 10*9/L) and US alternatives (10*6/uL, 10*3/uL).",
      "openQuestions": [
        "Confirm inclusion of Total Granulocyte Count alongside differential sub-counts.",
        "Confirm dual unit support (absolute counts vs. differential percentages %)."
      ],
      "originalContent": "# Labor Hematology & Lymph Nodes\n\n- Blood cell count available: yes/no\n- Erythrocytes\n- Granulocytes (Neutrophils, Eosinophils, Basophils)\n- Monocytes\n- Lymphocytes\n- Thrombocytes",
      "notesContent": "# Notes — Labor Hematology\n\n- Differential cell types support `%` (default), `10*3/uL`, and `10*9/L`.\n- Dual LOINC mappings for absolute counts and percentage fractions.",
      "fhirJson": {
        "resourceType": "Questionnaire",
        "id": "lab.blood-count",
        "status": "draft",
        "title": "Labor — blood cell count",
        "subjectType": ["Patient"],
        "item": [
          { "linkId": "available", "type": "boolean", "text": "Blood cell count available", "code": [{ "system": "http://snomed.info/sct", "code": "88308000", "display": "Blood cell count" }] },
          {
            "linkId": "erythrocytes",
            "type": "quantity",
            "text": "Erythrocytes",
            "code": [{ "system": "http://snomed.info/sct", "code": "14089001", "display": "Red blood cell count" }],
            "extension": [
              { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "10*6/uL", "display": "10^6/µL" } },
              { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "10*12/L", "display": "10^12/L" } }
            ],
            "enableWhen": [{ "question": "available", "operator": "=", "answerBoolean": true }]
          },
          {
            "linkId": "granulocytes",
            "type": "quantity",
            "text": "Granulocytes (total)",
            "code": [{ "system": "http://snomed.info/sct", "code": "118138007", "display": "Granulocyte count" }],
            "extension": [
              { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "%", "display": "%" } },
              { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "10*3/uL", "display": "10^3/µL" } },
              { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "10*9/L", "display": "10^9/L" } }
            ],
            "enableWhen": [{ "question": "available", "operator": "=", "answerBoolean": true }],
            "item": [
              {
                "linkId": "neutrophils",
                "type": "quantity",
                "text": "Neutrophils",
                "code": [{ "system": "http://snomed.info/sct", "code": "30630007", "display": "Neutrophil count" }],
                "extension": [
                  { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "%", "display": "%" } },
                  { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "10*3/uL", "display": "10^3/µL" } },
                  { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "10*9/L", "display": "10^9/L" } }
                ]
              },
              {
                "linkId": "eosinophils",
                "type": "quantity",
                "text": "Eosinophils",
                "code": [{ "system": "http://snomed.info/sct", "code": "71960002", "display": "Eosinophil count" }],
                "extension": [
                  { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "%", "display": "%" } },
                  { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "10*3/uL", "display": "10^3/µL" } },
                  { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "10*9/L", "display": "10^9/L" } }
                ]
              },
              {
                "linkId": "basophils",
                "type": "quantity",
                "text": "Basophils",
                "code": [{ "system": "http://snomed.info/sct", "code": "42351005", "display": "Basophil count" }],
                "extension": [
                  { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "%", "display": "%" } },
                  { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "10*3/uL", "display": "10^3/µL" } },
                  { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "10*9/L", "display": "10^9/L" } }
                ]
              }
            ]
          },
          {
            "linkId": "monocytes",
            "type": "quantity",
            "text": "Monocytes",
            "code": [{ "system": "http://snomed.info/sct", "code": "67776007", "display": "Monocyte count" }],
            "extension": [
              { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "%", "display": "%" } },
              { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "10*3/uL", "display": "10^3/µL" } }
            ],
            "enableWhen": [{ "question": "available", "operator": "=", "answerBoolean": true }]
          },
          {
            "linkId": "lymphocytes",
            "type": "quantity",
            "text": "Lymphocytes",
            "code": [{ "system": "http://snomed.info/sct", "code": "74765001", "display": "Lymphocyte count" }],
            "extension": [
              { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "%", "display": "%" } },
              { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "10*3/uL", "display": "10^3/µL" } }
            ],
            "enableWhen": [{ "question": "available", "operator": "=", "answerBoolean": true }]
          },
          {
            "linkId": "thrombocytes",
            "type": "quantity",
            "text": "Thrombocytes",
            "code": [{ "system": "http://snomed.info/sct", "code": "61928009", "display": "Platelet count" }],
            "extension": [
              { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "10*3/uL", "display": "10^3/µL" } },
              { "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption", "valueCoding": { "system": "http://unitsofmeasure.org", "code": "10*9/L", "display": "10^9/L" } }
            ],
            "enableWhen": [{ "question": "available", "operator": "=", "answerBoolean": true }]
          }
        ]
      }
    },
    {
      "id": "diagnostic",
      "title": "Master Diagnostic Assessment List",
      "category": "05 Diagnostic",
      "topography": [],
      "status": "ready_for_review",
      "deprecated": false,
      "sourceOriginal": "doc/jundt/Diagnostic Assessment/General diagnostic assement fields.md",
      "sourceNotes": "doc/jundt/Diagnostic Assessment/General diagnostic assement fields.notes.md",
      "sourceFhir": "blocks/05 Diagnostic/diagnostic.json",
      "summary": "Post-examination diagnostic report fields: surgical margin status, soft tissue extension, tumor size classes, and tumor depth.",
      "openQuestions": [
        "Add explicit choice option for intermediate tumor size: '5 - 10 cm'?",
        "Clarify applicability of tumor depth (Superficial vs Deep) to bone tumors ('Gilt auch fuer Knochen!')."
      ],
      "originalContent": "# Diagnostic Assessment\n\n- Margin status (R0, R1, R2, No data)\n- Soft tissue Tumours (& Bone): Size (<5cm, >10cm), Depth (Superficial, Deep), Previous radiation therapy\n- Breast: Family history, Other cancers, Microcalcifications",
      "notesContent": "# Notes — Diagnostic Assessment\n\n- Standard UICC R-classification mapped to SNOMED CT.\n- Distinction between superficial and sub-fascial tumor depths.",
      "fhirJson": {
        "resourceType": "Questionnaire",
        "id": "diagnostic",
        "status": "draft",
        "title": "Master Diagnostic Assessment List",
        "subjectType": ["Patient"],
        "item": [
          {
            "linkId": "margin.status",
            "type": "choice",
            "text": "Margin status (all Tumors)",
            "code": [{ "system": "http://snomed.info/sct", "code": "396631000", "display": "Surgical margin status" }],
            "answerOption": [
              { "valueCoding": { "system": "http://snomed.info/sct", "code": "443930003", "display": "R0 microscopically negative surgical margins" } },
              { "valueCoding": { "system": "http://snomed.info/sct", "code": "443929007", "display": "R1 microscopically positive surgical margins" } },
              { "valueCoding": { "system": "http://snomed.info/sct", "code": "396632007", "display": "R2 macroscopically incomplete with gross residual tumor" } },
              { "valueString": "No data" }
            ]
          },
          {
            "linkId": "tumor.size",
            "type": "choice",
            "text": "Tumor size",
            "code": [{ "system": "http://snomed.info/sct", "code": "371024008", "display": "Tumor size" }],
            "answerOption": [
              { "valueString": "Size < 5cm" },
              { "valueString": "Size > 10cm" },
              { "valueString": "Size: no data" }
            ]
          },
          {
            "linkId": "tumor.depth",
            "type": "choice",
            "text": "Tumor depth",
            "answerOption": [
              { "valueCoding": { "system": "http://snomed.info/sct", "code": "371003006", "display": "Superficial (above fascia)" } },
              { "valueCoding": { "system": "http://snomed.info/sct", "code": "371004000", "display": "Deep (below fascia/intramuscular)" } },
              { "valueString": "Depth: no data" }
            ]
          },
          {
            "linkId": "radiation.history",
            "type": "choice",
            "text": "Previous radiation therapy to the area now affected",
            "code": [{ "system": "http://snomed.info/sct", "code": "416237000", "display": "History of radiation therapy" }],
            "answerOption": [
              { "valueString": "Yes" },
              { "valueString": "No" },
              { "valueString": "No data" }
            ]
          }
        ]
      }
    }
  ],
  "caseDescriptions": [
    {
      "id": "jundt-hematology-lymph",
      "title": "Hematology and Lymph nodes (Historical Prototype)",
      "category": "Case Description",
      "topography": ["C42", "C77"],
      "status": "superseded",
      "deprecated": true,
      "sourceOriginal": "doc/jundt/Case Descriptions/04 Lab/Hematology [C42-C42] and Lymph nodes [C77].md",
      "sourceNotes": "staging/jundt-hematology-lymph/jundt-hematology-lymph.review.md",
      "sourceFhir": "staging/jundt-hematology-lymph/jundt-hematology-lymph.json",
      "summary": "Initial prototype for hematology and lymph node cases (superseded by modular building blocks).",
      "openQuestions": [],
      "originalContent": "# Hematology and Lymph nodes [C42, C77]\n\nInitial prototype draft for hematology and lymph node consultations.",
      "notesContent": "# Staging Review — Hematology & Lymph Nodes\n\nHistorical review vehicle created during Phase 0 intake. Superseded by modular blocks (04 Lab).",
      "fhirJson": {
        "resourceType": "Questionnaire",
        "id": "jundt-hematology-lymph",
        "title": "Hematology and Lymph nodes",
        "status": "draft",
        "subjectType": ["Patient"],
        "item": [
          { "linkId": "lab.available", "type": "boolean", "text": "Blood cell count available" },
          { "linkId": "lab.erythrocytes", "type": "quantity", "text": "Erythrocytes", "enableWhen": [{ "question": "lab.available", "operator": "=", "answerBoolean": true }] }
        ]
      }
    },
    {
      "id": "jundt-imaging",
      "title": "Imaging (Historical Prototype)",
      "category": "Case Description",
      "topography": [],
      "status": "superseded",
      "deprecated": true,
      "sourceOriginal": "doc/jundt/Case Descriptions/03 Imaging/Imaging (generelly used techniques irrespective of speciality).md",
      "sourceNotes": "staging/jundt-imaging/jundt-imaging.review.md",
      "sourceFhir": "staging/jundt-imaging/jundt-imaging.json",
      "summary": "Initial prototype for imaging requests (superseded by block imaging.json).",
      "openQuestions": [],
      "originalContent": "# Imaging (Historical Prototype)\n\nOriginal draft for imaging consultation fields.",
      "notesContent": "# Staging Review — Imaging\n\nSuperseded by modular blocks (03 Imaging).",
      "fhirJson": {
        "resourceType": "Questionnaire",
        "id": "jundt-imaging",
        "title": "Imaging",
        "status": "draft",
        "subjectType": ["Patient"],
        "item": [
          { "linkId": "imaging.xray", "type": "boolean", "text": "Conventional X-rays (2 planes)" }
        ]
      }
    }
  ]
};
