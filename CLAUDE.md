# Working rules for The Time Killer Remake

Instructions for anyone — human or AI — working in this repo. Claude Code reads
this file automatically at the start of every session.

These rules were accumulated the expensive way. Almost every one exists because
breaking it cost real hours. Where that's true, the cost is written down, because
a rule with a reason attached survives and a bare rule gets rationalised away.

**Related docs:** [`ARCHITECTURE.md`](ARCHITECTURE.md) — how the systems connect
and why. [`CHANGELOG.md`](CHANGELOG.md) — what changed, newest on top, updated
every push. [`TEAM_SETUP.md`](TEAM_SETUP.md) — per-machine setup for teammates.
[`SESSIONS.md`](SESSIONS.md) — **read this if more than one session is working:**
who owns which files, and the four resources they share.

---

## 1. The first principle

**Research first, then act. Always.** Measure the thing you are about to change,
*in the place it actually lives*, before changing it. Check the API before
calling it, look at the asset before judging it, render in the real scene before
tuning for it.

This is also the first entry in `ARCHITECTURE.md` → Principles, with the four
cases that earned it. The short version:

- Blood VFX tuned against an isolated neutral backdrop and *darkened* for
  "palette match" → **0.26% screen coverage, gone in 0.75s** on the real floor.
  Invisible in play. A measurement taken in the wrong context is still a guess.
- Droplets drawn without once placing them beside the wardrobe → **saturation
  0.93 / value 165** against game art measuring **0.07 / 52**.
- `TextureSheetAnimation.startFrame` set in frames because the ScriptReference
  example shows a 0–7 slider. Unity 6 clamps it to **0–1**; that example is
  version-stale.

**Corollary — for anything the player FEELS, research the reference before you
design.** (Added 2026-08-28, the expensive way.) Three threat effects were built
from geometry and screen-coverage numbers, measured at 1.21% and 2.61% against a
readability bar, and shipped. The user's verdict: *"looks like cheap, not feeling
dangerous, not feels like the horror game effect... before adding something you
should always check other best horror games and then decide."*

The measurement was real and answered the wrong question: it proved the effects
were VISIBLE, never that they were FRIGHTENING. Coverage is a legibility metric,
not a mood one, and no amount of it catches a crescent slash being an
action-game idiom.

What the references actually say, and they agree: horror communicates threat by
changing the player's VISION — obscuration, distortion, mediation — not by
drawing decorations into the world. Dead by Daylight, whose loop this game
copies, makes threat feedback **continuous, proximity-scaled, diegetic and
directional**: the Red Stain is emitted by the killer and leaks around corners
to betray where he is; the Terror Radius scales with distance and its visual form
is a heart beating in the survivor's own chest.

So before building anything a player feels — an effect, a sound, a camera move —
name the game that already does it well and say what it does. A number cannot
tell you that a shape belongs to the wrong genre.

**Corollary — if you would eyeball the same question twice, build the
measurement instead.** `TimeKiller/Verify/VFX Visibility` exists because "does
this read?" is a number, not an opinion, and a number is repeatable by the next
person. Prefer adding a `TimeKiller/Verify/...` probe over repeating a
judgement call.

## 2. Never guess — label uncertainty

*"Never guess, say which is 100 percent — we are paying attention to the
quality, not fastness."*

The failure mode is not being wrong. It is stating a belief in the same voice as
a measurement, because the reader cannot tell them apart.

- **Verify before delivering**, not after the user finds the problem.
- **Label every unverified claim in the sentence itself**: "I measured X" vs
  "I expect X — not verified". Never let the two share a voice.
- Never state prices, third-party product behaviour, or market claims as fact
  without checking. Say "I don't know" instead.
- Distinguish **"compiles"** / **"smoke check passes"** / **"runtime-verified"**.
  They are different claims.
- **Speed is explicitly not the priority.** Another tool call to be certain is
  always the right trade here.

## 3. Communication

- **English**, always — code, comments, commits, and conversation. This includes
  the end-of-response options block, even when a global or personal instruction
  file writes it in another language: in this repo it is English. *(Re-stated
  2026-08-27, after the options block came back in Uzbek.)*
- **Session start:** a short status recap — done / in progress / blocked — then
  pick the task together.
- **Interview before ANY new feature or improvement.** Ask design questions
  first. Every option must carry a concrete **example** and state **what it
  fixes**. No building on assumptions.
