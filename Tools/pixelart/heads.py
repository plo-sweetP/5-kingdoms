"""
The heroes' own heads and the armor sets' looks (docs/design/ART.md, "Heroes built from layers" and "Equipment that
shows on the sprite").

Everything here is drawn in "head space": the pixel grid of the pack Archer's head drawing, whose face is where a
hero's face goes (eyes around x 21-35, y 31-35; the head spans about 0-41 by 0-48, looking right). A rig says where
that grid's origin is on each of its frames, so one head works on every rig.

A hero's head comes in parts, stacked in this order with an armor set's head piece:
  back   hair behind the head and body (long hair, headband tails); always shown
  skull  the head itself with the face
  side   hair over the back and side of the head; always shown, so it pokes out under any helmet
  bangs  the fringe over the forehead; shown under hoods and open helmets
  full   the whole hairstyle (crown and fringe in one piece); shown bare-headed and under hats
  piece  the armor set's head piece
"""
import pack as P
from draw import OUTLINE, Canvas, Part, outlined, stack
from px import Image, hexc

# What a head part may reach, relative to the head-space origin: (x0, y0, x1, y1). Rig cells leave this much room.
HEAD_BOX = (-26, -20, 56, 70)

BLUSH = hexc('#f0a08c')
EYE_WHITE = hexc('#ffffff')


# ---- the head and the face ----

def skull(eye, lashes=False, blush=False):
    """
    A bare head in the pack's skin tones with the hero's eyes: the pack's dark dot on top (with a glint), the hero's
    eye colour below it, so the colour reads at game size.
    """
    c = Canvas(48, 40, ox=0, oy=-14)
    c.ellipse(23.5, 32.5, 15.5, 13.0, P.SKIN_DARK)
    c.ellipse(26.5, 36.0, 11.5, 8.5, P.SKIN, only=(P.SKIN_DARK,))        # Lit lower front, like the pack's faces.
    for ex, ey in ((21, 32), (32, 31)):
        c.rect(ex, ey, 3, 2, OUTLINE)
        c.set(ex + 2, ey + 1, EYE_WHITE)
        c.rect(ex, ey + 2, 3, 2, eye)
    if lashes:
        c.dots(OUTLINE, [(20, 31), (20, 32), (35, 30), (35, 31)])
    if blush:
        c.dots(BLUSH, [(19, 37), (20, 37), (34, 36), (35, 36)])
    c.dots(OUTLINE, [(28, 40), (29, 40)])                                  # A small mouth.
    return c.part()


def _hair(shapes, light=(), dark=(), tones=None, erase=(), then=(), extra=None):
    """
    Hair from polygons: every shape in the mid tone, the `erase` shapes cut away (the face), the `then` shapes added
    over the cut (a fringe), and last the parts inside `dark` and `light` shapes re-toned. Shapes are
    ('poly', points) or ('oval', cx, cy, rx, ry).
    """
    lit, mid, shade = tones
    c = Canvas(96, 112, ox=30, oy=22)

    def put(shape, color, only=None):
        if shape[0] == 'poly':
            c.polygon(shape[1], color, only)
        else:
            c.ellipse(shape[1], shape[2], shape[3], shape[4], color, only)

    for shape in shapes:
        put(shape, mid)
    for shape in erase:
        put(shape, 0)
    for shape in then:
        put(shape, mid)
    for shape in dark:
        put(shape, shade, only=(mid,))
    for shape in light:
        put(shape, lit, only=(mid,))
    if extra:
        extra(c)
    return c.part().trimmed()


def poly(*points):
    return ('poly', list(points))


def oval(cx, cy, rx, ry):
    return ('oval', cx, cy, rx, ry)


FACE = poly((14, 28), (42, 25), (44, 50), (12, 50), (12, 38))   # What the hairline leaves open.


# ---- the three heroes ----

UZUKI_HAIR = (hexc('#7fd0f2'), hexc('#3f8ee0'), hexc('#2a55a8'))
HAIDEN_HAIR = (hexc('#f7a05c'), hexc('#d9642b'), hexc('#94401a'))
KRISTELA_HAIR = (hexc('#fff3a8'), hexc('#f3d34a'), hexc('#c9972a'))
CLIP = hexc('#e8862e')


