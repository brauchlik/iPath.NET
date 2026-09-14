# Snowstorm usage

Local SNOMED CT server, no auth.

- Base URL: `http://basyssrvdock1:8082`
- Swagger UI: `http://basyssrvdock1:8082/swagger-ui/index.html`
- Branch: `MAIN` (SNOMED International edition)

## Search for concepts by term

On this server the term filter on `/browser/MAIN/concepts` is **ignored**
(it returns the whole concept list regardless of `term`/`limit`). Use the
**description search** endpoint instead:

```
GET /browser/MAIN/descriptions?term=<term>&activeFilter=true&limit=20
```

Response: `items[]` each with `term`, `active`, `languageCode`, `concept`
(`conceptId`, `active`, `fsn`, `pt`). Also `totalElements`, pagination fields.
Filter to `concept.active == true` (the description-level `active` can be
false even for an active concept). The description-level term may include
historical/duplicate entries; dedupe by `concept.conceptId`.

Example (verified working):

```
GET http://basyssrvdock1:8082/browser/MAIN/descriptions?term=mammography&activeFilter=true&limit=5
# -> top match: concept 71651007 "Mammography (procedure)" (FULLY_DEFINED)
```

Fuzzy match caveat: `totalElements` can be large (broad substring matches);
trust the ordering, but always confirm the winning concept via the detail
endpoint before committing a code.

## Get one concept (confirm before committing a code)

```
GET /browser/MAIN/concepts/<sctid>
```

Returns the concept with `descriptions`, `classAxioms`, `inferredRelationships`
(including `parents` / `ancestors`). Use this to verify:
- the semantic tag in the FSN matches the intent (finding / procedure / observable / body structure);
- the preferred term fits the question;
- the parent concepts are consistent with the medical meaning.

## Coding policy

1. Only use concept IDs returned by the server. **Never fabricate a code.**
2. Code an answer when:
   - the concept is unambiguous (search returns a clear top hit), AND
   - the display/FSN fits the question text, AND
   - the semantic tag matches (e.g. `(observable entity)`, `(finding)`, `(procedure)`).
3. Otherwise fall back to plain text or a local custom code
   (`system: "http://ipath.local"`).
4. Prefer `pt` as the `display` value in `valueCoding`/`code`.
5. Record in `<id>.snomed.md`:
   - each term searched,
   - the query + result count,
   - the code chosen (with FSN) or why none was chosen,
   - any ambiguity/uncertainty noted for the pathologist's review.

## Tips

- Terms in the pathologist docs are often English non-FSN labels; try the PT and
  common synonyms. If the first search misses, retry with `searchMode` default
  and `term` variants before concluding "no concept".
- For "blood cell count" style labs, the observable entity / laboratory procedure
  hierarchy is the right place to look.
- Imaging modalities (X-ray, CT, MRI, PET, sonography, mammography, CBCT,
  panoramic radiograph) have well-established SNOMED procedure concepts.
