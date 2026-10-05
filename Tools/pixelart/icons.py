"""
64 px icons for the gear and hero screens to come (docs/design/ART.md, "Equipment that shows on the sprite"): one per
weapon, one per armor piece (head, body, hands, feet of each set) and one per ring set. Weapons and head pieces are
the same drawings the heroes wear; bodies, gloves, boots and rings are simple shapes in each set's colours with the
pack's outline.
"""
import math

import pack as P
from draw import Canvas, Part, outlined, stack
from px import Image, hexc

SIZE = 64
SLOTS = ('head', 'body', 'hands', 'feet')
GOLD, GOLD_DARK, GOLD_LIGHT = hexc('#dcaa46'), hexc('#886049'), hexc('#fffaba')


def fitted(image):
    """An image trimmed and centred on a 64 px icon (cut at the edges if it is larger)."""
    cut, _, _ = image.trimmed()
    out = Image(SIZE, SIZE)
    out.paste(cut, (SIZE - cut.w) // 2, (SIZE - cut.h) // 2)
    return out


def _merged(*images):
    out = images[0].copy()
    for image in images[1:]:
        out.paste(image)
    return out


def weapon_icons(wardrobe):
    """
    {weapon id: icon}: each weapon as it is held at rest, without its bearer and without what isn't the weapon itself
    (the Long Sword's shield, the bow's quiver, the hands on the staff).
    """
    import rigs
    warrior, archer = wardrobe.rigs['warrior'], wardrobe.rigs['archer']
    sword = warrior.layer('Sword')[0]
    grip = rigs.centroid(sword, rigs.GUARD_COLORS)
    angle = rigs._sword_axes([sword])[0][1]
    shield = warrior.layer('Shield')[0]
    box = shield.bbox()
    staff = wardrobe.weapons['mage_staff'].front[0].trimmed()[0]
    fists = wardrobe.weapons['gauntlets'].front[0]
    hands = fists.bbox()
    middle = (hands[0] + hands[2]) // 2
    left = fists.crop(hands[0], hands[1], middle - hands[0], hands[3] - hands[1]).trimmed()[0]
    right = fists.crop(middle, hands[1], hands[2] - middle, hands[3] - hands[1]).trimmed()[0]
    pair = Image(left.w + right.w + 4, max(left.h, right.h))
    pair.paste(left, 0, pair.h - left.h)
    pair.paste(right, left.w + 4, 0)
    crossed = sword.copy()
    crossed.paste(sword.flipped_x(), int(round(2 * grip[0])) - sword.w + 14, 0)
    return {
        'long_sword': fitted(sword),
        # A size down from what the heroes carry, so the whole blade fits the icon.
        'great_sword': fitted(rigs.reshape(sword, grip, angle, 1.18, 1.45, rigs.IRON)),
        'arcane_sword': fitted(sword.mapped(rigs.ARCANE)),
        'piercer_blade': fitted(rigs.reshape(sword, grip, angle, 1.18, 0.62, rigs.SILVER)),
        'dual_blades': fitted(crossed),
        'great_shield': fitted(rigs.reshape(shield, ((box[0] + box[2]) / 2.0, box[3] - 6), 0.0, 1.3, 1.34)),
        'mage_staff': fitted(staff.crop(0, 0, staff.w, min(staff.h, SIZE - 4))),   # Its head and as much pole as fits.
        'hunter_bow': fitted(archer.layer('Bow')[0]),
        'gauntlets': fitted(pair),
    }


def _body(cloth, metal):
    """A tunic: shoulders, a belt in the set's metal, a light front."""
    dark, light = cloth
    c = Canvas(44, 44)
    c.polygon([(8, 4), (16, 2), (22, 6), (28, 2), (36, 4), (43, 14), (37, 20), (34, 16), (35, 42), (9, 42), (10, 16), (7, 20), (1, 14)], dark)
    c.polygon([(14, 8), (22, 12), (30, 8), (31, 30), (13, 30)], light, only=(dark,))
    c.rect(9, 31, 26, 4, metal[1])
    c.rect(9, 34, 26, 1, metal[0])
    c.rect(20, 30, 4, 6, metal[2])
    return c.part()


def _hands(cloth, metal):
    """A pair of gloves with a cuff in the set's metal."""
    dark, light = cloth
    c = Canvas(48, 32)
    for x in (2, 26):
        c.polygon([(x + 4, 10), (x + 16, 10), (x + 19, 16), (x + 18, 27), (x + 4, 28), (x + 1, 20)], dark)
        c.polygon([(x + 5, 12), (x + 13, 12), (x + 14, 22), (x + 5, 22)], light, only=(dark,))
        c.rect(x + 3, 4, 14, 6, metal[1])
        c.rect(x + 3, 8, 14, 2, metal[0])
        c.rect(x + 4, 5, 5, 1, metal[2])
    return c.part()


def _feet(cloth, metal):
    """A pair of boots with a metal toe."""
    dark, light = cloth
    c = Canvas(52, 36)
    for x in (2, 26):
        c.polygon([(x + 4, 2), (x + 14, 2), (x + 14, 20), (x + 22, 24), (x + 22, 32), (x + 2, 32), (x + 4, 18)], dark)
        c.polygon([(x + 6, 4), (x + 11, 4), (x + 11, 20), (x + 6, 20)], light, only=(dark,))
        c.polygon([(x + 15, 24), (x + 22, 26), (x + 22, 32), (x + 15, 32)], metal[1])
        c.rect(x + 2, 30, 20, 2, metal[0])
    return c.part()


def armor_icons(armors):
    """{'<set id>_<slot>': icon} for the four pieces of every armor set."""
    out = {}
    for armor in armors:
        out[armor.id + '_head'] = fitted(armor.piece.image)
        out[armor.id + '_body'] = fitted(_body(armor.cloth, armor.metal).image)
        out[armor.id + '_hands'] = fitted(_hands(armor.cloth, armor.metal).image)
        out[armor.id + '_feet'] = fitted(_feet(armor.cloth, armor.metal).image)
    return out


def ring_icon(color):
    """A gold band with a stone in the ring set's colour."""
    c = Canvas(36, 40)
    c.ellipse(18.0, 26.0, 13.0, 11.0, GOLD)
    c.ellipse(18.0, 26.0, 8.0, 6.5, 0)
    c.ellipse(15.0, 23.0, 10.0, 8.0, GOLD_LIGHT, only=(GOLD,))
    c.ellipse(18.0, 26.5, 12.0, 10.0, GOLD, only=(GOLD_LIGHT,))
    c.ellipse(18.0, 31.0, 11.0, 6.0, GOLD_DARK, only=(GOLD,))
    c.ellipse(18.0, 25.0, 11.5, 9.0, GOLD, only=(GOLD_DARK,))
    band = c.part()
    g = Canvas(20, 18)
    g.polygon([(4, 2), (16, 2), (19, 8), (10, 16), (1, 8)], color)
    g.polygon([(4, 2), (10, 2), (8, 8), (1, 8)], hexc('#ffffff'), only=(color,))
    g.polygon([(10, 8), (19, 8), (10, 16)], _darker(color), only=(color,))
    stone = g.part().moved(8, 0)
    return fitted(stack([band, stone]).image)


def _darker(color):
    r, g, b = color & 255, (color >> 8) & 255, (color >> 16) & 255
    return (r * 6 // 10) | ((g * 6 // 10) << 8) | ((b * 6 // 10) << 16) | 0xFF000000


# ---- Skill icons: 24 px white glyphs in the pack's outline, to sit on a round button or on a sphere of the tree ----

INK, SHADE = hexc('#ffffff'), hexc('#bcc7d4')
GLYPH = 24


def _arrow(c, x0, y0, x1, y1, width=2, head=5.0, color=INK):
    """A shaft with a triangular head at (x1, y1)."""
    c.line(x0, y0, x1, y1, color, width)
    length = math.hypot(x1 - x0, y1 - y0) or 1.0
    ux, uy = (x1 - x0) / length, (y1 - y0) / length
    bx, by = x1 - ux * head, y1 - uy * head
    half = head * 0.7
    c.polygon([(x1 + ux * 1.5 + 0.5, y1 + uy * 1.5 + 0.5), (bx - uy * half + 0.5, by + ux * half + 0.5),
               (bx + uy * half + 0.5, by - ux * half + 0.5)], color)


def _ring(c, cx, cy, radius, thickness, color=INK, squash=1.0, keep=None):
    """A circle's outline (an ellipse with `squash` under 1); `keep(angle in degrees)` leaves gaps."""
    for y in range(GLYPH):
        for x in range(GLYPH):
            dx, dy = x + 0.5 - cx, (y + 0.5 - cy) / squash
            if radius - thickness <= math.hypot(dx, dy) <= radius and (keep is None or keep(math.degrees(math.atan2(dy, dx)))):
                c.set(x, y, color)


def _fist(c, x, y):
    """A closed fist seen from the side, knuckles to the right: 11 x 10."""
    c.rect(x + 1, y, 9, 10, INK)
    c.rect(x, y + 1, 11, 8, INK)
    for row in (2, 5):                                   # The fingers, rolled in.
        c.rect(x + 5, y + row, 6, 1, SHADE)
    c.rect(x + 1, y + 6, 5, 1, SHADE)                    # The thumb across them.
    c.rect(x + 5, y + 6, 1, 3, SHADE)
    c.rect(x, y + 8, 4, 1, SHADE)


def _shield(c, x, y, w, h, color=INK, shade=SHADE):
    """A heater shield: flat top, pointed foot, its right half in shade."""
    middle = x + w / 2.0
    c.polygon([(x, y), (x + w, y), (x + w, y + h * 0.5), (middle, y + h), (x, y + h * 0.5)], color)
    c.polygon([(middle, y), (x + w, y), (x + w, y + h * 0.5), (middle, y + h)], shade, only=(color,))


def _star(c, cx, cy, r, color=INK):
    """A four-pointed star."""
    k = r * 0.3
    c.polygon([(cx, cy - r), (cx + k, cy - k), (cx + r, cy), (cx + k, cy + k), (cx, cy + r), (cx - k, cy + k),
               (cx - r, cy), (cx - k, cy - k)], color)


def _plus(c, cx, cy, arm, width, color=INK):
    c.rect(cx - width // 2, cy - arm, width, 2 * arm, color)
    c.rect(cx - arm, cy - width // 2, 2 * arm, width, color)


def _hunters_mark(c):
    _ring(c, 12, 12, 8.6, 2.6)
    for x, y, w, h in ((11, 0, 2, 7), (11, 17, 2, 7), (0, 11, 7, 2), (17, 11, 7, 2)):
        c.rect(x, y, w, h, INK)
    c.rect(11, 11, 2, 2, SHADE)


def _power_shot(c):
    _arrow(c, 4, 20, 18, 6, width=3, head=9.0)
    c.line(2, 13, 6, 9, SHADE, 2)                        # It leaves the string hard.
    c.line(11, 22, 15, 18, SHADE, 2)


def _rolling_shot(c):
    _ring(c, 11, 13, 8.5, 2.6, keep=lambda a: not -75 < a < -5)
    c.polygon([(13, 2), (21, 5), (14, 11)], INK)         # The roll's arrowhead, where the circle opens.
    c.rect(10, 12, 3, 3, SHADE)


def _volley(c):
    _arrow(c, 5, 1, 5, 13, head=5.0)
    _arrow(c, 12, 5, 12, 20, head=6.0)
    _arrow(c, 19, 1, 19, 13, head=5.0)


def _heal(c):
    _plus(c, 12, 12, 9, 6)
    c.rect(9, 19, 6, 2, SHADE)
    c.rect(3, 13, 6, 2, SHADE)
    c.rect(15, 13, 6, 2, SHADE)


def _sword(c, tip=1, guard=15):
    c.polygon([(12, tip), (15, tip + 4), (15, guard), (9, guard), (9, tip + 4)], INK)
    c.polygon([(12, tip), (15, tip + 4), (15, guard), (12, guard)], SHADE, only=(INK,))
    c.rect(6, guard, 12, 2, INK)
    c.rect(11, guard + 2, 2, 5, SHADE)
    c.rect(10, guard + 6, 4, 2, INK)


def _divine_strike(c):
    _sword(c, tip=4, guard=15)
    for x0, y0, x1, y1 in ((3, 2, 6, 5), (20, 2, 17, 5), (1, 9, 5, 9), (22, 9, 18, 9)):   # It lights up.
        c.line(x0, y0, x1, y1, INK, 2)
    c.rect(11, 0, 2, 2, INK)


def _shoulder_bash(c):
    _shield(c, 2, 3, 13, 18)
    for x0, y0, x1, y1 in ((18, 6, 22, 3), (18, 12, 23, 12), (18, 18, 22, 21)):
        c.line(x0, y0, x1, y1, INK, 2)


def _aura(c):
    _ring(c, 12, 12, 11.5, 2.4)
    _shield(c, 7, 6, 10, 13)


def _piercing_punch(c):
    c.rect(15, 3, 2, 18, SHADE)                          # The first foe, and the blow going on through it.
    c.rect(20, 5, 2, 14, SHADE)
    _arrow(c, 11, 12, 22, 12, width=2, head=5.0)
    _fist(c, 1, 7)


def _ki_heal(c):
    c.polygon([(12, 0), (17, 8), (20, 14), (18, 20), (12, 23.5), (6, 20), (4, 14), (7, 8)], INK)
    c.polygon([(12, 12), (20, 14), (18, 20), (12, 23.5)], SHADE, only=(INK,))
    c.erase(11, 10, 2, 9)                                # A plus, cut out: the outline fills it in dark.
    c.erase(8, 13, 8, 2)


def _stun_strike(c):
    _fist(c, 1, 12)
    _star(c, 17, 7, 6.5)
    _star(c, 8, 5, 3.0, SHADE)


def _flurry(c):
    _fist(c, 12, 7)
    c.rect(1, 6, 9, 2, INK)
    c.rect(4, 11, 6, 2, SHADE)
    c.rect(1, 16, 9, 2, INK)


def _crippling_shot(c):
    _arrow(c, 2, 16, 14, 4, width=2, head=6.0)
    c.curve([(12, 13), (16, 17), (20, 13)], INK, 2)      # Two chevrons down: slower.
    c.curve([(12, 18), (16, 22), (20, 18)], SHADE, 2)


def _bouncing_shot(c):
    c.curve([(2, 20), (8, 8), (14, 18)], SHADE, 2)
    _arrow(c, 14, 18, 20, 5, width=2, head=5.0)
    c.rect(7, 6, 3, 3, INK)
    c.rect(13, 18, 3, 3, INK)


def _shield_wall(c):
    _shield(c, 0, 6, 10, 14, SHADE, SHADE)
    _shield(c, 14, 6, 10, 14, SHADE, SHADE)
    _shield(c, 6, 3, 12, 18)


def _healing_word(c):
    c.rect(3, 2, 18, 14, INK)
    c.rect(2, 3, 20, 12, INK)
    c.polygon([(6, 15), (6, 22), (13, 15)], INK)
    c.rect(3, 13, 18, 2, SHADE, only=(INK,))
    c.erase(11, 4, 2, 9)
    c.erase(8, 8, 8, 2)


def _sweeping_slash(c):
    for y in range(GLYPH):
        for x in range(GLYPH):
            ax, ay = (x + 0.5 - 12) / 11.5, (y + 0.5 - 15) / 12.5
            bx, by = (x + 0.5 - 12) / 11.5, (y + 0.5 - 19) / 10.5
            if ax * ax + ay * ay <= 1.0 and bx * bx + by * by > 1.0 and y < 17:
                c.set(x, y, INK if x < 14 else SHADE)
    c.polygon([(19, 13), (23, 20), (16, 18)], INK)       # Where the blade ends up.


def _step_of_the_wind(c):
    c.curve([(1, 6), (14, 6), (17, 4), (16, 1), (13, 2)], INK, 2)
    c.curve([(4, 12), (19, 12), (22, 10), (21, 7), (18, 8)], INK, 2)
    c.curve([(1, 18), (11, 18), (14, 20), (13, 22), (11, 21)], SHADE, 2)


def _attack_bow(c):
    c.curve([(7, 1), (14, 6), (16, 12), (14, 17), (7, 22)], INK, 2)
    c.line(7, 1, 7, 22, SHADE, 1)
    _arrow(c, 3, 12, 21, 12, width=2, head=5.0)


def _attack_sword(c):
    c.line(8, 15, 20, 3, INK, 3)
    c.line(9, 16, 20, 5, SHADE, 1)
    c.line(4, 11, 12, 19, INK, 2)
    c.line(3, 20, 6, 17, SHADE, 2)
    c.rect(1, 20, 3, 3, INK)


def _attack_fists(c):
    _fist(c, 4, 7)
    for x0, y0, x1, y1 in ((18, 6, 21, 3), (18, 12, 22, 12), (18, 17, 21, 20)):
        c.line(x0, y0, x1, y1, INK, 2)


def _momentum(c):
    for shift, color in ((0, SHADE), (8, INK)):
        c.polygon([(1 + shift, 4), (9 + shift, 12), (1 + shift, 20), (6 + shift, 20), (14 + shift, 12), (6 + shift, 4)], color)


def _snare(c):
    _ring(c, 12, 15, 10.0, 2.6, squash=0.55)
    c.rect(3, 6, 2, 8, SHADE)
    c.rect(19, 6, 2, 8, SHADE)
    c.rect(10, 7, 4, 3, INK)


def _unknown(c):
    c.polygon([(12, 3), (21, 12), (12, 21), (3, 12)], INK)
    c.polygon([(12, 12), (21, 12), (12, 21)], SHADE, only=(INK,))


# By skill id (SkillCatalog), plus the weapon attacks by weapon family, the passives, and one for anything unknown.
SKILL_GLYPHS = {
    'hunters_mark': _hunters_mark, 'power_shot': _power_shot, 'rolling_shot': _rolling_shot, 'volley': _volley,
    'paladin_heal': _heal, 'divine_strike': _divine_strike, 'shoulder_bash': _shoulder_bash, 'aura_of_protection': _aura,
    'piercing_punch': _piercing_punch, 'ki_heal': _ki_heal, 'stun_strike': _stun_strike, 'flurry_of_blows': _flurry,
    'crippling_shot': _crippling_shot, 'bouncing_shot': _bouncing_shot, 'shield_wall': _shield_wall,
    'healing_word': _healing_word, 'sweeping_slash': _sweeping_slash, 'step_of_the_wind': _step_of_the_wind,
    'attack_bow': _attack_bow, 'attack_sword': _attack_sword, 'attack_fists': _attack_fists,
    'momentum': _momentum, 'snare': _snare, 'unknown': _unknown,
}


def skill_icons():
    """{skill id: icon}: a white glyph with the pack's outline, 28 px with it."""
    out = {}
    for name, draw in SKILL_GLYPHS.items():
        c = Canvas(GLYPH, GLYPH)
        draw(c)
        out[name] = c.part().image
    return out


def build(wardrobe, ring_sets):
    """Every icon as {'Icons/<name>': image}."""
    out = {}
    for skill_id, image in skill_icons().items():
        out['Icons/skill_' + skill_id] = image
    for weapon_id, image in weapon_icons(wardrobe).items():
        out['Icons/weapon_' + weapon_id] = image
    for name, image in armor_icons(wardrobe.armors.values()).items():
        out['Icons/armor_' + name] = image
    for set_id, color in ring_sets:
        out['Icons/ring_' + set_id] = ring_icon(hexc(color))
    return out
