"""
Hero rigs: the pack's Blue unit sources taken apart into layers the game restacks (docs/design/ART.md, "Heroes built
from layers"). A rig gives, per animation frame, the body, the weapon's parts behind and in front, the effects, and
where the head sits, so a hero's own head, an armor set's head piece and colours, and any of the rig's weapons can be
combined freely. The weapon type picks the rig: swords and shields the Warrior, the bow the Archer, the staff the Monk,
gauntlets the Pawn.
"""
import math

import pack as P
from draw import OUTLINE_REACH, Canvas, Part, outlined
from px import Image, hexc

HEAD_REACH = 8  # How far a redrawn head may sit from the base head inside its cel, when matching the two.


class Strip:
    """One layer's frames, all cropped to the layer's common bounding box; (x, y) is that box's corner in the cell."""

    def __init__(self, frames, x, y):
        self.frames, self.x, self.y = frames, x, y

    @property
    def size(self):
        return self.frames[0].w, self.frames[0].h


class Weapon:
    def __init__(self, weapon_id, name, back=None, front=None, fx=None, mid=None, icon=None):
        self.id, self.name = weapon_id, name
        self.back, self.front, self.fx, self.mid = back, front, fx, mid  # Lists of canvas-sized images per sheet frame.
        self.icon = icon


class Rig:
    def __init__(self, pack, rig_id, source, anims, head_layer, head_offset, pivot, fps=10):
        self.id = rig_id
        self.ase = pack.ase(source)
        self.anims = anims  # [(name, [source frames], loops)]
        self.frames = [f for _, frames, _ in anims for f in frames]
        self.pivot = pivot  # The canvas point that stands on the tile's centre.
        self.fps = fps
        self.head = self._track_head(head_layer, head_offset)
        self.slots = {}     # name -> canvas-sized images, one per sheet frame
        self.weapons = []
        self.plumes = {}    # head piece id -> canvas-sized images (the pack's animated plume for its own helmet)
        self.hand = (pivot[0] + 20, pivot[1] + 8)   # Where a ring twinkles: the weapon hand at rest (canvas point).

    # ---- reading the source ----

    def layer(self, *names):
        """The named source layers flattened, one canvas-sized image per sheet frame."""
        present = [self._find(n) for n in names]
        return [self.ase.render(f, layers=present) for f in self.frames]

    def _find(self, name):
        """A source layer by name; the pack's files have stray spaces in some names ('Bow ')."""
        found = [l for l in self.ase.layers if l.name.strip() == name]
        if len(found) != 1:
            raise KeyError('%s: %d layers named %r' % (self.ase.path, len(found), name))
        return found[0]

    def cel_box(self, name):
        """Per sheet frame, the (x0, y0, x1, y1) of a layer's cel, or None."""
        layer = self._find(name)
        boxes = []
        for f in self.frames:
            cel = self.ase.cel(f, layer)
            boxes.append(None if cel is None or cel.image is None else (cel.x, cel.y, cel.x + cel.image.w, cel.y + cel.image.h))
        return boxes

    def _track_head(self, head_layer, head_offset):
        """
        Where the head's origin is on each frame. The pack moves one head drawing around for most frames and redraws it
        (a tilt, a squash) for a few; a redrawn head is matched to the base drawing by the shift that agrees best.
        """
        layer = self._find(head_layer)
        base = self.ase.cel(self.frames[0], layer)
        shifts = {}
        out = []
        for f in self.frames:
            cel = self.ase.cel(f, layer)
            if cel is None or cel.image is None:
                out.append(out[-1] if out else (base.x + head_offset[0], base.y + head_offset[1]))
                continue
            key = id(cel.image)
            if key not in shifts:
                shifts[key] = (0, 0) if cel.image.px == base.image.px and cel.image.w == base.image.w else _align(base.image, cel.image)
            sx, sy = shifts[key]
            out.append((cel.x + sx + head_offset[0], cel.y + sy + head_offset[1]))
        return out

    # ---- output ----

    def all_slots(self):
        slots = dict(self.slots)
        for weapon in self.weapons:
            for kind in ('back', 'mid', 'front', 'fx'):
                frames = getattr(weapon, kind)
                if frames is not None:
                    slots['%s_%s' % (weapon.id, kind)] = frames
        for piece, frames in self.plumes.items():
            slots['plume_' + piece] = frames
        return slots

    def cell(self, head_box):
        """
        The crop window (x0, y0, x1, y1) on the source canvas that holds every layer on every frame, and the head
        parts' reach (head_box, relative to the head's origin) around every head position.
        """
        x0 = y0 = 10 ** 9
        x1 = y1 = -1
        for frames in self.all_slots().values():
            for image in frames:
                box = image.bbox()
                if box:
                    x0, y0, x1, y1 = min(x0, box[0]), min(y0, box[1]), max(x1, box[2]), max(y1, box[3])
        for hx, hy in self.head:
            x0, y0 = min(x0, hx + head_box[0]), min(y0, hy + head_box[1])
            x1, y1 = max(x1, hx + head_box[2]), max(y1, hy + head_box[3])
        return x0, y0, x1, y1

    def strips(self, cell):
        """Every slot as a Strip in cell coordinates; empty slots are left out."""
        out = {}
        for name, frames in self.all_slots().items():
            x0 = y0 = 10 ** 9
            x1 = y1 = -1
            for image in frames:
                box = image.bbox()
                if box:
                    x0, y0, x1, y1 = min(x0, box[0]), min(y0, box[1]), max(x1, box[2]), max(y1, box[3])
            if x1 < 0:
                continue
            out[name] = Strip([image.crop(x0, y0, x1 - x0, y1 - y0) for image in frames], x0 - cell[0], y0 - cell[1])
        return out


