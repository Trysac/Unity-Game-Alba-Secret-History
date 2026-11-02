"""Sound effect recipes. Each function returns a rendered Track."""

import math

from synth import Track, bell, note_freq, reed, saw, sine, square, triangle, whistle


def gun(body_freq, length, volume, seed, crack=0.5, tail=0.5):
    t = Track(length + 0.3)
    t.add_noise(0, 0.03, crack, smoothing=0.0, decay_power=1.5, seed=seed)
    t.add_noise(0, length, volume, smoothing=0.75, decay_power=2.6, seed=seed + 1)
    t.add_tone(0, length * 0.5, body_freq, sine, volume * 0.9, freq_end=body_freq * 0.35, decay=length * 0.4,
               sustain=0.2, release=0.08)
    t.add_noise(0.04, length * 1.6, volume * tail * 0.25, smoothing=0.93, decay_power=1.4, seed=seed + 2)
    return t


def pistol():
    """A sharp revolver crack."""
    return gun(180, 0.22, 0.6, 10)


def shotgun():
    """A heavy boom with a long, dark tail."""
    t = gun(110, 0.45, 0.8, 20, crack=0.6, tail=0.9)
    t.add_tone(0.32, 0.03, 900, triangle, 0.12, release=0.02)
    t.add_noise(0.32, 0.04, 0.12, smoothing=0.3, decay_power=2, seed=23)
    t.add_tone(0.42, 0.03, 700, triangle, 0.12, release=0.02)
    return t


def rifle():
    """A short, punchy rifle round for automatic fire."""
    return gun(220, 0.13, 0.55, 30, crack=0.45, tail=0.3)


def dirk():
    """A blade swishing through the air."""
    t = Track(0.3)
    t.add_noise(0, 0.22, 0.35, smoothing=0.55, decay_power=0.8, seed=40, attack=0.06,
                swell=lambda x: math.sin(min(x / 0.22, 1) * math.pi))
    t.add_tone(0, 0.15, 1800, sine, 0.04, freq_end=900, release=0.05)
    return t


def dry_fire():
    """The hammer falls on nothing."""
    t = Track(0.12)
    t.add_tone(0, 0.012, 2400, triangle, 0.25, release=0.01)
    t.add_noise(0, 0.03, 0.2, smoothing=0.2, decay_power=2.5, seed=41)
    t.add_tone(0.05, 0.01, 1800, triangle, 0.15, release=0.01)
    return t


def hit():
    """A bullet into something soft."""
    t = Track(0.16)
    t.add_noise(0, 0.08, 0.5, smoothing=0.82, decay_power=2.0, seed=42)
    t.add_tone(0, 0.06, 160, sine, 0.4, freq_end=70, release=0.04)
    return t


def ricochet():
    """A pellet off stone or bark."""
    t = Track(0.3)
    t.add_noise(0, 0.03, 0.25, smoothing=0.3, decay_power=2.0, seed=43)
    t.add_tone(0.01, 0.2, 2600, sine, 0.08, freq_end=1300, decay=0.1, sustain=0.3, release=0.06)
    return t


def immune():
    """A shot passes through a wisp: a hollow, mocking breath."""
    t = Track(0.6)
    t.add_noise(0, 0.5, 0.2, smoothing=0.9, decay_power=0.8, seed=44, attack=0.08)
    t.add_tone(0, 0.4, note_freq("E5"), sine, 0.12, freq_end=note_freq("B4"), attack=0.05, release=0.15, vibrato=0.03)
    return t


def crawler_alert():
    """A crawler notices you: a dry chitter."""
    t = Track(0.55)
    for i in range(9):
        t.add_noise(i * 0.045, 0.03, 0.35 - i * 0.02, smoothing=0.4, decay_power=2.2, seed=50 + i)
        t.add_tone(i * 0.045, 0.02, 1100 + 140 * (i % 3), square, 0.05, release=0.01)
    t.add_tone(0, 0.4, 200, saw, 0.12, freq_end=150, attack=0.05, release=0.1)
    return t


def crawler_windup():
    """A rising hiss before the claws come down."""
    t = Track(0.45)
    t.add_noise(0, 0.38, 0.35, smoothing=0.25, decay_power=0.3, seed=60, attack=0.3)
    t.add_tone(0, 0.36, 300, saw, 0.12, freq_end=520, attack=0.2, release=0.05)
    return t


