"""Original music and ambience loops for the game, in Scottish modes over a pipe-like drone.

Melodies are 'NOTE:EIGHTHS' tokens ('R' is a rest), played on a tin whistle or a reed.
Drums are one character per eighth: B bodhran stroke, b soft stroke, t rim tap, '.' rest.
Every track is cross-faded at its seam so Unity can loop it.
"""

import math
import random

from synth import Track, bell, note_freq, reed, sine, whistle


def parse(melody):
    notes = []
    for token in melody.split():
        name, length = token.split(":")
        notes.append((None if name == "R" else name, int(length)))
    return notes


def bodhran(track, at, volume, seed):
    track.add_tone(at, 0.12, 95, sine, volume, freq_end=55, release=0.1)
    track.add_noise(at, 0.06, volume * 0.3, smoothing=0.75, decay_power=2.0, seed=seed)


def tap(track, at, seed):
    track.add_noise(at, 0.03, 0.08, smoothing=0.3, decay_power=2.2, seed=seed)


def drone(track, notes, volume, length, swell=0.0):
    for n in notes:
        track.add_tone(0, length, note_freq(n), reed, volume, attack=1.5, release=1.5, vibrato=0.002,
                       tremolo=swell)


def render(song):
    eighth = 60.0 / song["bpm"] / 2
    bars = [bar for section in song["form"] for bar in song["sections"][section]]
    steps = 8
    body = len(bars) * steps * eighth
    seam = 1.5
    track = Track(body + seam)

    drone(track, song["drone"], song["drone_volume"], body + seam - 1.5, song.get("swell", 0.0))
    instrument = whistle if song.get("lead", "whistle") == "whistle" else reed

    for bar_index, melody in enumerate(bars):
        bar_start = bar_index * steps * eighth
        notes = parse(melody)
        assert sum(length for _, length in notes) == steps, f"bar {bar_index} is not {steps} eighths: {melody}"
        cursor = 0
        for name, length in notes:
            if name:
                at = bar_start + cursor * eighth
                track.add_tone(at, length * eighth * 0.9, note_freq(name), instrument, song["lead_volume"],
                               attack=0.02, decay=0.06, sustain=0.8, release=0.1,
                               vibrato=0.007 if length >= 3 else 0.0)
                # A quick grace note above longer notes, as a piper would play it.
                if length >= 3 and song.get("grace"):
                    track.add_tone(at - 0.03, 0.025, note_freq(name) * 2 ** (5 / 12), instrument,
                                   song["lead_volume"] * 0.6, attack=0.003, release=0.01)
            cursor += length
        for step, kind in enumerate(song["drums"]):
            at = bar_start + step * eighth
            seed = bar_index * 31 + step
            if kind == "B":
                bodhran(track, at, song["drum_volume"], seed)
            elif kind == "b":
                bodhran(track, at, song["drum_volume"] * 0.55, seed)
            elif kind == "t":
                tap(track, at, seed)

    if song.get("bells"):
        rng = random.Random(song["seed"])
        scale = song["bells"]
        at = 1.0
        while at < body - 1:
            track.add_tone(at, 0.2, note_freq(rng.choice(scale)), bell, song["bell_volume"], release=2.0)
            at += rng.uniform(2.5, 5.5)

    track.echo(song.get("echo", 0.3), 0.35, song.get("echo_mix", 0.3))
    track.lowpass(song.get("dark", 0.2))
    track.loop_seam(seam)
    track.normalize(0.8)
    return track


def rain():
    """Steady rain on heather, with drops close by."""
    t = Track(10.0 + 1.0)
    t.add_noise(0, t.seconds, 0.35, smoothing=0.55, decay_power=0.0, seed=200)
    t.add_noise(0, t.seconds, 0.3, smoothing=0.93, decay_power=0.0, seed=201)
    rng = random.Random(202)
    for _ in range(140):
        at = rng.uniform(0, t.seconds - 0.1)
        t.add_noise(at, 0.02, rng.uniform(0.05, 0.18), smoothing=0.3, decay_power=2.0, seed=rng.randint(0, 9999))
    t.loop_seam(1.0)
    t.normalize(0.6)
    return t


def wind():
    """Wind over the moor and through the pines, rising and falling."""
    t = Track(12.0 + 1.5)
    length = t.seconds
    t.add_noise(0, length, 0.5, smoothing=0.97, decay_power=0.0, seed=210,
                swell=lambda x: 0.55 + 0.45 * math.sin(x / length * 2 * math.pi * 2))
    t.add_noise(0, length, 0.2, smoothing=0.9, decay_power=0.0, seed=211,
                swell=lambda x: 0.5 + 0.5 * math.sin(x / length * 2 * math.pi * 3 + 1.0))
    t.loop_seam(1.5)
    t.normalize(0.6)
    return t