def _align(base, variant):
    """(sx, sy) so that variant[x + sx, y + sy] agrees with base[x, y] on the most pixels."""
    best, best_shift = -1, (0, 0)
    bw, bh, vw, vh = base.w, base.h, variant.w, variant.h
    base_rows = [base.px[y * bw:(y + 1) * bw] for y in range(bh)]
    var_rows = [variant.px[y * vw:(y + 1) * vw] for y in range(vh)]
    for sy in range(-HEAD_REACH, HEAD_REACH + 1):
        for sx in range(-HEAD_REACH, HEAD_REACH + 1):
            score = 0
            for y in range(max(0, -sy), min(bh, vh - sy)):
                brow, vrow = base_rows[y], var_rows[y + sy]
                lo, hi = max(0, -sx), min(bw, vw - sx)
                if lo < hi:
                    score += sum(1 for a, b in zip(brow[lo:hi], vrow[lo + sx:hi + sx]) if a == b and a >> 24)
            if score > best:
                best, best_shift = score, (sx, sy)
    return best_shift


# ---- reshaping the pack's weapons ----

def peel_outline(image, rounds=3):
    """Take the dark outline off the outside of a shape, leaving its fills (and the lines inside it)."""
    out = image.copy()
    w, h = out.w, out.h
    for _ in range(rounds):
        px = out.px
        remove = []
        for i, p in enumerate(px):
            if p != P.OUTLINE:
                continue
            x, y = i % w, i // w
            if (x == 0 or not px[i - 1] >> 24 or x == w - 1 or not px[i + 1] >> 24
                    or y == 0 or not px[i - w] >> 24 or y == h - 1 or not px[i + w] >> 24):
                remove.append(i)
        for i in remove:
            px[i] = 0
    return out


def centroid(image, colors=None):
    """Mean position of the image's pixels (of the given colours), or None."""
    sx = sy = n = 0
    w = image.w
    for i, p in enumerate(image.px):
        if p >> 24 and (colors is None or p in colors):
            sx += i % w
            sy += i // w
            n += 1
    return (sx / n + 0.5, sy / n + 0.5) if n else None