def uzuki():
    """Spiky blue hair, cyan eyes."""
    tones = UZUKI_HAIR
    fringe = [poly((14, 21), (41, 19), (41, 24), (14, 26)),
              poly((15, 24), (18, 30), (22, 24)), poly((23, 24), (27, 29), (31, 23)), poly((31, 23), (37, 27), (40, 21))]
    full = _hair(
        [oval(21, 24, 18.5, 12.5),
         poly((4, 18), (-3, 8), (13, 12)), poly((11, 14), (11, 0), (22, 10)), poly((20, 12), (27, -3), (31, 11)),
         poly((29, 12), (41, 3), (38, 17)), poly((36, 16), (46, 13), (39, 23))],
        erase=[FACE], then=fringe,
        light=[oval(26, 14, 10, 3.5), poly((11, 4), (12, 0), (17, 8)), poly((25, 2), (27, -3), (29, 6)), poly((36, 8), (41, 3), (39, 12))],
        dark=[oval(6, 29, 6, 9)], tones=tones)
    bangs = _hair(fringe, tones=tones)
    side = _hair([poly((3, 26), (13, 27), (13, 39), (10, 47), (6, 41), (2, 45), (1, 36), (-4, 37), (1, 29))],
                 dark=[poly((-5, 34), (14, 34), (14, 50), (-5, 50))], tones=tones)
    back = _hair([poly((0, 22), (-7, 19), (1, 30)), poly((1, 30), (-8, 33), (3, 38))], dark=[oval(-2, 30, 6, 8)], tones=tones)
    return {'back': back, 'skull': skull(hexc('#35d6ee')), 'side': side, 'bangs': bangs, 'full': full}


def haiden():
    """
    Spiky orange-red hair and red eyes. The concept sketch's blue headband isn't part of his head (Peter, 2026-10-04):
    it would stick out from under every head piece. It can come back as a head piece of its own.
    """
    tones = HAIDEN_HAIR
    fringe = [poly((14, 21), (41, 19), (41, 24), (14, 26)),
              poly((15, 24), (19, 30), (23, 24)), poly((25, 24), (29, 29), (33, 23)), poly((34, 23), (39, 27), (41, 21))]
    full = _hair(
        [oval(21, 24, 18.5, 12.5),
         poly((3, 20), (-4, 12), (11, 13)), poly((8, 14), (5, 2), (18, 10)), poly((16, 11), (20, -2), (26, 10)),
         poly((24, 11), (33, 0), (33, 12)), poly((31, 13), (43, 6), (38, 18)), poly((36, 16), (45, 16), (39, 23))],
        erase=[FACE], then=fringe,
        light=[oval(25, 14, 9, 3.5), poly((7, 6), (5, 2), (12, 9)), poly((19, 3), (20, -2), (23, 7)), poly((30, 5), (33, 0), (33, 9))],
        dark=[oval(6, 29, 6, 9)], tones=tones)
    bangs = _hair(fringe, tones=tones)
    side = _hair([poly((3, 26), (13, 27), (13, 38), (9, 45), (6, 40), (2, 43), (1, 35), (-3, 35), (1, 29))],
                 dark=[poly((-5, 34), (14, 34), (14, 50), (-5, 50))], tones=tones)
    back = _hair([poly((0, 22), (-6, 18), (1, 29)), poly((1, 29), (-7, 31), (3, 37))], dark=[oval(-2, 29, 6, 8)], tones=tones)
    return {'back': back, 'skull': skull(hexc('#e8322c')), 'side': side, 'bangs': bangs, 'full': full}


