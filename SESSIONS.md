# Working in parallel — session roles

Three Claude sessions work on this game at the same time, in **one shared
working tree** and against **one shared Unity editor**. This file says who owns
what, so two sessions never write the same file or fight over the same editor.

Read this at the start of every session, together with [`CLAUDE.md`](CLAUDE.md).
If you were not told which role you are, **ask before touching anything.**

---

## The three lanes

### A — Programmer
Builds features and fixes gameplay code.

**Owns:** runtime `.cs` under `Assets/Resources/C#/` and their
`Configs/*.cs`, **except the visual folders listed under B**; EditMode tests in
`C#/Testing/Editor/`.

**Never touches:** art and audio files, `Tools/*Pipeline`, B's visual code,
scenes without the lock, `git push`.

### B — Artist & visual design
Makes everything the player looks at, **and the code that draws it**.

**Owns:** `Assets/Resources/Assets/**` (sprites, VFX strips, audio, fonts, maps),
`Tools/ArtPipeline`, `Tools/VfxPipeline`, `Tools/CharArt`, `Tools/UIArt`,
`Tools/AudioPipeline`, `Tools/MapPipeline`, all PixelLab work — **and the visual
runtime code**: `C#/Lighting`, `C#/Effects`, `C#/Blood`, `C#/HealthVfx`,
`C#/Environment`, `C#/Camera`, the *look* of `C#/Menu` and the HUD, and the URP
pipeline and volume profiles.

**Why B owns visual code and not only visual files.** The user, 2026-08-27:
*"you will be the design, every light or things which is touch the visual is
your job"*. A shadow's penumbra falloff, a torch's flicker curve and a 9-slice
border are authored in `.cs`, not in a `.png`. Split them from the art and A
owns code whose result A cannot judge, while B holds a judgement B cannot act
on.

**Never touches:** gameplay, AI and objective code; `git push`.

### C — Playtester & release
Proves the game works and is the only session that ships it.

**Owns:** `C#/Testing/` (bot playtester), `C#/Recording/`, `Tools/Playtest`,
the Windows build, `CHANGELOG.md`, `ARCHITECTURE.md`, and **all git**.

**Never touches:** feature code, art.

---

## The four things that actually collide

Folders are the easy part. These four are shared no matter how the folders are
split, and they are where parallel work goes wrong.

### 1. Play mode is exclusive, and it blocks everyone

While any session is in play mode, **nobody else's code can compile**, and any
`TimeKiller/Setup/NN` run in that window **half-completes** while still
reporting success (`CLAUDE.md` §9). Session C spends the most time in play mode
(bot batches, recordings).

**The rule:** C announces "taking play mode" and "play mode free". A and B check
`EditorApplication.isPlaying` **first**, every time, before any editor action.

### 2. Scenes are single-owner

`CastleWingLDtk`, `Catacombs` and `MainMenu` are hand-tuned and protected
(`CLAUDE.md` §5). Two sessions saving the same scene is unrecoverable work loss.

**The rule:** one session runs setup scripts at a time, and says so. A feature
that can install itself at runtime (see `ManiacEscalation`, `EffectPlayer`,
`ManiacNavigator`) should — a feature that needs no scene edit cannot collide.

### 3. `CHANGELOG.md` and `ARCHITECTURE.md` are the real conflict file

Every session wants to write them, and they are the two files most likely to
produce a merge mess.

**The rule:** C owns both. A and B report what changed in chat and let C write
it. If A or B must write, append **only** a new dated block at the top and never
reflow anything below it.

### 4. Every lane commits its own work — only C pushes

The dangerous verb is **push**, not commit. `push` takes the whole working tree,
including the other two lanes' half-finished work (`CLAUDE.md` §7), and it is
the step teammates feel. A commit is local and revertible, and the session that
did the work is the only one that can describe it honestly — a release session
cannot write down the colour it sampled or the glyph count it verified.

**The rule (user, 2026-08-27: "each session commits its own lane"):**

- A and B **commit** their own paths, always with **explicit paths**. Never
  `git commit -a`, `git add .` or `git add -A` — in a shared tree those sweep
  two other lanes' unreviewed work into your commit.
- Prefix the subject with your lane: `visual: ...`, `gameplay: ...`. Then
  `git log --oneline --grep '^visual'` is one role's history, which is the
  point of splitting.
- Check `git status --short` before and after staging. If a path you did not
  touch is staged, unstage it.
- **Only C runs `push`**, and never `git checkout`/`git reset`/`git stash` over
  another lane's files. A and B say "ready to push"; C runs the three traps.

**Two exceptions, both measured on 2026-08-27 — a lane-pure commit that does not
compile is worse than a commit that crosses a lane:**

1. **Shared foundation goes in first, alone.** `SetupGuard.cs` (Core) is called
   by six setup scripts across all three lanes. Under a strict lane rule nobody
   would ever have committed it and every lane would have broken. Prefix it
   `shared:` and commit it on its own, whoever notices.
2. **A feature crossing lanes is committed across lanes.** `ClockEffects.cs`
   (B) subscribes to `ClockHitEvent`, introduced in `ObjectiveEvents.cs` (A).
   Split by lane, one of the two commits will not compile. Commit the pair
   together and name both lanes.

---

## Handoff seams

The architecture already has the seams that let three people work without
waiting on each other. Use them instead of inventing coordination.

- **B → A, art:** B delivers the asset and names it; A points a **config field**
  at it. Content is swappable by editing configs only, never code
  (`CLAUDE.md` §4), so neither session needs the other's files.
- **A → C, verification:** every feature A ships carries a cheap repeatable
  check — a pure static with an EditMode test, an F1 overlay line, or a field in
  `state.jsonl`. C measures it; A never says "play it and look".
- **Feature effects → the feature's lane, not B's.** `C#/Effects/` holds two
  different things. The **framework** is B's: `EffectPlayer`, the recipe format,
  the VFX sprite pipeline, and the *content* of a recipe — timings, colours,
  sprites. A **binding** — the script that turns one feature's events into recipe
  plays — belongs to the lane that owns that feature, because every change to it
  follows a change in that feature's states, not in how effects are drawn.
  Measured 2026-08-27: `ManiacThreatEffects.cs`, its config and the
  `ManiacSpotted` / `ManiacSwing` recipes appeared in `C#/Effects/` from the
  maniac lane while B held the folder. So the maniac lane owns
  `ManiacThreatEffects*`; B owns the recipes those files play and the framework
  underneath. Same shape as the B→A art seam above: one lane delivers the
  content, the other points a config field at it.

- **A ↔ A, features:** systems talk through the Core `EventBus` only. A feature
  that subscribes to another feature's event type cannot be deleted
  independently — put the shared event in `Core/` instead (see
  `WorldProgressEvent`).

---

## Session start checklist

1. Confirm your role (A, B or C).
2. `git status` — is the tree clean? If not, C's push is overdue; say so.
3. Check whether Unity is in play mode before any editor action.
4. Work only inside your lane. If a task needs another lane's files, **stop and
   say which lane it belongs to** rather than reaching across.
