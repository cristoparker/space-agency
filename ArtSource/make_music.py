# Procedurally composes the game's background music (royalty free: every note is synthesised here, nothing is sampled or downloaded).
# Three cozy, loop-friendly tracks for a city-builder / space-agency tycoon: soft electric piano, warm bass, pads, light plucks and brushed drums.
#   python3 ArtSource/make_music.py     ->  Assets/Resources/Music/*.ogg  (needs numpy and oggenc)
import numpy as np, os, subprocess, wave

SR = 44100
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Resources", "Music")
os.makedirs(OUT, exist_ok=True)


def midi(n):
    return 440.0 * 2 ** ((n - 69) / 12.0)


def env(n, a, d_tau, sus=0.0):
    t = np.arange(n) / SR
    e = np.exp(-t / d_tau) * (1 - sus) + sus
    att = np.minimum(1.0, t / max(a, 1e-4))
    return e * att


def epiano(f, dur, vel=0.5):
    n = int(SR * dur)
    t = np.arange(n) / SR
    s = np.zeros(n)
    for h, amp, tau in ((1, 1.0, 1.6), (2, 0.38, 0.9), (3, 0.14, 0.5), (4, 0.06, 0.3), (6, 0.03, 0.2)):
        s += amp * np.sin(2 * np.pi * f * h * t + 0.3 * h) * np.exp(-t / tau)
    s += 0.35 * np.sin(2 * np.pi * f * 7.1 * t) * np.exp(-t / 0.04)             # tine
    s *= np.minimum(1.0, t / 0.006)
    rel = np.minimum(1.0, np.maximum(0.0, (dur - t) / 0.25))
    return s * rel * vel


def pad(f, dur, vel=0.3):
    n = int(SR * dur)
    t = np.arange(n) / SR
    s = np.zeros(n)
    for det in (-0.004, 0.0, 0.004):
        ff = f * (1 + det)
        for h, amp in ((1, 1.0), (2, 0.5), (3, 0.28), (4, 0.12)):
            s += amp * np.sin(2 * np.pi * ff * h * t + det * 50)
    e = np.minimum(1.0, t / 1.2) * np.minimum(1.0, np.maximum(0.0, (dur - t) / 1.5))
    return s * e * vel / 4


def bass(f, dur, vel=0.6):
    n = int(SR * dur)
    t = np.arange(n) / SR
    s = np.sin(2 * np.pi * f * t) + 0.35 * np.sin(2 * np.pi * f * 2 * t) * np.exp(-t / 0.3)
    e = np.minimum(1.0, t / 0.012) * np.exp(-t / 1.1) * np.minimum(1.0, np.maximum(0.0, (dur - t) / 0.12))
    return s * e * vel


def pluck(f, dur, vel=0.4):
    n = int(SR * dur)
    t = np.arange(n) / SR
    s = np.zeros(n)
    for h, amp, tau in ((1, 1.0, 0.55), (2, 0.5, 0.3), (3, 0.3, 0.2), (5, 0.12, 0.1)):
        s += amp * np.sin(2 * np.pi * f * h * t) * np.exp(-t / tau)
    s *= np.minimum(1.0, t / 0.004)
    return s * vel


def kick(vel=0.7):
    n = int(SR * 0.32)
    t = np.arange(n) / SR
    f = 46 + 90 * np.exp(-t / 0.045)
    ph = 2 * np.pi * np.cumsum(f) / SR
    return np.sin(ph) * np.exp(-t / 0.11) * vel


def noise_burst(dur, tau, lp, vel, rng, hp=0.0):
    n = int(SR * dur)
    x = rng.standard_normal(n)
    if lp > 0:                                           # one-pole low-pass
        a = np.exp(-2 * np.pi * lp / SR)
        y = np.zeros(n); acc = 0.0
        for i in range(n):
            acc = (1 - a) * x[i] + a * acc
            y[i] = acc
        x = y * 6
    if hp > 0:
        a = np.exp(-2 * np.pi * hp / SR)
        y = np.zeros(n); lo = 0.0
        for i in range(n):
            lo = (1 - a) * x[i] + a * lo
            y[i] = x[i] - lo
        x = y
    t = np.arange(n) / SR
    return x * np.exp(-t / tau) * vel


