// Which rooms get a lamp, and how far an existing lamp may be stretched to reach
// a prop that just misses its edge.
//
// Why this exists. Measured on 2026-08-27 in CastleWingLDtk: of 51 placed
// ShadowCaster2D components, only 29 sat inside a shadow-casting light's radius.
// The other 22 carry the cost of a caster and draw nothing, because no casting
// light reaches them. They were not scattered either - they clustered into two
// rooms with no casting lamp at all (the kitchen and the armory) plus a line of
// pillars sitting just outside the corridor torches.
//
// Two different fixes, and they are deliberately separate knobs:
//   * `lamps`      - new light sources for rooms that have none.
//   * `widen...`   - a bounded stretch of an EXISTING lamp, for a prop that is
//                    only a metre or so past its edge. Cheaper than a new light
//                    and it keeps the room's existing look.
//
// Positions here are a STARTING POINT, not a decision. Lighting placement is the
// user's creative call (CLAUDE.md 5); Setup/55 never moves a lamp that already
// exists, so dragging one in the editor sticks and re-running will not undo it.
using System;
using UnityEngine;

namespace TimeKiller.Lighting
{
    [CreateAssetMenu(menuName = "TimeKiller/Lighting/Room Lighting Config", fileName = "RoomLightingConfig")]
    public class RoomLightingConfig : ScriptableObject
    {
        /// Path used by Resources.Load, so the setup script and any runtime code
        /// find the same asset without a serialized reference.
        public const string ResourcesPath = "C#/Lighting/Configs/RoomLightingConfig";

        [Serializable]
        public class Lamp
        {
            [Tooltip("Object name in the scene. Setup/55 finds an existing lamp by this name and will NOT move it, so a lamp you have dragged stays where you put it.")]
            public string name = "RoomLamp";

            [Tooltip("Where the lamp is created the FIRST time only. Derived from the centroid of the dark props it is meant to cover.")]
            public Vector2 position;

            [Tooltip("0 = copy the template torch's radius. Set a value only when this lamp needs to differ.")]
            public float outerRadiusOverride = 0f;

            [Tooltip("Torches in this castle breathe. Off gives a steady lamp, which reads as unnatural next to the flickering ones.")]
            public bool flicker = true;

            [Tooltip("Free text, shown in the setup report so the reason for each lamp survives.")]
            public string why = "";
        }

        [Header("Where the dark rooms are")]
        public Lamp[] lamps =
        {
            new Lamp { name = "KitchenLamp_A", position = new Vector2(32.6f, -8.4f),  why = "kitchen_table and kitchen_bench" },
            new Lamp { name = "KitchenLamp_B", position = new Vector2(36.8f, -11.0f), why = "kitchen_crates, too far from A to share" },
            new Lamp { name = "ArmoryLamp_A",  position = new Vector2(43.4f, 2.6f),   why = "armory_dummy, armory_anvil, armory_grindstone" },
            new Lamp { name = "ArmoryLamp_B",  position = new Vector2(48.0f, 5.6f),   why = "armory_chest, in the corner past A" },
        };

        [Header("Copy the look from an existing light")]
        [Tooltip("New lamps clone this light's colour, intensity, blend style and falloff, so they match the art instead of guessing at it. Nothing is hardcoded.")]
        public string templateLightName = "TorchLight";

        [Header("Stretching a lamp to reach a near-miss prop")]
        [Tooltip("Off leaves every existing lamp exactly as tuned. On lets Setup/55 widen a lamp that is nearly reaching a dark prop.")]
        public bool widenNearMissLights = true;

        [Tooltip("How far past a lamp's edge a dark prop may sit and still count as a near miss. Beyond this it needs its own lamp, not a bigger neighbour.")]
        [Range(0f, 5f)] public float nearMissThreshold = 2f;

        [Tooltip("Slack added past the prop so it sits inside the light rather than exactly on its rim.")]
        [Range(0f, 1f)] public float nearMissMargin = 0.3f;

        [Tooltip("Hard ceiling on a widened lamp. Stops one stubborn prop turning a torch into a floodlight and washing the room out.")]
        [Range(4f, 12f)] public float maxWidenedRadius = 7f;

        [Header("Scene layout")]
        [Tooltip("Parent object the new lamps are created under, so they are one tidy group you can disable or delete in one action.")]
        public string parentName = "RoomLights";
    }
}