- **Unclear or short prompts** → ask clarifying questions until the task is
  clear, then work. Don't guess.
- **Deliveries** always include the *teach part* (what each script does and why —
  the user is learning Unity/C#) and *how to test it in Unity*.
- **Disagreement:** state the concern and an alternative **once**, then follow
  the user's decision without arguing.
- **"Finished" means FROZEN.** When the user says a feature is finished —
  "heartbeat finished", "this is done" — stop touching it. Do not retune it, do
  not "improve" it, do not adjust its numbers because a measurement looks off.
  A metric that disagrees with a finished feature is a **report**, not a mandate:
  say what it says and let him decide.
  Objective defects inside a frozen feature (compile errors, null refs, crashes,
  clipping) may still be fixed. Anything a player would **hear or feel** may not.
  *(Added 2026-08-02, the expensive way. The fear system was built to a detailed
  spec and verified against its own target table — then retuned unprompted from a
  SINGLE bot session: the whole awareness ladder dropped, `detectedMultiplier`
  1.75→1.25, and every heartbeat asset lost 3 dB. The justification was a number
  that had been flagged one message earlier as partly an artifact of our own
  stage-labelling code. Working feel was changed to satisfy a metric already known
  to be unreliable, at n=1.)*
- **Answer it yourself first; the user is the LAST resort, not the first.**
  *(User ruling 2026-08-27.)* When a check "touches him" — needs playing, needs
  an ear, needs eyes on the screen — the order is:
  1. **Work it out yourself.** Compile it, test it, run the probe, run the bot,
     read the numbers. Most questions that feel like "he has to play this" are
     actually a measurement nobody has taken yet.
  2. **Then just tell him what you did.** He has said plainly this is faster for
     him than loading the game and hunting for what changed. A clear written
     account of the change is the deliverable, not an invitation to go looking.
  3. **Only if it is genuinely impossible without playing, ask.** "Does it feel
     right" is a real example: no probe answers it.
- **A question for the user must be checkable and carry its expected answer.**
  Name the ONE thing to check, and say what it should look like if it is right,
  in plain language. *"In the dark corridor by the armory, walk for ten seconds:
  he should start noticing you from further away than usual. Does he?"* — not
  *"please judge whether composure feels balanced"*. A question he cannot act on
  without re-deriving the context is the same as no question.
- **Long tasks:** post short progress updates at milestones, not silence.
- **After work deliveries** — anything built, fixed, verified or pushed — end with
  a **"What I did"** recap, then a "What's next?" of 2–3 concrete options and a
  progress block with a completion %. **A plain question gets a plain answer**:
  no recap, no options block, no percentage. (Settled 2026-07-28. It used to
  apply to every response, and on a one-line factual answer the ceremony
  outweighed the answer.)
- **THE REPORT FORMAT — every session, every result, no exceptions.**
  *(User ruling 2026-08-28: "i will not repeat it again remember it and let other
  sessions using this method always." He had just read a long delivery and said
  "so i still dont get what you did".)* End every piece of work with exactly
  these five lines, short, in plain language, in this order:

  ```
  WHAT IT WAS    — how it behaved before. One line.
  WHAT I DID     — the change. One line.
  WHAT HAPPENED  — the result, with the number if there is one. One line.
  CHECK THIS     — ONE thing for him to do, concrete enough to act on blind.
  SHOULD BE      — what he will see if it worked. The expected answer.
  ```

  The failure this replaces: reports that narrate the *process* — what broke, who
  held which lock, which commit went where — and leave the reader unable to say
  what changed in the game. He does not read the repo; if the five lines do not
  say it, it was not said. Detail may follow underneath, but never instead.

  If a delivery covers several things, five lines EACH, not one merged blob.
  If there is genuinely nothing for him to check, say so on the CHECK line
  rather than inventing a task.
- **Unfinished work is reminded unconditionally.** If something was left at 60%
  and attention moved elsewhere, say so with the short to-do until it reaches
  100% — whatever kind of response it is. This is the part that stops work
  quietly going missing, so it does not get the exemption above.

## 4. Code and architecture

- **Style — "mixed":** clean idiomatic C#, comments only on the tricky parts, a
  short summary comment at the top of every script explaining *why*.
- **Heavy architecture:** ScriptableObject configs, interfaces, state machines,
  event-driven communication. Namespace per feature (`TimeKiller.Player`).
- **Every tunable number lives in a ScriptableObject config** in
  `C#/<Feature>/Configs/`. Never hardcode a tunable.
- **Event-driven:** systems talk through the Core `EventBus`, never direct
  references. A system should compile even if the systems it talks about don't
  exist yet.
- **Input ≠ logic** — a second player (planned co-op) or an AI must be able to
  drive the same character code. No player singletons.
- **File layout:** scripts in `Assets/Resources/C#/<Feature>/`, configs in
  `C#/<Feature>/Configs/`, editor scripts in `C#/<Feature>/Editor/`.
- **Work only inside `Assets/Resources/`** unless leaving it is genuinely
  necessary (Scenes, ProjectSettings) — and say so when you do.

### Swappable content, removable features

Treated by the user as **"never forget — important"**:

- Any content (character, sounds, sprites) must be **replaceable by editing
  configs only**, never code. Before replacing anything, **ask what the
  replacement needs** ("idle + run × 8 directions, what frame counts?") and do
  **not** switch until the full asset set is ready — keep the old one working.
- Any feature must be **cleanly removable** from its object without breaking the
  others. Concretely: separate components, EventBus-only communication, content
  referenced through configs, no hard cross-feature references.
- **No dead config fields.** When code stops reading a field, delete the field.
  A config that outlives its reader is a trap for the next person.

### No placeholder art

Never **ship** grey-box or stand-in visuals. AI-generate the **real** art so
features arrive with actual styled assets. See §8.

A throwaway fixture used to prove a pipeline works is fine — and often the right
move, since it separates "does the plumbing work" from "is the art good". Delete
it in the same session. Nothing provisional stays in `Assets/`.

## 5. Scenes and hand-tuned work — the protected zone

**This is the rule most likely to destroy work that cannot be recovered.**

- **Never hand-edit scene or prefab YAML.** Build scene content with an editor
  script under the `TimeKiller/Setup/NN` menu.
- **Never hand-edit `.meta` files.** A regex over texture metas corrupted sprite
  fileIDs and every floor Tile lost its sprite — an hour of recovery. Change
  import settings through the `TextureImporter`/`AssetImporter` API only.
- **Setup scripts must be idempotent and non-destructive.** Find-or-create every
  object and re-wire in place; never destroy and rebuild. `Setup/22` and
  `Setup/25` used to rebuild their whole rig on every run and had been silently
  deleting a `DamageSfx` object nobody put back.
- **The user hand-places things.** Colliders in CastleHall, props and wardrobes
  dragged in the editor. Placement is **his creative call** — script positions
  look random to him. Any setup script that creates draggable objects must
  **preserve existing instances**. If scene objects look "wrongly placed", **ask
  before fixing** — it is probably his tuning.
- **Before running ANY `Setup/NN` on a scene that already has content, read the
  script and confirm it preserves what is there.** Don't trust a menu name.
  `Setup/9` and `Setup/11` rebuild the CastleHall colliders and will wipe the
  user's tuning — never run them on the existing hall without explicit approval.
  `Setup/22`, `Setup/25` and `Setup/38` have since been made preserving, but that
  is a property of their current code, not a guarantee about the next script
  someone writes.
- **Clean up after yourself.** Temporary objects get
  `HideFlags.HideAndDontSave`; check the scene's `isDirty` before and after, and
  never save a scene you did not intend to modify.
- **Check which scene is active before saving.** Play mode boots this project
  into `MainMenu`, so the active scene after exiting play is often not the one
  you were working in.

## 6. Verification ritual — before saying anything is done

Run after **any** C# edit or editor-script run:

1. `refresh_unity` with `compile: request`, then confirm
   `EditorApplication.isCompiling == false` **and**
   `EditorUtility.scriptCompilationFailed == false`.
2. `TimeKiller.EditorTools.SmokeCheck.Report()` → expect `"ok": true`.
3. **EditMode tests** (Window ▸ General ▸ Test Runner, or `run_tests`). They live
   in `C#/Testing/Editor/` and currently cover the maniac's scoring and
   perception. Expect all of them to pass — a pre-existing failure is not a
   reason to add another.
4. For anything visual or audible, **verify in the real scene**, at the real
   camera, against the real background (§1).

## 7. Git and the team

**The repo is shared. A bad push costs other people their day.**

- **Always commit and push to `main`** — no feature-branch/PR flow.
- **Push only when the user literally says "push".** That word means "I tested
  it in the Editor and it works". "works" or "good" are **not** push signals.
- **"push" authorises the WHOLE working tree** — including work from other
  sessions the user has not personally reviewed. He has explicitly taken that
  responsibility (settled 2026-07-28), so don't stop and ask. You still owe him
  two things every time: run the three traps below, and **state plainly what is
  in the diff that he may not be expecting**, so accepting the risk is a choice
  rather than a surprise.
- **Do not start a feature until the user explicitly says start.** Answering an
  interview question by choosing an option counts as starting *that* option — it
  does not authorise the next feature over the horizon.
- **Three traps to check before every push:**
  1. **Read the staged `.unity` diff, not the summary.** The URP template import
     once overwrote `SampleScene` (114 GameObjects → 3) and it looked like an
     ordinary scene change.
  2. **`Packages/manifest.json` must never appear.** It is `skip-worktree`'d
     because `com.coplaydev.unity-mcp` is local-only. If it shows up, something
     un-skipped it — stop and ask. **`Packages/packages-lock.json` is the same
     trap wearing a different name** and is *not* skipped: on 2026-08-27 it was
     dirty with `unity-mcp` v10.1.0 → v10.1.2, i.e. it was recording exactly the
     local-only dependency the skip exists to hide. Committing it hands teammates
     a package they do not have. Check it by name before every push.
  3. **Binaries must go through LFS.** `.gitattributes` routes `*.png *.wav
     *.fbx` and friends. Confirm with `git lfs status`; a raw-committed binary
     bloats the repo permanently.
- `*.unity`, `*.prefab`, `*.asset` are `-text merge=unityyamlmerge`. Never "fix"
  their line endings, never resolve a scene conflict by hand-editing YAML.
- **Update `CHANGELOG.md` and `ARCHITECTURE.md` as part of the work**, not as an
  afterthought.
- **Split large pushes into logical commits per feature** so a teammate can
  bisect or revert one thing without losing the rest.

### Three sessions, one working tree (added 2026-08-27)

**The rules live in [`SESSIONS.md`](SESSIONS.md)** — lane ownership, the four
shared resources, and the commit/push split. Read it at the start of every
session. Do not restate its rules here; two rulebooks is how they drift apart.

Only the measured facts belong in this file, because they change what the rules
have to defend against. Measured 2026-08-27: the three sessions share **one
working tree**, **one branch** (`main`) and **one Unity Editor**. The
`.claude/worktrees/*` entries are stale July branches and are **not** in use. So
`git status` shows you every other lane's work, `git commit -a` would commit it,
and two sessions driving the Editor overwrite each other.

The live Editor claim is [`SESSION_LOCK.md`](SESSION_LOCK.md): take it before
saving a scene or running any `TimeKiller/Setup/NN`, release it in the same
session.

## 8. Tools

- **PixelLab** (MCP) — generates concept art *and* real game assets.
  **⚠️ THE SUBSCRIPTION EXPIRED 2026-08-25.** Measured that day:
  `generations_remaining: 0`, all 2000 frozen, $0.94 credits left. Every
  generation tool fails until it is renewed, which only the user can do. This
  file previously promised "art is effectively free — iterate rather than
  agonise", and planning around a budget that no longer exists is how a session
  gets three quarters into a task before finding out. Call `get_balance` FIRST
  rather than quoting any figure, this one included.
  Check `get_character` / `get_object` **before** regenerating something you may
  already own.
- **Procedural VFX generators** in `Tools/VfxPipeline/` are the right tool when
  an effect is geometry and light rather than a picture — and not merely a
  fallback while PixelLab is down. `gen_light_shaft.py` states the test: "a shaft
  is a gradient, not a drawing". Generating buys exact frame counts, alpha that
  follows luminance (no dark interior to key out), and a palette sampled from the
  game's own art. `gen_threat_vfx.py` is the worked example, including the three
  shapes it rejected and why.
- **Unity MCP** — Claude drives the Editor directly (setup scripts, tests,
  offscreen renders).
- **Blender** for 3D environments, **Daz** for character sprite sheets.
- **Project skills** in `.claude/skills/`: `/status` `/push` `/new-feature`
  `/art` `/debug` `/scene-setup` `/verify`.
- Proactively recommend a fitting tool rather than doing it the hard way.

### Blanket permission

Standing permission for all technical project work — creating and editing files,
installing Unity packages, changing project settings, working outside
`Assets/Resources` when a feature needs it. **Don't ask "may I"** for technical
work; do it and explain. The "check key decisions" rule is about **design**
choices that affect other features, not permission to act.

## 9. Known traps

### Unity MCP

- **`execute_code` succeeding does NOT mean the project compiles.** It compiles
  your snippet against the last successfully built assembly, so reflection finds
  your new type happily while Assembly-CSharp is red. Cost ~6 wasted round trips.
- **`read_console` silently drops every info-level `Debug.Log`.** This is the real
  shape of the old "returns 0 entries even when Unity has clearly logged" trap, and
  it is **still present in MCP for Unity 10.1.2** — measured 2026-08-27 by logging
  one line of each level with a unique marker:

  | emitted | in `Editor.log` | returned by `read_console` |
  |---|---|---|
  | `Debug.Log` | yes | **no** |
  | `Debug.LogWarning` | yes | yes |
  | `Debug.LogError` | yes | yes |

  `types: ["log"]` returns **0 entries for the whole console buffer**, not just for
  the probe, and `types: ["all"]` does not bring them back either — so it is a type
  filter, not a timing or flush problem. The failure is nasty because `Debug.Log`
  is the natural thing to reach for when checking "did my code run?", and its
  silence reads as "the code never ran" or "the console is broken".

  **Do this instead:** probe with `Debug.LogWarning`, or read
  `%LOCALAPPDATA%\Unity\Editor\Editor.log` with `grep -a` (it holds all three
  levels and the call stack; it is not valid UTF-8 throughout, and it rotates, so
  a missing line is not proof the event never happened). Never read silence as
  success — confirm via `execute_code` return values plus explicit `isCompiling` /
  `scriptCompilationFailed` checks.
- **Check `EditorApplication.isPlaying` FIRST.** In play mode scripts don't
  compile and `execute_menu_item` on a setup script **half-completes** — it may
  have done only part of its job while reporting success. The MCP's own play
  flag can also desync from Unity's real state; trust `isPlaying`.
- **Entering play mode reloads the domain** and drops subscriptions and statics.
  For long-running probes, write a **report file** and read it with Bash — it's
  the only durable channel.

### PixelLab

- **`size` is silently ignored when a reference image is passed** — output
  follows the reference's dimensions. Downscale the reference instead.
- **Inline base64 truncates in transit.** Keep PNGs small (crop to the bounding
  box, `optimize=True`, quantise the palette); ~1.3KB works reliably.
- **Identity transfer via reference works very well** — re-render an approved
  design from its own sprite rather than re-rolling the text prompt.

### Performance profiling

- **Reflection inflates micro-benchmarks ~20×** (11.69ms → 0.58ms for the same
  method). Bind with `Delegate.CreateDelegate` before timing anything.
- **`execute_code` compiles on the main thread and forges a hitch** — one run
  reported a 1068ms frame that was purely the CodeDom compile. Discard the first
  ~60–90 samples.
- **Sample on wall-clock duration (60s+), not frame count.** 900 frames is 3.7
  seconds at 240fps and will report a stuttering game as perfectly healthy.
- **`Time.unscaledDeltaTime` cannot separate game code from editor/OS stalls.**
  Get a per-subsystem Profiler breakdown before blaming any game system.

## 10. Game design constraints

Full design context is in `ARCHITECTURE.md` and the design memory. The
constraints that affect *code* decisions:

- Survival horror, **hide & run** (Outlast-like), little to no combat.
- Angled ¾ 2.5D (HD-2D), hand-drawn sprites, dark painterly mood, PC only,
  WASD + Shift.
- **No stamina system** (cancelled by team decision — don't reintroduce it).
  **Health = 3 points.** Sanity/fear is planned but **not designed yet** — don't
  build it before the design interview.
- **Co-op is planned** — never write player singletons, keep input separable.
- **World scale contract: 32 pixels per world unit, ~29px characters, feet 17px
  below the sprite pivot.** Match all three or new art looks pasted in, or
  out-scales the maniac.
- **The map is a work in progress.** Don't treat the current layout as final, and
  don't assume a room's absence is a decision. Check `CHANGELOG.md` for where it
  actually got to rather than trusting this line.
