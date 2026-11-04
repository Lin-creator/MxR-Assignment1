"""Synthesises the printer sound effects (16-bit mono WAV, 44.1 kHz). Standard library only.

Usage (from repo root):  python3 Tools/Audio/generate_sounds.py Assets/Audio

- printer_motor_loop.wav  2 s seamless loop: stepper-motor whine (all frequencies complete whole
                          cycles in 2 s, so the loop has no click at the seam)
- beep_start.wav          rising two-tone beep (print started / resumed)
- beep_pause.wav          falling two-tone beep (print paused)
- chime_done.wav          three-note major chord arpeggio (print finished)
- move_pick.wav           short rising sweep (sign / printer picked up with the ray)
- move_drop.wav           short falling sweep + soft thud (sign / printer put down)
"""
import math
import os
import random
import struct
import sys
import wave

RATE = 44100


def write(path, samples):
    peak = max(1e-9, max(abs(s) for s in samples))
    gain = 0.8 / peak
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s * gain * 32767)) for s in samples))
    print("wrote", path, f"{len(samples) / RATE:.2f}s")


def envelope(i, n, attack=0.005, release=0.04):
    t, d = i / RATE, n / RATE
    return min(1.0, t / attack, max(0.0, (d - t) / release))


def tone(freq, dur, harmonics=(1.0, 0.3, 0.1)):
    n = int(dur * RATE)
    return [envelope(i, n) * sum(a * math.sin(2 * math.pi * freq * (k + 1) * i / RATE)
                                 for k, a in enumerate(harmonics)) for i in range(n)]


def silence(dur):
    return [0.0] * int(dur * RATE)


def sweep(f0, f1, dur, harmonics=(1.0, 0.25)):
    """Tone whose pitch glides exponentially from f0 to f1."""
    n = int(dur * RATE)
    out, phase = [], 0.0
    for i in range(n):
        f = f0 * (f1 / f0) ** (i / n)
        phase += 2 * math.pi * f / RATE
        out.append(envelope(i, n, 0.004, 0.03) * sum(a * math.sin(phase * (k + 1)) for k, a in enumerate(harmonics)))
    return out


def thud(dur=0.08, freq=120):
    n = int(dur * RATE)
    return [math.exp(-i / (0.015 * RATE)) * math.sin(2 * math.pi * freq * i / RATE) for i in range(n)]


def motor_loop(dur=2.0):
    rnd = random.Random(42)
    n = int(dur * RATE)
    out = []
    for i in range(n):
        t = i / RATE
        # stepper whine: odd harmonics of 220 Hz (square-ish) + higher partial, wobbling at 3 Hz
        whine = sum(math.sin(2 * math.pi * 220 * h * t) / h for h in (1, 3, 5, 7))
        whine += 0.35 * math.sin(2 * math.pi * 1320 * t)
        wobble = 0.75 + 0.25 * math.sin(2 * math.pi * 3 * t)
        out.append(0.6 * whine * wobble + 0.08 * (rnd.random() * 2 - 1))  # + fan noise
    return out


def main(out_dir):
    os.makedirs(out_dir, exist_ok=True)
    write(os.path.join(out_dir, "printer_motor_loop.wav"), motor_loop())
    write(os.path.join(out_dir, "beep_start.wav"), tone(880, 0.09) + silence(0.03) + tone(1320, 0.12))
    write(os.path.join(out_dir, "beep_pause.wav"), tone(1320, 0.09) + silence(0.03) + tone(880, 0.12))
    chime = []
    for f in (1046.5, 1318.5, 1568.0):  # C6 E6 G6
        chime += tone(f, 0.16, (1.0, 0.5, 0.2))
    chime += tone(2093.0, 0.45, (1.0, 0.4, 0.1))  # C7
    write(os.path.join(out_dir, "chime_done.wav"), chime)
    write(os.path.join(out_dir, "move_pick.wav"), sweep(500, 1100, 0.09))
    drop = sweep(900, 420, 0.08)
    write(os.path.join(out_dir, "move_drop.wav"), [d + 0.9 * t for d, t in zip(drop + [0.0] * 3000, thud() + [0.0] * 10000)])


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "Assets/Audio")
