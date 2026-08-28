// Every tunable of composure — the sanity system.
//
// DESIGN SETTLED WITH THE USER 2026-08-27, three rulings:
//   1. Only TOTAL darkness drains, not dim light. A torch-lit corridor is free;
//      a genuinely black room costs. This makes the torches meaningful as
//      landmarks and gives level design a direct lever.
//   2. There IS a floor. It bottoms out bad-but-survivable, because a game with
//      3 HP, no healing and no weapons must not produce runs that are lost
//      several minutes before they end.
//   3. It resets each run. GameFlow already does a full scene reload precisely
//      so nothing leaks between runs, and there is no save layer to carry it.
//
// WHAT IT DELIBERATELY IS NOT: a bar on the screen. Health is diegetic here by
// design — the screen is the health bar — and a numeric sanity meter would break
// that language. The only thing that shows composure is what your own body does.
//
// AND IT IS NOT RESTORED BY ITEMS, ever. Light and progress restore it. An item
// economy would reintroduce the "resource abundance" failure the design has
// structurally avoided by having no weapons, no healing and 3 HP — which is one
// of the genre's two big traps and currently the one this game is immune to.
using UnityEngine;

namespace TimeKiller.Sanity
{
    [CreateAssetMenu(fileName = "ComposureConfig", menuName = "TimeKiller/Configs/Composure")]
    public class ComposureConfig : ScriptableObject
    {
        public const string ResourcesPath = "C#/Sanity/Configs/ComposureConfig";

        [Tooltip("Master switch. Off = the dial stays at 1 and the game is exactly what it was before this feature existed.\n\nThis is a difficulty change and has not had the user's ear yet.")]
        public bool enabled = true;

        [Header("What counts as dark")]
        [Tooltip("Light level at or below which the player counts as being in TOTAL darkness.\n\nThis is the user's ruling in one number: dim light must be FREE and only genuine black must cost. Set it too high and a torch-lit corridor starts draining, which turns the whole castle into a cost and makes the torches meaningless. Sampled from the URP 2D lights actually in the scene, so it moves with the level rather than with a guess.")]
        [Range(0f, 1f)] public float darkAtOrBelow = 0.12f;

        [Tooltip("Seconds of continuous total darkness before composure is fully spent, ignoring the floor.\n\nNot a per-second rate, because the number people actually reason about is 'how long can I stand in the dark'. 25s is roughly two rooms of the servant passage at a walk.")]
        [Range(2f, 120f)] public float secondsToSpendInDark = 25f;

        [Tooltip("Seconds hiding in a wardrobe before composure is fully spent.\n\nA wardrobe IS darkness, so this is conceptually the same drain — but it is its own number because sitting in a box should be gentler than standing in a black corridor. It exists to stop 'wait him out' being free: hiding measured as INERT in the bot playtests, and turtling costs nothing today.")]
        [Range(5f, 300f)] public float secondsToSpendHiding = 70f;

        [Header("What brings it back")]
        [Tooltip("Seconds in light to recover fully from empty. Deliberately faster than the drain — the dark should be a place you visit and leave, not a debt you spend the run repaying.")]
        [Range(2f, 120f)] public float secondsToRecoverInLight = 16f;

        [Tooltip("Composure returned by fixing a clock. PROGRESS HEALS, which is the Amnesia rule and the reason this needs no pickups: it ties sanity to the win condition instead of to an item economy.")]
        [Range(0f, 1f)] public float clockRestore = 0.35f;

        [Header("What it costs you")]
        [Tooltip("How much louder your footsteps are at ZERO composure. 1 = no effect.\n\nThis is the feature's only mechanical output, and it is aimed at HEARING on purpose: hearing is the sense the player controls. Walk instead of run and you are quiet again, so low composure demands more care rather than punishing someone already doing everything right. Sight would just make him notice a careful player anyway.")]
        [Range(1f, 3f)] public float loudnessAtEmpty = 1.6f;

        [Header("Safety rails")]
        [Tooltip("The floor — composure never falls below this, so the loudness penalty never reaches its full value.\n\nThe user's ruling: bad but survivable. Without a floor a long spell in the dark makes you permanently audible and the run is lost several minutes before it ends, which is exactly the death spiral that no-stamina, no-items and 3 HP were chosen to avoid.")]
        [Range(0f, 0.9f)] public float floor = 0.25f;

        [Tooltip("Seconds between light samples. This is not a per-frame system: the light rig is ~25 lights and 0.2s is far finer than walking speed can outrun.")]
        [Range(0.05f, 1f)] public float sampleInterval = 0.2f;
    }
}