def add(buf, snd, at):
    i = int(at * SR)
    if i >= len(buf):
        return
    j = min(len(buf), i + len(snd))
    buf[i:j] += snd[: j - i]


def reverb(x, seconds, rng, wet):
    n = int(SR * seconds)
    t = np.arange(n) / SR
    ir = rng.standard_normal(n) * np.exp(-t / (seconds / 4.2))
    ir[: int(0.012 * SR)] *= np.linspace(0, 1, int(0.012 * SR))
    a = np.exp(-2 * np.pi * 3500 / SR)                   # darker tail
    acc = 0.0
    for i in range(n):
        acc = (1 - a) * ir[i] + a * acc
        ir[i] = acc
    ir /= np.sqrt(np.sum(ir ** 2)) + 1e-9
    N = 1 << int(np.ceil(np.log2(len(x) + n)))
    y = np.fft.irfft(np.fft.rfft(x, N) * np.fft.rfft(ir, N), N)[: len(x) + n]
    out = np.zeros(len(y))
    out[: len(x)] = x * (1 - wet)
    out += y * wet
    return out


NOTE = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def chord(root, kind):
    base = {"maj7": (0, 4, 7, 11), "m7": (0, 3, 7, 10), "6": (0, 4, 7, 9), "sus": (0, 5, 7, 10), "add9": (0, 4, 7, 14), "m9": (0, 3, 7, 14), "7": (0, 4, 7, 10)}[kind]
    return [root + b for b in base]


