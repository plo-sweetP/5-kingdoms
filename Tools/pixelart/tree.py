"""
The skill-tree screen's art (docs/design/ART.md, "Skill tree screen"; PROGRESSION.md, "Building 1g", step 6): a dark
panel with gold trim, spheres that the game tints in each path's colour, the lines between them, the tier track's
marks, a lock for rows without content, and small badges. The pack's UI kit has nothing like it (its panels are
paper) and the reference's painted vines aren't pixel art, so everything here is drawn like the HUD's slim frame:
flat colours in the pack's outline, with corners that keep their pixels at any size.
"""
from draw import OUTLINE, THIN_REACH, Canvas, outlined
from px import Image, hexc
from ui import Piece

GOLD, GOLD_LIGHT, GOLD_DARK = hexc('#dcaa46'), hexc('#fffaba'), hexc('#886049')
NIGHT, NIGHT_LIGHT, NIGHT_DEEP = hexc('#232838'), hexc('#2f364b'), hexc('#1a1e2c')

# The three paths, left to right: the colours the game tints the white art with (SkillTreeScreen has the same).
PATH_COLORS = (hexc('#5aa9ee'), hexc('#7ccb62'), hexc('#bb82f0'))

# A sphere's tones, light to dark. They are greys so one drawing serves every path.
SPHERE_TONES = (hexc('#ffffff'), hexc('#e3e8ef'), hexc('#b4bdcb'), hexc('#8690a3'), hexc('#667086'))


def _rounded(c, x, y, w, h, colour, cut=2):
    """A rectangle with its corners cut off by `cut` pixels, like the kit's buttons."""
    for i in range(cut):
        c.rect(x + cut - i, y + i, w - 2 * (cut - i), 1, colour)
        c.rect(x + cut - i, y + h - 1 - i, w - 2 * (cut - i), 1, colour)
    c.rect(x, y + cut, w, h - 2 * cut, colour)


def dark_panel():
    """The screen's panel: dark, with a bevelled gold trim and a gold stud in each corner."""
    size = 40
    c = Canvas(size, size)
    _rounded(c, 0, 0, size, size, GOLD, cut=3)
    # A bevel: light along the top and the left, dark along the bottom and the right.
    _rounded(c, 0, 0, size, size - 1, GOLD_LIGHT, cut=3)
    _rounded(c, 1, 1, size - 1, size - 1, GOLD_DARK, cut=3)
    _rounded(c, 1, 1, size - 2, size - 2, GOLD, cut=3)
    _rounded(c, 3, 3, size - 6, size - 6, OUTLINE, cut=2)
    _rounded(c, 4, 4, size - 8, size - 8, NIGHT, cut=2)
    c.rect(6, 4, size - 12, 1, NIGHT_LIGHT)                    # A lighter line under the top trim.
    for sx, sy in ((2, 2), (size - 9, 2), (2, size - 9), (size - 9, size - 9)):   # The studs.
        c.polygon([(sx + 3.5, sy), (sx + 7, sy + 3.5), (sx + 3.5, sy + 7), (sx, sy + 3.5)], OUTLINE)
        c.polygon([(sx + 3.5, sy + 1), (sx + 6, sy + 3.5), (sx + 3.5, sy + 6), (sx + 1, sy + 3.5)], GOLD)
        c.set(sx + 3, sy + 2, GOLD_LIGHT)
        c.set(sx + 2, sy + 3, GOLD_LIGHT)
    return Piece('panel_dark', c.part().image, (14, 14, 14, 14))


def inset_piece():
    """A sunken box inside the dark panel (the info text, a row of the class list): darker, with a thin lit lower edge."""
    c = Canvas(14, 14)
    _rounded(c, 0, 0, 14, 14, NIGHT_LIGHT, cut=2)
    _rounded(c, 0, 0, 14, 13, OUTLINE, cut=2)
    _rounded(c, 1, 1, 12, 11, NIGHT_DEEP, cut=1)
    return Piece('panel_inset', c.part(outline=False).image, (5, 5, 5, 5))


def sphere(diameter):
    """
    A sphere lit from the upper left, in bands of grey like the pack shades everything (no gradients), with the
    pack's outline. White, so one drawing serves every path: the game tints it.
    """
    image = Image(diameter, diameter)
    centre = radius = diameter / 2.0
    for y in range(diameter):
        for x in range(diameter):
            dx, dy = x + 0.5 - centre, y + 0.5 - centre
            distance = (dx * dx + dy * dy) ** 0.5
            if distance > radius:
                continue
            # How far from the lit spot, up and to the left of the middle.
            lit = (((dx + radius * 0.32) ** 2 + (dy + radius * 0.36) ** 2) ** 0.5) / radius
            tone = 1 if lit < 0.42 else 2 if lit < 0.82 else 3 if lit < 1.12 else 4
            if distance > radius - 1.5:
                tone = max(tone, 3)        # The rim never catches the light.
            image.px[y * diameter + x] = SPHERE_TONES[tone]
    return outlined(image)


def sphere_shine(diameter):
    """The glint on a sphere, kept apart so it stays white over any tint. The same size as the sphere."""
    image = Image(diameter + 4, diameter + 4)
    radius = diameter / 2.0
    cx, cy = 2 + radius * 0.56, 2 + radius * 0.5
    for y in range(image.h):
        for x in range(image.w):
            nx, ny = (x + 0.5 - cx) / (radius * 0.2), (y + 0.5 - cy) / (radius * 0.13)
            if nx * nx + ny * ny <= 1.0:
                image.px[y * image.w + x] = hexc('#ffffff', 210)
    image.set(int(2 + radius * 0.84), int(2 + radius * 0.4), hexc('#ffffff', 170))
    return image


