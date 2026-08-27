// Puts a door between you and the room while you are hidden.
//
// WHY THIS EXISTS. Hiding already changes your BODY — PlayerHeartbeat boosts
// itself 1.3x while hidden and PlayerBreathing holds the breath down to 5% — but
// it did not change the ROOM at all. Measured 2026-08-27: the project contained
// no AudioLowPassFilter, no AudioHighPassFilter, no AudioReverbFilter and no
// AudioMixer asset, anywhere. From inside a wardrobe the world sounded
// byte-identical to standing in the corridor.
//
// That costs the mechanic its trade. In a hide-and-run game the muffle is not
// decoration: it is the price of the sanctuary. You are safer in there AND you
// can hear less, so you cannot tell when he has gone — which is the whole reason
// leaving is a decision instead of a formality. The bot data measured hiding as
// very nearly inert (81.8% vs 81.9% death rate); this is the audio half of
// making it matter.
//
// WHAT GETS MUFFLED, AND WHY THAT LINE. Only AudioSources with spatialBlend at
// or above the config threshold — things that are IN THE ROOM. The project
// already draws this line in its own source comments: the heartbeat is "YOUR
// heart: fully 2D, never positional", the breath is "your own lungs, not a world
// sound", PlayerVoice is "your own throat", FearDrone is "inside your head, not
// in the room". A wardrobe door muffles none of those, so neither does this. The
// maniac's three sources are spatialBlend 1 (deliberately, to keep the "he is to
// my left" panning) and they are exactly what should go dull.
//
// SELF-INSTALLING, no scene edit. Three sessions share one Editor and the scenes
// are hand-tuned and protected; a feature that needs no scene change cannot
// collide with anyone and works in Catacombs and any level added later. Same
// precedent as AudioMix and ManiacThreatEffects.
//
// REMOVABLE: delete WorldMuffleConfig.asset and this never installs. The filters
// it adds are DISABLED rather than destroyed on the way out, and reopened to the
// config's open cutoff first, so a half-finished ramp can never leave a source
// permanently dull.
using System.Collections.Generic;
using TimeKiller.Core;
using UnityEngine;

namespace TimeKiller.Audio
{
    public class WorldMuffle : MonoBehaviour
    {
        static WorldMuffle instance;

        WorldMuffleConfig config;
        readonly List<AudioLowPassFilter> filters = new();

        bool hidden;
        float amount;          // 0 = open room, 1 = fully muffled
        float rescanAt;

        /// 0..1, for the F1 overlay and for anyone measuring whether this fired.
        public float Amount => amount;
        public int FilteredSources => filters.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (instance != null) return;
            var config = Resources.Load<WorldMuffleConfig>(WorldMuffleConfig.ResourcesPath);
            if (config == null || !config.enabled) return;   // no asset = not installed

            var go = new GameObject("WorldMuffle");
            Object.DontDestroyOnLoad(go);
            instance = go.AddComponent<WorldMuffle>();
            instance.config = config;
        }

        void OnEnable()
        {
            EventBus.Subscribe<TimeKiller.Hiding.PlayerHidEvent>(OnHid);
            EventBus.Subscribe<TimeKiller.Hiding.PlayerUnhidEvent>(OnUnhid);
            DebugOverlay.Watch("Muffle", () =>
                $"{amount:0.00} over {filters.Count} world source(s){(hidden ? "  HIDDEN" : "")}");
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<TimeKiller.Hiding.PlayerHidEvent>(OnHid);
            EventBus.Unsubscribe<TimeKiller.Hiding.PlayerUnhidEvent>(OnUnhid);
            DebugOverlay.Unwatch("Muffle");
            Release();
        }

        void OnHid(TimeKiller.Hiding.PlayerHidEvent evt)
        {
            hidden = true;
            Rescan();          // the maniac's sources are made in Awake; a clock's
                               // beacon may not exist until its scene is loaded.
        }

        void OnUnhid(TimeKiller.Hiding.PlayerUnhidEvent evt) => hidden = false;

        void Update()
        {
            if (config == null) return;

            float target = hidden ? 1f : 0f;
            if (!Mathf.Approximately(amount, target))
            {
                float seconds = target > amount ? config.closeSeconds : config.openSeconds;
                amount = Mathf.MoveTowards(amount, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds));
                Apply();
            }
            else if (amount <= 0f && filters.Count > 0)
            {
                Release();     // fully open again: hand every source back untouched
            }

            // A source can appear mid-hide (he walks into the level, a clock is
            // repaired). Cheap enough at 2 Hz, and only while it matters.
            if (hidden && Time.unscaledTime >= rescanAt) Rescan();
        }

        /// Unscaled time throughout: the pause menu freezes Time.timeScale, and a
        /// muffle frozen half-closed would have the player set the volume sliders
        /// against a sound the game never actually makes.
        void Rescan()
        {
            rescanAt = Time.unscaledTime + 0.5f;
            filters.Clear();

            var sources = Object.FindObjectsByType<AudioSource>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var source in sources)
            {
                if (source == null || source.spatialBlend < config.spatialThreshold) continue;

                var filter = source.GetComponent<AudioLowPassFilter>();
                if (filter == null) filter = source.gameObject.AddComponent<AudioLowPassFilter>();
                filter.enabled = true;
                filters.Add(filter);
            }
            Apply();
        }

        void Apply()
        {
            float cutoff = Mathf.Lerp(config.openCutoffHz, config.cutoffHz, amount);
            float q = Mathf.Lerp(1f, config.resonance, amount);
            for (int i = filters.Count - 1; i >= 0; i--)
            {
                var filter = filters[i];
                if (filter == null) { filters.RemoveAt(i); continue; }
                filter.cutoffFrequency = cutoff;
                filter.lowpassResonanceQ = q;
            }
        }

        /// Open every filter fully and switch it off. Disabled rather than
        /// destroyed: destroying a component on someone else's AudioSource mid-run
        /// is a good way to surprise a system that cached a reference to it.
        void Release()
        {
            foreach (var filter in filters)
            {
                if (filter == null) continue;
                filter.cutoffFrequency = config != null ? config.openCutoffHz : 22000f;
                filter.lowpassResonanceQ = 1f;
                filter.enabled = false;
            }
            filters.Clear();
        }
    }
}
