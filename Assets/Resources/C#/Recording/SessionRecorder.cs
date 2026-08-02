// The game records itself, so someone with no screen can still study it.
//
// Claude has no display and cannot watch a screen or hear a speaker. But a game
// that writes its own frames, its own final audio mix and its own state to disk
// can be studied afterwards in complete detail — and better than by watching,
// because every frame is joined to the exact state that produced it instead of
// being guessed at from pixels.
//
// THREE STREAMS, one folder per session under <project>/Recordings/:
//   state.jsonl  — 4 Hz: fear, stage, bpm, maniac state, distances, HP, room
//   frames/*.jpg — ~1 Hz, downscaled, exactly what the player saw (overlay included)
//   audio.wav    — the FINAL mix (see SessionAudioCapture)
//
// WHY OUTSIDE Assets/: a few thousand PNGs inside the project would all be
// imported by Unity, generating .meta files and bloating both the library and
// the repo. Recordings are output, not content.
//
// THE HOTKEYS ARE THE POINT. F9/F10/F11 stamp "boring", "unfair" and "that was
// great" into the log. Everything else here is measurable by machine; whether a
// moment was BORING is not, and never will be. Those three keys are the only
// signal in the whole pipeline that no bot could ever produce, which is exactly
// why a human tester is worth more than the bot playtester we already have.
//
// Dev-only tooling: it never changes gameplay, and removing it changes nothing.
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using TimeKiller.Core;
using TimeKiller.Maniac;
using TimeKiller.Player;
using UnityEngine;

namespace TimeKiller.Recording
{
    public class SessionRecorder : MonoBehaviour
    {
        [Header("What to capture")]
        [SerializeField] bool captureState = true;
        [SerializeField] bool captureFrames = true;
        [Tooltip("State samples per second. 4 Hz is enough to see pacing and cheap enough that a 20-minute session is a small text file.")]
        [SerializeField] float stateHz = 4f;
        [Tooltip("Frames per second written to disk. Keep LOW — this is for studying pacing, not for playback, and each grab stalls the frame.")]
        [SerializeField] float frameHz = 1f;
        [Tooltip("Long edge of a saved frame. 480 is plenty to see what happened and keeps a session's folder small.")]
        [SerializeField] int frameWidth = 480;
        [Range(1, 100)] [SerializeField] int jpegQuality = 70;

        [Header("Keys")]
        [SerializeField] KeyCode toggleKey = KeyCode.F8;
        [SerializeField] KeyCode boringKey = KeyCode.F9;
        [SerializeField] KeyCode unfairKey = KeyCode.F10;
        [SerializeField] KeyCode greatKey = KeyCode.F11;

        public bool Recording { get; private set; }
        public string SessionDir { get; private set; } = "";
        public int StateSamples { get; private set; }
        public int FramesSaved { get; private set; }
        public int Annotations { get; private set; }

        // ---- surviving a scene reload ---------------------------------------
        // GameFlow reloads the scene when the player dies, which destroys this
        // component — and the first version simply stopped there. A 110s bot run
        // recorded 24s, and worse, the part it threw away was everything AFTER
        // the death, plus the death itself. The moments most worth recording are
        // exactly the ones that ended the run.
        //
        // Statics survive a scene load but NOT a domain reload, which is exactly
        // the lifetime wanted: one session per play-mode session, resumed across
        // any number of scene reloads inside it.
        static string resumeDir = "";
        static int resumeFrames;
        static float resumeElapsed;
        static bool resumeActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            resumeDir = ""; resumeFrames = 0; resumeElapsed = 0f; resumeActive = false;
        }

        StreamWriter state;
        float nextStateAt, nextFrameAt, startedAt;
        // Last timestamp written. The session clock is an INVARIANT the analyser
        // depends on (it sorts on t and matches frames by it), so rather than
        // trusting Time to behave across a reload, monotonicity is enforced here.
        // The first attempt rebased on Time.time and produced t running
        // 0.3 -> -38.7s after a death, which silently reordered every post-death
        // row and attached the wrong frame to every finding.
        float lastT = -1f;
        Transform player;
        ManiacController maniac;
        TimeKiller.Fear.FearConductor fear;
        TimeKiller.Heartbeat.PlayerHeartbeat heart;
        SessionAudioCapture audioCapture;
        int hp = -1;
        readonly StringBuilder line = new StringBuilder(512);

