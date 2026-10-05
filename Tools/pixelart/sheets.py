"""
Preview sheets for review on a phone: about 1000 px wide, as tall as they need to be, pixel art enlarged by whole
numbers (docs/design/ART.md, "Preview sheets for Peter"). Labels use a small built-in 5x7 font.
"""
import os

import looks
from px import Image, hexc

FONT = {
    'A': '01110 10001 10001 11111 10001 10001 10001', 'B': '11110 10001 10001 11110 10001 10001 11110',
    'C': '01110 10001 10000 10000 10000 10001 01110', 'D': '11110 10001 10001 10001 10001 10001 11110',
    'E': '11111 10000 10000 11110 10000 10000 11111', 'F': '11111 10000 10000 11110 10000 10000 10000',
    'G': '01110 10001 10000 10111 10001 10001 01111', 'H': '10001 10001 10001 11111 10001 10001 10001',
    'I': '01110 00100 00100 00100 00100 00100 01110', 'J': '00111 00010 00010 00010 00010 10010 01100',
    'K': '10001 10010 10100 11000 10100 10010 10001', 'L': '10000 10000 10000 10000 10000 10000 11111',
    'M': '10001 11011 10101 10101 10001 10001 10001', 'N': '10001 11001 10101 10011 10001 10001 10001',
    'O': '01110 10001 10001 10001 10001 10001 01110', 'P': '11110 10001 10001 11110 10000 10000 10000',
    'Q': '01110 10001 10001 10001 10101 10010 01101', 'R': '11110 10001 10001 11110 10100 10010 10001',
    'S': '01111 10000 10000 01110 00001 00001 11110', 'T': '11111 00100 00100 00100 00100 00100 00100',
    'U': '10001 10001 10001 10001 10001 10001 01110', 'V': '10001 10001 10001 10001 10001 01010 00100',
    'W': '10001 10001 10001 10101 10101 11011 10001', 'X': '10001 10001 01010 00100 01010 10001 10001',
    'Y': '10001 10001 01010 00100 00100 00100 00100', 'Z': '11111 00001 00010 00100 01000 10000 11111',
    '0': '01110 10001 10011 10101 11001 10001 01110', '1': '00100 01100 00100 00100 00100 00100 01110',
    '2': '01110 10001 00001 00010 00100 01000 11111', '3': '11110 00001 00001 01110 00001 00001 11110',
    '4': '00010 00110 01010 10010 11111 00010 00010', '5': '11111 10000 11110 00001 00001 10001 01110',
    '6': '00110 01000 10000 11110 10001 10001 01110', '7': '11111 00001 00010 00100 01000 01000 01000',
    '8': '01110 10001 10001 01110 10001 10001 01110', '9': '01110 10001 10001 01111 00001 00010 01100',
    ' ': '00000 00000 00000 00000 00000 00000 00000', '.': '00000 00000 00000 00000 00000 01100 01100',
    ',': '00000 00000 00000 00000 01100 00100 01000', ':': '00000 01100 01100 00000 01100 01100 00000',
    '-': '00000 00000 00000 11111 00000 00000 00000', '+': '00000 00100 00100 11111 00100 00100 00000',
    "'": '00100 00100 01000 00000 00000 00000 00000', '(': '00010 00100 01000 01000 01000 00100 00010',
    ')': '01000 00100 00010 00010 00010 00100 01000', '/': '00001 00001 00010 00100 01000 10000 10000',
    '?': '01110 10001 00001 00010 00100 00000 00100', '!': '00100 00100 00100 00100 00100 00000 00100',
    '=': '00000 00000 11111 00000 11111 00000 00000', '%': '11001 11010 00010 00100 01000 01011 10011',
}

INK = hexc('#f4efe0')
DIM = hexc('#a9b7a2')
PAPER = hexc('#22332a')
PAPER_LIGHT = hexc('#3d5247')
GRASS = hexc('#3a5a40')
LINE = hexc('#16221b')


def text_width(string, scale=2):
    return len(string) * 6 * scale - scale


def draw_text(image, x, y, string, color=INK, scale=2):
    for ch in string:
        rows = FONT.get(ch.upper(), FONT['?']).split()
        for ry, row in enumerate(rows):
            for rx, bit in enumerate(row):
                if bit == '1':
                    image.rect(x + rx * scale, y + ry * scale, scale, scale, color)
        x += 6 * scale
    return x


