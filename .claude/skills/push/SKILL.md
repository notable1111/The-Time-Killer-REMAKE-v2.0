---
name: push
description: Safe release ritual for The Time Killer Remake — check the teammate-breaking traps, update docs, review the staged diff, commit, push to main. Use ONLY when the user says "push" (their word for "tested and approved").
---

# Safe Push to Main

The user saying "push" means they tested the current work in the Unity Editor and
it works. Main must stay always-working for teammates — the repo is shared, so a
bad push costs other people their day, not just this session.

## Steps

1. **Survey.** `git status --short` and `git diff --stat`. Anything unexpected or
   unrelated in the diff: ask before continuing.

2. **Run the three teammate traps.** These are the things that break other
   people and that a generic diff review waves straight through:

   - **`.unity` scene files — read the STAGED diff, not the summary.** A URP
     template import silently clobbers `SampleScene`. It shows up as an
     ordinary scene change. `git diff --cached -- '*.unity'` and actually look
     at what changed before shipping any scene.
   - **`Packages/manifest.json` must NOT appear.** It is `skip-worktree`'d on
     purpose because unity-mcp is a local-only package — git should never show
     it dirty. If it appears in the diff, something un-skipped it. Stop and ask;
     do not push it.
   - **Binaries must be in LFS.** `.gitattributes` routes `*.png *.wav *.fbx
     *.blend *.mp4 *.zip` and friends through LFS. Confirm new binaries went to
     LFS (`git lfs status`) rather than being committed raw — a raw-committed
     binary bloats the repo permanently and cannot be cleanly undone.

3. **Update `CHANGELOG.md`** (repo root, newest entry on top): date + short list
   of what changed. Create the file if missing.

4. **Update `ARCHITECTURE.md`** if the change added or altered systems, or how
   they connect.

5. **Stage and commit** with a clear message describing the feature/fix
   (English, imperative mood).

6. **`git push origin main`.**

7. **Report back**: what was pushed, the commit hash, and what the changelog now
   says.

## The rules

- Never push without step 1 and step 2.
- Never push work the user has not confirmed as working.
- `*.unity`, `*.prefab`, `*.asset` etc. are `-text merge=unityyamlmerge` in
  `.gitattributes` — never "fix" line endings on them, and never resolve a
  scene conflict by hand-editing YAML. See `TEAM_SETUP.md`.