def kristela():
    """Long wavy blonde hair, blue eyes, a gold X-shaped clip; lashes and a blush."""
    tones = KRISTELA_HAIR

    def clip(c):
        for i in range(5):
            c.set(8 + i, 27 + i, CLIP)
            c.set(12 - i, 27 + i, CLIP)
        c.set(10, 29, hexc('#fff3a8'))

    # A side-swept fringe: longer toward her cheek, parted over the far eye.
    fringe = [poly((14, 20), (41, 18), (41, 23), (38, 26), (33, 23), (26, 27), (19, 28), (14, 27))]
    full = _hair(
        [oval(21, 23.5, 19, 13.5)], erase=[FACE], then=fringe,
        light=[poly((16, 12), (30, 11), (33, 14), (16, 16))],
        dark=[oval(5, 31, 5, 8)], tones=tones, extra=clip)
    bangs = _hair(fringe, tones=tones)
    # Hair over the side of her head, running into the lock that falls in front of her shoulder.
    side = _hair([poly((3, 24), (13, 25), (14, 34), (12, 42), (14, 50), (11, 57), (8, 50), (9, 42), (5, 37), (2, 30))],
                 light=[poly((8, 30), (10, 30), (11, 48), (9, 48))],
                 dark=[poly((2, 30), (6, 30), (8, 44), (4, 40))], tones=tones, extra=clip)
    # The long hair down her back: it waves out past her shoulder and ends in a curl at her hip.
    back = _hair(
        [poly((2, 16), (-5, 26), (-8, 36), (-5, 44), (-9, 51), (-12, 58), (-8, 62), (-3, 59), (1, 63), (7, 59), (12, 62),
              (18, 58), (20, 40), (14, 18))],
        light=[poly((-3, 28), (0, 28), (-3, 54), (-6, 54))],
        dark=[poly((6, 40), (14, 40), (12, 63), (5, 63)), poly((-12, 57), (-8, 57), (-6, 63), (-10, 63))], tones=tones)
    return {'back': back, 'skull': skull(hexc('#3f86ee'), lashes=True, blush=True),
            'side': side, 'bangs': bangs, 'full': full}


def ribbon():
    """A pink bow at the back of the head (a cosmetic head piece)."""
    pink, pink_dark, pink_light = hexc('#f08fb0'), hexc('#b4527a'), hexc('#ffc4d6')
    c = Canvas(48, 40, ox=20, oy=0)
    c.polygon([(3, 19), (-7, 11), (-10, 20), (-5, 25)], pink)       # Back loop.
    c.polygon([(3, 19), (7, 9), (14, 13), (9, 22)], pink)           # Front loop.
    c.polygon([(1, 21), (-4, 30), (1, 31), (5, 24)], pink_dark)     # A tail.
    c.ellipse(3.5, 20.0, 2.6, 2.6, pink_dark)
    c.dots(pink_light, [(-6, 14), (-5, 15), (8, 12), (9, 13)])
    return c.part().trimmed()


HEROES = {'haiden': haiden, 'kristela': kristela, 'uzuki': uzuki}


# ---- armor sets: colours and head pieces ----

class Armor:
    def __init__(self, armor_id, name, cloth, metal, piece=None, piece_back=None, hair='side'):
        self.id, self.name = armor_id, name
        self.cloth = cloth        # (dark, light): replaces the pack's Blue cloth tones on the body.
        self.metal = metal        # (dark, mid, light): replaces the pack's metal tones on the body.
        self.piece, self.piece_back = piece, piece_back
        # How much of the hero's hair shows with the head piece on: 'full' (hats sit on the whole hairstyle),
        # 'bangs' (hoods and open helmets leave the fringe), 'side' (closed helmets: only what pokes out).
        self.hair = hair


STEEL = (P.METAL_DARK, P.METAL, P.METAL_LIGHT)


def _c(*names):
    return tuple(hexc(n) for n in names)


def extract(image, colors):
    """
    The piece of a pack drawing made of the given colours, with the dark lines that belong to it: those that touch
    nothing but the piece and empty space.
    """
    w, h = image.w, image.h
    out = Image(w, h)
    for i, p in enumerate(image.px):
        if p in colors:
            out.px[i] = p
    for i, p in enumerate(image.px):
        if p != OUTLINE:
            continue
        x, y = i % w, i // w
        near_piece, clean = False, True
        for dy in range(-2, 3):
            for dx in range(-2, 3):
                nx, ny = x + dx, y + dy
                if not (0 <= nx < w and 0 <= ny < h):
                    continue
                q = image.px[ny * w + nx]
                if q in colors:
                    near_piece = True
                elif q >> 24 and q != OUTLINE:
                    clean = False
        if near_piece and clean:
            out.px[i] = OUTLINE
    return out