def centered(image, cx, y, string, color=INK, scale=2):
    draw_text(image, cx - text_width(string, scale) // 2, y, string, color, scale)


class Sheet:
    """Rows of captioned cells under a title, on a dark page."""

    def __init__(self, title, subtitle='', width=1080):
        self.title, self.subtitle, self.width = title, subtitle, width
        self.rows = []  # (label or None, [(image, caption)], background)

    def row(self, cells, label=None):
        self.rows.append((label, cells))

    def render(self):
        margin, gap = 16, 8
        y = margin
        heights = []
        for label, cells in self.rows:
            height = max(c[0].h for c in cells) + (26 if any(c[1] for c in cells) else 0) + (26 if label else 0)
            heights.append(height)
        total = margin + 34 + (24 if self.subtitle else 0) + sum(h + gap for h in heights) + margin
        page = Image(self.width, total, fill=PAPER)
        draw_text(page, margin, y, self.title, INK, 3)
        y += 34
        if self.subtitle:
            draw_text(page, margin, y, self.subtitle, DIM, 2)
            y += 24
        for (label, cells), height in zip(self.rows, heights):
            if label:
                draw_text(page, margin, y + 4, label, INK, 2)
                y += 26
            # A cell is as wide as its picture or its caption, whichever is wider.
            widths = [max(c[0].w, text_width(c[1], 2) + 8 if c[1] else 0) for c in cells]
            row_width = sum(widths) + gap * (len(cells) - 1)
            x = max(margin, (self.width - row_width) // 2)
            for (image, caption), width in zip(cells, widths):
                page.paste(image, x + (width - image.w) // 2, y)
                if caption:
                    centered(page, x + width // 2, y + image.h + 6, caption, DIM, 2)
                x += width + gap
            y += height - (26 if label else 0) + gap
        return page


def cell(image, crop, scale, background=GRASS):
    """A frame cropped to (x0, y0, x1, y1), on grass, enlarged."""
    x0, y0, x1, y1 = crop
    out = Image(x1 - x0, y1 - y0, fill=background)
    out.paste(image.crop(x0, y0, x1 - x0, y1 - y0))
    return out.scaled(scale)


HERO_NAMES = {'haiden': 'Haiden', 'kristela': 'Kristela', 'uzuki': 'Uzuki'}
HERO_ORDER = ('haiden', 'kristela', 'uzuki')
RING_AURAS = [
    ("Hawk's Eye", '#ffd84a'), ("Executioner's Seal", '#e8322c'), ('Bloodbond', '#c2185b'),
    ("Mender's Band", '#6fe06a'), ('Arcane Ward', '#8f7bff'), ('Stoneguard', '#b9a58c'),
    ('Heartwood Band', '#3fae6b'), ('Venom Coil', '#b8ff00'), ('Tempo Signet', '#4fd6ff'),
]


def ring_id(name):
    """A ring set's id from its name: "Hawk's Eye" -> hawks_eye."""
    return name.lower().replace("'", '').replace(' ', '_')


def tinted(image, color):
    r, g, b = color & 255, (color >> 8) & 255, (color >> 16) & 255
    out = image.copy()
    for i, p in enumerate(out.px):
        if p >> 24:
            out.px[i] = ((p & 255) * r // 255) | (((p >> 8) & 255) * g // 255 << 8) | (((p >> 16) & 255) * b // 255 << 16) | (p & 0xFF000000)
    return out


def anim_start(rig, name):
    start = 0
    for anim, frames, _ in rig.anims:
        if anim == name:
            return start, len(frames)
        start += len(frames)
    raise KeyError(name)


def build(wardrobe, folder):
    """Write every preview sheet into `folder`; returns the file paths."""
    os.makedirs(folder, exist_ok=True)
    w = wardrobe
    paths = []

    def save(sheet, name):
        path = os.path.join(folder, name)
        sheet.render().save(path)
        paths.append(path)

    def frame(look, index, crop, scale):
        return cell(w.compose(look, index), crop, scale)

    def attack_frame(look, offset=2):
        rig = w.rig_of(look)
        name = 'attack' if any(a[0] == 'attack' for a in rig.anims) else 'cast'
        start, count = anim_start(rig, name)
        return start + min(offset, count - 1)

    close = (52, 34, 140, 142)     # One hero with a weapon at rest.
    wide = (31, 22, 162, 150)      # Room for a swing.

    # 1. The heroes: default look, without the head piece, and in motion.
    sheet = Sheet('THE THREE HEROES', 'Default looks until gear exists. Top: with the head piece. Below: without it.')
    sheet.row([(frame(w.default_look(h), 0, close, 3), HERO_NAMES[h]) for h in HERO_ORDER])
    sheet.row([(frame(looks.Look(h, *looks.DEFAULT_LOOKS[h], helmet=False), 0, close, 3), HERO_NAMES[h] + ', bare-headed') for h in HERO_ORDER])
    sheet.row([(frame(w.default_look(h), anim_start(w.rig_of(w.default_look(h)), 'run')[0] + 1, close, 3), 'running') for h in HERO_ORDER])
    sheet.row([(frame(w.default_look(h), attack_frame(w.default_look(h)), wide, 2), 'attacking') for h in HERO_ORDER])
    save(sheet, '01-heroes.png')

    # 2. Every weapon on every hero.
    sheet = Sheet('THE NINE WEAPONS', 'The weapon picks the body: swords and shields, bow, staff, gauntlets.')
    rest, action = (56, 30, 138, 142), (40, 30, 132, 142)      # Standing, and mid-attack (which needs more room).
    strike = {'warrior': 1, 'archer': 4, 'monk': 4, 'pawn': 1}
    for weapon_id in ('long_sword', 'great_sword', 'arcane_sword', 'piercer_blade', 'dual_blades', 'great_shield',
                      'mage_staff', 'hunter_bow', 'gauntlets'):
        cells = []
        for h in HERO_ORDER:
            look = looks.Look(h, weapon_id, looks.DEFAULT_LOOKS[h][1])
            pose = Image(rest[2] - rest[0] + action[2] - action[0], rest[3] - rest[1], fill=GRASS)
            pose.paste(w.compose(look, 0).crop(rest[0], rest[1], rest[2] - rest[0], rest[3] - rest[1]))
            swing = w.compose(look, attack_frame(look, strike[w.rig_of(look).id]))
            pose.paste(swing.crop(action[0], action[1], action[2] - action[0], action[3] - action[1]), rest[2] - rest[0], 0)
            cells.append((pose.scaled(2), HERO_NAMES[h]))
        sheet.row(cells, w.weapons[weapon_id].name)
    save(sheet, '02-weapons.png')

    # 3. Every armor set on every hero.
    sheet = Sheet('THE NINE ARMOR SETS', 'Head piece and colours. Each hero keeps the face and hair under it.')
    for armor in w.armors.values():
        sheet.row([(frame(looks.Look(h, looks.DEFAULT_LOOKS[h][0], armor.id), 0, close, 3), HERO_NAMES[h]) for h in HERO_ORDER], armor.name)
    save(sheet, '03-armor.png')

    # 4. Cosmetic head pieces, worn instead of an armor set's. (Sheet 04 used to compare three options for Kristela's
    # look; Peter chose the skirt without the ribbon on 2026-10-04, and the ribbon became the hair bow here.)
    sheet = Sheet('COSMETIC HEAD PIECES', 'Worn instead of an armor set\'s head piece, over the whole hairstyle.')
    for cosmetic in w.cosmetics.values():
        sheet.row([(frame(looks.Look(h, *looks.DEFAULT_LOOKS[h], cosmetic=cosmetic.id), 0, close, 3), HERO_NAMES[h]) for h in HERO_ORDER],
                  cosmetic.name)
    save(sheet, '04-cosmetic-head-pieces.png')

    # 5. Rings: a twinkle on the hand in the ring set's colour, and a flash of it when the set's bonus fires.
    import fx
    sheet = Sheet('RINGS', 'A twinkle on the hand in the ring set\'s colour. Last row: the flash when its bonus fires.')
    glint = fx.twinkle().frames[3]
    cells = []
    for i, (name, color) in enumerate(RING_AURAS):
        look = w.default_look(HERO_ORDER[i % 3])
        hand = w.rig_of(look).hand
        image = w.compose(look, 0)
        image.paste(tinted(glint, hexc(color)), hand[0] - glint.w // 2, hand[1] - glint.h // 2)
        cells.append((cell(image, close, 3), name))
        if len(cells) == 3:
            sheet.row(cells)
            cells = []
    cells = []
    for i, (name, color) in enumerate(RING_AURAS[:3]):
        look = w.default_look(HERO_ORDER[i])
        image = w.compose(look, 0)
        body = w.compose(look, 0, shadow=False)
        image.paste(body.silhouette(hexc(color)).faded(150))
        cells.append((cell(image, close, 3), name + ' fires'))
    sheet.row(cells)
    save(sheet, '05-rings.png')

    # 6. Animation strips.
    sheet = Sheet('ANIMATION STRIPS', 'Frame by frame. The pack runs 10 a second, the game plays attacks faster.')
    strips = [('Haiden: Sword Slash', looks.Look('haiden', 'long_sword', 'heavy_armor'), 'attack'),
              ('Haiden: guard (Shoulder Bash, Aura)', looks.Look('haiden', 'long_sword', 'heavy_armor'), 'guard'),
              ('Uzuki: shot', looks.Look('uzuki', 'hunter_bow', 'archers_garb'), 'attack'),
              ('Kristela: Thrust', looks.Look('kristela', 'piercer_blade', 'light_warrior'), 'attack'),
              ("Kristela: a lunge's stab", looks.Look('kristela', 'piercer_blade', 'light_warrior'), 'attack2'),
              ("Kristela: Riposte's stance", looks.Look('kristela', 'piercer_blade', 'light_warrior'), 'guard'),
              ('The Monk: Jab (the Gauntlets)', looks.Look('kristela', 'gauntlets', 'light_warrior'), 'attack'),
              ('Kristela with the Arcane Sword', looks.Look('kristela', 'arcane_sword', 'mage_robe'), 'attack2'),
              ('Uzuki with the Mage Staff: casting', looks.Look('uzuki', 'mage_staff', 'mage_robe'), 'cast')]
    for label, look, anim in strips:
        start, count = anim_start(w.rig_of(look), anim)
        picks = list(range(start, start + count)) if count <= 8 else list(range(start, start + count, 2))[:6]
        per_row = 4
        for i in range(0, len(picks), per_row):
            sheet.row([(frame(look, f, wide, 2), '') for f in picks[i:i + per_row]], label if i == 0 else None)
    save(sheet, '06-animations.png')

    # 7. Icons for the gear and hero screens to come.
    import icons
    drawn = icons.build(w, [(ring_id(name), color) for name, color in RING_AURAS])
    tile = lambda name: cell(drawn[name], (0, 0, icons.SIZE, icons.SIZE), 2, PAPER_LIGHT)
    sheet = Sheet('ICONS', 'One per weapon, armor piece and ring set, 64 px each, for the gear screens to come.')
    weapons = [weapon for rig in w.rigs.values() for weapon in rig.weapons]
    for i in range(0, len(weapons), 3):
        sheet.row([(tile('Icons/weapon_' + weapon.id), weapon.name) for weapon in weapons[i:i + 3]], 'Weapons' if i == 0 else None)
    for armor in w.armors.values():
        sheet.row([(tile('Icons/armor_%s_%s' % (armor.id, slot)), slot) for slot in icons.SLOTS], armor.name)
    rings = [(tile('Icons/ring_' + ring_id(name)), name) for name, _ in RING_AURAS]
    for i in range(0, len(rings), 3):
        sheet.row(rings[i:i + 3], 'Ring sets' if i == 0 else None)
    save(sheet, '07-icons.png')

    # 8. Kristela's Fencer kit: the skill icons, the icon over her head in the stance, and the effects.
    import fx
    glyphs = icons.skill_icons()
    big = lambda image, k=4: cell(image, (0, 0, image.w, image.h), k, PAPER_LIGHT)
    sheet = Sheet("KRISTELA'S FENCER KIT", 'Skill icons: 24 px white glyphs for round buttons and the skill tree, shown 4 times their size.')
    sheet.row([(big(glyphs[key]), name) for key, name in icons.FENCER_ICONS], 'Skill icons')
    sheet.row([(big(drawn['Icons/weapon_piercer_blade'], 3), 'Piercer Blade'), (big(fx.status_icons()['riposte']), 'Riposte stance')] +
              [(big(image, 2), 'Thrust %d' % (i + 1)) for i, image in enumerate(fx.thrust_frames())], 'Her blade, the stance over her head, a stab')
    sheet.row([(big(image, 2), 'Slash %d' % (i + 1)) for i, image in enumerate(fx.slash_frames())], 'A cut (Blade Dance, the counter)')
    start, count = anim_start(w.rig_of(looks.Look('kristela', 'piercer_blade', 'light_warrior')), 'idle')
    sheet.row([(frame(looks.Look('kristela', 'piercer_blade', 'light_warrior'), start, wide, 3), 'Kristela with the Piercer Blade')] +
              [(frame(looks.Look('haiden', 'piercer_blade', 'heavy_armor'), start, wide, 3), 'On Haiden')], 'In the dungeon')
    save(sheet, '08-fencer-kit.png')
    return paths
