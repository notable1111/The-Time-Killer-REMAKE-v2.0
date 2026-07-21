---
name: art
description: Generate a game art asset for The Time Killer Remake in the locked hand-drawn horror style and save it to the right folder. Use when the user asks for a sprite, background, prop, or concept art.
---

# Art Asset Pipeline

All generated art must look like it belongs to ONE game: hand-drawn / painterly, dark survival-horror mood, angled ¾ HD-2D perspective, PC game fidelity.

## Steps

1. Clarify what's needed if ambiguous: asset type (character sprite / sprite sheet / background / prop / concept), size and intended in-game use.
2. Build the generation prompt from three parts:
   - **Style base (always):** "hand-drawn painterly style, dark muted palette, survival horror atmosphere, consistent game art style"
   - **Asset specifics:** subject, pose/angle (¾ view for world objects), animation frames if a sheet.
   - **Technical:** transparent background for sprites/props; check existing assets in `Assets\Resources\Assets\` first and reference their look for consistency.
3. Generate with the connected image-generation MCP. For character animation frames, consider the Daz Studio route (pose 3D character → render frames) when smooth multi-frame animation matters.
4. Show the result to the user for approval BEFORE integrating. Art is a creative call — the user decides.
5. On approval, save under `Assets\Resources\Assets\<Category>\` with a clear name, and state which import settings to use (Sprite 2D/UI, PPU, filter mode) or set them via editor script.
6. Concept art (not for in-game use) goes to `Assets\Resources\Assets\Concepts\`.
