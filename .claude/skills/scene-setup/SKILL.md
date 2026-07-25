---
name: scene-setup
description: Build or change Unity scene content the safe way — an editor script under TimeKiller/Setup, never hand-edited YAML, and never a destructive re-run over hand-tuned work. Use before running ANY Setup/NN menu item on an existing scene, and when writing a new one.
---

# Scene Setup, Without Destroying Work

Scene content is created by editor scripts, never by hand-editing `.unity` or
`.prefab` YAML. But the greater danger runs the other way: re-running a build
script over a scene the user has since hand-fixed destroys work that cannot be
recovered, and the tool call reports success while doing it.

## Protected — do not re-run blind

- **`Setup/9 - Build Castle Hall`** and **`Setup/11 - Hall Realism`** rebuild the
  hall. The hall has since been fixed by hand. Do not run either on the existing
  hall scene without explicit, in-this-session permission from the user.
- **`CastleHall/Colliders`** are hand-tuned side/band colliders. No setup script
  may regenerate them. Their tuning is durable only because of the snapshot at
  `Assets/Resources/C#/Environment/Configs/HallColliderSnapshot.json`.

## Steps — before running any Setup/NN on a scene that already exists

1. **Ask what state the scene is in.** If the scene may contain manual fixes,
   confirm with the user before running anything destructive. "It's a setup
   script, it's idempotent" is an assumption, not a check.
2. **Snapshot first.** Run `TimeKiller/Setup/17 - Hall Colliders: Export
   Snapshot` before anything that could touch the hall. The snapshot is
   committed to git, so it survives even a full scene loss.
3. **Run the script.**
4. **Verify** with the `/verify` skill (compile + `SmokeCheck.Report()`).
5. **If something was clobbered**, the recovery tools are
   `Setup/17 - Hall Colliders: Re-apply Snapshot` (restores the exact hand
   tuning) and `Setup/16 - Repaint Floors (recovery)`.

## Steps — writing a NEW setup script

1. Menu path `TimeKiller/Setup/NN - <clear description>`, taking the next free
   number (34 is currently the highest: the batch escape hatch).
2. **Idempotent and additive.** Find-or-create; never blind-delete a hierarchy.
   The script must be safe to run twice.
3. Never touch protected objects. If the script needs the hall colliders, call
   `HallColliderGuard.ApplyTo(holder)` rather than generating new ones — this is
   how `Setup/18` gives the LDtk scene the identical hand tuning.
4. Use `Undo.RegisterFullObjectHierarchyUndo` before mutating existing objects,
   and `EditorSceneManager.MarkSceneDirty` after.
5. Editor scripts live in an `Editor/` subfolder of their feature.
6. Log what was created, with a `[TimeKiller Setup]` prefix.

## The rule

A destructive scene operation and a successful tool call look identical in the
transcript. Confirm before, snapshot before, verify after.
