"""
Mock-ups of the skill-tree screen, for review before and while it is built in Unity (PROGRESSION.md, "Building
1g", step 6). The screen is laid out here the way `SkillTreeScreen` lays it out: in the HUD's canvas units (1080
high on any screen), from the same pieces (tree.py, icons.py, the HUD kit), at the whole number of screen pixels
per art pixel the device plays at. The preview sheets' small font stands in for the game's smooth one.
"""
import icons
import tree
from px import Image, hexc
from sheets import draw_text, text_width, tinted

BACKDROP = hexc('#10131c')
TEXT, DIM, FAINT = hexc('#f4efe0'), hexc('#9aa3b5'), hexc('#5d667b')
GOLD_TEXT = hexc('#ffd866')
LOCKED, PASSED = hexc('#444a5e'), hexc('#666d82')      # Sphere tints: no content yet; the row's pick went elsewhere.
TRACK_DONE, TRACK_NEXT, TRACK_AHEAD = hexc('#ffd866'), hexc('#ffffff'), hexc('#4a5063')
ULTIMATE = hexc('#f0b93c')

# The Archer's options as drafted (PROGRESSION.md, "Starting class content"): name, icon, whether it upgrades a skill.
ARCHER_ROWS = (
    (5, (('Deadly Mark', 'hunters_mark', True), ('Crippling Shot', 'crippling_shot', False), ('Bouncing Shot', 'bouncing_shot', False))),
    (10, (('Heavy Draw', 'power_shot', True), ('Barbed Snare', 'snare', True), ('Piercing Arrow', 'power_shot', True))),
    (15, (('Steady Aim', 'attack_hunter_bow', True), ('Shared Mark', 'hunters_mark', True), ('Splitting Arrows', 'attack_hunter_bow', True))),
    (20, (('Deadeye', 'deadeye', False), ('Pinning Volley', 'volley', True), ('Storm of Arrows', 'volley', True))),
    (25, (('Master Marksman', 'mastery', False), ('Master Hunter', 'mastery', False), ('Master Trickshot', 'mastery', False))),
)
ARCHER_PATHS = ('Marksman', 'Hunter', 'Trickshot')


def dimmed(color, factor):
    """A colour with its red, green and blue scaled; alpha kept."""
    r, g, b = color & 255, (color >> 8) & 255, (color >> 16) & 255
    return int(r * factor) | int(g * factor) << 8 | int(b * factor) << 16 | (color & 0xFF000000)