        void Start()
        {
            EventBus.Subscribe<PlayerHealthChangedEvent>(OnHealth);
            EventBus.Subscribe<PlayerHitEvent>(e => Mark("hit"));
            EventBus.Subscribe<PlayerDiedEvent>(e => Mark("death"));
            EventBus.Subscribe<TimeKiller.Fear.FearDetectionEvent>(e => Mark("detected"));
            EventBus.Subscribe<TimeKiller.Hiding.PlayerHidEvent>(e => Mark("hide"));
            EventBus.Subscribe<TimeKiller.Hiding.PlayerUnhidEvent>(e => Mark("unhide"));
            EventBus.Subscribe<TimeKiller.Objectives.ClockFixedEvent>(e => Mark("clock"));

            DebugOverlay.Watch("REC", () => Recording
                ? $"● {Time.unscaledTime - startedAt:0}s  {StateSamples} samples  {FramesSaved} frames  {Annotations} marks  [F9 boring F10 unfair F11 great]"
                : $"off — {toggleKey} to start");

            // A reload happened mid-session — pick the same folder back up rather
            // than losing everything from the death onward.
            if (resumeActive && !string.IsNullOrEmpty(resumeDir)) Begin();
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<PlayerHealthChangedEvent>(OnHealth);
            DebugOverlay.Unwatch("REC");
            // A scene reload lands HERE, not in Stop(). Park what we know so the
            // next instance can continue, then close the handles. Deliberately
            // does NOT clear the statics — only an explicit Stop() ends a session.
            if (Recording)
            {
                resumeDir = SessionDir;
                resumeFrames = FramesSaved;
                resumeElapsed = Mathf.Max(0f, Time.unscaledTime - startedAt);
                resumeActive = true;
                WriteState("reload");
                CloseFiles();
                Recording = false;
            }
        }

        void OnHealth(PlayerHealthChangedEvent e) => hp = e.Current;

        void Update()
        {
            if (Input.GetKeyDown(toggleKey)) { if (Recording) Stop(); else Begin(); }
            if (!Recording) return;

            if (Input.GetKeyDown(boringKey)) Mark("BORING");
            if (Input.GetKeyDown(unfairKey)) Mark("UNFAIR");
            if (Input.GetKeyDown(greatKey)) Mark("GREAT");

            if (captureState && Time.unscaledTime >= nextStateAt)
            {
                nextStateAt = Time.unscaledTime + 1f / Mathf.Max(0.1f, stateHz);
                WriteState(null);
            }
            if (captureFrames && Time.unscaledTime >= nextFrameAt)
            {
                nextFrameAt = Time.unscaledTime + 1f / Mathf.Max(0.05f, frameHz);
                StartCoroutine(GrabFrame());
            }
        }

        public void Begin()
        {
            if (Recording) return;
            bool resuming = resumeActive && !string.IsNullOrEmpty(resumeDir);

            // Date is taken here rather than passed in: this is the one moment a
            // wall-clock stamp is meaningful, and it is what joins a folder to
            // the tester's own notes.
            string stamp = resuming
                ? Path.GetFileName(resumeDir)
                : System.DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            SessionDir = resuming
                ? resumeDir
                : Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Recordings", stamp);
            Directory.CreateDirectory(Path.Combine(SessionDir, "frames"));

            // APPEND when resuming, so the rows either side of a death end up in
            // one continuous timeline instead of two files nobody can join.
            state = new StreamWriter(Path.Combine(SessionDir, "state.jsonl"), resuming);
            // UNSCALED on purpose. GameFlow sets Time.timeScale = 0 on death, so
            // Time.time freezes exactly during the reload this has to survive —
            // and a frozen clock either side of a rebase is how t went negative.
            // unscaledTime keeps advancing through the death screen and the load.
            startedAt = Time.unscaledTime - (resuming ? Mathf.Max(0f, resumeElapsed) : 0f);
            lastT = resuming ? Mathf.Max(0f, resumeElapsed) : -1f;
            StateSamples = Annotations = 0;
            FramesSaved = resuming ? resumeFrames : 0;
            resumeActive = true;
            resumeDir = SessionDir;

            player = Object.FindAnyObjectByType<PlayerController>()?.transform;
            maniac = Object.FindAnyObjectByType<ManiacController>();
            fear = Object.FindAnyObjectByType<TimeKiller.Fear.FearConductor>();
            heart = Object.FindAnyObjectByType<TimeKiller.Heartbeat.PlayerHeartbeat>();

            audioCapture = Object.FindAnyObjectByType<SessionAudioCapture>();
            if (audioCapture != null) audioCapture.Begin(Path.Combine(SessionDir, "audio.wav"), resuming);

            if (!resuming)
                state.WriteLine("{\"meta\":true,\"scene\":\"" +
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().name +
                    "\",\"started\":\"" + stamp + "\",\"stateHz\":" + stateHz.ToString(CultureInfo.InvariantCulture) +
                    ",\"audio\":" + (audioCapture != null ? "true" : "false") + "}");
            Recording = true;
            Debug.Log($"[SessionRecorder] {(resuming ? "RESUMED" : "recording")} -> {SessionDir}");
        }

        /// Deliberate end of a session (F8, or teardown). Unlike a scene reload
        /// this clears the resume handle, so the next Begin() starts a new folder.
        public void Stop()
        {
            if (!Recording) return;
            CloseFiles();
            Recording = false;
            resumeActive = false;
            resumeDir = "";
            Debug.Log($"[SessionRecorder] stopped. {FramesSaved} frames, {Annotations} marks -> {SessionDir}");
        }

