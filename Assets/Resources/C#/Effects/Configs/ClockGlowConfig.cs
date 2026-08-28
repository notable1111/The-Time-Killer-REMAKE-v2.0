// The clock's own light — how a repair reads from across a dark room.
//
// WHY THIS REPLACED A PUFF. The clock beats used to be two sprite bursts: a gold
// spark on an earned press, a flourish when it was fixed. The user, 2026-08-28:
// "the effect for the clocks they are not bad but looks cheap you should work on
// it." Same diagnosis as the threat sprites — a one-shot decoration that plays
// and vanishes, leaving the world exactly as it found it.
//
// THE REFERENCE. Dead by Daylight's generators, which are the same beat as our
// clocks (the objective you repair while hunted), give feedback three ways and
// none of them is a burst: the PISTONS move faster as charge builds, so progress
// is readable from a distance; on completion the generator's LIGHTS TURN ON and
// illuminate the area around it; a regressing generator emits continuous sparks.
// All of it lives ON THE OBJECT, persists, and changes the room.
//
// So the clock becomes a lamp. It glows faintly as you work, brighter witheach 
// earned press, and when it is fixed it stays lit and lights the floor around it.
// That is diegetic, continuous, and readable at a glance — and it quietly fixes a
// real gameplay problem, because until now nothing told you which clocks were
// already done once you had walked away from them.
using UnityEngine;

namespace TimeKiller.Effects
{
    [CreateAssetMenu(menuName = "TimeKiller/Effects/Clock Glow Config", fileName = "ClockGlowConfig")]
    public class ClockGlowConfig : ScriptableObject
    {
        public const string ResourcesPath = "C#/Effects/Configs/ClockGlowConfig";

        [Header("The light itself")]
        [Tooltip("Pale gold — the clock's brass waking up. Deliberately NOT the torches' (1.0, 0.62, 0.28): a repaired clock must not be mistakable for a wall torch at a distance, or the whole point of marking it is lost.")]
        public Color glowColour = new Color(1f, 0.86f, 0.55f);

        [Tooltip("Radius once the clock is FIXED. Big enough to light the floor around it so it reads from across the room, which is what makes it a marker rather than a decoration.")]
        [Range(0.5f, 8f)] public float fixedRadius = 3.4f;

        [Tooltip("Intensity once fixed. This clock is now a light source in the level.")]
        [Range(0f, 3f)] public float fixedIntensity = 1.15f;

        [Header("While you are still working on it")]
        [Tooltip("Radius at zero progress. Small: an unrepaired clock should hint, not announce.")]
        [Range(0f, 4f)] public float workingRadiusMin = 0.7f;

        [Tooltip("Intensity at zero progress.")]
        [Range(0f, 2f)] public float workingIntensityMin = 0.18f;

        [Tooltip("How much of the fixed brightness a clock reaches at 99% progress. Under 1 so that FINISHING it is still a visible jump rather than the last few percent of a ramp.")]
        [Range(0f, 1f)] public float workingCeiling = 0.55f;

        [Header("The earned press")]
        [Tooltip("Extra intensity punched in on each successful press, on top of the progress level. This is the beat the player feels for getting the timing right.")]
        [Range(0f, 2f)] public float pressPunch = 0.55f;

        [Tooltip("Seconds for that punch to swell in. Small but not zero — instant is a blink.")]
        [Range(0f, 0.3f)] public float pressAttack = 0.045f;

        [Tooltip("Seconds for it to fall away. Short: it is a tick of progress, not an event.")]
        [Range(0.05f, 2f)] public float pressRelease = 0.34f;

        [Header("Alive, not static")]
        [Tooltip("How much a FIXED clock breathes, 0..1 of its intensity. A dead-steady light reads as a UI marker; a breathing one reads as a thing that is running. 0 switches it off.")]
        [Range(0f, 0.5f)] public float breathAmount = 0.09f;

        [Tooltip("Breaths per second for a fixed clock. Slow — this is a pendulum, not a pulse.")]
        [Range(0.05f, 3f)] public float breathHz = 0.45f;

        [Header("Install")]
        [Tooltip("Off leaves the game exactly as it was. Deleting this asset uninstalls the feature.")]
        public bool enabled = true;
    }
}