def nine(piece, w, h):
    """A 9-slice piece stretched to w x h art pixels: its corners keep their pixels, the middles stretch."""
    left, bottom, right, top = piece.border
    src = piece.image
    w, h = max(w, left + right + 1), max(h, top + bottom + 1)

    def spans(size, target, first, last):
        out = list(range(first))
        middle = size - first - last
        out += [first + (i * middle) // max(1, target - first - last) for i in range(target - first - last)]
        out += [size - last + i for i in range(last)]
        return out

    xs, ys = spans(src.w, w, left, right), spans(src.h, h, top, bottom)
    out = Image(w, h)
    for y, sy in enumerate(ys):
        row = sy * src.w
        base = y * w
        for x, sx in enumerate(xs):
            out.px[base + x] = src.px[row + sx]
    return out


def wrap(string, width, scale):
    """Break a string into lines of at most `width` screen pixels in the sheet font."""
    lines, line = [], ''
    for word in string.split():
        trial = (line + ' ' + word).strip()
        if line and text_width(trial, scale) > width:
            lines.append(line)
            line = word
        else:
            line = trial
    return lines + ([line] if line else [])


class Screen:
    """A screen of `width` x `height` pixels to compose on, in the HUD's canvas units (1080 high, y down)."""

    def __init__(self, width, height):
        self.w, self.h = width, height
        self.k = height / 1080.0
        self.s = max(1, int(2 * self.k + 0.25))        # Screen pixels per art pixel (UiArtScaler's rule).
        self.width = width / self.k                    # The canvas's width in units.
        self.page = Image(width, height, fill=BACKDROP)

    def px(self, units):
        return int(round(units * self.k))

    def art(self, image, cx, cy, tint=None):
        """A piece at its own size, centred on (cx, cy)."""
        big = (tinted(image, tint) if tint is not None else image).scaled(self.s)
        self.page.paste(big, self.px(cx) - big.w // 2, self.px(cy) - big.h // 2)

    def panel(self, piece, x, y, w, h, tint=None):
        """A 9-slice piece over the rect (x, y, w, h)."""
        image = nine(piece, self.px(w) // self.s, self.px(h) // self.s)
        self.page.paste((tinted(image, tint) if tint is not None else image).scaled(self.s), self.px(x), self.px(y))

    def fill(self, x, y, w, h, color):
        self.page.rect(self.px(x), self.px(y), self.px(w), self.px(h), color)

    def scale_of(self, size):
        """The sheet font's scale that stands in for the game's font at `size` units."""
        return max(1, int(round(size * self.k / 10.0)))

    def text(self, x, y, string, color=TEXT, size=24, align='left', fit=None):
        """A line whose top is at y; x is its left edge, its middle or its right edge by `align`. It shrinks to `fit` units."""
        scale = self.scale_of(size)
        while fit is not None and scale > 1 and text_width(string, scale) > self.px(fit):
            scale -= 1
        width = text_width(string, scale)
        left = self.px(x) - (width // 2 if align == 'centre' else width if align == 'right' else 0)
        draw_text(self.page, left, self.px(y), string, color, scale)

    def paragraph(self, x, y, width, string, color=TEXT, size=22):
        """Wrapped text; returns the y below it."""
        scale = self.scale_of(size)
        for line in wrap(string, self.px(width), scale):
            draw_text(self.page, self.px(x), self.px(y), line, color, scale)
            y += size * 1.35
        return y


class State:
    """What a mock-up shows: one hero's progress in one class, and what is selected."""

    def __init__(self, hero, level, tier, picks, written, selected, classes, loadout, info):
        self.hero, self.level, self.tier = hero, level, tier
        self.picks = picks            # {milestone tier: path}
        self.written = written        # Whether the milestone rows have their options.
        self.selected = selected      # (milestone tier, path), or (tier, None) for the row as a whole.
        self.classes = classes        # [(name, tier)], the first one shown in the tree.
        self.loadout = loadout        # Icon names: three skills, then the ultimate.
        self.info = info              # {'title', 'kind', 'body', 'note', 'button', 'ready'}
        self.points = level - sum(t for _, t in classes)


def draw(screen, state, pieces, glyphs, portraits, heroes):
    """Compose the whole screen; returns the page."""
    s = screen
    by = {piece.name: piece for piece in pieces}
    margin = 110.0 if s.width > 2000 else 16.0                      # A phone's rounded corners and camera.
    block = min(s.width - 2 * margin, 2012.0)
    x0 = (s.width - block) / 2.0
    left_w = 380.0
    info_w = max(480.0, min(600.0, 0.3 * block))
    tree_w = block - left_w - info_w - 32.0
    x1, x2 = x0 + left_w + 16.0, x0 + left_w + 16.0 + tree_w + 16.0
    top, bottom = 104.0, 1064.0

    # ---- The top bar: the heroes, the points left, Close ----
    tab_w = 300.0
    for i, (name, level, free) in enumerate(heroes):
        tx = x0 + i * (tab_w + 12.0)
        chosen = name == state.hero
        s.panel(by['panel_inset'], tx, 16.0, tab_w, 76.0, None if chosen else hexc('#b9bfcc'))
        if chosen:
            s.fill(tx + 8.0, 84.0, tab_w - 16.0, 4.0, GOLD_TEXT)
        s.art(portraits[name.lower()], tx + 46.0, 54.0)
        s.text(tx + 88.0, 24.0, name, GOLD_TEXT if chosen else TEXT, 28)
        s.text(tx + 88.0, 58.0, 'Lv %d' % level, DIM, 22)
        if free:
            s.text(tx + tab_w - 14.0, 58.0, '%d free' % free, GOLD_TEXT, 22, 'right')
    s.panel(by['button_dark'], x0 + block - 170.0, 18.0, 170.0, 72.0)
    s.text(x0 + block - 85.0, 40.0, 'Close', TEXT, 26, 'centre')
    s.text(x0 + block - 200.0, 26.0, 'Points left', DIM, 22, 'right')
    s.text(x0 + block - 200.0, 54.0, str(state.points), GOLD_TEXT, 34, 'right')

    # ---- Left: the classes, the loadout, unlearn ----
    s.text(x0 + 4.0, top + 2.0, 'Classes', DIM, 22)
    y = top + 34.0
    for i, (name, tier) in enumerate(state.classes):
        chosen = i == 0
        s.panel(by['panel_inset'], x0, y, left_w, 84.0, None if chosen else hexc('#b9bfcc'))
        if chosen:
            s.fill(x0 + 6.0, y + 10.0, 6.0, 64.0, GOLD_TEXT)
        s.text(x0 + 26.0, y + 14.0, name, GOLD_TEXT if chosen else TEXT, 28)
        s.text(x0 + 26.0, y + 50.0, 'Tier %d of 25' % tier if tier else 'Not learned', DIM, 20)
        y += 92.0
    y += 14.0
    s.text(x0 + 4.0, y, 'Loadout', DIM, 22)
    y += 34.0
    step = (left_w - 4 * 44 * s.s / s.k) / 3.0 + 44 * s.s / s.k       # Four spheres across the column.
    for i, name in enumerate(state.loadout):
        cx, cy = x0 + 44 * s.s / s.k / 2.0 + i * step, y + 50.0
        tint = ULTIMATE if i == 3 else hexc('#8d98ad')
        s.art(by['sphere'].image, cx, cy, tint)
        s.art(by['sphere_shine'].image, cx, cy)
        s.art(glyphs[name], cx, cy)
    y += 112.0
    s.text(x0 + 4.0, y, 'Three skills and the ultimate.', FAINT, 20)
    s.text(x0 + 4.0, y + 28.0, 'Tap one to change it.', FAINT, 20)
    s.panel(by['button_red'], x0, bottom - 76.0, left_w, 76.0)
    s.text(x0 + left_w / 2.0, bottom - 54.0, 'Unlearn %s' % state.classes[0][0], TEXT, 24, 'centre')

    # ---- Middle: the tree ----
    s.panel(by['panel_dark'], x1, top, tree_w, bottom - top)
    class_name = state.classes[0][0]
    s.text(x1 + tree_w / 2.0, top + 26.0, class_name, GOLD_TEXT, 36, 'centre')
    s.text(x1 + tree_w / 2.0, top + 72.0, 'Tier %d of 25' % state.tier, DIM, 22, 'centre')
    track_x = x1 + 92.0
    tier_y = lambda t: top + 166.0 + (t - 1) * (710.0 / 24.0)
    inner_left, inner_right = x1 + 150.0, x1 + tree_w - 30.0
    column = (inner_right - inner_left) / 3.0
    path_x = [inner_left + column * (p + 0.5) for p in range(3)]
    line_w = 4 * s.s / s.k                                               # The line piece's width in units.
    rows = ARCHER_ROWS

    # Lines first: each path's, top to bottom, bright down to its deepest pick; each row's, from the track.
    for p in range(3):
        colour = tree.PATH_COLORS[p]
        deepest = max([t for t, picked in state.picks.items() if picked == p] or [0])
        spans = [(top + 142.0, tier_y(5) - 40.0, 5)] + [(tier_y(t - 5) + 82.0, tier_y(t) - 40.0, t) for t in (10, 15, 20, 25)]
        for y_from, y_to, t in spans:
            s.panel(by['tree_line'], path_x[p] - line_w / 2.0, y_from, line_w, y_to - y_from, colour if t <= deepest else dimmed(colour, 0.38))
        s.text(path_x[p], top + 110.0, ARCHER_PATHS[p], colour if state.written else dimmed(colour, 0.6), 24, 'centre')
    for t, _ in rows:
        s.panel(by['tree_line'], track_x, tier_y(t) - line_w / 2.0, path_x[2] - track_x, line_w, hexc('#3a4156'))
    s.panel(by['tree_line'], track_x - line_w / 2.0, tier_y(1), line_w, tier_y(25) - tier_y(1), hexc('#3a4156'))

    # The tier track: a mark per tier, a larger one with its number at a milestone.
    for t in range(1, 26):
        colour = TRACK_DONE if t <= state.tier else TRACK_NEXT if t == state.tier + 1 and (state.written or t % 5) else TRACK_AHEAD
        if t % 5 == 0:
            s.art(by['tree_node'].image, track_x, tier_y(t), colour)
            s.text(track_x - 26.0, tier_y(t) - 13.0, str(t), GOLD_TEXT if t <= state.tier else DIM, 26, 'right')
        else:
            s.art(by['tree_pip'].image, track_x, tier_y(t), colour)

    # The spheres.
    for t, options in rows:
        cy = tier_y(t)
        for p, (name, icon, upgrade) in enumerate(options):
            cx = path_x[p]
            colour = tree.PATH_COLORS[p]
            picked = state.picks.get(t)
            if not state.written:
                s.art(by['sphere'].image, cx, cy, LOCKED)
                s.art(by['lock'].image, cx, cy, hexc('#9aa3b5'))
                continue
            if picked == p:
                s.art(by['sphere_glow'].image, cx, cy, colour)
                s.art(by['sphere_ring'].image, cx, cy)
                s.art(by['sphere'].image, cx, cy, colour)
                s.art(by['sphere_shine'].image, cx, cy)
                s.art(glyphs[icon], cx, cy)
                s.art(by['badge_learned'].image, cx + 36.0, cy + 34.0)
                label = TEXT
            elif picked is not None:
                s.art(by['sphere'].image, cx, cy, PASSED)
                s.art(glyphs[icon], cx, cy, hexc('#8a91a3'))
                label = FAINT
            elif t == state.tier + 1:
                s.art(by['sphere_glow'].image, cx, cy, colour)
                s.art(by['sphere'].image, cx, cy, colour)
                s.art(by['sphere_shine'].image, cx, cy)
                s.art(glyphs[icon], cx, cy)
                label = TEXT
            else:
                s.art(by['sphere'].image, cx, cy, dimmed(colour, 0.5))
                s.art(glyphs[icon], cx, cy, hexc('#aab1c2'))
                label = DIM
            if upgrade and picked is None:
                s.art(by['badge_upgrade'].image, cx + 36.0, cy - 34.0)
            s.text(cx, cy + 52.0, name, label, 20, 'centre', fit=column - 12.0)
        if not state.written:
            s.text((path_x[0] + path_x[2]) / 2.0, cy + 52.0, 'Not written yet', FAINT, 20, 'centre')
    # The cursor around what is selected, like the reference's gold box.
    t, p = state.selected
    if p is None:
        s.panel(by['tree_cursor'], path_x[0] - 76.0, tier_y(t) - 58.0, path_x[2] - path_x[0] + 152.0, 138.0)
    else:
        half = min(column / 2.0 - 4.0, 128.0)
        s.panel(by['tree_cursor'], path_x[p] - half, tier_y(t) - 58.0, 2 * half, 138.0)

    # ---- Right: what is selected ----
    s.panel(by['panel_dark'], x2, top, info_w, bottom - top)
    info = state.info
    cx, cy = x2 + 92.0, top + 96.0
    if info.get('icon'):
        colour = tree.PATH_COLORS[p] if p is not None else LOCKED
        s.art(by['sphere_big_glow'].image, cx, cy, colour)
        s.art(by['sphere_big'].image, cx, cy, colour)
        s.art(by['sphere_big_shine'].image, cx, cy)
        s.art(glyphs[info['icon']], cx, cy)
    else:
        s.art(by['sphere_big'].image, cx, cy, LOCKED)
        s.art(by['lock'].image, cx, cy, hexc('#9aa3b5'))
    title_scale_width = info_w - 190.0
    ty = top + 52.0
    for line in wrap(info['title'], s.px(title_scale_width), s.scale_of(30)):
        s.text(x2 + 168.0, ty, line, GOLD_TEXT, 30)
        ty += 38.0
    for line in info['kind']:
        s.text(x2 + 168.0, ty + 4.0, line, DIM, 20)
        ty += 28.0
    box_y = max(top + 190.0, ty + 24.0)
    s.panel(by['panel_inset'], x2 + 24.0, box_y, info_w - 48.0, 470.0)
    ty = s.paragraph(x2 + 44.0, box_y + 22.0, info_w - 88.0, info['body'], TEXT, 24)
    ty += 18.0
    for line in info['note']:
        ty = s.paragraph(x2 + 44.0, ty, info_w - 88.0, line, DIM, 20) + 10.0
    button_y = bottom - 110.0
    s.paragraph(x2 + 28.0, box_y + 470.0 + 18.0, info_w - 56.0, info['status'], GOLD_TEXT if info['ready'] else DIM, 20)
    s.panel(by['button_gold' if info['ready'] else 'button_dark'], x2 + 24.0, button_y, info_w - 48.0, 80.0)
    s.text(x2 + info_w / 2.0, button_y + 24.0, info['button'], TEXT if info['ready'] else FAINT, 28, 'centre')
    return s.page


def states():
    """The two mock-ups' states: the tree with its options written (as drafted), and the tree as it is today."""
    classes = [('Archer', 14), ('Paladin', 0), ('Fencer', 0), ('Monk', 0)]
    loadout = ('hunters_mark', 'power_shot', 'rolling_shot', 'volley')
    written = State('Uzuki', 16, 14, {5: 0, 10: 1}, True, (15, 2), classes, loadout, {
        'icon': 'attack_hunter_bow', 'title': 'Splitting Arrows', 'kind': ['Trickshot, tier 15', 'Upgrades Quick Shot'],
        'body': 'Quick Shot bounces once to a second foe for 50%.',
        'note': ['Quick Shot now: 100% ATK to a foe in sight within 6 tiles. Always ready.',
                 'Every tier of Archer: +0.4% ATK, +0.2% Crit Rate.'],
        'status': 'Costs 1 point. A pick stays until the class is unlearned.',
        'button': 'Learn', 'ready': True})
    today = State('Uzuki', 6, 4, {}, False, (5, None), [('Archer', 4), ('Paladin', 0), ('Fencer', 0), ('Monk', 0)], loadout, {
        'icon': None, 'title': 'Tier 5', 'kind': ['Archer, a milestone', 'One of three options'],
        'body': "Not written yet. The Archer's milestone options come with the next update, so the class stops at tier 4 for now.",
        'note': ['Your points keep. They can go into another class meanwhile, or wait.',
                 'Every tier of Archer: +0.4% ATK, +0.2% Crit Rate.'],
        'status': 'Tier 5 is locked.',
        'button': 'Locked', 'ready': False})
    return written, today


def build(pack, wardrobe):
    """{file name: image}: the phone with the options written, the phone as it is today, the tablet."""
    import looks
    import ui
    pieces = [piece for piece in ui.build(pack)] + tree.pieces()
    glyphs = icons.skill_icons(drafts=True)
    portraits = {}
    for hero in ('haiden', 'kristela', 'uzuki'):
        look = wardrobe.default_look(hero)
        hx, hy = wardrobe.rig_of(look).head[0]
        portraits[hero] = wardrobe.compose(look, 0, shadow=False).crop(hx + 8, hy + 10, 32, 32)
    written, today = states()
    out = {}
    for name, size, state, level in (('skill-tree-phone', (2340, 1080), written, 16), ('skill-tree-today', (2340, 1080), today, 6),
                                     ('skill-tree-tablet', (2800, 1752), written, 16)):
        heroes = [('Haiden', level, 0), ('Kristela', level, 1), ('Uzuki', level, state.points)]
        out[name] = draw(Screen(*size), state, pieces, glyphs, portraits, heroes)
    return out
