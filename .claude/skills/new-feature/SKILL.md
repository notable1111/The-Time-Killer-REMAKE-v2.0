---
name: new-feature
description: Scaffold a new feature for The Time Killer Remake the heavy-architecture way — feature folder, namespace, config, editor setup script, docs entry. Use when starting any new gameplay/system feature.
---

# New Feature Scaffold

Every feature starts with the same professional skeleton so the codebase stays uniform.

## Steps

1. Confirm the feature name and scope with the user if not already clear (one short question max if truly ambiguous).
2. Create `Assets\Resources\C#\<Feature>\` containing:
   - Scripts in namespace `TimeKiller.<Feature>`, each with a short summary comment at the top.
   - `Configs\` subfolder + a `<Feature>Config` ScriptableObject class (`[CreateAssetMenu]`) holding every tunable value — no magic numbers in logic code.
   - If the feature needs scene objects: an editor script in an `Editor\` subfolder adding a menu item under `TimeKiller/Setup/...` that creates and wires the objects. Never hand-edit scene/prefab YAML.
3. Architecture rules (from project memory):
   - Small single-purpose components; systems communicate via the Core event bus, not direct references.
   - Input separate from character/system logic (future co-op).
   - State machines for multi-state behavior.
4. Add a section to `ARCHITECTURE.md` describing the feature and how it connects.
5. Deliver with the teach-part (what each script does and why) + Unity test steps, per communication rules.
