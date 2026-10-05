"""
Putting a hero together: a look names the hero, the weapon (which picks the rig), the armor set and whether the head
piece is worn, and compose() stacks the layers for one frame. The game does the same stacking at run time
(Assets/Scripts/Shared/HeroComposer.cs) from the files build_art.py writes; this copy draws the preview sheets.

Stacking order, bottom first: the hero's back hair, the weapon's parts behind the body, the body in the armor's
colours, the weapon's parts between body and head, the head (skull, hair, head piece, the helmet's own plume), the
weapon's parts in front, the effects.
"""
import pack as P
from draw import Part, outlined
from px import Image

# Default looks until gear exists (milestone 1h sets them from the equipped items).
DEFAULT_LOOKS = {
    'haiden': ('long_sword', 'heavy_armor'),
    'kristela': ('piercer_blade', 'light_warrior'),   # A Fencer since 2026-10-05 (the Gauntlets were hers as a Monk).
    'uzuki': ('hunter_bow', 'archers_garb'),
}

SKIRTED = {'kristela'}  # Heroes whose body gets the skirted outline (ART.md: female heroes read as feminine).


def body_colors(armor):
    """The colour swap that dresses a rig's Blue body in an armor set."""
    if armor is None:
        return {}
    return {P.CLOTH_DARK: armor.cloth[0], P.CLOTH_LIGHT: armor.cloth[1],
            P.METAL_DARK: armor.metal[0], P.METAL: armor.metal[1], P.METAL_LIGHT: armor.metal[2]}


def skirted(image):
    """
    A body frame with a skirt: the lower part of the tunic flares out and its hem drops over the top of the legs,
    with a line of trim. Works on any of the round-bodied rigs' frames.
    """
    cloth = (P.CLOTH_DARK, P.CLOTH_LIGHT)
    w, h = image.w, image.h
    rows = {}
    for i, p in enumerate(image.px):
        if p in cloth:
            y = i // w
            x = i % w
            lo, hi = rows.get(y, (x, x))
            rows[y] = (min(lo, x), max(hi, x))
    if not rows:
        return image
    bottom = max(rows)
    top = min(rows)
    widest = max(rows.values(), key=lambda r: r[1] - r[0])
    centre = (widest[0] + widest[1]) // 2
    half = (widest[1] - widest[0]) // 2
    flare = Image(w, h)
    depth, hem = min(9, max(4, (bottom - top) // 2)), 4
    for k in range(depth + hem):
        y = bottom - depth + 1 + k
        reach = half + 1 + (k * 5) // (depth + hem - 1)     # Widens steadily down to the hem.
        for x in range(centre - reach, centre + reach + 1):
            if not (0 <= x < w and 0 <= y < h):
                continue
            if k == depth + hem - 1 and (x - centre) % 6 in (2, 3):
                continue                                     # A scalloped edge.
            pleat = k >= 2 and (x - centre) % 6 == 0
            trim = k == depth + hem - 3
            flare.px[y * w + x] = P.CLOTH_DARK if pleat or trim else P.CLOTH_LIGHT
    ring = outlined(flare)
    out = image.copy()
    out.paste(ring.crop(2, 2, w, h))
    return out


class Look:
    def __init__(self, hero, weapon, armor=None, helmet=True, skirt=None, cosmetic=None, aura=None):
        self.hero, self.weapon, self.armor, self.helmet, self.aura = hero, weapon, armor, helmet, aura
        self.skirt = (hero in SKIRTED) if skirt is None else skirt
        self.cosmetic = cosmetic   # A cosmetic head piece's id, worn instead of the armor set's.


class Wardrobe:
    """
    Everything a look can be built from: the rigs with their weapons, the heroes' head parts, the armor sets and the
    cosmetic head pieces.
    """

    def __init__(self, rigs, hero_parts, armors, cosmetics=()):
        self.rigs = {rig.id: rig for rig in rigs}
        self.weapon_rig = {weapon.id: rig for rig in rigs for weapon in rig.weapons}
        self.weapons = {weapon.id: weapon for rig in rigs for weapon in rig.weapons}
        self.heroes = hero_parts
        self.armors = {armor.id: armor for armor in armors}
        self.cosmetics = {cosmetic.id: cosmetic for cosmetic in cosmetics}
        self._skirts = {}
        # A helmet's plume is animated on the rig it comes from; on the other rigs it rides on the head, still.
        self.still_plumes = {}
        for rig in rigs:
            for piece, frames in rig.plumes.items():
                image, x, y = frames[0].trimmed()
                self.still_plumes[piece] = Part(image, x - rig.head[0][0], y - rig.head[0][1])

    def rig_of(self, look):
        return self.weapon_rig[look.weapon]

    def skirt_body(self, rig):
        if rig.id not in self._skirts:
            self._skirts[rig.id] = rig.slots['body'] if rig.id == 'monk' else [skirted(f) for f in rig.slots['body']]
        return self._skirts[rig.id]

    def compose(self, look, frame, shadow=True):
        """One sheet frame of a look, on the rig's full canvas."""
        import heads
        rig = self.rig_of(look)
        weapon = self.weapons[look.weapon]
        armor = self.armors.get(look.armor)
        worn = self.cosmetics.get(look.cosmetic) or (armor if look.helmet else None)
        back, front = heads.head_layers(self.heroes[look.hero], worn)
        hx, hy = rig.head[frame]
        out = Image(rig.ase.width, rig.ase.height)
        if shadow:
            out.paste(rig.slots['shadow'][frame])
        back.paste_on(out, hx, hy)
        if weapon.back:
            out.paste(weapon.back[frame])
        body = self.skirt_body(rig) if look.skirt else rig.slots['body']
        out.paste(body[frame].mapped(body_colors(armor)))
        if weapon.mid:
            out.paste(weapon.mid[frame].mapped(body_colors(armor)))
        front.paste_on(out, hx, hy)
        if worn is not None and worn.id in rig.plumes:
            out.paste(rig.plumes[worn.id][frame].mapped(body_colors(armor)))
        elif worn is not None and worn.id in self.still_plumes:
            self.still_plumes[worn.id].mapped(body_colors(armor)).paste_on(out, hx, hy)
        if weapon.front:
            out.paste(weapon.front[frame])
        if weapon.fx:
            out.paste(weapon.fx[frame])
        return out

    def default_look(self, hero):
        weapon, armor = DEFAULT_LOOKS[hero]
        return Look(hero, weapon, armor)
