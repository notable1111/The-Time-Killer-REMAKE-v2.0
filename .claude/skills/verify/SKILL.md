---
name: verify
description: Prove a Unity change actually works before reporting it done — compile, console, smoke check. Use after ANY C# edit or editor-script run, and before telling the user something is ready.
---

# Verify Before Claiming

"It compiles" and "it works" are different claims. This skill exists because the
most common way Claude wastes the user's time is saying "done" on a change that
had never been through the Unity compiler at all — the user finds it by hitting
Play and getting a red console.

Never report a change as ready without walking these steps and stating which
level of verification was actually reached.

## Steps

1. **Compile.** `refresh_unity` to trigger a domain reload, then `read_console`
   filtered to errors/exceptions. Compile errors mean the work is NOT done —
   fix and repeat. Do not report progress in between.

2. **Read the console honestly.** Warnings are not automatically fine. A new
   warning that names a script just touched is a finding, not noise.

3. **Scene integrity (any change that touches scene content).** Call
   `TimeKiller.EditorTools.SmokeCheck.Report()` via `execute_code`. It is
   read-only and returns JSON: `{"ok":bool,"problems":[...]}`. A non-empty
   `problems` array means not done. It checks the player rig (input, health,
   hiding + config), the maniac, >=7 hiding spots with sprites, the Furniture
   root, null SpriteRenderers anywhere, and the camera rig.

4. **Runtime behaviour is a separate claim.** A clean compile and a clean smoke
   check say nothing about whether the maniac chases correctly or the clock
   repairs. If the change affects play-mode behaviour, either exercise it (play
   mode, or a bot batch via `TimeKiller/Setup/33`) or say plainly that it is
   compile-verified and runtime-unverified. Never let the user assume the
   stronger claim.

5. **Report what was verified, in those words.** For example: "compiles clean,
   smoke check ok, not yet run in play mode" — not "done".

## The rule

If asked whether something works, the answer must be traceable to something
actually observed in this session. If it isn't, say so instead of implying it.
