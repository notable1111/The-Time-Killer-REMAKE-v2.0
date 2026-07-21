---
name: push
description: Safe release ritual for The Time Killer Remake — update docs, sanity-check the diff, commit, push to main. Use ONLY when the user says "push" (their word for "tested and approved").
---

# Safe Push to Main

The user saying "push" means they tested the current work in the Unity Editor and it works. Main must stay always-working for teammates.

## Steps

1. Run `git status --short` and `git diff --stat` — review what is about to ship. If anything unexpected or unrelated is in the diff, ask before continuing.
2. Update `CHANGELOG.md` (repo root, newest entry on top): date + short list of what changed. Create the file if missing.
3. If the change added/altered systems or how they connect, update `ARCHITECTURE.md` accordingly.
4. Stage everything relevant, commit with a clear message describing the feature/fix (English, imperative mood).
5. `git push origin main`.
6. Report back: what was pushed, the commit hash, and what the changelog now says.

Never push without step 1's review. Never push work the user hasn't confirmed as working.
