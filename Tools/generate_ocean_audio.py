"""Generate a seamless, original pirate-ocean ambience loop as a WAV file."""

from __future__ import annotations

import argparse
import math
import random
import wave
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("output", type=Path)
    parser.add_argument("--seconds", type=float, default=24.0)
    args = parser.parse_args()

    sample_rate = 22050
    frame_count = int(sample_rate * args.seconds)
    rng = random.Random(3084)
    args.output.parent.mkdir(parents=True, exist_ok=True)

    # Multiple slow oscillators make the surf swell; filtered noise supplies wind/water.
    filtered = 0.0
    frames = bytearray()
    for i in range(frame_count):
        t = i / sample_rate
        white = rng.uniform(-1.0, 1.0)
        filtered = 0.992 * filtered + 0.008 * white
        swell = 0.50 + 0.24 * math.sin(2 * math.pi * t / 6.0) + 0.12 * math.sin(2 * math.pi * t / 3.0)
        surf = filtered * swell
        wind = 0.035 * white * (0.6 + 0.4 * math.sin(2 * math.pi * t / 8.0))
        creak = 0.0
        for start in (3.0, 9.0, 15.0, 21.0):
            dt = t - start
            if 0 <= dt < 1.3:
                creak += 0.07 * math.sin(2 * math.pi * (115 - 38 * dt) * dt) * math.exp(-2.2 * dt)
        low_horn = 0.018 * math.sin(2 * math.pi * 55 * t) * (0.5 + 0.5 * math.sin(2 * math.pi * t / 12.0))
        sample = max(-1.0, min(1.0, 0.72 * surf + wind + creak + low_horn))
        value = int(sample * 32767)
        frames.extend(value.to_bytes(2, "little", signed=True))
        frames.extend(value.to_bytes(2, "little", signed=True))

    with wave.open(str(args.output), "wb") as wav:
        wav.setnchannels(2)
        wav.setsampwidth(2)
        wav.setframerate(sample_rate)
        wav.writeframes(frames)
    print(f"Generated {args.seconds:.1f}s ocean ambience: {args.output}")


if __name__ == "__main__":
    main()
