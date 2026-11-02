"""Tiny synthesizer used to generate every sound and music track in Alba: Secret History.

Standard library only. Renders 16-bit mono WAV files; `build.py` encodes them
as Ogg Vorbis for Unity.
"""

import array
import math
import random
import wave

SAMPLE_RATE = 44100

NOTE_OFFSETS = {"C": -9, "D": -7, "E": -5, "F": -4, "G": -2, "A": 0, "B": 2}


def note_freq(name):
    """Convert a note like 'A4', 'F#5' or 'Bb3' to its frequency in Hz."""
    letter = name[0]
    accidental = 0
    rest = name[1:]
    if rest and rest[0] in "#b":
        accidental = 1 if rest[0] == "#" else -1
        rest = rest[1:]
    octave = int(rest)
    semitones = NOTE_OFFSETS[letter] + accidental + (octave - 4) * 12
    return 440.0 * 2 ** (semitones / 12)


# --- oscillators -----------------------------------------------------------

def sine(phase):
    return math.sin(2 * math.pi * phase)


def square(phase, duty=0.5):
    return 1.0 if (phase % 1.0) < duty else -1.0


def triangle(phase):
    p = phase % 1.0
    return 4 * p - 1 if p < 0.5 else 3 - 4 * p


def saw(phase):
    return 2 * (phase % 1.0) - 1


def whistle(phase):
    """A tin whistle: a sine with a breath of its octave."""
    return 0.86 * sine(phase) + 0.1 * sine(phase * 2) + 0.04 * sine(phase * 3)


def reed(phase):
    """A bagpipe-like reed: bright odd harmonics, softened."""
    return 0.55 * sine(phase) + 0.25 * sine(phase * 3) + 0.12 * sine(phase * 5) + 0.08 * saw(phase)


def bell(phase):
    """A cold bell: inharmonic partials."""
    return 0.6 * sine(phase) + 0.25 * sine(phase * 2.76) + 0.15 * sine(phase * 5.4)


def envelope(t, length, attack=0.005, decay=0.05, sustain=0.7, release=0.05):
    """Classic ADSR envelope; `length` is the note-on time in seconds."""
    if t < attack:
        return t / attack
    if t < attack + decay:
        return 1 - (1 - sustain) * (t - attack) / decay
    if t < length:
        return sustain
    if t < length + release:
        return sustain * (1 - (t - length) / release)
    return 0.0


class Track:
    """A mono buffer that voices are mixed into."""

    def __init__(self, seconds):
        self.samples = [0.0] * int(seconds * SAMPLE_RATE)

    @property
    def seconds(self):
        return len(self.samples) / SAMPLE_RATE

    def add_tone(self, start, length, freq, wave_fn, volume=0.3, freq_end=None,
                 attack=0.005, decay=0.05, sustain=0.7, release=0.05, vibrato=0.0, tremolo=0.0):
        """Mix a pitched tone. `freq_end` sweeps the pitch linearly."""
        freq_end = freq if freq_end is None else freq_end
        total = length + release
        first = int(start * SAMPLE_RATE)
        phase = 0.0
        two_pi = 2 * math.pi
        for i in range(int(total * SAMPLE_RATE)):
            idx = first + i
            if idx >= len(self.samples):
                break
            t = i / SAMPLE_RATE
            f = freq + (freq_end - freq) * min(t / max(length, 1e-6), 1.0)
            if vibrato:
                f *= 1 + vibrato * math.sin(two_pi * 5.2 * t)
            phase += f / SAMPLE_RATE
            env = envelope(t, length, attack, decay, sustain, release)
            if tremolo:
                env *= 1 - tremolo * (0.5 + 0.5 * math.sin(two_pi * 7 * t))
            self.samples[idx] += wave_fn(phase) * env * volume

    def add_noise(self, start, length, volume=0.3, smoothing=0.0, decay_power=1.0, seed=None,
                  attack=0.0, swell=None):
        """Mix a noise burst. Higher `smoothing` (0..1) sounds darker. `swell(t)` scales it over time."""
        rng = random.Random(seed)
        first = int(start * SAMPLE_RATE)
        last = 0.0
        count = int(length * SAMPLE_RATE)
        for i in range(count):
            idx = first + i
            if idx >= len(self.samples):
                break
            value = rng.uniform(-1, 1)
            last = last * smoothing + value * (1 - smoothing)
            env = (1 - i / count) ** decay_power
            t = i / SAMPLE_RATE
            if attack and t < attack:
                env *= t / attack
            if swell:
                env *= swell(t)
            self.samples[idx] += last * env * volume

    def lowpass(self, amount):
        """A one-pole low-pass over the whole track (0 = none, close to 1 = very dark)."""
        last = 0.0
        out = self.samples
        for i, s in enumerate(out):
            last = last * amount + s * (1 - amount)
            out[i] = last

    def echo(self, delay, feedback=0.35, mix=0.4):
        """A simple feedback delay for the sense of a big, empty place."""
        offset = int(delay * SAMPLE_RATE)
        buffer = list(self.samples)
        for i in range(offset, len(buffer)):
            buffer[i] += buffer[i - offset] * feedback
        self.samples = [d * (1 - mix) + w * mix for d, w in zip(self.samples, buffer)]

    def normalize(self, peak=0.89):
        top = max(abs(s) for s in self.samples) or 1.0
        gain = peak / top
        self.samples = [s * gain for s in self.samples]

    def fade(self, fade_in=0.0, fade_out=0.0):
        n = len(self.samples)
        fi = int(fade_in * SAMPLE_RATE)
        fo = int(fade_out * SAMPLE_RATE)
        for i in range(min(fi, n)):
            self.samples[i] *= i / fi
        for i in range(min(fo, n)):
            self.samples[n - 1 - i] *= i / fo

    def loop_seam(self, seconds):
        """Crossfades the tail into the head so the track loops without a click."""
        n = int(seconds * SAMPLE_RATE)
        head = self.samples[:n]
        tail = self.samples[-n:]
        for i in range(n):
            t = i / n
            head[i] = head[i] * t + tail[i] * (1 - t)
        self.samples = head + self.samples[n:-n]

    def write(self, path):
        data = array.array("h", (int(max(-1.0, min(1.0, s)) * 32767) for s in self.samples))
        with wave.open(path, "wb") as wav:
            wav.setnchannels(1)
            wav.setsampwidth(2)
            wav.setframerate(SAMPLE_RATE)
            wav.writeframes(data.tobytes())
