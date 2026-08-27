// Every tunable of the maniac's presence — the sound of him, in the world.
//
// Until 2026-07-28 he made NO sound at all: there was not one AudioSource
// anywhere under C#/Maniac. The music told you how much danger you were in, but
// nothing told you WHERE he was, so the only warning you ever got was seeing him.
// That is also why hiding measured as inert in the bot playtests (81.8% vs 81.9%
// death rate): the bot only ever hid AFTER being seen, because being seen was
// the first information the game gave it.
//
// So this is a gameplay system wearing an audio costume. The breathing bed is
// the counterplay: it is 3D and it is deliberately NOT occluded by walls, so a
// player who is listening can place him through stone and decide to hide BEFORE
// he rounds the corner. Muffling it would take that back.
//
// NON-VERBAL ONLY (user ruling 2026-07-28). He never speaks. Breath, throat and
// boots — nothing that needs a language, dates itself, or explains him.
//
// Asset: C#/Maniac/Configs/ManiacVoiceConfig.asset (clips wired by Setup/33).
using UnityEngine;

namespace TimeKiller.Maniac
{
    [CreateAssetMenu(menuName = "TimeKiller/Configs/Maniac Voice", fileName = "ManiacVoiceConfig")]
    public class ManiacVoiceConfig : ScriptableObject
    {
        [Header("The breathing bed — a loop that never stops, only changes")]
        [Tooltip("Looping breath. One clip: the STATE is carried by volume and pitch, not by swapping clips, so there is never an audible seam at the moment he notices you.")]
        public AudioClip breathLoop;
        [Tooltip("Distance (world units) at which he becomes audible at all. This is the player's warning range — the single most important number in this file. Too small and hiding stays reactive; too large and he is everywhere at once.")]
        public float hearingRadius = 11f;
        [Tooltip("Inside this distance the breath is at full volume. Keep it small — the whole point is that volume reads as distance.")]
        public float fullVolumeRadius = 1.5f;

        [Header("Breath intensity by what he knows")]
        [Tooltip("Volume while he has no idea you exist. Low, slow, almost bored — this is the sound you learn to relax around.")]
        [Range(0f, 1f)] public float breathVolumeUnaware = 0.35f;
        [Tooltip("Volume while something has him suspicious. The change from Unaware is the tell that you made a mistake.")]
        [Range(0f, 1f)] public float breathVolumeSuspicious = 0.55f;
        [Tooltip("Volume while he has you. Loud enough to be felt over the chase music.")]
        [Range(0f, 1f)] public float breathVolumeDetected = 0.85f;
        [Tooltip("Playback pitch when calm — slow, deep, unhurried.")]
        [Range(0.5f, 1.5f)] public float breathPitchCalm = 0.82f;
        [Tooltip("Playback pitch when hunting — faster and higher, a body working.")]
        [Range(0.5f, 2f)] public float breathPitchHunting = 1.25f;
        [Tooltip("Seconds for the breath to move between intensities. Slow enough that it reads as a mood changing rather than a switch flipping.")]
        public float breathBlendSeconds = 1.1f;

        [Header("Vocalizations (non-verbal — no words, ever)")]
        [Tooltip("The instant he sees you. This is the sound that should make the player run.")]
        public AudioClip[] spottedGrowls;
        [Tooltip("He had you and lost you: frustration, close by, in the dark. Rewards the escape AND warns you he is still right there.")]
        public AudioClip[] lostYouGrowls;
        [Tooltip("The swing itself.")]
        public AudioClip[] attackRoars;
        [Tooltip("Occasional throat noise while he patrols unaware — the ambient reminder that he exists. Kept rare on purpose: anything you hear on a schedule stops being frightening.")]
        public AudioClip[] idleMutters;
        [Range(0f, 1f)] public float vocalVolume = 0.9f;

        [Header("Vocal timing")]
        [Tooltip("Seconds between spotted growls, so re-acquiring you mid-chase does not restack them.")]
        public float spottedCooldown = 5f;
        [Tooltip("Seconds between frustration growls.")]
        public float lostYouCooldown = 6f;
        [Tooltip("Shortest gap between idle mutters.")]
        public float mutterMinInterval = 14f;
        [Tooltip("Longest gap between idle mutters. The spread is what stops them feeling metronomic.")]
        public float mutterMaxInterval = 34f;

        [Header("Footsteps")]
        [Tooltip("His boots. Heavier and slower than the player's — the contrast is how you tell whose steps you are hearing.")]
        public AudioClip[] footstepClips;
        [Tooltip("World units travelled per step. Driven by DISTANCE, not animation frames, so the cadence stays correct at every speed and does not depend on event frames being authored on his clips.")]
        public float strideMeters = 0.78f;
        [Range(0f, 1f)] public float footstepVolume = 0.7f;
        [Tooltip("How close he must be, on the same 0..1 reach curve his footsteps already use, before the mix leans back for his PRESENCE. 0.45 is roughly half the hearing radius. 0 = the mix leans back the instant he is audible at all; 1 = effectively never.")]
        [Range(0f, 1f)] public float presenceReach = 0.45f;
        [Tooltip("Random pitch spread per step, so a walk down a corridor never sounds looped.")]
        [Range(0f, 0.5f)] public float pitchJitter = 0.08f;

        [Header("Removal")]
        [Tooltip("Kill switch for the whole system without deleting the component — useful for A/B playtests of whether hearing him actually changes how players hide.")]
        public bool enabled = true;
    }
}