def crawler_die():
    """A shrill shriek that collapses."""
    t = Track(0.8)
    t.add_tone(0, 0.5, 900, saw, 0.25, freq_end=180, decay=0.4, sustain=0.5, release=0.2, vibrato=0.05)
    t.add_noise(0, 0.6, 0.25, smoothing=0.6, decay_power=1.4, seed=61)
    return t


def wisp_alert():
    """A wisp turns towards you: a whispering chord."""
    t = Track(1.2)
    for i, n in enumerate(["E5", "G5", "B5"]):
        t.add_tone(i * 0.08, 0.6, note_freq(n), sine, 0.12, attack=0.2, release=0.4, vibrato=0.02, tremolo=0.5)
    t.add_noise(0, 1.0, 0.12, smoothing=0.92, decay_power=1.0, seed=62, attack=0.3)
    t.echo(0.18, 0.4, 0.4)
    return t


def wisp_die():
    """A wisp burns away."""
    t = Track(1.0)
    t.add_tone(0, 0.6, note_freq("B5"), sine, 0.2, freq_end=note_freq("B6"), release=0.3, tremolo=0.6)
    t.add_noise(0, 0.7, 0.25, smoothing=0.4, decay_power=1.6, seed=63)
    t.echo(0.12, 0.35, 0.35)
    return t


def brute_roar():
    """Am Fomhair bellows."""
    t = Track(1.6)
    t.add_tone(0, 1.1, 75, saw, 0.45, freq_end=55, attack=0.1, release=0.4, vibrato=0.04)
    t.add_tone(0, 1.1, 113, square, 0.15, freq_end=80, attack=0.1, release=0.4, vibrato=0.05)
    t.add_noise(0, 1.3, 0.35, smoothing=0.8, decay_power=0.9, seed=70, attack=0.15)
    t.lowpass(0.55)
    return t


def brute_windup():
    """The giant draws breath and raises its fists."""
    t = Track(0.9)
    t.add_noise(0, 0.8, 0.3, smoothing=0.85, decay_power=0.2, seed=71, attack=0.6)
    t.add_tone(0, 0.8, 60, saw, 0.3, freq_end=95, attack=0.5, release=0.1)
    t.lowpass(0.4)
    return t


def brute_slam():
    """Fists into the earth."""
    t = Track(1.0)
    t.add_tone(0, 0.4, 70, sine, 0.9, freq_end=30, release=0.3)
    t.add_noise(0, 0.6, 0.6, smoothing=0.88, decay_power=1.8, seed=72)
    t.add_noise(0.02, 0.25, 0.25, smoothing=0.3, decay_power=2.0, seed=73)
    return t


def brute_die():
    """The giant falls."""
    t = Track(2.2)
    t.add_tone(0, 1.2, 90, saw, 0.4, freq_end=35, attack=0.05, release=0.5, vibrato=0.05)
    t.add_tone(1.0, 0.5, 50, sine, 0.9, freq_end=25, release=0.5)
    t.add_noise(1.0, 1.0, 0.6, smoothing=0.9, decay_power=1.5, seed=74)
    t.lowpass(0.5)
    return t


def hurt():
    """Something tears at you."""
    t = Track(0.35)
    t.add_noise(0, 0.12, 0.5, smoothing=0.6, decay_power=1.8, seed=80)
    t.add_tone(0, 0.18, 220, saw, 0.25, freq_end=120, release=0.08)
    t.lowpass(0.3)
    return t


def heartbeat():
    """Two thumps of a racing heart."""
    t = Track(0.7)
    for at, v in ((0, 0.9), (0.2, 0.6)):
        t.add_tone(at, 0.07, 60, sine, v, freq_end=40, release=0.06)
    return t


def death():
    """Everything drops away."""
    t = Track(2.4)
    t.add_tone(0, 1.8, note_freq("A3"), saw, 0.2, freq_end=note_freq("A1"), attack=0.02, release=0.5)
    t.add_tone(0, 1.8, note_freq("E4"), saw, 0.12, freq_end=note_freq("E2"), attack=0.02, release=0.5)
    t.add_noise(0, 2.0, 0.2, smoothing=0.95, decay_power=0.8, seed=81)
    t.lowpass(0.6)
    t.echo(0.25, 0.4, 0.4)
    return t


