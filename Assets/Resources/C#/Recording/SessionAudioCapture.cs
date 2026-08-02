// Captures the game's FINAL audio mix to a WAV.
//
// This is the stream that turns "the heartbeat accelerates as he closes" from a
// claim reasoned out of source code into a measurement. With the real mix on
// disk you can count beat onsets and watch the interval shorten, count how many
// times the detection sting actually fired, measure how far the ambience ducked
// in dB, and find the silences. None of that needs ears.
//
// HOW IT WORKS. Sits on the AudioListener. OnAudioFilterRead is handed the mixed
// output of everything the listener hears, immediately before it reaches the
// speakers — so this is the true final mix including every volume, pitch and
// duck the game applied, not a re-render of the sources.
//
// THE AUDIO THREAD RULE. OnAudioFilterRead does NOT run on the main thread, and
// touching the Unity API from it is undefined behaviour. So it does exactly one
// thing: copy samples into a buffer under a lock. All file work happens on the
// main thread in Update. Getting this wrong produces crashes that look random
// and are hell to trace, which is why it is spelled out here.
//
// The WAV header cannot be written until the length is known, so it is stamped
// with placeholders up front and patched on Stop.
using System;
using System.IO;
using UnityEngine;

namespace TimeKiller.Recording
{
    [RequireComponent(typeof(AudioListener))]
    public class SessionAudioCapture : MonoBehaviour
    {
        public bool Capturing { get; private set; }
        public float SecondsCaptured { get; private set; }
        public string Problem { get; private set; } = "";

        FileStream file;
        BinaryWriter wav;
        readonly object gate = new object();
        float[] pending = new float[1 << 16];
        int pendingCount;
        int channels = 2;
        int sampleRate = 48000;
        int samplesWritten;

        void Awake() => sampleRate = AudioSettings.outputSampleRate;

        public void Begin(string path, bool append = false)
        {
            Stop();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                // RESUMING after a scene reload: reopen the same WAV, seek past
                // what is already there and keep writing into the same data
                // chunk. Two files either side of a death would have to be joined
                // by hand, and the join is exactly where the interesting audio is.
                bool resume = append && File.Exists(path) && new FileInfo(path).Length > 44;
                if (resume)
                {
                    file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite);
                    // Recover the sample count from the file rather than trusting
                    // a static: 16-bit mono-interleaved, so 2 bytes per sample.
                    samplesWritten = (int)((file.Length - 44) / 2);
                    file.Seek(0, SeekOrigin.End);
                    wav = new BinaryWriter(file);
                }
                else
                {
                    file = new FileStream(path, FileMode.Create, FileAccess.Write);
                    wav = new BinaryWriter(file);
                    WriteHeaderPlaceholder();
                    samplesWritten = 0;
                }
                pendingCount = 0;
                SecondsCaptured = channels > 0 ? (float)samplesWritten / channels / sampleRate : 0f;
                Problem = "";
                Capturing = true;
            }
            catch (Exception e)
            {
                Problem = "audio capture failed: " + e.Message;
                Capturing = false;
            }
        }

        public void Stop()
        {
            if (!Capturing) { CloseQuietly(); return; }
            Capturing = false;
            Flush();          // whatever the audio thread left behind
            PatchHeader();
            CloseQuietly();
        }

        void OnDestroy() => Stop();

        // ---- audio thread: copy and get out ---------------------------------
        void OnAudioFilterRead(float[] data, int chans)
        {
            if (!Capturing) return;
            lock (gate)
            {
                channels = chans;
                if (pendingCount + data.Length > pending.Length)
                {
                    // Grow rather than drop. A dropped block would silently shift
                    // everything after it in time, and a recording whose clock
                    // drifts is worse than no recording — every later measurement
                    // would be joined to the wrong moment.
                    var bigger = new float[Mathf.NextPowerOfTwo(pendingCount + data.Length)];
                    Array.Copy(pending, bigger, pendingCount);
                    pending = bigger;
                }
                Array.Copy(data, 0, pending, pendingCount, data.Length);
                pendingCount += data.Length;
            }
        }

        // ---- main thread: all file work -------------------------------------
        void Update()
        {
            if (Capturing) Flush();
        }

        void Flush()
        {
            if (wav == null) return;
            float[] block; int count;
            lock (gate)
            {
                if (pendingCount == 0) return;
                block = pending;
                count = pendingCount;
                pending = new float[block.Length];
                pendingCount = 0;
            }

            for (int i = 0; i < count; i++)
            {
                // 16-bit PCM. Clamped because the mix can legitimately exceed 1.0
                // when several loud channels land together, and wrapping that
                // would read as a click in the analysis — a fake defect.
                short s = (short)(Mathf.Clamp(block[i], -1f, 1f) * short.MaxValue);
                wav.Write(s);
            }
            samplesWritten += count;
            SecondsCaptured = channels > 0 ? (float)samplesWritten / channels / sampleRate : 0f;
        }

        void WriteHeaderPlaceholder()
        {
            wav.Write(new char[] { 'R', 'I', 'F', 'F' });
            wav.Write(0);                                  // patched on Stop
            wav.Write(new char[] { 'W', 'A', 'V', 'E' });
            wav.Write(new char[] { 'f', 'm', 't', ' ' });
            wav.Write(16);
            wav.Write((short)1);                           // PCM
            wav.Write((short)channels);
            wav.Write(sampleRate);
            wav.Write(sampleRate * channels * 2);          // byte rate
            wav.Write((short)(channels * 2));              // block align
            wav.Write((short)16);                          // bits
            wav.Write(new char[] { 'd', 'a', 't', 'a' });
            wav.Write(0);                                  // patched on Stop
        }

        void PatchHeader()
        {
            if (wav == null) return;
            int dataBytes = samplesWritten * 2;
            wav.Flush();
            // Channel count is only known once audio has actually flowed, so the
            // header is corrected here too rather than trusting the guess made
            // before the first buffer arrived.
            file.Seek(22, SeekOrigin.Begin); wav.Write((short)channels);
            file.Seek(28, SeekOrigin.Begin); wav.Write(sampleRate * channels * 2);
            file.Seek(32, SeekOrigin.Begin); wav.Write((short)(channels * 2));
            file.Seek(4, SeekOrigin.Begin); wav.Write(36 + dataBytes);
            file.Seek(40, SeekOrigin.Begin); wav.Write(dataBytes);
            wav.Flush();
        }

        void CloseQuietly()
        {
            try { wav?.Dispose(); } catch { }
            try { file?.Dispose(); } catch { }
            wav = null; file = null;
        }
    }
}