def complete_outline(image, colors):
    """Add the pack's outline wherever the piece's fills (the given colours) lack one. Returns a Part grown by 2."""
    fills = Image(image.w, image.h)
    for i, p in enumerate(image.px):
        if p in colors:
            fills.px[i] = p
    ring = outlined(fills)
    out = Image(ring.w, ring.h)
    out.paste(ring)
    out.paste(image, 2, 2)
    return Part(out, -2, -2)


def archer_helm(pack):
    """
    The pack Archer's pointed helmet, lifted off the Archer's head, without its nose guard (Peter, 2026-10-04: the
    bar hung in front of the eyes), so both eyes show.
    """
    head = pack.ase(P.UNITS + '/Archer.aseprite')
    cel = head.cel(0, head.layer('Head'))
    metal = {P.METAL, P.METAL_DARK, P.METAL_LIGHT}
    piece = extract(cel.image, metal)
    w = piece.w
    for i, p in enumerate(piece.px):
        if not p >> 24:
            continue
        x, y = i % w, i // w
        if y >= 30 or (y == 29 and x >= 27):
            piece.px[i] = 0                       # The bar itself, below the helmet's rim.
        elif 20 <= x <= 32 and 20 <= y <= 29 and p in (P.METAL_DARK, OUTLINE):
            piece.px[i] = P.METAL                 # Its root on the dome, smoothed over.
    return complete_outline(piece, metal)


def warrior_helm(pack):
    """The pack Warrior's great helm with its front opened, so the hero's face shows."""
    head = pack.ase(P.UNITS + '/Warrior.aseprite')
    cel = head.cel(0, head.layer('Head'))
    tones = {P.SKIN, P.SKIN_DARK}
    c = Canvas(64, 64, ox=8, oy=8)
    # The helm's drawing sits at (1, 6) in head space. Its outer outline comes off (it goes back on at the end) and
    # the cross slit on its front is closed, so the visor can be cut where a hero's eyes and mouth are.
    w = cel.image.w
    for i, p in enumerate(cel.image.px):
        x, y = i % w, i // w
        if p in tones:
            c.set(x + 1, y + 6, p)
        elif p == OUTLINE and 13 <= x <= 36 and 12 <= y <= 39:
            near = [cel.image.get(x + dx, y + dy) for dx in (-4, 0, 4) for dy in (-4, 0, 4)]
            if sum(1 for q in near if q in tones) >= 5:
                c.set(x + 1, y + 6, P.SKIN)
    c.polygon([(18, 29), (22, 27), (36, 26), (41, 28), (41, 40), (37, 45), (23, 46), (18, 41)], 0)   # The open face.
    return c.part().trimmed()


def crest_cap():
    """Light Warrior: an open steel cap with a red crest from front to back."""
    c = Canvas(64, 48, ox=10, oy=12)
    c.ellipse(21.5, 20.0, 17.0, 13.0, P.METAL)
    c.rect(0, 22, 46, 20, 0)                                          # Cut level at the brow...
    c.polygon([(3, 22), (15, 22), (14, 31), (5, 30)], P.METAL)        # ...with a cheek guard at the back.
    c.ellipse(15.0, 17.0, 9.0, 8.0, P.METAL_DARK, only=(P.METAL,))
    c.ellipse(27.0, 13.5, 6.0, 3.0, P.METAL_LIGHT, only=(P.METAL,))
    c.rect(4, 20, 36, 2, P.METAL_DARK)
    crest, crest_dark, crest_light = hexc('#e76161'), hexc('#924159'), hexc('#f9856c')
    c.polygon([(4, 12), (8, 3), (20, -3), (32, 0), (38, 7), (33, 8), (22, 5), (12, 9), (8, 14)], crest)
    c.polygon([(4, 12), (8, 6), (14, 6), (8, 14)], crest_dark, only=(crest,))
    c.polygon([(18, -2), (30, -1), (34, 4), (22, 2)], crest_light, only=(crest,))
    return c.part().trimmed()


