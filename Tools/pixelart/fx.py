"""
Effects, items and markers: the pack's own where it has them (the arrow, dust, explosions, fire, the heal effect, the
target brackets), and the rest drawn in the pack's palette and outline (the berry, the snare, status icons, the punch
burst, the shock ring, a ring's twinkle).
"""
import math

import pack as P
from anim import Strip, cropped_strip
from draw import OUTLINE, Canvas, outlined
from px import Image, hexc, with_alpha

WHITE = hexc('#ffffff')
PARTICLES = P.FREE + '/Particle FX/Particle FX.aseprite'


def centre_pivot(frames):
    return frames[0].w // 2, frames[0].h // 2


def particle(pack, name, first, last, fps=14):
    """One of the pack's particle effects, pivot in the middle of what it covers."""
    ase = pack.ase(PARTICLES)
    frames = [ase.render(f) for f in range(first, last + 1)]
    strip = cropped_strip(name, frames, (0, 0), loop=False, fps=fps)
    strip.pivot = centre_pivot(strip.frames)
    return strip


def arrow(pack):
    ase = pack.ase(P.UNITS + '/Archer.aseprite')
    image, _, _ = ase.render(ase.tag('Arrow').start).trimmed()
    return Strip('arrow', [image], (image.w // 2, image.h // 2), loop=False)


def heal(pack):
    """The Monk's heal effect: a ring of light rising around whoever is healed. The pivot is the healed one's tile."""
    ase = pack.ase(P.UNITS + '/Monk.aseprite')
    tag = ase.tag('Heal Effect')
    frames = [ase.render(f, layers=['Effect 1', 'Effect 2']) for f in tag.frames]
    return cropped_strip('heal', frames, (96, 110), loop=False, fps=16)


def reticle(pack):
    """
    Target brackets around one tile, from the pack's selection cursor (drawn for a bigger box: its four corners are
    moved in to hug a 64 px tile).
    """
    cursor = pack.png(P.FREE + '/UI Elements/UI Elements/Cursors/Cursor_04.png')
    half = cursor.w // 2
    size = 76
    out = Image(size, size)
    for qx, qy in ((0, 0), (1, 0), (0, 1), (1, 1)):
        corner, _, _ = cursor.crop(qx * half, qy * half, half, half).trimmed()
        out.paste(corner, (size - corner.w) if qx else 0, (size - corner.h) if qy else 0)
    return Strip('reticle', [out], (size // 2, size // 2), loop=False)


def berry():
    """A pair of red berries on a leafy stalk."""
    red, red_dark, red_light = hexc('#e76161'), hexc('#924159'), hexc('#f9856c')
    leaf, leaf_dark = hexc('#6fae5f'), hexc('#3f6b4a')
    c = Canvas(40, 40, ox=0, oy=0)
    c.polygon([(18, 16), (21, 8), (23, 9), (20, 17)], leaf_dark)
    c.polygon([(21, 9), (30, 4), (34, 8), (28, 13), (22, 12)], leaf)
    c.polygon([(26, 9), (34, 8), (28, 13)], leaf_dark, only=(leaf,))
    for cx, cy in ((13.5, 24.0), (25.0, 26.0)):
        c.ellipse(cx, cy, 7.5, 7.5, red)
        c.ellipse(cx + 1.5, cy + 2.0, 6.0, 5.5, red_dark, only=(red,))
        c.ellipse(cx - 0.5, cy - 0.5, 5.5, 5.5, red, only=(red_dark,))
        c.dots(red_light, [(int(cx) - 3, int(cy) - 4), (int(cx) - 2, int(cy) - 4), (int(cx) - 3, int(cy) - 3)])
    part = c.part().trimmed()
    return Strip('berry', [part.image], (part.image.w // 2, part.image.h // 2 - 4), loop=False)


def snare():
    """Uzuki's snare, lying on the ground: a rope loop pegged down at both ends."""
    rope, rope_dark, rope_light = hexc('#dab570'), hexc('#b48150'), hexc('#fffaba')
    wood, wood_dark = hexc('#866353'), hexc('#654848')
    c = Canvas(56, 36, ox=0, oy=0)
    cx, cy, rx, ry = 28.0, 20.0, 19.0, 9.0
    for y in range(36):
        for x in range(56):
            nx, ny = (x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry
            d = math.sqrt(nx * nx + ny * ny)
            if abs(d - 1.0) <= 0.13:
                c.set(x, y, rope_dark if ny > 0.35 else rope_light if ny < -0.6 else rope)
    for px in (7, 47):
        c.rect(px, 14, 3, 9, wood)
        c.rect(px + 2, 14, 1, 9, wood_dark)
        c.rect(px - 1, 13, 5, 2, wood_dark)
    c.rect(26, 9, 5, 4, rope_dark)
    c.set(28, 10, rope_light)
    part = c.part().trimmed()
    return Strip('snare', [part.image], (part.image.w // 2, part.image.h // 2 - 2), loop=False)


def punch():
    """Impact burst for gauntlet blows: a four-pointed star, white so the game can tint it; three frames, growing."""
    frames = []
    for grow in (0.55, 0.85, 1.0):
        size = 64
        image = Image(size, size)
        for y in range(size):
            for x in range(size):
                dx, dy = x + 0.5 - size / 2, y + 0.5 - size / 2
                r = math.hypot(dx, dy)
                spike = (11.0 + 15.0 * max(0.0, math.cos(math.atan2(dy, dx) * 4.0)) ** 3) * grow
                if r <= spike:
                    image.px[y * size + x] = WHITE if r <= spike * 0.6 else hexc('#fffaba')
        frames.append(outlined(image).crop(2, 2, size, size))
    return Strip('punch', frames, (32, 32), loop=False, fps=20)


def ring():
    """A shock ring spreading over the ground (a slam, a shove, a piercing blow): white, tinted in the game."""
    frames = []
    w, h = 144, 88
    for i, (radius, thickness, alpha) in enumerate(((20, 7, 255), (34, 6, 255), (48, 5, 230), (60, 4, 170), (68, 3, 100))):
        image = Image(w, h)
        for y in range(h):
            for x in range(w):
                nx, ny = (x + 0.5 - w / 2) / radius, (y + 0.5 - h / 2) / (radius * 0.58)
                d = math.sqrt(nx * nx + ny * ny)
                if abs(d - 1.0) * radius <= thickness / 2.0:
                    image.px[y * w + x] = with_alpha(WHITE, alpha)
        frames.append(image)
    return Strip('ring', frames, (w // 2, h // 2), loop=False, fps=18)


def twinkle():
    """
    A ring's sparkle on its wearer's hand (Peter, 2026-10-04: a twinkle on the hand, not a glow on the ground, which
    is where the game shows what the player has to read): a four-pointed glint that opens and closes. White, tinted
    in the ring set's colour.
    """
    size = 18
    frames = []
    for arm, diagonal in ((1, 0), (3, 0), (6, 2), (8, 3), (5, 1), (2, 0)):
        image = Image(size, size)
        c = size // 2
        for i in range(-arm, arm + 1):
            fade = 255 if abs(i) <= arm // 2 else 170
            for x, y in ((c + i, c), (c + i, c - 1), (c, c + i), (c - 1, c + i)):
                image.set(x, y, with_alpha(WHITE, fade))
        for i in range(1, diagonal + 1):
            for dx, dy in ((i, i), (-i - 1, i), (i, -i - 1), (-i - 1, -i - 1)):
                image.set(c + dx, c + dy, with_alpha(WHITE, 200))
        frames.append(image)
    return Strip('twinkle', frames, (size // 2, size // 2), loop=False, fps=14)


THRUST = ((4, 34, 3.0, 255), (4, 66, 2.4, 255), (34, 68, 1.3, 170))


def thrust_frames():
    """
    A stab's streak (the Piercer Blade): a thin lance of light pointing right, which the game turns the way the blade
    goes and tints. Three frames: it shoots out, reaches the target, and its tail catches up.
    """
    w, h = 72, 14
    frames = []
    for x0, x1, half, alpha in THRUST:
        image = Image(w, h)
        for y in range(h):
            for x in range(x0, x1):
                t = (x + 0.5 - x0) / (x1 - x0)
                width = half * (t / 0.75 if t < 0.75 else (1.0 - t) / 0.25)   # A lance: widest near the point.
                d = abs(y + 0.5 - h / 2.0)
                if d <= max(0.5, width):
                    core = d <= max(0.5, width * 0.45)
                    image.px[y * w + x] = with_alpha(WHITE if core else hexc('#fffaba'), int(alpha * min(1.0, 0.3 + t)))
        frames.append(image)
    return frames


def thrust():
    return Strip('thrust', thrust_frames(), (4, 7), loop=False, fps=24)


def slash_frames():
    """A blade's cut across a target (Blade Dance, a counter): a thin crescent, white, tinted in the game. It sweeps, then fades."""
    size = 64
    frames = []
    for sweep, thick, alpha in ((0.5, 6.0, 255), (1.0, 5.0, 255), (1.0, 2.4, 150)):
        image = Image(size, size)
        for y in range(size):
            for x in range(size):
                dx, dy = x + 0.5 - 18, y + 0.5 - 46          # The arc's centre: the cut runs from the top left to the right.
                k = (105.0 - math.degrees(math.atan2(-dy, dx))) / 130.0   # 0 where the cut begins, 1 where it ends.
                if not 0.0 <= k <= sweep:
                    continue
                taper = math.sin(math.pi * k)
                if abs(math.hypot(dx, dy) - 30.0) <= thick * taper / 2.0 + 0.2:
                    image.px[y * size + x] = with_alpha(WHITE, alpha)
        frames.append(image)
    return frames


def slash():
    return Strip('slash', slash_frames(), (32, 32), loop=False, fps=22)


# ---- status icons: 24 px, outlined ----

def _icon(draw):
    c = Canvas(24, 24)
    draw(c)
    part = c.part()
    return part.image


def _mark(c):
    red, light = hexc('#e76161'), WHITE
    for y in range(24):
        for x in range(24):
            d = math.hypot(x + 0.5 - 12, y + 0.5 - 12)
            if 6.0 <= d <= 8.6:
                c.set(x, y, red)
    c.rect(11, 1, 2, 7, red)
    c.rect(11, 16, 2, 7, red)
    c.rect(1, 11, 7, 2, red)
    c.rect(16, 11, 7, 2, red)
    c.rect(11, 11, 2, 2, light)


def _rooted(c):
    rope, dark = hexc('#dab570'), hexc('#b48150')
    for y in range(24):
        for x in range(24):
            d = math.hypot((x + 0.5 - 12) / 1.0, (y + 0.5 - 14) / 0.62)
            if 6.2 <= d <= 9.0:
                c.set(x, y, rope if y < 14 else dark)
    c.rect(10, 3, 4, 5, dark)
    c.rect(11, 4, 2, 2, rope)


def _taunt(c):
    orange, light = hexc('#f9856c'), hexc('#fffaba')
    c.rect(10, 2, 5, 13, orange)
    c.rect(10, 2, 2, 13, light)
    c.rect(10, 18, 5, 4, orange)
    c.rect(10, 18, 2, 2, light)


def _stunned(c):
    yellow, light = hexc('#e9f044'), hexc('#fffaba')
    for cx, cy, size in ((7, 8, 4), (16, 15, 4)):
        for i in range(size + 1):
            width = size - i
            c.rect(cx - width, cy + i - 1, 2 * width + 1, 1, yellow)
            c.rect(cx - width, cy - i - 1, 2 * width + 1, 1, yellow)
        c.rect(cx - size, cy - 1, 2 * size + 1, 1, yellow)
        c.set(cx, cy - 1, light)


def _guard(c):
    blue, light, dark = P.CLOTH_LIGHT, hexc('#8fd9d2'), P.CLOTH_DARK
    for y in range(2, 22):
        half = 8 if y < 13 else max(0, 8 - (y - 12))
        for x in range(12 - half, 12 + half):
            c.set(x, y, light if x < 12 and y < 9 else blue if x < 12 else dark)


def _aura(c):
    gold, light = hexc('#dcaa46'), hexc('#fffaba')
    c.ellipse(12, 12, 5.5, 5.5, gold)
    c.ellipse(11, 11, 3.0, 3.0, light)
    for dx, dy in ((0, -10), (0, 9), (-10, 0), (9, 0), (-7, -7), (6, -7), (-7, 6), (6, 6)):
        c.rect(12 + dx - 1, 12 + dy - 1, 2, 2, gold)


def _riposte(c):
    """A counter stance: a thin blade held ready, its round gold guard, and a glint at its point."""
    steel, gold, dark = hexc('#cfdde4'), hexc('#f3d34a'), hexc('#c9972a')
    c.line(8, 16, 19, 5, steel, 2)
    c.line(8, 15, 18, 5, WHITE, 1)
    c.line(3, 21, 6, 18, dark, 2)
    c.ellipse(7, 17, 3.2, 3.2, gold)
    c.rect(19, 1, 2, 8, WHITE)
    c.rect(16, 4, 8, 2, WHITE)


def status_icons():
    return {'mark': _icon(_mark), 'rooted': _icon(_rooted), 'taunt': _icon(_taunt), 'stunned': _icon(_stunned),
            'guard': _icon(_guard), 'aura': _icon(_aura), 'riposte': _icon(_riposte)}


def build(pack):
    """Every effect strip, by name."""
    # Of the pack's particle effects the game uses the first dust puff and the first explosion; the file also has a
    # second of each (frames 8-17 and 26-35), three fires (36-65) and a water splash (66-74).
    strips = [arrow(pack), heal(pack), reticle(pack), berry(), snare(), punch(), ring(), twinkle(), thrust(), slash(),
              particle(pack, 'dust', 0, 7), particle(pack, 'explosion', 18, 25)]
    for name, image in status_icons().items():
        strips.append(Strip('status_' + name, [image], (image.w // 2, image.h // 2), loop=False))
    return strips