# A Aeolian (A B C D E F G): the slow air of the title.
TITLE = [
    "A4:3 B4:1 C5:2 E5:2", "D5:4 C5:2 B4:2", "A4:3 G4:1 E4:4", "A4:6 R:2",
    "C5:3 D5:1 E5:2 G5:2", "A5:4 G5:2 E5:2", "D5:3 C5:1 B4:2 G4:2", "A4:6 R:2",
]

# D Dorian over a low drone for the night chapters: a sparse, wandering line.
NIGHT_A = [
    "D4:6 R:2", "F4:3 E4:1 D4:4", "R:8", "A4:3 G4:1 F4:2 E4:2",
    "D4:8", "R:4 C4:2 D4:2", "F4:6 E4:2", "D4:6 R:2",
]
NIGHT_B = [
    "A4:4 C5:4", "B4:3 A4:1 G4:4", "R:8", "F4:3 G4:1 A4:4",
    "D5:6 C5:2", "A4:4 G4:4", "F4:3 E4:1 C4:4", "D4:6 R:2",
]

# E minor for the keep: a driving reed riff.
KEEP_A = [
    "E4:1 E4:1 G4:1 E4:1 B4:2 A4:2", "G4:1 A4:1 G4:1 E4:1 D4:4",
    "E4:1 E4:1 G4:1 E4:1 B4:2 D5:2", "B4:2 A4:2 E4:4",
]
KEEP_B = [
    "B4:2 D5:2 E5:4", "D5:1 B4:1 A4:2 G4:4",
    "A4:2 B4:2 D5:2 B4:2", "A4:2 G4:2 E4:4",
]

# A Mixolydian for the dawn: brighter, a reel over the drums.
DAWN_A = [
    "A4:1 B4:1 C#5:1 E5:1 A5:2 E5:2", "F#5:1 E5:1 C#5:1 B4:1 A4:4",
    "G4:1 A4:1 B4:1 D5:1 G5:2 D5:2", "E5:1 D5:1 B4:1 G4:1 A4:4",
]
DAWN_B = [
    "E5:2 A5:2 G5:1 F#5:1 E5:2", "D5:2 F#5:2 E5:4",
    "C#5:1 D5:1 E5:1 F#5:1 G5:2 E5:2", "D5:1 C#5:1 B4:1 G4:1 A4:4",
]

SONGS = {
    "title_theme.ogg": {
        "bpm": 66, "form": "AA", "sections": {"A": TITLE}, "drone": ["A2", "E3"], "drone_volume": 0.12,
        "lead_volume": 0.3, "drums": "B.......", "drum_volume": 0.35, "grace": True, "echo": 0.36, "echo_mix": 0.38,
        "swell": 0.2, "dark": 0.25,
    },
    "night_theme.ogg": {
        "bpm": 56, "form": "AB", "sections": {"A": NIGHT_A, "B": NIGHT_B}, "drone": ["D2", "A2"], "drone_volume": 0.14,
        "lead_volume": 0.18, "drums": "B...b...", "drum_volume": 0.4, "bells": ["D5", "F5", "A5", "C6", "E5"],
        "bell_volume": 0.08, "seed": 5, "echo": 0.42, "echo_mix": 0.42, "swell": 0.35, "dark": 0.45,
    },
    "keep_theme.ogg": {
        "bpm": 104, "form": "AABA", "sections": {"A": KEEP_A, "B": KEEP_B}, "drone": ["E2", "B2"], "drone_volume": 0.12,
        "lead_volume": 0.22, "lead": "reed", "drums": "B.tbB.bt", "drum_volume": 0.55, "echo": 0.29, "echo_mix": 0.25,
        "dark": 0.3,
    },
    "dawn_theme.ogg": {
        "bpm": 112, "form": "AABA", "sections": {"A": DAWN_A, "B": DAWN_B}, "drone": ["A2", "E3"], "drone_volume": 0.11,
        "lead_volume": 0.28, "drums": "B.tbB.tb", "drum_volume": 0.5, "grace": True, "echo": 0.27, "echo_mix": 0.22,
        "dark": 0.15,
    },
}

AMBIENCE = {
    "rain.ogg": rain,
    "wind.ogg": wind,
}

