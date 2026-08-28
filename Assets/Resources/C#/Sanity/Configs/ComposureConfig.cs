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

        // ⚠️ BOTH NUMBERS BELOW WERE SET BY MEASUREMENT, NOT BY TASTE, and the
        // measurement is the reason they are not what they look like they should be.
        // Sampled 3685 points across CastleWing on 2026-08-27:
        //
        //   the map has a GLOBAL light - the MINIMUM level anywhere is 0.32, so
        //   there is no total darkness in this castle at all;
        //   CORRECTED 2026-08-28: that 77.7% counted WALLS AND VOID. Re-measured
        //   over WALKABLE ground only, using the maniac's own walkability grid:
        //   3610 samples, median light 0.32, and 55.1% of walkable ground is dark
        //   at the 0.40 threshold. Still over half the castle, but the inflated
        //   figure is what pushed the drain to 75s, which made a 10-second stay in
        //   the dark cost 0.13 - invisible on a debug line and unfeelable in play.
        //   The user's verdict was 'sanity is not working'. It was working; it was
        //   imperceptible, which for a player is the same thing.
        //
        // So an "only total darkness" threshold under 0.32 makes this feature
        // COMPLETELY INERT, which is the installed-but-does-nothing trap this
        // project hit three times in one day. 0.40 is the only honest reading of
        // the ruling on this map: it means NO TORCH REACHES YOU.
        //
        // And because 77.7% of the map is out of torch reach, the drain had to
        // slow down by 3x or the player would sit at the floor permanently and
        // this would stop being a mechanic and become a flat difficulty change.
        [Header("What counts as dark")]
        [Tooltip("Light level at or below which the player counts as being in darkness. MEASURED, not chosen: CastleWing has a global light so its minimum level anywhere is 0.32, and 0.40 is the value that means exactly NO TORCH REACHES YOU. Anything below 0.32 makes this feature inert. See the block comment above for the full sample.")]
        [Range(0f, 1f)] public float darkAtOrBelow = 0.40f;

        [Tooltip("Seconds of continuous darkness before composure is fully spent, ignoring the floor. Not a per-second rate, because the number people reason about is how long they can stand in the dark. 75s because 77.7% of the map is out of torch reach: at 25s the player would sit at the floor permanently and this would be a flat difficulty change rather than a mechanic.")]
        [Range(2f, 300f)] public float secondsToSpendInDark = 40f;

        [Tooltip("Seconds hiding in a wardrobe before composure is fully spent.\n\nA wardrobe IS darkness, so this is conceptually the same drain - but it is its own number because sitting in a box should be gentler than standing in a black corridor. It exists to stop 'wait him out' being free: hiding measured as INERT in the bot playtests, and turtling costs nothing today.\n\n110s and not 70s because raising the darkness drain to 75s (see the measurement above) INVERTED the relationship and made a wardrobe harsher than a dark corridor. ComposureTests.HidingCostsLessThanTheDark caught that; the two numbers have to move together.")]
        [Range(5f, 400f)] public float secondsToSpendHiding = 110f;

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
