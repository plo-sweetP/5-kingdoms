#!/usr/bin/env python3
"""
Placeholder pixel art for 5 Kingdoms, generated from code so it is reproducible and easy to tweak.

Writes 32x32 PNGs (1 tile = 32 px) into Assets/Art/Resources/Sprites. Final art replaces these files
one at a time; keep the same names and sizes and the game picks them up.
Python 3 standard library only.

Usage:
    python Tools/pixelart/make_sprites.py                    # write the sprites
    python Tools/pixelart/make_sprites.py --preview out.png  # also write an enlarged mock-up scene
"""
import argparse
import math
import os
import random
import struct
import zlib

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
OUT = os.path.join(ROOT, 'Assets', 'Art', 'Resources', 'Sprites')
T = 32  # Tile size in pixels.
CLEAR = (0, 0, 0, 0)


def rgb(hex_string, alpha=255):
    h = hex_string.lstrip('#')
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), alpha)


def shade(color, amount):
    r, g, b, a = color
    clamp = lambda v: max(0, min(255, v))
    return (clamp(r + amount), clamp(g + amount), clamp(b + amount), a)


class Canvas:
    def __init__(self, w=T, h=T, fill=CLEAR):
        self.w, self.h = w, h
        self.px = [[fill] * w for _ in range(h)]

    def inside(self, x, y):
        return 0 <= x < self.w and 0 <= y < self.h

    def get(self, x, y):
        return self.px[y][x] if self.inside(x, y) else CLEAR

    def set(self, x, y, color):
        if self.inside(x, y):
            self.px[y][x] = color

    def copy(self):
        c = Canvas(self.w, self.h)
        c.px = [row[:] for row in self.px]
        return c

    def blend(self, x, y, color):
        """Alpha-over a color onto one pixel."""
        if not self.inside(x, y) or color[3] == 0:
            return
        sr, sg, sb, sa = color
        dr, dg, db, da = self.px[y][x]
        a = sa / 255.0
        out_a = sa + da * (1 - a)
        mix = lambda s, d: int(round((s * sa + d * da * (1 - a)) / out_a))
        self.px[y][x] = (mix(sr, dr), mix(sg, dg), mix(sb, db), int(round(out_a)))

    def paste(self, other, ox, oy, flip_x=False):
        for y in range(other.h):
            for x in range(other.w):
                sx = other.w - 1 - x if flip_x else x
                self.blend(ox + x, oy + y, other.px[y][sx])

    def scaled(self, k):
        c = Canvas(self.w * k, self.h * k)
        for y in range(self.h):
            for x in range(self.w):
                color = self.px[y][x]
                for dy in range(k):
                    row = c.px[y * k + dy]
                    for dx in range(k):
                        row[x * k + dx] = color
        return c

    def save(self, path):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        raw = bytearray()
        for row in self.px:
            raw.append(0)  # PNG filter type: none.
            for r, g, b, a in row:
                raw.extend((r, g, b, a))

        def chunk(tag, data):
            body = tag + data
            return struct.pack('>I', len(data)) + body + struct.pack('>I', zlib.crc32(body) & 0xFFFFFFFF)

        png = b'\x89PNG\r\n\x1a\n'
        png += chunk(b'IHDR', struct.pack('>IIBBBBB', self.w, self.h, 8, 6, 0, 0, 0))
        png += chunk(b'IDAT', zlib.compress(bytes(raw), 9))
        png += chunk(b'IEND', b'')
        with open(path, 'wb') as f:
            f.write(png)


def from_grid(rows, palette):
    width = len(rows[0])
    for i, row in enumerate(rows):
        assert len(row) == width, 'row %d is %d wide, expected %d' % (i, len(row), width)
    c = Canvas(width, len(rows))
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch != '.':
                c.set(x, y, palette[ch])
    return c


