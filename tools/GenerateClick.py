"""Generate a quiet, low-passed 55 ms copy tap; no system alert samples."""
import math
import random
import struct
import wave
from pathlib import Path

rate = 44100
count = round(rate * 0.055)
randomizer = random.Random(0xAC0C0)
filtered = filtered_twice = 0.0
samples = []
for index in range(count):
    time = index / rate
    filtered += 0.14 * (randomizer.uniform(-1, 1) - filtered)
    filtered_twice += 0.14 * (filtered - filtered_twice)
    envelope = (1 - math.exp(-time / 0.0014)) * math.exp(-time / 0.0075)
    envelope *= (1 - (index / (count - 1)) ** 4)
    samples.append((0.85 * filtered_twice + 0.15 * math.sin(2 * math.pi * 380 * time)) * envelope)
gain = 0.06 / max(abs(value) for value in samples)
destination = Path(__file__).resolve().parents[1] / "assets" / "copy-click.wav"
destination.parent.mkdir(exist_ok=True)
with wave.open(str(destination), "wb") as output:
    output.setparams((1, 2, rate, count, "NONE", "not compressed"))
    output.writeframes(b"".join(struct.pack("<h", round(value * gain * 32767)) for value in samples))
print(f"Generated {destination.name}: {count / rate * 1000:.0f} ms, peak -24.4 dBFS")