def reshape(image, origin, angle, along, across, recolors=None):
    """
    Stretch a drawn piece about `origin`: `along` times along the direction `angle` (radians), `across` times across
    it. The outline comes off first and goes back on afterwards, so it keeps the pack's thickness at any size.
    """
    fill = peel_outline(image)
    if recolors:
        fill = fill.mapped(recolors)
    box = fill.bbox()
    if box is None:
        return Image(image.w, image.h)
    ux, uy = math.cos(angle), math.sin(angle)
    ox, oy = origin
    reach = int(max(box[2] - box[0], box[3] - box[1]) * max(along, across)) + 4
    out = Image(image.w, image.h)
    w, h = image.w, image.h
    for y in range(max(0, int(oy) - reach), min(h, int(oy) + reach)):
        for x in range(max(0, int(ox) - reach), min(w, int(ox) + reach)):
            vx, vy = x + 0.5 - ox, y + 0.5 - oy
            t, s = (vx * ux + vy * uy) / along, (-vx * uy + vy * ux) / across
            sx, sy = int(math.floor(ox + t * ux - s * uy)), int(math.floor(oy + t * uy + s * ux))
            if 0 <= sx < w and 0 <= sy < h:
                p = fill.px[sy * w + sx]
                if p >> 24:
                    out.px[y * w + x] = p
    return _outline_in_place(out)


def _outline_in_place(image):
    """The pack's outline around a canvas-sized image (kept at canvas size)."""
    grown = outlined(image)
    return grown.crop(2, 2, image.w, image.h)


def recolored(frames, mapping):
    return [f.mapped(mapping) for f in frames]


# ---- the Warrior rig: swords and shields ----

GUARD_COLORS = {P.CLOTH_LIGHT, P.CLOTH_DARK, P.SKIN_DARK}
BLADE_COLORS = {P.METAL, P.METAL_LIGHT, P.METAL_DARK}

IRON = {P.METAL_LIGHT: hexc('#e6ebf0'), P.METAL: hexc('#aab6c6'), P.METAL_DARK: hexc('#6c788c'),
        P.CLOTH_LIGHT: hexc('#dcaa46'), P.CLOTH_DARK: hexc('#886049')}
ARCANE = {P.METAL_LIGHT: hexc('#f0e2ff'), P.METAL: hexc('#b99cf2'), P.METAL_DARK: hexc('#7460c4'),
          P.CLOTH_LIGHT: hexc('#ab6e9c'), P.CLOTH_DARK: hexc('#5c547e'), P.SKIN_DARK: hexc('#e9f044')}
ARCANE_FX = {P.METAL_LIGHT: hexc('#e6d2ff'), P.METAL: hexc('#b99cf2'), hexc('#a2beaf'): hexc('#b99cf2')}
SILVER = {P.METAL_LIGHT: hexc('#ffffff'), P.METAL: hexc('#cfdde4'), P.METAL_DARK: hexc('#8fa3b4'),
          P.CLOTH_LIGHT: hexc('#e76161'), P.CLOTH_DARK: hexc('#924159')}


def _sword_axes(sword_frames):
    """Per frame, the sword's grip point and the angle its blade points along (carried over where a frame has none)."""
    axes = []
    last = ((96.0, 110.0), -math.pi / 3)
    for image in sword_frames:
        grip = centroid(image, GUARD_COLORS)
        blade = centroid(image, BLADE_COLORS)
        if grip is None and blade is None:
            axes.append(None)
            continue
        if grip is None or blade is None:
            axes.append(((grip or blade), last[1]))
        else:
            last = (grip, math.atan2(blade[1] - grip[1], blade[0] - grip[0]))
            axes.append(last)
    return axes


def _reshaped_sword(rig, along, across, recolors):
    back, front = rig.layer('Sword'), rig.layer('Sword (Front)')
    both = rig.layer('Sword', 'Sword (Front)')
    axes = _sword_axes(both)
    make = lambda frames: [Image(i.w, i.h) if axis is None or i.bbox() is None else reshape(i, axis[0], axis[1], along, across, recolors)
                           for i, axis in zip(frames, axes)]
    return make(back), make(front)


def _merge(*layers):
    """Stack lists of canvas-sized frames (first at the bottom)."""
    out = []
    for frames in zip(*layers):
        image = frames[0].copy()
        for other in frames[1:]:
            image.paste(other)
        out.append(image)
    return out


def _box_centre(box):
    return (box[0] + box[2]) / 2.0, (box[1] + box[3]) / 2.0


