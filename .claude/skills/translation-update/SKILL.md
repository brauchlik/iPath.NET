---
name: Translation Update
description: Sync and complete DE/FR/IT translations before cutting a release - run this when preparing a release, or when asked to update/check translations.
allowed-tools: Bash, Read, Edit
---

# Translation update before release

Locale files live at `src/ui/iPath.Blazor.Server/Locales/{en,de,fr,it}.json` and are tracked in
the repo. `en.json` is the master key list (identity: key == value); `de`/`fr`/`it` hold the
translation, or `""` if still untranslated.

## Steps

1. **Check for drift** - run the scanner in dry-run mode from the repo root:
   ```bash
   dotnet run --project tools/iPath.LocalizationSync -- sync
   ```
   This statically scans `src/ui/**` for every `T["..."]`/`@T["..."]` call site and diffs the
   result against the four locale files. Exit code `1` means something changed since the files
   were last synced (new strings added to the UI, or a string's English text edited).

2. **Read the report.** It lists, per locale:
   - `missing` keys - new source strings not yet in a locale file at all.
   - `orphaned` keys - keys in a locale file no longer found in source (renamed/removed string).
   - Flagged non-literal call sites (e.g. `T[someVariable]`) - the scanner can't extract these
     statically; check them by hand if the report shows any (there's normally exactly one,
     `AnnotationItemView.razor` - if new ones appear, look at what they render and add/update the
     translation for the resolved value manually).

3. **Pull in the new keys**, then translate them by hand:
   ```bash
   dotnet run --project tools/iPath.LocalizationSync -- sync --write
   ```
   This appends missing keys as `""` placeholders in `de.json`/`fr.json`/`it.json` (and as
   identity entries in `en.json`). It does **not** translate anything - do that yourself, one
   key at a time, by editing the three files directly.

   **Translate with context, not in isolation.** Before translating a key, find where it's used
   (grep the source for the exact English string) and read the surrounding component/page. Short
   or generic phrases are ambiguous out of context - e.g. "not allowed" could be a file-upload
   rejection or a permissions error, and translates differently depending on which. This project
   deliberately does not use the app's built-in AI auto-translate pipeline for this reason: it
   only ever sees the bare string, with no surrounding context, and produces translations that
   can't be trusted without exactly the same manual check anyway.

   Keep it consistent with what's already translated nearby (tone, terminology) rather than
   translating each string as a one-off.

   **If a key is ambiguous because the source text itself is badly worded, fix the source - don't
   translate around it.** E.g. a bare `T["Body"]` turned out to be an email message-body field
   (`SendMailDialog.razor`) - ambiguous with "body" as in anatomical body, which this app also
   talks about constantly. The fix was renaming the call site itself to `T["Message Body"]`, not
   inventing a clever disambiguating translation for the vague original. A context-aware pass is
   exactly what makes this possible (unlike the AI pipeline, which only ever sees the bare string
   and has no way to even notice the ambiguity, let alone fix it).

   One mechanical gotcha this implies: **`en.json`'s value is never actually read to render the
   English UI.** `StringLocalizerService.GetTranslation` returns the key itself whenever the
   current culture is English - it only consults the locale JSON for non-English cultures. So the
   key *is* the English display text, unconditionally; there's no way to have a clearer/longer key
   for translators while keeping a shorter English label via `en.json`. If you rename a key,
   English users see the new wording too - treat that as a real (usually positive) UI change, not
   just a translation-side rename.

4. **Review orphaned keys** in the report. If a key is genuinely gone (confirm with `git grep` for
   the exact string), remove it with:
   ```bash
   dotnet run --project tools/iPath.LocalizationSync -- sync --write --purge
   ```
   Never purge without first confirming the string really isn't used anywhere - a scan blind spot
   deleting a real translation is worse than an unused one sitting in the file.

5. **Confirm clean**, then build:
   ```bash
   dotnet run --project tools/iPath.LocalizationSync -- sync
   # exit code 0, "All locale files are in sync with source."
   dotnet build iPath.NET.slnx
   ```

6. **Review and commit** the diff to `Locales/*.json` as its own commit, separate from any other
   release changes.

## What this is not

Not a substitute for reviewing existing untranslated (`""`) entries already in the backlog before
a release - the scanner only finds *new* drift since the last sync, it doesn't track how much of
the existing backlog is still unfinished. Check `TranslatedKeys`/`MissingKeys` counts via the
Admin > AI Status > Translations Manager UI, or count empty values in the JSON directly, if you
need that picture too.