        void CloseFiles()
        {
            if (audioCapture != null) audioCapture.Stop();
            if (state != null) { state.Flush(); state.Dispose(); state = null; }
        }

        /// A moment worth looking at. Written as its own row so the analyser can
        /// find it without scanning, and carrying the full state so an annotation
        /// is self-contained evidence rather than just a timestamp.
        public void Mark(string what)
        {
            if (!Recording) return;
            Annotations++;
            WriteState(what);
        }

        void WriteState(string mark)
        {
            if (state == null) return;
            var ci = CultureInfo.InvariantCulture;
            // Monotonic by construction. If the clock ever moves backwards across
            // a reload, nudge forward instead of emitting a row that would sort
            // before rows already on disk — a corrupt ordering is far worse than
            // a slightly wrong timestamp, because it silently misattributes every
            // later finding to the wrong moment and the wrong frame.
            float t = Mathf.Max(Time.unscaledTime - startedAt, lastT + 0.001f);
            lastT = t;
            line.Clear();
            line.Append("{\"t\":").Append(t.ToString("0.00", ci));
            if (mark != null) line.Append(",\"mark\":\"").Append(mark).Append('"');

            if (player != null)
                line.Append(",\"px\":").Append(((Vector2)player.position).x.ToString("0.0", ci))
                    .Append(",\"py\":").Append(((Vector2)player.position).y.ToString("0.0", ci));
            line.Append(",\"hp\":").Append(hp);

            if (fear != null)
            {
                line.Append(",\"fear\":").Append(fear.Fear.ToString("0.000", ci));
                line.Append(",\"stage\":\"").Append(fear.Stage).Append('"');
                line.Append(",\"threatDist\":").Append(float.IsInfinity(fear.ThreatDistance) ? "null" : fear.ThreatDistance.ToString("0.0", ci));
                line.Append(",\"directDist\":").Append(float.IsInfinity(fear.DirectDistance) ? "null" : fear.DirectDistance.ToString("0.0", ci));
                line.Append(",\"aware\":\"").Append(fear.AwarenessName).Append('"');
                line.Append(",\"recovering\":").Append(fear.Recovering ? "true" : "false");
            }
            if (heart != null)
            {
                line.Append(",\"bpm\":").Append(heart.Bpm.ToString("0", ci));
                line.Append(",\"beats\":").Append(heart.BeatCount);
            }
            if (maniac != null)
                line.Append(",\"mx\":").Append(maniac.Motor.Position.x.ToString("0.0", ci))
                    .Append(",\"my\":").Append(maniac.Motor.Position.y.ToString("0.0", ci));
            line.Append(",\"world\":").Append(AudioDucking.World.ToString("0.00", ci));
            line.Append(",\"frame\":").Append(FramesSaved);
            line.Append('}');

            state.WriteLine(line.ToString());
            StateSamples++;
            // Flushed every sample on purpose: a crash mid-session is exactly the
            // session worth reading, and a buffered writer would lose the last
            // few seconds — which is where the crash is.
            state.Flush();
        }

        /// Grab what the player actually saw, overlay and all.
        ///
        /// Must wait for end of frame: ScreenCapture reads the back buffer, and
        /// calling it mid-Update returns either the previous frame or nothing.
        IEnumerator GrabFrame()
        {
            yield return new WaitForEndOfFrame();
            if (!Recording) yield break;

            Texture2D shot = null, small = null;
            try
            {
                shot = ScreenCapture.CaptureScreenshotAsTexture();
                int w = Mathf.Max(64, frameWidth);
                int h = Mathf.Max(36, Mathf.RoundToInt(w * (float)shot.height / shot.width));

                // Downscale through a RenderTexture — Texture2D has no resize, and
                // a 1080p JPEG every second would be gigabytes over a long session.
                var rt = RenderTexture.GetTemporary(w, h, 0);
                Graphics.Blit(shot, rt);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                small = new Texture2D(w, h, TextureFormat.RGB24, false);
                small.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                small.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);

                // JPEG, not PNG: this is photographic content and a tenth the size.
                //
                // InvariantCulture on the TIMESTAMP is load-bearing, not tidiness.
                // On a comma-decimal machine (this one) plain interpolation writes
                // "f00003_3,3.jpg", and the analyser parses the seconds back out of
                // that name to match a finding to its frame — so every finding came
                // back with no evidence attached. Caught by running the tool on a
                // real recording rather than by reading the code.
                // Same clock as the state rows, or a finding's timestamp would
                // not match the frame the analyser pairs with it.
                string seconds = (Time.unscaledTime - startedAt).ToString("0.0", CultureInfo.InvariantCulture);
                File.WriteAllBytes(
                    Path.Combine(SessionDir, "frames", $"f{FramesSaved:00000}_{seconds}.jpg"),
                    small.EncodeToJPG(jpegQuality));
                FramesSaved++;
            }
            finally
            {
                if (shot != null) Destroy(shot);
                if (small != null) Destroy(small);
            }
        }
    }
}
