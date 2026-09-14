# AI-Assisted Commit Attribution Plan

> **Status:** Proposed. Pending decision. No code or tooling changes yet.

**Date:** 2026-09-14
**Author:** Kurt Brauchli
**Triggered by:** PR #38 (`chore/blazor-cleanup`) merged without AI co-author trailers
**Target:** Future commit workflow on this repository

---

## Goal

When a commit is created with assistance from an AI tool (OpenCode, Claude Code, Cursor, etc.), the commit message must include a `Co-Authored-By:` trailer naming the tool, so GitHub displays you and the AI tool as co-authors. Opt-in per commit — never automatic for every commit.

---

## Background

The 5 commits in PR #38 (`b0a41ad`, `b513097`, `4d62aae`, `e9b40ce`, `a6d3dc4`) all show only `Kurt Brauchli <kurt.brauchli@basysdata.ch>` as author/committer. No AI attribution is included in the metadata.

We decided **not** to retroactively amend those commits — that would require force-push and rewrite PR history. The plan captures a convention for future commits instead.

---

## Proposal: PowerShell alias + AGENTS.md convention

### 1. PowerShell alias in `$PROFILE`

Add to `$PROFILE` (typically `~\Documents\PowerShell\Microsoft.PowerShell_profile.ps1`):

```powershell
# OpenCode-assisted commit: append Co-Authored-By trailer
function Invoke-OpenCodeCommit {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0, ValueFromRemainingArguments = $true)]
        [string[]]$Message
    )
    $body = ($Message -join ' ') + "`n`nCo-Authored-By: OpenCode <noreply@opencode.ai>"
    git commit -m $body @args
}
Set-Alias -Name cco -Value Invoke-OpenCodeCommit
```

**Usage:**

```powershell
cco "feat(i18n): add new translation"
```

is equivalent to:

```bash
git commit -m "feat(i18n): add new translation

Co-Authored-By: OpenCode <noreply@opencode.ai>"
```

**Why explicit alias over auto-hook:** avoids surprising attribution on commits you make entirely by hand.

### 2. Document the convention in `AGENTS.md`

Add a new section under "Special Instructions":

```markdown
### AI Co-Authoring

When a commit is created with assistance from an AI tool (OpenCode, Claude Code,
Cursor, etc.), the commit message must include a `Co-Authored-By:` trailer naming
the tool. For OpenCode, the canonical trailer is:

    Co-Authored-By: OpenCode <noreply@opencode.ai>

Use the PowerShell alias `cco` (defined in `$PROFILE`) instead of `git commit`
when the change was AI-assisted. Pure-human commits continue to use `git commit`
unchanged — no trailer.
```

### 3. (Optional) Test commit to verify

After this plan is approved, on a fresh branch off `main`:

```powershell
git checkout -b chore/cco-convention-test main
# edit something trivial (e.g., add a blank line to AGENTS.md)
git add AGENTS.md
cco "docs: add blank line to verify cco alias"
git push -u origin chore/cco-convention-test
```

Then verify on GitHub that the commit shows both `Kurt Brauchli` and `OpenCode <noreply@opencode.ai>` as co-authors. If `noreply@opencode.ai` doesn't link to a real GitHub profile (i.e., not a registered user), it will show as plain text — still better than nothing.

---

## Open decisions

| # | Question | Default if unanswered |
|---|----------|-----------------------|
| 1 | Is `noreply@opencode.ai` the canonical OpenCode email? | Use as proposed; easy to change later |
| 2 | Alias name: `cco` vs `ocommit` vs other? | `cco` |
| 3 | Should `AGENTS.md` include the full PowerShell snippet, or just a pointer? | Full snippet, copy-pasteable |
| 4 | Do we add a `.git/hooks/prepare-commit-msg` later for fully-automatic tagging, or stay opt-in forever? | Stay opt-in for now; revisit in 3 months |
| 5 | Should we expose a similar alias for Git Bash on Windows (since some tools invoke that)? | Skip for now; PowerShell covers your workflow |

---

## Files this plan touches (when implemented)

- **Modified:** `AGENTS.md` — add "AI Co-Authoring" section
- **Modified:** `$PROFILE` — add `cco` alias (outside the repo)
- **Created:** a new commit on a fresh branch off `main`, to verify the trailer renders on GitHub

No changes to source code, no impact on PR #38.

---

## Tradeoffs

| Pro | Con |
|-----|-----|
| Explicit, opt-in | Requires you to remember to use `cco` |
| No history rewrite of PR #38 | If `noreply@opencode.ai` isn't a real GitHub user, no clickable profile link |
| Documents convention for future AI agents | One more thing to maintain in `$PROFILE` |
| Easy to disable by removing the alias | |

---

## When to revisit

After this plan is filed, decide:
- ✅ Adopt as proposed → execute steps 1–2
- ❌ Skip entirely → no changes, document decision somewhere
- 🔧 Modify approach → discuss what to change before implementing

---

## Traceability

- Triggered by merge of PR #38 (commit `c54d174` on `main`)
- Branch `chore/blazor-cleanup` deleted locally after merge (recommendation)
- Decision deferred until PR #38 was confirmed merged by the user