def wizard_hat():
    """Mage Robe: a pointed hat with a wide brim and a gold band, its tip bent back."""
    hat, hat_dark, hat_light = hexc('#ab6e9c'), hexc('#5c547e'), hexc('#d59ac6')
    gold, gold_dark = hexc('#dcaa46'), hexc('#886049')
    c = Canvas(80, 56, ox=18, oy=24)
    c.polygon([(8, 18), (13, 4), (11, -7), (4, -15), (-4, -17), (0, -11), (20, -7), (29, 5), (35, 18)], hat)
    c.polygon([(8, 18), (13, 4), (11, -6), (16, 4), (17, 18)], hat_dark, only=(hat,))
    c.polygon([(22, -2), (27, 4), (31, 14), (27, 14)], hat_light, only=(hat,))
    c.rect(8, 14, 28, 4, gold)
    c.rect(8, 16, 28, 2, gold_dark)
    c.ellipse(21.5, 21.5, 24.0, 5.0, hat)                             # Brim.
    c.ellipse(21.5, 23.0, 22.0, 3.0, hat_dark, only=(hat,))
    c.ellipse(29.0, 20.0, 10.0, 1.6, hat_light, only=(hat,))
    return c.part().trimmed()


def cavalier_hat():
    """Duelist's Leathers: a wide leather hat with a red feather swept back."""
    hat, hat_dark, hat_light = P.BROWN, P.BROWN_DARK, P.BROWN_LIGHT
    feather, feather_dark, feather_light = hexc('#e76161'), hexc('#924159'), hexc('#f9856c')
    c = Canvas(84, 52, ox=20, oy=20)
    c.ellipse(21.0, 20.0, 25.0, 5.5, hat)                             # Brim.
    c.ellipse(21.0, 21.5, 23.0, 3.4, hat_dark, only=(hat,))
    c.ellipse(21.5, 12.0, 14.0, 9.0, hat)                             # Crown.
    c.ellipse(15.0, 13.0, 7.0, 6.0, hat_dark, only=(hat,))
    c.ellipse(27.0, 8.0, 6.0, 2.6, hat_light, only=(hat,))
    c.rect(8, 15, 28, 2, hat_dark)
    c.polygon([(12, 14), (6, 4), (-2, -3), (-12, -5), (-15, 0), (-8, 1), (-2, 5), (4, 13), (8, 17)], feather)
    c.polygon([(8, 16), (2, 8), (-6, 2), (-13, 0), (-8, 1), (-2, 5), (4, 13)], feather_dark, only=(feather,))
    c.dots(feather_light, [(-9, -3), (-8, -3), (-5, -2), (-4, -1), (-1, 1), (0, 2)])
    return c.part().trimmed()


def pilgrim_hood():
    """Pilgrim's Vestments: a white cowl with gold trim around the face."""
    cloth, cloth_dark, cloth_light = hexc('#e9e3d2'), hexc('#b3a994'), hexc('#ffffff')
    gold = hexc('#dcaa46')
    c = Canvas(64, 64, ox=10, oy=10)
    c.ellipse(20.0, 25.0, 19.0, 21.0, cloth)
    c.polygon([(0, 30), (12, 30), (14, 46), (8, 50), (0, 46)], cloth)
    c.ellipse(27.5, 33.5, 13.5, 12.5, 0)                              # The face opening.
    c.rect(14, 46, 30, 12, 0)
    c.ellipse(9.0, 28.0, 8.0, 16.0, cloth_dark, only=(cloth,))
    c.ellipse(24.0, 9.0, 9.0, 3.5, cloth_light, only=(cloth,))
    for y in range(16, 48):                                           # Gold edge around the opening.
        for x in range(8, 44):
            if c.get(x, y) in (cloth, cloth_dark) and any(c.get(x + dx, y + dy) == 0 and 14 <= x + dx <= 41 and 21 <= y + dy <= 46
                                                           for dx, dy in ((1, 0), (0, 1), (-1, 0), (0, -1))):
                c.set(x, y, gold)
    return c.part().trimmed()


def winged_circlet():
    """Windrider's Cloak: a gold circlet with a white wing swept back from the temple."""
    gold, gold_dark = hexc('#dcaa46'), hexc('#886049')
    wing, wing_dark = hexc('#ffffff'), hexc('#a9d3d6')
    gem = hexc('#4697ac')
    c = Canvas(72, 48, ox=22, oy=14)
    c.polygon([(6, 21), (41, 18), (41, 21), (6, 24)], gold)
    c.polygon([(6, 23), (41, 20), (41, 21), (6, 24)], gold_dark, only=(gold,))
    c.polygon([(12, 23), (10, 9), (2, 1), (-8, -3), (-4, 5), (-12, 5), (-5, 12), (-10, 15), (0, 19), (6, 25)], wing)
    c.polygon([(-4, 5), (-12, 5), (-5, 12), (-10, 15), (0, 19), (2, 13)], wing_dark, only=(wing,))
    c.ellipse(34.5, 20.0, 2.5, 2.5, gem)
    return c.part().trimmed()