def warrior(pack):
    rig = Rig(pack, 'warrior', P.UNITS + '/Warrior.aseprite',
              anims=[('idle', list(range(0, 8)), True), ('run', list(range(8, 14)), True),
                     ('attack', list(range(14, 18)), False), ('attack2', list(range(18, 22)), False),
                     ('guard', list(range(22, 28)), False)],
              head_layer='Head', head_offset=(-1, -6), pivot=(96, 110))
    rig.slots['shadow'] = rig.layer('Shadow')
    rig.slots['body'] = rig.layer('Body')
    rig.plumes['heavy_armor'] = rig.layer('Top')
    sword_back, sword_front = rig.layer('Sword'), rig.layer('Sword (Front)')
    shield_back, shield_front = rig.layer('Shield (back)'), rig.layer('Shield')
    fx = rig.layer('Effect', 'Effect 2')
    rig.hand = tuple(int(v) for v in centroid(sword_back[0], GUARD_COLORS))

    rig.weapons.append(Weapon('long_sword', 'Long Sword', back=_merge(sword_back, shield_back),
                              front=_merge(shield_front, sword_front), fx=fx))

    big_back, big_front = _reshaped_sword(rig, 1.38, 1.5, IRON)
    rig.weapons.append(Weapon('great_sword', 'Great Sword', back=big_back, front=big_front, fx=fx))

    rig.weapons.append(Weapon('arcane_sword', 'Arcane Sword', back=recolored(sword_back, ARCANE),
                              front=recolored(sword_front, ARCANE), fx=recolored(fx, ARCANE_FX)))

    thin_back, thin_front = _reshaped_sword(rig, 1.22, 0.62, SILVER)
    rig.weapons.append(Weapon('piercer_blade', 'Piercer Blade', back=thin_back, front=thin_front, fx=fx))

    # Dual Blades: a second blade where the shield is, held the other way round.
    second = sword_back[0].flipped_x()
    grip = centroid(second, GUARD_COLORS)
    off_back, off_front = [], []
    for back_box, front_box in zip(rig.cel_box('Shield (back)'), rig.cel_box('Shield')):
        for box, out in ((back_box, off_back), (front_box, off_front)):
            image = Image(second.w, second.h)
            if box is not None:
                cx, cy = _box_centre(box)
                image.paste(second, int(round(cx - grip[0])), int(round(cy + 6 - grip[1])))
            out.append(image)
    rig.weapons.append(Weapon('dual_blades', 'Dual Blades', back=_merge(sword_back, off_back),
                              front=_merge(off_front, sword_front), fx=fx))

    # Great Shield: the shield grown about its middle; the sword stays.
    def grown(frames, boxes):
        return [image if box is None else reshape(image, (_box_centre(box)[0], box[3] - 6), 0.0, 1.3, 1.34)
                for image, box in zip(frames, boxes)]
    rig.weapons.append(Weapon('great_shield', 'Great Shield',
                              back=_merge(sword_back, grown(shield_back, rig.cel_box('Shield (back)'))),
                              front=_merge(grown(shield_front, rig.cel_box('Shield')), sword_front), fx=fx))
    return rig


# ---- the Archer rig: the bow ----

def archer(pack):
    rig = Rig(pack, 'archer', P.UNITS + '/Archer.aseprite',
              anims=[('idle', list(range(0, 6)), True), ('run', list(range(6, 10)), True),
                     ('attack', list(range(10, 18)), False)],
              head_layer='Head', head_offset=(0, 0), pivot=(96, 110))
    rig.slots['shadow'] = rig.layer('Shadow')
    rig.slots['body'] = rig.layer('Body')
    rig.plumes['archers_garb'] = rig.layer('Top')
    rig.weapons.append(Weapon('hunter_bow', 'Hunter Bow', back=rig.layer('Quiver', 'Bow'),
                              front=rig.layer('Hand', 'Bow (Front)', 'Arrow')))
    rig.hand = tuple(int(v) for v in _box_centre(rig.cel_box('Hand')[0]))
    return rig


# ---- the Monk rig: the staff ----

STAFF_WOOD, STAFF_WOOD_DARK = hexc('#b48150'), hexc('#866353')
ORB, ORB_LIGHT, ORB_DARK = hexc('#ab6e9c'), hexc('#f0e2ff'), hexc('#5c547e')
GOLD, GOLD_DARK = hexc('#dcaa46'), hexc('#886049')


