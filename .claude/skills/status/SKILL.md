---
name: status
description: Project status recap for The Time Killer Remake — what's done, in progress, blocked, and suggested next steps. Use at session start or whenever the user asks "status" / "where are we".
---

# Project Status Recap

Produce a short, scannable status report of The Time Killer Remake.

## Steps

1. Run `git log --oneline -15` and `git status --short` to see recent work and uncommitted changes.
2. Read `CHANGELOG.md` (repo root) if it exists — the newest entries are the freshest "done" list.
3. Check `Assets\Resources\C#\` subfolders to see which feature systems exist.
4. Report in this shape:
   - **Done** — shipped features (from changelog/git)
   - **In progress** — uncommitted work or half-built features
   - **Blocked** — anything waiting on the user (art approvals, testing, decisions)
   - **Suggested next** — 2–3 concrete options, consistent with the project rules in Claude's memory (survival/horror, HD-2D, heavy architecture)

Keep it under ~15 lines. This report replaces the normal session-start recap when invoked.