def add_outline(c, color):
    """Adds a 1 px outline on every transparent pixel that touches the sprite orthogonally."""
    out = c.copy()
    for y in range(c.h):
        for x in range(c.w):
            if c.get(x, y)[3] == 0 and any(c.get(x + dx, y + dy)[3] > 0 for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                out.set(x, y, color)
    return out


# ---------------------------------------------------------------------------------------------
# Characters

UZUKI_PALETTE = {
    'H': rgb('#3f7fe6'), 'h': rgb('#2a55b4'), 'L': rgb('#8fd0ff'), 'd': rgb('#1d3a85'),
    'S': rgb('#f6d2b3'), 's': rgb('#d9a07e'),
    'e': rgb('#163a52'), 'E': rgb('#3ad0e6'), 'W': rgb('#ffffff'), 'n': rgb('#b85c55'),
    'R': rgb('#c43b30'), 'r': rgb('#8a2420'),
    'B': rgb('#8a5532'), 'b': rgb('#5b3420'),
    'M': rgb('#c4cedb'), 'm': rgb('#6f7c8e'),
    'P': rgb('#d0b07a'), 'p': rgb('#9c7f4c'),
    'O': rgb('#6b4329'), 'o': rgb('#41281a'),
    'w': rgb('#e9e6df'), 'k': rgb('#1b1530'),
}

# Uzuki, from the concept sketch: spiky blue hair, cyan eyes, sleeveless top with a diagonal strap,
# pauldron on one shoulder, wrapped wrist, baggy cuffed pants and boots. Faces the viewer, slightly right.
UZUKI = [
    '................................',
    '..............H....H............',
    '........H....Hh...Hh............',
    '........Hh..HLHh.HLHh...h.......',
    '........HHHHHLHHHHHLHHh.Hh......',
    '......HHHHHLHHHHLHHHHHHHhh......',
    '.....hHHHHHHHHHHHHHHHHHHhhh.....',
    '......hHHHHHHHHHHHHHHHHHhh......',
    '.......hHHHHHHHHHHHHHHHhh.......',
    '.......hHHHSHHHHSHHHHSHHh.......',
    '.......hHSHSSHHSSSHHSSHHh.......',
    '.......hHsSeeSSSSSeeSSsHh.......',
    '.......hHsSWESSSSSWESSsHh.......',
    '.......hHsSEESSSSSEESSsHh.......',
    '........hsSSSSSSSSSSSSsh........',
    '.........sSSSSSnnSSSSSs.........',
    '..........ssSSSSSSSSss..........',
    '..............ssss..............',
    '.........sSRRRRRRRBMMMM.........',
    '........sSSRRRRRRBbMMMMm........',
    '........sSSRRRRRBbRRrmmm........',
    '........sSSRRRRBbRRRrwww........',
    '........wwwRRRBbRRRRrSSs........',
    '........SSsRRBbRRRRRrSSs........',
    '..........bBBBBMMBBBBb..........',
    '..........pPPPPPPPPPPp..........',
    '.........pPPPPPppPPPPPp.........',
    '.........pPPPPp..pPPPPp.........',
    '..........ppppp..ppppp..........',
    '..........OOOOO..OOOOO..........',
    '..........ooooo..ooooo..........',
    '................................',
]


def make_uzuki():
    return add_outline(from_grid(UZUKI, UZUKI_PALETTE), rgb('#1b1530'))


def padded(rows):
    """Rows given as (left padding, pixels), filled out to 32 columns, so hand-drawn art can't drift a column."""
    out = []
    for pad, pixels in rows:
        row = '.' * pad + pixels
        assert len(row) <= T, 'row %r is %d wide' % (row, len(row))
        out.append(row + '.' * (T - len(row)))
    assert len(out) == T, '%d rows' % len(out)
    return out


HAIDEN_PALETTE = {
    'H': rgb('#c8692c'), 'h': rgb('#8f4519'), 'L': rgb('#f2a564'),
    'K': rgb('#2f62d6'), 'k': rgb('#1d3c8a'),
    'S': rgb('#f6d2b3'), 's': rgb('#d9a07e'),
    'e': rgb('#3a0f12'), 'E': rgb('#e0302c'), 'W': rgb('#ffffff'), 'n': rgb('#b85c55'),
    'R': rgb('#b03a2e'), 'r': rgb('#6e1f1a'), 'G': rgb('#e8b84a'),
    'w': rgb('#e9e6df'), 'B': rgb('#2b2b38'),
    'P': rgb('#4a4f63'), 'p': rgb('#2f3242'),
    'O': rgb('#3a2618'), 'o': rgb('#22160d'),
}

# Haiden (Dynasty Nation), from the concept sketch: spiky orange-brown hair, a blue headband whose long tails stream
# out to one side, red eyes. A crimson tunic with a gold front, wrapped forearms, slate trousers.
HAIDEN = padded([
    (0, ''),
    (11, 'H....H...H'),
    (7, 'H...HH..HH..HH...H'),
    (7, 'HH.HLHhHLHhHLHh.Hh'),
    (6, 'hHHHHLHHHLHHHLHHHHHh'),
    (5, 'hHHHHHHHHHHHHHHHHHHHHh'),
    (5, 'hHHHHHHHHHHHHHHHHHHHHh'),
    (6, 'kKKKKKKKKKKKKKKKKKKk'),
    (6, 'kKKKKKKKKKKKKKKKKKKkKKk'),
    (7, 'hHSHHHSSHHHHSHHHHhkKKKk'),
    (7, 'hHSSHSSSSHHSSSHSHh..kKKk'),
    (7, 'hHsSeeSSSSSSeeSSsh...kKk'),
    (7, 'hHsSWESSSSSSWESSsh....kk'),
    (7, 'hHsSEESSSSSSEESSsh'),
    (8, 'hsSSSSSSSSSSSSSsh'),
    (9, 'sSSSSSSnnSSSSSs'),
    (10, 'ssSSSSSSSSSss'),
    (14, 'ssss'),
    (9, 'sRRRRRGGRRRRRs'),
    (8, 'sSRRRRRGGRRRRRSs'),
    (8, 'sSRRRRRGGRRRRRSs'),
    (8, 'wwRRRRRGGRRRRRww'),
    (8, 'wwrRRRRGGRRRRrww'),
    (8, 'SSrRRRRGGRRRRrSS'),
    (10, 'BBBBBBBBBBBB'),
    (10, 'pPPPPPPPPPPp'),
    (9, 'pPPPPPppPPPPPp'),
    (9, 'pPPPPp..pPPPPp'),
    (10, 'ppppp..ppppp'),
    (10, 'OOOOO..OOOOO'),
    (10, 'ooooo..ooooo'),
    (0, ''),
])


def make_haiden():
    return add_outline(from_grid(HAIDEN, HAIDEN_PALETTE), rgb('#1b1530'))


KRISTELA_PALETTE = {
    'Y': rgb('#f3d34a'), 'y': rgb('#c9a12a'), 'L': rgb('#fff3a8'),
    'X': rgb('#b8741a'),
    'S': rgb('#f6d2b3'), 's': rgb('#d9a07e'),
    'e': rgb('#12224a'), 'E': rgb('#3a7be0'), 'W': rgb('#ffffff'), 'n': rgb('#d0747a'),
    'w': rgb('#f2efe6'), 'B': rgb('#3d6fd1'), 'b': rgb('#24438a'),
    'M': rgb('#c4cedb'), 'm': rgb('#6f7c8e'),
    'P': rgb('#3c4a78'), 'p': rgb('#26304f'),
    'O': rgb('#6b4329'), 'o': rgb('#41281a'),
}

# Kristela (Medieval Realm, a princess and Monk), from the concept sketch: long wavy blonde hair falling past her
# shoulders, blue eyes, a gold X-shaped clip on one side. A white gi with a blue sash, metal gauntlets, blue trousers.
KRISTELA = padded([
    (0, ''),
    (11, 'yYYYYYYYYy'),
    (9, 'yYYYLLYYYYYYy'),
    (8, 'yYYYLLYYYYYYYYy'),
    (7, 'yYYYYYYYYYYYYYYYy'),
    (6, 'yYXYXYYYYYYYYYYYYYy'),
    (6, 'yYYXYYYYYYYYYYYYYYYy'),
    (6, 'yYXYXYYSYYYYYSYYYYYy'),
    (6, 'yYYYYSSSYYYYSSSSYYYy'),
    (6, 'yYYSSSSSSSSSSSSSSYYy'),
    (6, 'yYYSSSSSSSSSSSSSSSYy'),
    (6, 'yYYsSeeSSSSSSeeSSsYy'),
    (6, 'yYYsSWESSSSSSWESSsYy'),
    (6, 'yYYsSEESSSSSSEESSsYy'),
    (6, 'yYYYsSSSSSSSSSSSsYYy'),
    (5, 'yYYYYsSSSSnnSSSSsYYYYy'),
    (5, 'yYYYY.ssSSSSSSss.YYYYy'),
    (5, 'yYYYY....ssss....YYYYy'),
    (5, 'yYYYYswwwwBBwwwwsYYYYy'),
    (5, 'yYYYsSwwwwBBwwwwSsYYYy'),
    (5, 'yYYYsSwwwwBBwwwwSsYYYy'),
    (6, 'yYYMMwwwwBBwwwwMMYYy'),
    (7, 'yYMMwwwwBBwwwwMMYy'),
    (8, 'ymmwwwwBBwwwwmmy'),
    (10, 'bBBBBBBBBBBBb'),
    (10, 'pPPPPPPPPPPPp'),
    (9, 'pPPPPPPpPPPPPPp'),
    (9, 'pPPPPp...pPPPPp'),
    (10, 'ppppp...ppppp'),
    (10, 'OOOOO...OOOOO'),
    (10, 'ooooo...ooooo'),
    (0, ''),
])


def make_kristela():
    return add_outline(from_grid(KRISTELA, KRISTELA_PALETTE), rgb('#1b1530'))


def make_arrow():
    """An arrow flying right (rotated in-game): fletching, a wooden shaft and an iron head."""
    shaft, head, head_dark = rgb('#c8a46a'), rgb('#dfe6ee'), rgb('#7d8794')
    feather, feather_dark = rgb('#f2efe6'), rgb('#c43b30')
    c = Canvas()
    for x in range(9, 23):
        c.set(x, 16, shaft)
    for x, y in ((23, 16), (24, 16), (25, 16), (23, 15), (23, 17), (24, 15)):
        c.set(x, y, head)
    c.set(24, 17, head_dark)
    for x, y in ((6, 14), (7, 15), (8, 15), (6, 18), (7, 17), (8, 17), (7, 14), (7, 18)):
        c.set(x, y, feather)
    c.set(6, 15, feather_dark)
    c.set(6, 17, feather_dark)
    return add_outline(c, rgb('#2a1a10'))


def make_spider():
    base, light, dark, deep = rgb('#5ecb5a'), rgb('#9be890'), rgb('#3a9442'), rgb('#25682f')
    shine, eye, outline = rgb('#f2fff0'), rgb('#132613'), rgb('#163d1d')
    c = Canvas()
    cx, cy, rx, ry, bottom = 16.0, 21.5, 11.5, 10.0, 29
    for y in range(T):
        for x in range(T):
            nx, ny = (x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry
            d = nx * nx + ny * ny
            if d > 1.0 or y > bottom:
                continue
            nz = math.sqrt(max(0.0, 1.0 - d))
            light_amount = -0.45 * nx - 0.55 * ny + 0.70 * nz  # Lit from the upper left.
            color = light if light_amount > 0.78 else base if light_amount > 0.42 else dark if light_amount > 0.12 else deep
            if y >= bottom - 1:
                color = deep
            c.set(x, y, color)
    for x, y in ((17, 11), (17, 10), (18, 9)):  # Little curl on top.
        c.set(x, y, base)
    for x, y in ((10, 15), (11, 15), (10, 16), (12, 14)):
        c.set(x, y, shine)
    for ex in (12, 19):
        for y in range(19, 23):
            c.set(ex, y, eye)
            c.set(ex + 1, y, eye)
        c.set(ex, 19, shine)
    for x, y in ((14, 24), (15, 25), (16, 25), (17, 24)):
        c.set(x, y, eye)
    return add_outline(c, outline)


KING_CROWN = [
    '........yy........',
    '.......yYYy.......',
    '..y....yYRy....y..',
    '.yYy...yYYy...yYy.',
    '.yYYy.yYYYYy.yYYy.',
    '.yYYYyYYYYYYyYYYy.',
    '.yYYYYYYYYYYYYYYy.',
    '.yYRYYYYRYYYYRYYy.',
    '.yYYYYYYYYYYYYYYy.',
    '.yyyyyyyyyyyyyyyy.',
]


def make_troll():
    """48x48 boss: a royal purple spider with a gold crown, angry brows and fangs."""
    base, light, dark, deep = rgb('#9b5de5'), rgb('#c9a2f5'), rgb('#6a3cb0'), rgb('#472580')
    shine, ink, white = rgb('#f6ecff'), rgb('#1e0c30'), rgb('#ffffff')
    size = 48
    c = Canvas(size, size)
    cx, cy, rx, ry, bottom = 24.0, 32.0, 17.5, 14.5, 45
    for y in range(size):
        for x in range(size):
            nx, ny = (x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry
            d = nx * nx + ny * ny
            if d > 1.0 or y > bottom:
                continue
            nz = math.sqrt(max(0.0, 1.0 - d))
            light_amount = -0.45 * nx - 0.55 * ny + 0.70 * nz
            color = light if light_amount > 0.8 else base if light_amount > 0.44 else dark if light_amount > 0.14 else deep
            if y >= bottom - 1:
                color = deep
            c.set(x, y, color)
    for x, y in ((12, 25), (13, 24), (14, 23), (15, 23), (13, 25)):
        c.set(x, y, shine)
    # Angry brows slanting toward the middle, eyes, a grin with two fangs.
    for x, y in ((15, 25), (16, 25), (17, 26), (18, 26), (19, 27), (32, 25), (31, 25), (30, 26), (29, 26), (28, 27)):
        c.set(x, y, ink)
    for ex in (17, 28):
        for y in range(28, 33):
            for dx in range(3):
                c.set(ex + dx, y, ink)
        c.set(ex, 28, white)
    for x in range(19, 30):
        c.set(x, 36, ink)
    c.set(18, 35, ink)
    c.set(30, 35, ink)
    for x in (21, 27):
        c.set(x, 37, white)
    crown = from_grid(KING_CROWN, {'y': rgb('#b8901e'), 'Y': rgb('#f2c94c'), 'R': rgb('#e0443c')})
    c.paste(crown, 15, 11)
    return add_outline(c, rgb('#2a1240'))


def make_bat():
    """A fast cave bat: a round purple body, big ears, yellow eyes and fangs, scalloped wings spread wide. It flies, so
    it sits higher in the tile than the spiders."""
    body, light, dark = rgb('#6b4a9e'), rgb('#9a7fd0'), rgb('#45306c')
    wing, wing_dark, bone = rgb('#5b3d86'), rgb('#432b66'), rgb('#2e1d47')
    eye, pupil, fang, ear_in = rgb('#ffd84a'), rgb('#2a1a10'), rgb('#ffffff'), rgb('#d48ab4')
    c = Canvas()
    cx, cy = 16.0, 15.0
    # Wings behind the body: from the shoulder up to a pointed tip, the trailing edge dipping between three finger bones.
    for side in (-1, 1):
        for x in range(T):
            d = (x + 0.5 - cx) * side
            if d < 3.0 or d > 15.5:
                continue
            t = (d - 3.0) / 12.5
            top = 12.0 - 7.5 * t
            bottom = 20.5 - 12.0 * t ** 1.4 - 2.6 * abs(math.sin(t * math.pi * 3.0)) * (1.0 - 0.4 * t)
            for y in range(T):
                if top <= y + 0.5 <= max(top + 1.0, bottom):
                    c.set(x, y, wing_dark if y + 0.5 > bottom - 1.2 else wing)
        for k in (1, 2, 3):  # Finger bones from the shoulder to each low point of the edge.
            t_end = k / 3.0
            for i in range(14):
                t = t_end * i / 13.0
                d = 3.0 + 12.5 * t
                y = (12.0 - 7.5 * t) + ((20.5 - 12.0 * t_end ** 1.4) - (12.0 - 7.5 * t_end) - 0.5) * (i / 13.0) * 0.9
                c.set(int(cx + side * d - (0 if side > 0 else 1)), int(y), bone)
    for y in range(T):
        for x in range(T):
            nx, ny = (x + 0.5 - cx) / 5.6, (y + 0.5 - cy) / 6.2
            d2 = nx * nx + ny * ny
            if d2 > 1.0:
                continue
            nz = math.sqrt(max(0.0, 1.0 - d2))
            lit = -0.45 * nx - 0.55 * ny + 0.70 * nz
            c.set(x, y, light if lit > 0.8 else body if lit > 0.3 else dark)
    for ex, sign in ((12, -1), (19, 1)):  # Ears.
        for i, (dx, dy) in enumerate(((0, 0), (0, -1), (sign, -1), (0, -2), (sign, -2), (sign, -3))):
            c.set(ex + dx, 9 + dy, body if i != 2 else ear_in)
    for ex in (13, 18):
        c.set(ex, 14, eye)
        c.set(ex + (1 if ex == 13 else -1), 14, eye)
        c.set(ex + (1 if ex == 13 else -1), 15, pupil)
        c.set(ex, 15, eye)
    for x in range(14, 18):
        c.set(x, 18, dark)
    c.set(14, 19, fang)
    c.set(17, 19, fang)
    return add_outline(c, rgb('#1b1030'))


def make_shadow():
    c = Canvas()
    for y in range(T):
        for x in range(T):
            nx, ny = (x + 0.5 - 16) / 9.0, (y + 0.5 - 29.5) / 2.6
            if nx * nx + ny * ny <= 1.0:
                c.set(x, y, (0, 0, 0, 90))
    return c


# ---------------------------------------------------------------------------------------------
# Tiles (Troll's Hollow)

FLOOR = rgb('#4d443b')


def make_floor(variant):
    rnd = random.Random(100 + variant)
    c = Canvas()
    for y in range(T):
        for x in range(T):
            c.set(x, y, shade(FLOOR, rnd.randint(-4, 4)))
    for _ in range(12):
        c.set(rnd.randrange(T), rnd.randrange(T), shade(FLOOR, -14))
    for _ in range(rnd.randint(1, 3)):
        x, y = rnd.randrange(2, T - 3), rnd.randrange(2, T - 3)
        c.set(x, y, shade(FLOOR, 20))
        c.set(x + 1, y, shade(FLOOR, 12))
        c.set(x, y + 1, shade(FLOOR, -16))
        c.set(x + 1, y + 1, shade(FLOOR, -18))
    if variant == 1:  # Hairline crack.
        x, y = rnd.randrange(6, 20), rnd.randrange(6, 20)
        for _ in range(9):
            c.set(x, y, shade(FLOOR, -22))
            x += rnd.choice((0, 1))
            y += 1
    elif variant == 2:  # Moss.
        for _ in range(16):
            x, y = 12 + rnd.randint(-6, 6), 14 + rnd.randint(-5, 5)
            c.set(x, y, rgb('#4f6a3a') if rnd.random() < 0.6 else rgb('#5f7f45'))
    elif variant == 3:  # Bigger stone.
        x, y = rnd.randrange(8, 20), rnd.randrange(8, 20)
        for dy in range(3):
            for dx in range(4):
                c.set(x + dx, y + dy, shade(FLOOR, 16 - dy * 6))
        for dx in range(4):
            c.set(x + dx, y + 3, shade(FLOOR, -20))
    for i in range(T):  # Faint seams so the movement grid stays readable.
        c.set(i, T - 1, shade(c.get(i, T - 1), -9))
        c.set(T - 1, i, shade(c.get(T - 1, i), -9))
    return c


def make_wall_top():
    rnd = random.Random(11)
    base = rgb('#2a2436')
    c = Canvas()
    for y in range(T):
        for x in range(T):
            c.set(x, y, shade(base, rnd.randint(-3, 3)))
    for _ in range(14):
        c.set(rnd.randrange(T), rnd.randrange(T), shade(base, 16))
    for _ in range(10):
        c.set(rnd.randrange(T), rnd.randrange(T), shade(base, -10))
    return c


def make_wall_face():
    rnd = random.Random(12)
    base = rgb('#463c55')
    c = Canvas()
    for y in range(T):
        for x in range(T):
            c.set(x, y, shade(base, rnd.randint(-3, 3)))
    for x in range(T):
        c.set(x, 0, rgb('#6c6080'))
        c.set(x, 1, rgb('#5a4f6c'))
    for line_y in (9, 17, 24):
        x = 0
        while x < T:
            run = rnd.randint(4, 10)
            for i in range(run):
                if x + i < T:
                    c.set(x + i, line_y, shade(base, -18))
                    c.set(x + i, line_y - 1, shade(base, 8))
            x += run + rnd.randint(1, 4)
    for i, amount in enumerate((-8, -16, -24, -32)):
        for x in range(T):
            c.set(x, T - 4 + i, shade(c.get(x, T - 4 + i), amount))
    return c


def make_shadow_top():
    """Overlay for floor tiles just below a wall: ambient occlusion along the wall's foot."""
    c = Canvas()
    for y, alpha in enumerate((120, 88, 62, 40, 24, 12, 5)):
        for x in range(T):
            c.set(x, y, (0, 0, 0, alpha))
    return c


def make_stairs():
    c = Canvas()
    rim, rim_dark = rgb('#8a7c6c'), rgb('#5e5346')
    for y in range(4, 28):
        for x in range(4, 28):
            c.set(x, y, rim)
    for x in range(4, 28):
        c.set(x, 27, rim_dark)
    steps = (rgb('#6e6356'), rgb('#564d43'), rgb('#3e3731'), rgb('#26221f'))
    for i, color in enumerate(steps):
        top = 7 + i * 5
        for y in range(top, top + 5):
            for x in range(7, 25):
                c.set(x, y, color)
        for x in range(7, 25):
            c.set(x, top, shade(color, 18))
            c.set(x, top + 4, shade(color, -10))
    for y in range(7, 27):
        c.set(7, y, shade(c.get(7, y), -14))
        c.set(24, y, shade(c.get(24, y), -14))
    return add_outline(c, rgb('#1a1612'))


# ---------------------------------------------------------------------------------------------
# Items and effects

def make_berry():
    red, red_dark, red_light = rgb('#d8433a'), rgb('#8e2622'), rgb('#ff9c8c')
    leaf, leaf_dark = rgb('#57b04f'), rgb('#2f6e2c')
    c = Canvas()
    for bx, by in ((13.0, 22.0), (19.0, 23.0)):
        for y in range(T):
            for x in range(T):
                dx, dy = x + 0.5 - bx, y + 0.5 - by
                d = math.hypot(dx, dy)
                if d <= 4.3:
                    c.set(x, y, red_dark if dx + dy > 3.2 else red)
        c.set(int(bx) - 2, int(by) - 2, red_light)
        c.set(int(bx) - 1, int(by) - 2, red_light)
    for x, y in ((16, 17), (16, 16), (15, 16), (17, 15), (18, 15), (18, 14), (14, 15)):
        c.set(x, y, leaf)
    c.set(17, 16, leaf_dark)
    return add_outline(c, rgb('#2a1010'))


def make_snare():
    """Uzuki's snare trap, lying on the floor: a rope noose pegged down at both ends, with a knot."""
    rope, rope_dark, rope_light = rgb('#c9a66b'), rgb('#8c6a3c'), rgb('#ead3a0')
    wood, wood_dark = rgb('#7a5332'), rgb('#4a3020')
    c = Canvas()
    cx, cy, rx, ry = 16.0, 20.0, 9.0, 4.5
    for y in range(T):
        for x in range(T):
            nx, ny = (x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry
            d = math.sqrt(nx * nx + ny * ny)
            if abs(d - 1.0) <= 0.16:
                c.set(x, y, rope_dark if ny > 0.3 else rope_light if ny < -0.6 else rope)
    for px in (6, 26):  # Pegs holding it down.
        for y in range(17, 22):
            c.set(px, y, wood if y < 21 else wood_dark)
        c.set(px - 1, 17, wood_dark)
        c.set(px + 1, 17, wood_dark)
    for x, y in ((15, 15), (16, 15), (17, 15), (16, 14), (15, 16), (17, 16)):  # The knot.
        c.set(x, y, rope_dark)
    c.set(16, 15, rope_light)
    return add_outline(c, rgb('#2a1a10'))


def make_reticle():
    """Target marker for the aiming highlight: four corner brackets (tinted in-game)."""
    white = rgb('#ffffff')
    c = Canvas()
    for corner_x, corner_y, sx, sy in ((1, 1, 1, 1), (30, 1, -1, 1), (1, 30, 1, -1), (30, 30, -1, -1)):
        for i in range(7):
            c.set(corner_x + sx * i, corner_y, white)
            c.set(corner_x, corner_y + sy * i, white)
            c.set(corner_x + sx * i, corner_y + sy, white)
            c.set(corner_x + sx, corner_y + sy * i, white)
    return c


def make_punch():
    """Impact burst for gauntlet blows (Kristela's Jab and Flurry): a four-pointed star, drawn white and tinted in-game."""
    core, edge = rgb('#ffffff'), rgb('#ffe9a8')
    c = Canvas()
    for y in range(T):
        for x in range(T):
            dx, dy = x + 0.5 - 16, y + 0.5 - 16
            r = math.hypot(dx, dy)
            angle = math.atan2(dy, dx)
            spike = 6.0 + 7.0 * max(0.0, math.cos(angle * 4.0)) ** 3
            if r <= spike:
                c.set(x, y, core if r <= spike * 0.55 else edge)
    return c


def icon(draw):
    """A 12x12 status icon (shown in a row above an actor), outlined so it reads on any background."""
    c = Canvas(12, 12)
    draw(c)
    return add_outline(c, rgb('#140c1c'))


def draw_mark(c):
    red, white = rgb('#ff5a4e'), rgb('#ffffff')
    for y in range(12):
        for x in range(12):
            d = math.hypot(x + 0.5 - 6, y + 0.5 - 6)
            if 3.4 <= d <= 4.6:
                c.set(x, y, red)
    for i in range(1, 11):
        if not 4 <= i <= 7:
            c.set(i, 6, red)
            c.set(6, i, red)
    c.set(6, 6, white)


def draw_root(c):
    rope, dark = rgb('#d8b071'), rgb('#8c6a3c')
    for y in range(12):
        for x in range(12):
            d = math.hypot((x + 0.5 - 6) / 1.0, (y + 0.5 - 7) / 0.6)
            if 3.1 <= d <= 4.4:
                c.set(x, y, rope if y < 7 else dark)
    for x, y in ((6, 2), (5, 3), (6, 3), (7, 3), (6, 4)):
        c.set(x, y, dark)


def draw_taunt(c):
    orange, light = rgb('#ff8a3a'), rgb('#ffd29a')
    for y in range(1, 8):
        c.set(5, y, orange)
        c.set(6, y, orange)
    c.set(5, 1, light)
    for x in (5, 6):
        c.set(x, 9, orange)
        c.set(x, 10, orange)


def draw_stun(c):
    yellow, light = rgb('#ffd84a'), rgb('#fff6c2')
    for cx, cy, size in ((3, 4, 2), (8, 7, 2)):
        c.set(cx, cy, light)
        for i in range(1, size + 1):
            for dx, dy in ((i, 0), (-i, 0), (0, i), (0, -i)):
                c.set(cx + dx, cy + dy, yellow)


def draw_guard(c):
    blue, light, dark = rgb('#5d8dff'), rgb('#b8ceff'), rgb('#2f4fa8')
    for y in range(1, 11):
        half = 4 if y < 7 else 4 - (y - 6)
        for x in range(6 - half, 6 + half):
            c.set(x, y, light if x < 6 and y < 5 else blue if x < 6 else dark)


def draw_aura(c):
    gold, light = rgb('#ffcc4a'), rgb('#fff1b8')
    for y in range(12):
        for x in range(12):
            d = math.hypot(x + 0.5 - 6, y + 0.5 - 6)
            if d <= 2.6:
                c.set(x, y, light if d < 1.4 else gold)
    for dx, dy in ((0, -5), (0, 4), (-5, 0), (4, 0), (-4, -4), (3, -4), (-4, 3), (3, 3)):
        c.set(6 + dx, 6 + dy, gold)


def make_slash(frame):
    """Crescent swipe; three frames: start, full, fade. Drawn for a rightward swing, rotated in-game."""
    core, edge = rgb('#ffffff'), rgb('#a8f4ff')
    c = Canvas()
    cx, cy, radius = 8.0, 16.0, 13.0
    start, end = ((-40, 10), (-62, 62), (15, 62))[frame]
    alpha = 255 if frame < 2 else 150
    for y in range(T):
        for x in range(T):
            dx, dy = x + 0.5 - cx, cy - (y + 0.5)
            angle = math.degrees(math.atan2(dy, dx))
            if not start <= angle <= end:
                continue
            t = (angle - start) / float(end - start)
            thickness = 3.4 * math.sin(math.pi * t) + 0.4
            off = abs(math.hypot(dx, dy) - radius)
            if off <= thickness / 2:
                color = core if off <= thickness / 4 else edge
                c.set(x, y, color[:3] + (alpha,))
    return c


SPRITES = {
    'Characters/uzuki': make_uzuki,
    'Characters/haiden': make_haiden,
    'Characters/kristela': make_kristela,
    'Characters/spider': make_spider,
    'Characters/troll': make_troll,
    'Characters/bat': make_bat,
    'Effects/shadow': make_shadow,
    'Effects/arrow': make_arrow,
    'Effects/snare': make_snare,
    'Effects/reticle': make_reticle,
    'Effects/punch': make_punch,
    'Effects/status_mark': lambda: icon(draw_mark),
    'Effects/status_rooted': lambda: icon(draw_root),
    'Effects/status_taunt': lambda: icon(draw_taunt),
    'Effects/status_stunned': lambda: icon(draw_stun),
    'Effects/status_guard': lambda: icon(draw_guard),
    'Effects/status_aura': lambda: icon(draw_aura),
    'Effects/slash_0': lambda: make_slash(0),
    'Effects/slash_1': lambda: make_slash(1),
    'Effects/slash_2': lambda: make_slash(2),
    'Tiles/floor_0': lambda: make_floor(0),
    'Tiles/floor_1': lambda: make_floor(1),
    'Tiles/floor_2': lambda: make_floor(2),
    'Tiles/floor_3': lambda: make_floor(3),
    'Tiles/wall_top': make_wall_top,
    'Tiles/wall_face': make_wall_face,
    'Tiles/shadow_top': make_shadow_top,
    'Tiles/stairs': make_stairs,
    'Items/berry': make_berry,
}

PREVIEW_MAP = [
    '##############',
    '##############',
    '#.....########',
    '#.......>....#',
    '#.....@...s..#',
    '#..b......s..#',
    '###.....######',
    '##############',
]


def make_preview(sprites, scale):
    """Mock-up of a dungeon room using the same tile rules as the game, enlarged for review."""
    h, w = len(PREVIEW_MAP), len(PREVIEW_MAP[0])
    floor = lambda x, y: 0 <= y < h and 0 <= x < w and PREVIEW_MAP[y][x] != '#'
    scene = Canvas(w * T, h * T, rgb('#0c0a12'))
    for y in range(h):
        for x in range(w):
            if floor(x, y):
                scene.paste(sprites['Tiles/floor_%d' % ((x * 7 + y * 3) % 4)], x * T, y * T)
                if not floor(x, y - 1):
                    scene.paste(sprites['Tiles/shadow_top'], x * T, y * T)
            else:
                scene.paste(sprites['Tiles/wall_face' if floor(x, y + 1) else 'Tiles/wall_top'], x * T, y * T)
    for y in range(h):
        for x in range(w):
            ch = PREVIEW_MAP[y][x]
            if ch == '>':
                scene.paste(sprites['Tiles/stairs'], x * T, y * T)
            elif ch == 'b':
                scene.paste(sprites['Items/berry'], x * T, y * T)
            elif ch in '@s':
                scene.paste(sprites['Effects/shadow'], x * T, y * T)
                scene.paste(sprites['Characters/uzuki' if ch == '@' else 'Characters/spider'], x * T, y * T, flip_x=ch == 's')
    scene.paste(sprites['Effects/slash_1'], 10 * T, 5 * T)
    # Large single-sprite close-ups along the bottom.
    strip = Canvas(w * T, 2 * T, rgb('#1d1a26'))
    x = 8
    for name in ('Characters/uzuki', 'Characters/haiden', 'Characters/kristela', 'Characters/spider', 'Characters/bat',
                 'Characters/troll', 'Items/berry', 'Effects/arrow', 'Effects/snare', 'Effects/punch',
                 'Effects/status_mark', 'Effects/status_rooted', 'Effects/status_taunt', 'Effects/status_stunned',
                 'Effects/status_guard', 'Effects/status_aura'):
        big = sprites[name].scaled(2 if sprites[name].h <= T else 1)
        strip.paste(big, x, 2 * T - big.h)
        x += big.w + 16
    full = Canvas(w * T, (h + 2) * T)
    full.paste(scene, 0, 0)
    full.paste(strip, 0, h * T)
    return full.scaled(scale)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--preview', help='also write an enlarged mock-up scene to this path')
    parser.add_argument('--scale', type=int, default=3)
    args = parser.parse_args()

    sprites = {}
    for name, make in SPRITES.items():
        sprites[name] = make()
        sprites[name].save(os.path.join(OUT, name + '.png'))
    print('Wrote %d sprites to %s' % (len(sprites), OUT))
    if args.preview:
        make_preview(sprites, args.scale).save(args.preview)
        print('Preview: %s' % args.preview)


if __name__ == '__main__':
    main()