def sphere_glow(diameter):
    """The light around a sphere that can be picked or has been: rings of fading white, tinted in the path's colour."""
    size = diameter + 28
    image = Image(size, size)
    centre = size / 2.0
    for y in range(size):
        for x in range(size):
            distance = ((x + 0.5 - centre) ** 2 + (y + 0.5 - centre) ** 2) ** 0.5 - diameter / 2.0
            alpha = 150 if distance < 3 else 100 if distance < 6 else 60 if distance < 9 else 30 if distance < 12 else 0
            if alpha:
                image.px[y * size + x] = hexc('#ffffff', alpha)
    return image


def sphere_ring(diameter):
    """A gold band around the sphere the hero picked."""
    size = diameter + 12
    image = Image(size, size)
    centre = size / 2.0
    for y in range(size):
        for x in range(size):
            dx, dy = x + 0.5 - centre, y + 0.5 - centre
            distance = (dx * dx + dy * dy) ** 0.5
            if diameter / 2.0 + 1.0 < distance <= diameter / 2.0 + 4.0:
                image.px[y * size + x] = GOLD_LIGHT if dx + dy < -size * 0.28 else GOLD_DARK if dx + dy > size * 0.3 else GOLD
    return outlined(image, reach=THIN_REACH)


def line_piece():
    """A line between spheres: a white core in a thin outline, 9-sliced so it runs any length either way; tinted."""
    c = Canvas(2, 2)
    c.rect(0, 0, 2, 2, hexc('#ffffff'))
    return Piece('tree_line', c.part(reach=THIN_REACH).image, (1, 1, 1, 1))


def pip_pieces():
    """The tier track's marks: a small diamond for an ordinary tier, a larger one for a milestone. White: the game tints them."""
    white, shade = hexc('#ffffff'), hexc('#b4bdcb')
    small = Canvas(6, 6)
    small.polygon([(3, 0), (6, 3), (3, 6), (0, 3)], white)
    small.polygon([(3, 3), (6, 3), (3, 6)], shade, only=(white,))
    large = Canvas(12, 12)
    large.polygon([(6, 0), (12, 6), (6, 12), (0, 6)], white)
    large.polygon([(6, 6), (12, 6), (6, 12)], shade, only=(white,))
    return [Piece('tree_pip', small.part(reach=THIN_REACH).image), Piece('tree_node', large.part().image)]


def cursor_piece():
    """A gold box around what is selected, like the reference's: 9-sliced, empty inside."""
    c = Canvas(12, 12)
    c.rect(0, 0, 12, 12, GOLD)
    c.rect(0, 0, 12, 1, GOLD_LIGHT).rect(0, 0, 1, 12, GOLD_LIGHT)
    c.rect(0, 11, 12, 1, GOLD_DARK).rect(11, 0, 1, 12, GOLD_DARK)
    c.erase(2, 2, 8, 8)
    return Piece('tree_cursor', c.part(reach=THIN_REACH).image, (4, 4, 4, 4))


def lock_piece():
    """A padlock for a row whose options aren't written yet."""
    metal, light, dark = hexc('#b4bdcb'), hexc('#e3e8ef'), hexc('#667086')
    c = Canvas(16, 20)
    for y in range(0, 10):
        for x in range(16):
            d = ((x + 0.5 - 8) ** 2 + (y + 0.5 - 7) ** 2) ** 0.5
            if 3.2 <= d <= 5.8 and y < 8:
                c.set(x, y, metal if x >= 6 else light)
    c.rect(2, 8, 2, 2, light)
    c.rect(12, 8, 2, 2, metal)
    _rounded(c, 1, 9, 14, 11, metal, cut=1)
    c.rect(2, 10, 12, 2, light)
    c.rect(2, 17, 12, 2, dark)
    c.rect(7, 13, 2, 4, OUTLINE)
    c.rect(6, 12, 4, 2, OUTLINE)
    return Piece('lock', c.part().image)


def badge_pieces():
    """Small marks on a sphere: an arrow up for an option that upgrades a skill, a tick for one that is learned."""
    green, green_light = hexc('#6fae5f'), hexc('#c9f08c')
    up = Canvas(12, 12)
    up.polygon([(6, 0), (12, 6), (8, 6), (8, 12), (4, 12), (4, 6), (0, 6)], green)
    up.polygon([(6, 1), (9, 5), (3, 5)], green_light, only=(green,))
    tick = Canvas(14, 12)
    tick.curve([(1, 6), (5, 10), (12, 1)], GOLD, width=3)
    tick.curve([(1, 5), (5, 9), (12, 0)], GOLD_LIGHT, width=1)
    return [Piece('badge_upgrade', up.part().image), Piece('badge_learned', tick.part().image)]


def pieces():
    """Everything the skill-tree screen is built from, as UI pieces."""
    out = [dark_panel(), inset_piece(), line_piece(), cursor_piece(), lock_piece()] + pip_pieces() + badge_pieces()
    for name, diameter in (('sphere', 40), ('sphere_big', 52)):
        out.append(Piece(name, sphere(diameter)))
        out.append(Piece(name + '_shine', sphere_shine(diameter)))
        out.append(Piece(name + '_glow', sphere_glow(diameter)))
        out.append(Piece(name + '_ring', sphere_ring(diameter)))
    return out