def render(name, bpm, key_root, prog, scale, bars, seed, swing=0.12, lead_density=0.4, drums=True, pad_level=1.0, lead_oct=0, vol=0.4):
    rng = np.random.default_rng(seed)
    beat = 60.0 / bpm
    bar = beat * 4
    length = bar * bars
    buf = np.zeros(int((length + 4.0) * SR))             # a few seconds of headroom for tails
    L = np.zeros_like(buf); R = np.zeros_like(buf)
    mel = np.zeros_like(buf)
    drm = np.zeros_like(buf)
    sc = [key_root + s for s in scale]
    # --- harmony: one chord per bar, the progression repeats
    for b in range(bars):
        root, kind = prog[b % len(prog)]
        notes = chord(root, kind)
        t0 = b * bar
        # pad
        for k, nn in enumerate(notes):
            add(buf, pad(midi(nn + 12), bar + 0.8, 0.5 * pad_level), t0)
        # electric piano: gentle rolled chords on 1 and the "and" of 2, a light echo on 3
        pat = [(0.0, 0.55, 0), (1.5, 0.4, 1), (2.5, 0.32, 2), (3.0, 0.22, 3)]
        for off, vel, k in pat:
            for i, nn in enumerate(notes):
                add(buf, epiano(midi(nn + 12 * (1 if i < 2 else 0)), beat * 1.6, vel * (0.9 if i else 1.0)), t0 + off * beat + i * 0.018 + (swing * beat if off % 1 else 0))
        # bass
        add(buf, bass(midi(notes[0] - 12), beat * 2.6, 0.62), t0)
        add(buf, bass(midi(notes[0] - 12), beat * 1.2, 0.4), t0 + 2.5 * beat + swing * beat)
        if b % 2:
            add(buf, bass(midi(notes[2] - 12), beat * 0.9, 0.34), t0 + 3.5 * beat + swing * beat)
        # drums
        if drums:
            add(drm, kick(0.6), t0)
            add(drm, kick(0.45), t0 + 2.5 * beat + swing * beat)
            for s in (1, 3):
                add(drm, noise_burst(0.18, 0.05, 2800, 0.22, rng), t0 + s * beat)               # soft brush snare
            for q in range(8):
                v = 0.10 if q % 2 == 0 else 0.06
                add(drm, noise_burst(0.05, 0.012, 0, v, rng, hp=7000), t0 + q * beat / 2 + (swing * beat if q % 2 else 0))
    # --- lead: a phrase of 2 bars that is repeated with small changes
    phrase = []
    for p in range(2):
        for step in range(8):
            if rng.random() < lead_density + (0.25 if step in (0, 4) else 0):
                phrase.append((p * 8 + step, sc[rng.integers(0, len(sc))] + 12 * (1 + lead_oct)))
    for rep in range(bars // 2):
        if rep % 4 == 3:
            continue                                      # let it breathe every fourth phrase
        for pos, nn in phrase:
            if rng.random() < 0.18:
                nn = sc[rng.integers(0, len(sc))] + 12 * (1 + lead_oct)
            tt = rep * 2 * bar + pos * beat / 2 + (swing * beat if pos % 2 else 0)
            add(mel, pluck(midi(nn), beat * 1.5, 0.28), tt)
            if rng.random() < 0.25:
                add(mel, pluck(midi(nn), beat * 1.5, 0.12), tt + beat * 0.75)                  # echo
    mix = buf + mel * 0.9
    mix = reverb(mix, 2.6, rng, 0.28)
    mix[: len(drm)] += drm
    cut = int(length * SR)                                  # whatever rings past the end is mixed onto the start, so the loop is seamless
    tail = mix[cut:]
    mix = mix[:cut].copy()
    mix[: len(tail)] += tail[: len(mix)]
    # stereo width: tiny delay + the reverb already smeared
    d = int(0.011 * SR)
    left = mix + np.concatenate([np.zeros(d), mix[:-d]]) * 0.25
    right = mix + np.concatenate([mix[d:], np.zeros(d)]) * 0.25
    out = np.stack([left, right], axis=1)[: int(length * SR)]
    out = np.tanh(out * 0.9) / 0.9                                            # tape-ish softness
    out *= vol / (np.max(np.abs(out)) + 1e-9) * 0.9
    # short fade at the very ends only protects against a click; the tail is already wrapped onto the start
    f = int(0.01 * SR)
    out[:f] *= np.linspace(0, 1, f)[:, None]; out[-f:] *= np.linspace(1, 0, f)[:, None]
    pcm = (np.clip(out, -1, 1) * 32767).astype(np.int16)
    wav = os.path.join(OUT, name + ".wav")
    with wave.open(wav, "wb") as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())
    ogg = os.path.join(OUT, name + ".ogg")
    subprocess.run(["oggenc", "-q", "4", "-o", ogg, wav, "--quiet", "-t", name, "-a", "Space Agency (generated)"], check=True)
    os.remove(wav)
    print(name, round(length, 1), "s", os.path.getsize(ogg) // 1024, "KB")


C, D, E, F, G, A, B = (NOTE[k] + 48 for k in "CDEFGAB")
PENTA = [0, 2, 4, 7, 9]
MINPENTA = [0, 3, 5, 7, 10]

# 1. warm, sunny base-building loop (C major, 84 bpm)
render("Cozy Base", 84, C, [(C, "maj7"), (A - 12, "m7"), (F - 12, "maj7"), (G - 12, "sus")], PENTA, 32, 11, lead_density=0.38)
# 2. calm and starry, for the night (A minor, 72 bpm, more pads)
render("Starlit Control Room", 72, A - 12, [(A - 12, "m9"), (F - 12, "maj7"), (C, "add9"), (G - 12, "6")], MINPENTA, 28, 23, swing=0.16, lead_density=0.3, pad_level=1.5, lead_oct=0)
# 3. brighter morning launch prep (F major, 96 bpm)
render("Launch Day Morning", 96, F, [(F - 12, "maj7"), (D - 12, "m7"), (G - 12, "m7"), (C, "7")], PENTA, 32, 37, lead_density=0.42, lead_oct=0)
