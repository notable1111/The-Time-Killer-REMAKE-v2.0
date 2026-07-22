"""Synthesize a realistic 'lub-dub' heartbeat sample (pure stdlib).

Physiology model: S1 ('lub') — mitral/tricuspid closure, lower pitch, longer;
~120ms later S2 ('dub') — aortic closure, slightly higher pitch, shorter.
Each thump = low sine sweep (~62->45 Hz) with fast attack, exponential decay,
plus a soft band-limited noise tap for the valve 'click'.
Output: one lub-dub pair, 0.45 s, 44.1 kHz mono 16-bit — trigger once per beat.
"""
import math, random, struct, wave

SR = 44100
DUR = 0.45
random.seed(7)

def thump(t, start, length, f0, f1, gain):
    if t < start or t > start + length:
        return 0.0
    x = (t - start) / length                      # 0..1 within the thump
    freq = f0 + (f1 - f0) * x                     # downward pitch sweep
    env = (x / 0.06) if x < 0.06 else math.exp(-6.5 * (x - 0.06))  # fast attack, exp decay
    body = math.sin(2 * math.pi * freq * (t - start)) * env
    click = (random.random() * 2 - 1) * math.exp(-40 * x) * 0.12   # valve click
    return (body + click) * gain

samples = []
for i in range(int(SR * DUR)):
    t = i / SR
    s = thump(t, 0.000, 0.16, 62, 44, 1.00)      # S1 'lub'
    s += thump(t, 0.150, 0.12, 74, 52, 0.72)     # S2 'dub'
    samples.append(s)

# soft-knee limit + normalize to -1.5 dBFS
peak = max(abs(s) for s in samples)
norm = 0.84 / peak
out = r"D:\The Time Killer Remake\Assets\Resources\Assets\HealthVfx\heartbeat.wav"
with wave.open(out, "wb") as w:
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s * norm)) * 32767)) for s in samples))
print("saved", out)