def potion():
    """A gulp from the flask and a warm chime."""
    t = Track(0.8)
    for i in range(3):
        t.add_tone(i * 0.09, 0.06, 240 + i * 30, sine, 0.3, freq_end=140, release=0.03)
    for i, n in enumerate(["A5", "C#6", "E6"]):
        t.add_tone(0.3 + i * 0.06, 0.2, note_freq(n), whistle, 0.15, release=0.2)
    return t


def orb():
    """A small cold chime."""
    t = Track(0.6)
    t.add_tone(0, 0.08, note_freq("E6"), bell, 0.25, decay=0.08, sustain=0.4, release=0.4)
    t.add_tone(0.06, 0.08, note_freq("B6"), bell, 0.18, decay=0.08, sustain=0.4, release=0.35)
    return t


def gem():
    """A sealstone: a deep bell and a bright one."""
    t = Track(1.8)
    t.add_tone(0, 0.2, note_freq("A3"), bell, 0.35, decay=0.2, sustain=0.4, release=1.2)
    for i, n in enumerate(["E5", "A5", "C6", "E6"]):
        t.add_tone(0.08 + i * 0.07, 0.15, note_freq(n), bell, 0.15, decay=0.1, sustain=0.4, release=0.9)
    t.echo(0.2, 0.3, 0.3)
    return t


def page():
    """A journal page: paper and a soft whistle phrase."""
    t = Track(1.2)
    t.add_noise(0, 0.25, 0.25, smoothing=0.2, decay_power=1.2, seed=90, attack=0.04)
    for i, n in enumerate(["E5", "G5", "A5"]):
        t.add_tone(0.2 + i * 0.16, 0.16, note_freq(n), whistle, 0.2, attack=0.02, release=0.2, vibrato=0.01)
    return t


def ammo():
    """Rounds and a box lid."""
    t = Track(0.3)
    for i, f in enumerate((1700, 2100, 1500)):
        t.add_tone(i * 0.05, 0.015, f, triangle, 0.2, release=0.02)
        t.add_noise(i * 0.05, 0.02, 0.15, smoothing=0.2, decay_power=2, seed=91 + i)
    return t


def lantern():
    """A lantern catches: a soft whoomp of flame and a bell."""
    t = Track(1.6)
    t.add_noise(0, 0.5, 0.45, smoothing=0.88, decay_power=1.0, seed=95, attack=0.08)
    t.add_tone(0.1, 0.3, note_freq("D5"), bell, 0.2, release=1.0)
    t.add_tone(0.2, 0.3, note_freq("A5"), bell, 0.15, release=1.0)
    t.echo(0.22, 0.3, 0.3)
    return t


def portal_open():
    """The stones open a road: a long rising shimmer."""
    t = Track(3.0)
    t.add_tone(0, 2.2, note_freq("A2"), reed, 0.25, attack=0.6, release=0.6)
    t.add_tone(0, 2.2, note_freq("E3"), reed, 0.15, attack=0.6, release=0.6)
    for i, n in enumerate(["A4", "C#5", "E5", "A5", "C#6", "E6"]):
        t.add_tone(0.2 + i * 0.25, 0.4, note_freq(n), bell, 0.12, release=0.9)
    t.add_noise(0, 2.6, 0.08, smoothing=0.5, decay_power=0.6, seed=96, attack=1.0)
    t.echo(0.3, 0.35, 0.35)
    return t


def escape():
    """Stepping through: a whoosh that rises into light."""
    t = Track(2.2)
    t.add_noise(0, 1.4, 0.45, smoothing=0.6, decay_power=0.8, seed=97, attack=0.4,
                swell=lambda x: math.sin(min(x / 1.4, 1) * math.pi))
    t.add_tone(0, 1.4, 200, sine, 0.25, freq_end=1200, attack=0.3, release=0.5)
    t.echo(0.18, 0.35, 0.35)
    return t


def torch():
    """The torch switch."""
    t = Track(0.08)
    t.add_tone(0, 0.008, 3200, triangle, 0.3, release=0.008)
    t.add_noise(0, 0.015, 0.2, smoothing=0.1, decay_power=2, seed=98)
    return t


def torch_empty():
    """The torch dies: a falling buzz."""
    t = Track(0.6)
    t.add_tone(0, 0.45, 120, square, 0.12, freq_end=60, release=0.1, tremolo=0.8)
    return t


def step():
    """A footstep in wet heather."""
    t = Track(0.16)
    t.add_noise(0, 0.11, 0.35, smoothing=0.7, decay_power=1.6, seed=99)
    t.add_tone(0, 0.04, 110, sine, 0.15, freq_end=70, release=0.03)
    return t


