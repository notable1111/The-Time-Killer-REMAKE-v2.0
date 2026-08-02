// Carves room in the music so everything else can be heard.
//
// Measured 2026-07-28 across all 20 wired tracks: the music puts **61% of its
// energy below 60 Hz** and 81% below 150 Hz. Two consequences, both bad:
//
//   1. MASKING. Low frequencies mask higher ones far more than the reverse (the
//      "upward spread of masking" — a property of the basilar membrane, not of
//      the mix), and the masking curves WIDEN as level rises. So that sub-bass
//      was not merely eating headroom, it was smearing everything above it.
//   2. IT IS NOT EVEN AUDIBLE. Laptop speakers cannot reproduce below roughly
//      150-200 Hz, so on the machine most players will use, 81% of the music's
//      energy simply does not come out — while on headphones it turns to mud.
//      This project already learned this once: heartbeat v1 sat 97% below 100 Hz
//      and could not be heard at all on laptop speakers regardless of volume.
//
// So: a high-pass to throw away what nobody hears, and a gentle dip in the
// 150-300 Hz "mud zone" where every element in the game was colliding. What
// stays untouched is 300 Hz upward — the maniac's growl (peaks 300-800), the
// stings (300-800), footsteps and gasps (0.8-2k). Those measured as correctly
// placed already, so the fix is entirely about getting the music out of the way.
//
// Done at RUNTIME rather than by processing the files. Baking 20 tracks to disk
// would have cost roughly half a gigabyte in a repo that already ships a 1.6GB
// pack, and it could not be tuned by ear afterwards.
//
// Removable: delete the component and the music is unfiltered again.
using UnityEngine;

namespace TimeKiller.Audio
{
    /// Two cascaded biquads (RBJ cookbook) on whichever AudioSource this sits on.
    [RequireComponent(typeof(AudioSource))]
    public class MusicEq : MonoBehaviour
    {
        // Coefficients, written on the main thread and read on the audio thread.
        // Plain floats on purpose: OnAudioFilterRead runs on the audio thread and
        // must never touch a Unity API or allocate.
        float hb0, hb1, hb2, ha1, ha2;
        float pb0, pb1, pb2, pa1, pa2;
        bool active;

        // TWO cascaded high-pass biquads = 4th order, 24 dB/octave. A single
        // biquad was measured and rejected: at a 55 Hz corner it only took the
        // chase track's sub-60 content from 61% to 51%, because 12 dB/octave
        // barely touches the 40-60 Hz range where most of that energy actually
        // sits. Cascaded at 80 Hz it reaches 28%.
        readonly float[] hx1 = new float[8], hx2 = new float[8], hy1 = new float[8], hy2 = new float[8];
        readonly float[] gx1 = new float[8], gx2 = new float[8], gy1 = new float[8], gy2 = new float[8];
        readonly float[] px1 = new float[8], px2 = new float[8], py1 = new float[8], py2 = new float[8];

        float appliedHp, appliedDipHz, appliedDipDb, appliedDipQ;
        bool appliedEnabled;

        void OnEnable() => Rebuild(force: true);

        void Update()
        {
            // Cheap poll so the config can be tuned live in Play Mode — the whole
            // point of putting these numbers in a ScriptableObject.
            var cfg = AudioMix.Config;
            if (cfg == null) { active = false; return; }
            if (appliedEnabled != cfg.musicEqEnabled || !Mathf.Approximately(appliedHp, cfg.musicHighPassHz)
                || !Mathf.Approximately(appliedDipHz, cfg.musicDipHz)
                || !Mathf.Approximately(appliedDipDb, cfg.musicDipDb)
                || !Mathf.Approximately(appliedDipQ, cfg.musicDipQ))
                Rebuild(force: false);
        }

        void Rebuild(bool force)
        {
            var cfg = AudioMix.Config;
            if (cfg == null) { active = false; return; }
            appliedEnabled = cfg.musicEqEnabled;
            appliedHp = cfg.musicHighPassHz;
            appliedDipHz = cfg.musicDipHz;
            appliedDipDb = cfg.musicDipDb;
            appliedDipQ = cfg.musicDipQ;

            int sr = AudioSettings.outputSampleRate;
            HighPass(appliedHp, 0.707f, sr);       // Butterworth Q — no resonant bump at the corner
            Peaking(appliedDipHz, appliedDipQ, appliedDipDb, sr);
            active = appliedEnabled;
        }

        void HighPass(float f0, float q, int sr)
        {
            float w0 = 2f * Mathf.PI * Mathf.Clamp(f0, 10f, sr * 0.45f) / sr;
            float cos = Mathf.Cos(w0), alpha = Mathf.Sin(w0) / (2f * q);
            float a0 = 1f + alpha;
            hb0 = ((1f + cos) * 0.5f) / a0;
            hb1 = (-(1f + cos)) / a0;
            hb2 = hb0;
            ha1 = (-2f * cos) / a0;
            ha2 = (1f - alpha) / a0;
        }

        void Peaking(float f0, float q, float gainDb, int sr)
        {
            float A = Mathf.Pow(10f, gainDb / 40f);
            float w0 = 2f * Mathf.PI * Mathf.Clamp(f0, 20f, sr * 0.45f) / sr;
            float cos = Mathf.Cos(w0), alpha = Mathf.Sin(w0) / (2f * Mathf.Max(0.1f, q));
            float a0 = 1f + alpha / A;
            pb0 = (1f + alpha * A) / a0;
            pb1 = (-2f * cos) / a0;
            pb2 = (1f - alpha * A) / a0;
            pa1 = (-2f * cos) / a0;
            pa2 = (1f - alpha / A) / a0;
        }

        /// Audio thread. No allocations, no Unity API calls, no logging.
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (!active || channels > hx1.Length) return;
            for (int i = 0; i < data.Length; i += channels)
                for (int c = 0; c < channels; c++)
                {
                    float x = data[i + c];

                    // High-pass, stage 1
                    float h1 = hb0 * x + hb1 * hx1[c] + hb2 * hx2[c] - ha1 * hy1[c] - ha2 * hy2[c];
                    hx2[c] = hx1[c]; hx1[c] = x;
                    hy2[c] = hy1[c]; hy1[c] = h1;

                    // High-pass, stage 2 — same coefficients, cascaded for 24 dB/oct
                    float h2 = hb0 * h1 + hb1 * gx1[c] + hb2 * gx2[c] - ha1 * gy1[c] - ha2 * gy2[c];
                    gx2[c] = gx1[c]; gx1[c] = h1;
                    gy2[c] = gy1[c]; gy1[c] = h2;

                    // Mud-zone dip
                    float py = pb0 * h2 + pb1 * px1[c] + pb2 * px2[c] - pa1 * py1[c] - pa2 * py2[c];
                    px2[c] = px1[c]; px1[c] = h2;
                    py2[c] = py1[c]; py1[c] = py;

                    data[i + c] = py;
                }
        }
    }
}