def shadow_hood():
    """Shadowstalker: a dark hood drawn low, with a mask over nose and mouth; only the eyes show."""
    cloth, cloth_dark, cloth_light = hexc('#4b4468'), hexc('#2e2a44'), hexc('#6a618f')
    c = Canvas(64, 64, ox=10, oy=10)
    c.polygon([(20, -2), (30, 2), (38, 12), (42, 24), (41, 28), (14, 30), (13, 46), (6, 50), (0, 44), (0, 22), (6, 8), (13, 1)], cloth)
    c.polygon([(0, 22), (6, 8), (12, 12), (9, 30), (11, 46), (6, 50), (0, 44)], cloth_dark, only=(cloth,))
    c.polygon([(22, 4), (30, 6), (36, 14), (28, 12)], cloth_light, only=(cloth,))
    c.polygon([(15, 38), (40, 37), (40, 42), (34, 48), (22, 48), (15, 44)], cloth_dark)    # The mask.
    c.rect(15, 38, 26, 1, cloth)
    return c.part().trimmed()


def horned_hide():
    """Bloodrage Hide: a fur cap with two bone horns."""
    fur, fur_dark, fur_light = hexc('#a0623c'), hexc('#6b3f2c'), hexc('#c98a52')
    bone, bone_dark = P.SKIN, P.SKIN_DARK
    c = Canvas(72, 56, ox=16, oy=16)
    c.polygon([(8, 10), (4, 0), (-4, -8), (-10, -8), (-6, -2), (-2, 8), (2, 16)], bone)        # Back horn.
    c.polygon([(32, 8), (38, -2), (46, -8), (50, -6), (45, -1), (41, 8), (37, 14)], bone)      # Front horn.
    c.polygon([(8, 10), (2, 2), (-4, -4), (-2, 6), (2, 16)], bone_dark, only=(bone,))
    c.polygon([(37, 14), (41, 8), (45, -1), (41, 2), (36, 10)], bone_dark, only=(bone,))
    c.ellipse(21.0, 18.0, 18.5, 13.0, fur)
    c.rect(-2, 24, 50, 16, 0)
    c.polygon([(2, 22), (14, 23), (14, 34), (10, 40), (7, 34), (3, 38), (2, 30)], fur)         # Fur flap over the ear.
    for x in range(4, 40, 5):                                                                  # A ragged edge.
        c.polygon([(x, 23), (x + 2, 27), (x + 4, 23)], fur)
    c.polygon([(2, 14), (10, 8), (12, 20), (9, 34), (3, 38), (2, 30)], fur_dark, only=(fur,))
    c.ellipse(27.0, 10.0, 7.0, 3.0, fur_light, only=(fur,))
    return c.part().trimmed()


def armors(pack):
    """GEAR.md's nine armor sets, each with body colours and a head piece."""
    return [
        Armor('archers_garb', "Archer's Garb", _c('#3f6b4a', '#6fae5f'), STEEL, archer_helm(pack), hair='side'),
        Armor('light_warrior', 'Light Warrior', _c('#924159', '#e76161'), STEEL, crest_cap(), hair='side'),
        Armor('mage_robe', 'Mage Robe', _c('#5c547e', '#ab6e9c'), _c('#886049', '#dcaa46', '#fffaba'), wizard_hat(), hair='full'),
        Armor('heavy_armor', 'Heavy Armor', (P.CLOTH_DARK, P.CLOTH_LIGHT), STEEL, warrior_helm(pack), hair='side'),
        Armor('duelists_leathers', "Duelist's Leathers", (P.BROWN_DARK, P.BROWN), _c('#947467', '#cf9c71', '#e8ce91'), cavalier_hat(), hair='full'),
        Armor('pilgrims_vestments', "Pilgrim's Vestments", _c('#b3a994', '#e9e3d2'), _c('#886049', '#dcaa46', '#fffaba'), pilgrim_hood(), hair='bangs'),
        Armor('windriders_cloak', "Windrider's Cloak", _c('#3d8f9f', '#8fd9d2'), STEEL, winged_circlet(), hair='full'),
        Armor('shadowstalker', 'Shadowstalker', _c('#2e2a44', '#4b4468'), _c('#5a6366', '#8c9695', '#b8c1c3'), shadow_hood(), hair='side'),
        Armor('bloodrage_hide', 'Bloodrage Hide', _c('#6b3f2c', '#a0623c'), _c('#947467', '#cf9c71', '#e8ce91'), horned_hide(), hair='bangs'),
    ]