def wave():
    """A war horn across the cliffs: another wave comes."""
    t = Track(2.0)
    t.add_tone(0, 1.2, note_freq("D3"), reed, 0.4, freq_end=note_freq("D3") * 0.97, attack=0.15, release=0.5, vibrato=0.01)
    t.add_tone(0, 1.2, note_freq("A3"), reed, 0.2, attack=0.2, release=0.5, vibrato=0.01)
    t.lowpass(0.4)
    t.echo(0.35, 0.4, 0.4)
    return t


def dawn():
    """The sun clears the sea."""
    t = Track(4.0)
    for i, n in enumerate(["A3", "E4", "A4", "C#5", "E5", "A5"]):
        t.add_tone(i * 0.2, 2.2, note_freq(n), whistle, 0.13, attack=0.5, release=1.0, vibrato=0.006)
    t.echo(0.3, 0.35, 0.35)
    return t


def move():
    """Menu highlight."""
    t = Track(0.08)
    t.add_tone(0, 0.03, note_freq("E5"), triangle, 0.18, release=0.03)
    return t


def select():
    """Menu choice."""
    t = Track(0.3)
    t.add_tone(0, 0.06, note_freq("A4"), bell, 0.25, release=0.15)
    t.add_tone(0.06, 0.1, note_freq("E5"), bell, 0.25, release=0.2)
    return t


def pause():
    """The game holds its breath."""
    t = Track(0.5)
    t.add_tone(0, 0.2, note_freq("E4"), bell, 0.25, release=0.25)
    t.add_tone(0.08, 0.2, note_freq("A3"), bell, 0.25, release=0.25)
    return t


def game_over():
    """A low, final sting."""
    t = Track(3.2)
    for i, n in enumerate(["A2", "E3", "C4"]):
        t.add_tone(i * 0.05, 2.2, note_freq(n), reed, 0.22, attack=0.05, release=0.9, vibrato=0.01)
    t.add_tone(0, 2.6, note_freq("A1"), sine, 0.35, release=0.6)
    t.lowpass(0.5)
    t.echo(0.35, 0.4, 0.35)
    return t


def victory():
    """You escaped: a bright pipe phrase over a drone."""
    t = Track(4.4)
    t.add_tone(0, 3.6, note_freq("A2"), reed, 0.18, attack=0.2, release=0.6)
    t.add_tone(0, 3.6, note_freq("E3"), reed, 0.1, attack=0.2, release=0.6)
    phrase = [("A4", 0.3), ("C#5", 0.3), ("E5", 0.3), ("F#5", 0.2), ("E5", 0.2), ("A5", 1.2)]
    at = 0.2
    for n, length in phrase:
        t.add_tone(at, length * 0.9, note_freq(n), whistle, 0.3, attack=0.02, release=0.12, vibrato=0.008)
        at += length
    t.echo(0.25, 0.3, 0.3)
    return t


EFFECTS = {
    "pistol.ogg": pistol,
    "shotgun.ogg": shotgun,
    "rifle.ogg": rifle,
    "dirk.ogg": dirk,
    "dry_fire.ogg": dry_fire,
    "hit.ogg": hit,
    "ricochet.ogg": ricochet,
    "immune.ogg": immune,
    "crawler_alert.ogg": crawler_alert,
    "crawler_windup.ogg": crawler_windup,
    "crawler_die.ogg": crawler_die,
    "wisp_alert.ogg": wisp_alert,
    "wisp_die.ogg": wisp_die,
    "brute_roar.ogg": brute_roar,
    "brute_windup.ogg": brute_windup,
    "brute_slam.ogg": brute_slam,
    "brute_die.ogg": brute_die,
    "hurt.ogg": hurt,
    "heartbeat.ogg": heartbeat,
    "death.ogg": death,
    "potion.ogg": potion,
    "orb.ogg": orb,
    "gem.ogg": gem,
    "page.ogg": page,
    "ammo.ogg": ammo,
    "lantern.ogg": lantern,
    "portal_open.ogg": portal_open,
    "escape.ogg": escape,
    "torch.ogg": torch,
    "torch_empty.ogg": torch_empty,
    "step.ogg": step,
    "wave.ogg": wave,
    "dawn.ogg": dawn,
    "move.ogg": move,
    "select.ogg": select,
    "pause.ogg": pause,
    "game_over.ogg": game_over,
    "victory.ogg": victory,
}