def staff_part():
    """The Mage Staff, upright: a wooden pole with a gold claw holding an orb. The origin is where the hand grips."""
    c = Canvas(20, 80, ox=10, oy=52)
    c.rect(-1, -34, 3, 60, STAFF_WOOD)
    c.rect(1, -34, 1, 60, STAFF_WOOD_DARK)
    c.rect(-2, 24, 5, 3, GOLD_DARK)                      # Foot cap.
    c.polygon([(-5, -34), (6, -34), (4, -30), (-3, -30)], GOLD)   # Claw under the orb.
    c.rect(-5, -38, 2, 5, GOLD)
    c.rect(4, -38, 2, 5, GOLD)
    c.ellipse(0.5, -42.5, 5.5, 5.5, ORB)
    c.ellipse(1.5, -41.0, 4.0, 3.6, ORB_DARK, only=(ORB,))
    c.ellipse(0.0, -43.5, 4.2, 4.0, ORB, only=(ORB_DARK,))
    c.dots(ORB_LIGHT, [(-2, -45), (-1, -46), (-2, -46), (-1, -45)])
    return c.part()


def monk(pack):
    rig = Rig(pack, 'monk', P.UNITS + '/Monk.aseprite',
              anims=[('idle', list(range(0, 6)), True), ('run', list(range(6, 10)), True),
                     ('cast', list(range(10, 21)), False)],
              head_layer='Head', head_offset=(3, -8), pivot=(96, 110))
    rig.slots['shadow'] = rig.layer('Shadow')
    rig.slots['body'] = rig.layer('Feet', 'Body')
    hands = rig.layer('Hands')
    staff = staff_part()
    front = []
    for image, box in zip(hands, rig.cel_box('Hands')):
        canvas = Image(image.w, image.h)
        if box is not None:
            staff.paste_on(canvas, box[2] - 5, int(round((box[1] + box[3]) / 2.0)) - 2)
        front.append(canvas)
    rig.weapons.append(Weapon('mage_staff', 'Mage Staff', mid=hands, front=front, fx=rig.layer('Effect 1', 'Effect 2')))
    rig.hand = tuple(int(v) for v in _box_centre(rig.cel_box('Hands')[0]))
    return rig


# ---- the Pawn rig: gauntlets ----

GAUNTLET, GAUNTLET_LIGHT, GAUNTLET_DARK = hexc('#aab6c6'), hexc('#e6ebf0'), hexc('#6c788c')


def gauntlets(frames):
    """
    Bare hands turned into iron fists: two sizes bigger and plated, lit from above. The hands are whatever is drawn in
    the pack's skin tone, which also finds the hand that holds a tool in the Pawn's work frames.
    """
    out = []
    for image in frames:
        w, h = image.w, image.h
        fill = Image(w, h)
        for i, p in enumerate(image.px):
            if p == P.SKIN_DARK or p == P.SKIN:
                fill.px[i] = GAUNTLET
        for ring in range(2):
            grown = fill.copy()
            for i, p in enumerate(fill.px):
                if not p >> 24:
                    continue
                x, y = i % w, i // w
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < w and 0 <= ny < h and not fill.px[ny * w + nx] >> 24:
                        edge = GAUNTLET_DARK if dy > 0 or dx < 0 else GAUNTLET_LIGHT
                        grown.px[ny * w + nx] = edge if ring == 1 else GAUNTLET
            fill = grown
        out.append(_outline_in_place(fill))
    return out


def pawn(pack):
    rig = Rig(pack, 'pawn', P.UNITS + '/Pawn.aseprite',
              anims=[('idle', list(range(0, 8)), True), ('run', list(range(64, 70)), True),
                     ('attack', list(range(121, 125)), False), ('attack2', list(range(115, 121)), False)],
              head_layer='Head', head_offset=(9, 3), pivot=(96, 110))
    rig.slots['shadow'] = rig.layer('Shadow')
    rig.slots['body'] = rig.layer('Body')
    # In the work frames the pack draws the working hand on the tool's layer; the fists come from both layers.
    rig.weapons.append(Weapon('gauntlets', 'Gauntlets', front=gauntlets(rig.layer('Hands', 'Object')), fx=rig.layer('Effect')))
    hands = rig.cel_box('Hands')[0]
    rig.hand = (hands[2] - 6, (hands[1] + hands[3]) // 2)   # The front fist.
    return rig


def build(pack):
    return [warrior(pack), archer(pack), monk(pack), pawn(pack)]
