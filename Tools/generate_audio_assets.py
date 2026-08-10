#!/usr/bin/env python3
"""Birthday Tactics向けの独自BGM/SFXをPCM WAVとして生成する。"""

from __future__ import annotations

import math
import pathlib
import random
import struct
import wave


ROOT = pathlib.Path(__file__).resolve().parent.parent
AUDIO = ROOT / "Assets/Resources/Audio"
SFX = AUDIO / "SFX"
RATE = 44100
TAU = math.tau


def midi(note: int) -> float:
    return 440.0 * 2.0 ** ((note - 69) / 12.0)


def oscillator(kind: str, phase: float) -> float:
    if kind == "triangle":
        return 2.0 * abs(2.0 * ((phase / TAU) % 1.0) - 1.0) - 1.0
    if kind == "square":
        return 1.0 if math.sin(phase) >= 0.0 else -1.0
    return math.sin(phase)


def add_note(buffer, start, duration, note, gain, kind="sine", pan=0.0):
    first = max(0, int(start * RATE))
    count = min(len(buffer) - first, int(duration * RATE))
    frequency = midi(note)
    for index in range(max(0, count)):
        t = index / RATE
        position = index / max(1, count - 1)
        attack = min(1.0, position / 0.045)
        release = min(1.0, (1.0 - position) / 0.16)
        envelope = attack * release * (0.78 + 0.22 * math.sin(math.pi * position))
        phase = TAU * frequency * t
        value = oscillator(kind, phase) * gain * envelope
        left = value * (1.0 - max(0.0, pan))
        right = value * (1.0 + min(0.0, pan))
        old_left, old_right = buffer[first + index]
        buffer[first + index] = (old_left + left, old_right + right)


def add_drum(buffer, start, gain, seed):
    rng = random.Random(seed)
    first = int(start * RATE)
    count = min(len(buffer) - first, int(0.12 * RATE))
    for index in range(max(0, count)):
        t = index / RATE
        envelope = math.exp(-34.0 * t)
        tone = math.sin(TAU * (94.0 - 42.0 * t) * t)
        noise = rng.uniform(-1.0, 1.0)
        value = (tone * 0.72 + noise * 0.28) * envelope * gain
        left, right = buffer[first + index]
        buffer[first + index] = (left + value, right + value)


def compose(track_id, bpm, roots, melody, meter=4, lead="triangle", drums=False):
    beat = 60.0 / bpm
    duration = len(roots) * meter * beat
    buffer = [(0.0, 0.0) for _ in range(round(duration * RATE))]
    for bar, root in enumerate(roots):
        bar_start = bar * meter * beat
        for chord_note, gain, pan in ((root, 0.11, -0.25), (root + 7, 0.065, 0.25), (root + 12, 0.045, 0.0)):
            add_note(buffer, bar_start, meter * beat * 0.94, chord_note, gain, "sine", pan)
        for pulse in range(meter * 2):
            add_note(buffer, bar_start + pulse * beat * 0.5, beat * 0.42,
                     root - 12 + (7 if pulse % 2 else 0), 0.075, "triangle", -0.15)
        for pulse in range(meter):
            note = melody[(bar * meter + pulse) % len(melody)]
            add_note(buffer, bar_start + pulse * beat, beat * 0.82, note, 0.135, lead, 0.18)
            if drums:
                add_drum(buffer, bar_start + pulse * beat, 0.075 if pulse else 0.13, bar * 17 + pulse)
    write_wav(AUDIO / f"{track_id}.wav", buffer)


def write_wav(path: pathlib.Path, frames):
    path.parent.mkdir(parents=True, exist_ok=True)
    peak = max(0.001, max(max(abs(left), abs(right)) for left, right in frames))
    scale = min(0.94 / peak, 1.0)
    with wave.open(str(path), "wb") as output:
        output.setnchannels(2)
        output.setsampwidth(2)
        output.setframerate(RATE)
        payload = bytearray()
        for left, right in frames:
            payload.extend(struct.pack("<hh",
                round(max(-1.0, min(1.0, left * scale)) * 32767),
                round(max(-1.0, min(1.0, right * scale)) * 32767)))
        output.writeframes(payload)


def make_sfx(name, duration, sampler):
    frames = []
    for index in range(round(duration * RATE)):
        t = index / RATE
        position = index / max(1, round(duration * RATE) - 1)
        value = sampler(t, position) * (1.0 - position) ** 1.6
        frames.append((value * 0.92, value))
    write_wav(SFX / f"{name}.wav", frames)


def main():
    AUDIO.mkdir(parents=True, exist_ok=True)
    SFX.mkdir(parents=True, exist_ok=True)
    compose("BD-01", 96, [48, 43, 45, 40, 48, 45, 43, 48],
            [64, 67, 69, 72, 71, 69, 67, 64, 62, 64, 67, 69, 72, 71, 67, 64], meter=3)
    compose("BD-02", 148, [45, 45, 41, 43, 45, 48, 41, 43],
            [69, 72, 76, 74, 69, 77, 76, 72, 67, 69, 72, 74, 76, 74, 72, 69], drums=True)
    compose("BD-03", 74, [50, 45, 47, 43, 50, 48, 45, 50],
            [62, 65, 69, 67, 65, 62, 60, 62, 69, 72, 71, 67, 65, 64, 62, 60], meter=3)
    compose("BD-04", 120, [43, 46, 41, 43, 48, 46, 41, 43],
            [67, 70, 74, 72, 67, 75, 74, 70, 65, 67, 70, 72, 74, 72, 70, 67], lead="square", drums=True)
    compose("BD-05", 88, [48, 52, 53, 55, 48, 45, 55, 48],
            [72, 76, 79, 76, 74, 77, 81, 77, 72, 74, 76, 79, 81, 79, 76, 72], meter=3)

    make_sfx("select", 0.11, lambda t, p: math.sin(TAU * (620 + 420 * p) * t) * 0.34)
    make_sfx("move", 0.09, lambda t, p: math.sin(TAU * (330 - 90 * p) * t) * (0.28 + 0.12 * math.sin(TAU * 33 * t)))
    make_sfx("attack", 0.24, lambda t, p: (math.sin(TAU * (190 - 80 * p) * t) + 0.45 * math.sin(TAU * 1040 * t)) * 0.28)
    make_sfx("victory", 0.72, lambda t, p: math.sin(TAU * midi([72, 76, 79, 84][min(3, int(p * 4))]) * t) * 0.32)
    make_sfx("defeat", 0.62, lambda t, p: math.sin(TAU * midi([57, 53, 50, 45][min(3, int(p * 4))]) * t) * 0.32)
    make_sfx("gift", 0.88, lambda t, p: (math.sin(TAU * midi([72, 76, 79, 84, 88][min(4, int(p * 5))]) * t) + 0.35 * math.sin(TAU * 1320 * t)) * 0.25)
    for path in sorted(AUDIO.rglob("*.wav")):
        print(path.relative_to(ROOT).as_posix())


if __name__ == "__main__":
    main()