# ---- cosmetic head pieces: worn instead of an armor set's head piece, over the whole hairstyle ----

class Cosmetic:
    def __init__(self, cosmetic_id, name, piece):
        self.id, self.name, self.piece = cosmetic_id, name, piece
        self.hair = 'full'
        self.piece_back = None


def crown():
    """A small gold crown with three points and a red stone, sitting on top of the hair."""
    gold, gold_dark, gold_light = hexc('#dcaa46'), hexc('#886049'), hexc('#fffaba')
    stone = hexc('#e76161')
    c = Canvas(40, 24, ox=-10, oy=-2)
    c.polygon([(12, 16), (12, 6), (17, 11), (22, 3), (27, 11), (32, 5), (32, 15)], gold)
    c.polygon([(12, 13), (32, 12), (32, 15), (12, 16)], gold_dark, only=(gold,))
    c.polygon([(13, 8), (15, 10), (15, 13), (13, 13)], gold_light, only=(gold,))
    c.dots(stone, [(21, 9), (22, 9), (21, 10), (22, 10)])
    c.dots(gold_light, [(22, 4), (22, 5)])
    return c.part().trimmed()


def headband():
    """A blue headband across the forehead, its long tails streaming back (the one from Haiden's concept sketch)."""
    lit, mid, shade = hexc('#6f9bff'), hexc('#2f62d6'), hexc('#1d3c8a')
    c = Canvas(96, 64, ox=30, oy=8)
    c.polygon([(6, 21), (-4, 20), (-13, 25), (-19, 34), (-15, 36), (-9, 29), (2, 26)], mid)      # Upper tail.
    c.polygon([(5, 24), (-2, 27), (-7, 36), (-8, 46), (-4, 46), (-3, 38), (2, 30), (6, 27)], shade)   # Lower tail.
    c.ellipse(4.0, 23.5, 3.0, 3.0, mid)                                                             # The knot.
    c.polygon([(5, 20), (41, 17), (41, 22), (5, 25)], mid)
    c.polygon([(5, 23), (41, 20), (41, 22), (5, 25)], shade, only=(mid,))
    c.polygon([(24, 18), (36, 17), (36, 19), (24, 20)], lit, only=(mid,))
    c.dots(lit, [(-5, 22), (-6, 23), (-7, 23), (-11, 27), (-12, 28)])
    return c.part().trimmed()


def cosmetics():
    """Head pieces that aren't part of an armor set (Peter, 2026-10-04: a bow, a crown, a headband, for variety)."""
    return [Cosmetic('hair_bow', 'Hair Bow', ribbon()), Cosmetic('crown', 'Crown', crown()),
            Cosmetic('headband', 'Headband', headband())]


def head_layers(parts, armor=None):
    """
    (back part, front part) of a hero's head in head space: bare, or under a head piece (an armor set's, or a
    cosmetic one: anything with a `piece` and a `hair` mode).
    """
    front = [parts['skull'], parts['side']]
    hair = 'full' if armor is None or armor.piece is None else armor.hair
    if hair == 'full':
        front.append(parts['full'])
    elif hair == 'bangs':
        front.append(parts['bangs'])
    if armor is not None and armor.piece is not None:
        front.append(armor.piece)
    back = [parts['back']]
    if armor is not None and armor.piece_back is not None:
        back.append(armor.piece_back)
    return stack(back), stack(front)


def build_head(hero, armor=None):
    return head_layers(HEROES[hero](), armor)
