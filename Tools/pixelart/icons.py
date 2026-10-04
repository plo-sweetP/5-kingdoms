"""
64 px icons for the gear and hero screens to come (docs/design/ART.md, "Equipment that shows on the sprite"): one per
weapon, one per armor piece (head, body, hands, feet of each set) and one per ring set. Weapons and head pieces are
the same drawings the heroes wear; bodies, gloves, boots and rings are simple shapes in each set's colours with the
pack's outline.
"""
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


def build(wardrobe, ring_sets):
    """Every icon as {'Icons/<name>': image}."""
    out = {}
    for weapon_id, image in weapon_icons(wardrobe).items():
        out['Icons/weapon_' + weapon_id] = image
    for name, image in armor_icons(wardrobe.armors.values()).items():
        out['Icons/armor_' + name] = image
    for set_id, color in ring_sets:
        out['Icons/ring_' + set_id] = ring_icon(hexc(color))
    return out
